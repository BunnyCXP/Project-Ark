using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public class ScanColliderWireframeFX : MonoBehaviour
    {
        [Header("Wave (Unscaled Time)")]
        public float DetectionRadius = 900f;
        public float WaveDuration = 0.45f;
        public float WaveWidth = 1.6f;
        public float FalloffRadiusExtra = 3f;

        [Header("Acquisition")]
        public float AcquireInterval = 0.15f;
        public int MaxHackableTargets = 20;
        public int MaxEnvironmentTargets = 12;
        public float MinEnvironmentSize = 8f;
        public bool PreferStaticEnvironment = false;

        [Header("Masks")]
        public LayerMask HackableMask = ~0;
        public LayerMask EnvironmentMask = ~0;

        [Header("Material")]
        public Material WireframeMaterial;

        [Header("Visual")]
        public Color BaseBlue = new Color(0.2f, 0.6f, 1f, 1f);
        public Color FlashWhite = new Color(1f, 1f, 1f, 1f);
        public float BaseAlpha = 0.65f;
        public float BaseWidth = 0.02f;
        public float WidthGain = 0.03f;

        [Header("Distance & Boost")]
        public float NearPower = 1.2f;

        [Header("Hackable Boost")]
        public float HackableBrightnessMultiplier = 2.0f;
        public float HackableWidthMultiplier = 1.5f;

        [Header("Occlusion Dimming")]
        public LayerMask OcclusionMask = ~0;
        [Range(0f, 1f)] public float OccludedDimFactor = 0.4f;
        public float OcclusionCheckInterval = 0.12f;

        [Header("Building Interior (Environment Only)")]
        [Range(0, 1)] public int EnvironmentInteriorDetailLevel = 1;
        public int MaxInteriorLinesPerBuilding = 18;

        [Header("MeshCollider Wireframe (Watch Dogs Style)")]
        public int MaxMeshEdges = 1200;
        [Range(0f, 180f)]
        public float MeshHardEdgeAngle = 35f;
        public bool MeshIncludeBoundaryEdges = true;

        private Transform _player;
        private Camera _cam;
        private bool _isScanning;
        private float _scanStartUnscaled;
        private float _nextAcquireUnscaled;

        private readonly Plane[] _frustumPlanes = new Plane[6];

        private readonly Dictionary<Collider, WireTarget> _targetsByCollider = new Dictionary<Collider, WireTarget>(128);
        private readonly List<WireTarget> _targets = new List<WireTarget>(128);
        private readonly List<LineSlot> _pool = new List<LineSlot>(256);
        private readonly Stack<WireTarget> _targetPool = new Stack<WireTarget>(128);

        private Material _cachedFallbackMat;
        private static readonly Collider[] _hackHitsAlloc = new Collider[200];
        private static readonly Collider[] _envHitsAlloc = new Collider[500];

        // 用于临时计算 Mesh 的全局列表
        private static readonly List<Vector3> _tempVertices = new List<Vector3>();
        private static readonly List<int> _tempTriangles = new List<int>();

        private static readonly List<(Collider c, float d)> _tempEnvList = new List<(Collider c, float d)>(128);
        private static readonly Vector3[] _tempCorners = new Vector3[8];
        private static readonly int[] _boxEdges = {
            0,1, 1,2, 2,3, 3,0,
            4,5, 5,6, 6,7, 7,4,
            0,4, 1,5, 2,6, 3,7
        };

        private readonly Dictionary<Mesh, MeshEdgeCache> _meshEdgeCache = new Dictionary<Mesh, MeshEdgeCache>(64);

        private class WireTarget
        {
            public Collider Col;
            public bool IsHackable;
            public Bounds LastBounds;
            public bool Occluded;
            public float NextOccCheckUnscaled;
            public readonly List<int> LineIndices = new List<int>(32);

            public bool IsVisible = false;
            public Color LastColor = Color.clear;
            public float LastWidth = -1f;
            public float NextRebuildUnscaled = 0f;
        }

        private class LineSlot
        {
            public GameObject Go;
            public LineRenderer Lr;
            public bool InUse;
        }

        // 【核心修复】直接缓存“局部坐标线段 (LocalLines)”，不再存顶点编号！
        private struct MeshEdgeCache
        {
            public float HardAngleDeg;
            public bool IncludeBoundary;
            public int MaxEdges;
            public Vector3[] LocalLines;
        }

        private WireTarget GetWireTarget()
        {
            var tg = _targetPool.Count > 0 ? _targetPool.Pop() : new WireTarget();
            tg.IsVisible = false;
            tg.LastColor = Color.clear;
            tg.LastWidth = -1f;
            tg.NextRebuildUnscaled = 0f;
            return tg;
        }

        private void ReleaseWireTarget(WireTarget tg)
        {
            tg.Col = null;
            tg.LineIndices.Clear();
            _targetPool.Push(tg);
        }

        private void Start()
        {
            if (WireframeMaterial == null) _cachedFallbackMat = new Material(Shader.Find("Unlit/Color"));

            for (int i = 0; i < 100; i++)
            {
                GetLineSlot();
                _pool[i].InUse = false;
                _pool[i].Go.SetActive(false);
            }
        }

        public void BeginScan(Transform player, Camera mainCamera)
        {
            _player = player;
            _cam = mainCamera;
            _isScanning = true;
            _scanStartUnscaled = Time.unscaledTime;
            _nextAcquireUnscaled = 0f;

            ClearAll();
            AcquireTargetsUnscaled(force: true);
        }

        public void EndScan()
        {
            _isScanning = false;
            ClearAll();
        }

        private void Update()
        {
            if (!_isScanning) return;
            if (_player == null || _cam == null) { EndScan(); return; }

            float now = Time.unscaledTime;
            float t = Mathf.Clamp01((now - _scanStartUnscaled) / Mathf.Max(0.0001f, WaveDuration));
            float waveRadius = Mathf.Lerp(0f, DetectionRadius, t);

            if (now >= _nextAcquireUnscaled)
            {
                _nextAcquireUnscaled = now + Mathf.Max(0.02f, AcquireInterval);
                AcquireTargetsUnscaled(force: false);
            }

            GeometryUtility.CalculateFrustumPlanes(_cam, _frustumPlanes);

            Vector3 origin = _player.position;
            float falloffRadius = Mathf.Max(1f, DetectionRadius + FalloffRadiusExtra);

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                WireTarget tg = _targets[i];
                if (tg.Col == null)
                {
                    RemoveTargetAt(i);
                    continue;
                }

                Bounds b = tg.Col.bounds;

                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b))
                {
                    SetVisible(tg, false);
                    continue;
                }

                float dist = Vector3.Distance(origin, b.center);
                float inner = waveRadius - WaveWidth;
                bool inBand = (dist >= inner && dist <= waveRadius);

                if (!inBand)
                {
                    SetVisible(tg, false);
                    continue;
                }

                if (now >= tg.NextOccCheckUnscaled)
                {
                    tg.NextOccCheckUnscaled = now + Mathf.Max(0.02f, OcclusionCheckInterval);
                    tg.Occluded = ComputeOccluded(b.center, tg.Col);
                }

                if (now >= tg.NextRebuildUnscaled && !BoundsApproximatelyEqual(tg.LastBounds, b))
                {
                    tg.LastBounds = b;
                    tg.NextRebuildUnscaled = now + 0.2f;
                    RedrawTarget(tg);
                }

                float a = Mathf.InverseLerp(inner, waveRadius, dist);
                float s = Smooth01(a);
                float band = 4f * s * (1f - s);

                float dist01 = Mathf.Clamp01(dist / falloffRadius);
                float nearBoost = Mathf.Pow(1f - dist01, NearPower);

                float occlMul = tg.Occluded ? OccludedDimFactor : 1f;
                float front01 = Mathf.Clamp01((dist - inner) / Mathf.Max(0.0001f, WaveWidth));
                float flash = Mathf.Pow(front01, 6f);
                Color col = Color.Lerp(BaseBlue, FlashWhite, flash);

                float alpha = BaseAlpha * band * nearBoost * occlMul;
                float width = BaseWidth + nearBoost * WidthGain;

                if (tg.IsHackable)
                {
                    alpha *= HackableBrightnessMultiplier;
                    width *= HackableWidthMultiplier;
                }

                if (alpha < 0.01f)
                {
                    SetVisible(tg, false);
                    continue;
                }

                col.a = alpha;

                bool colorChanged = Mathf.Abs(tg.LastColor.a - col.a) > 0.015f || Mathf.Abs(tg.LastColor.r - col.r) > 0.02f;
                bool widthChanged = Mathf.Abs(tg.LastWidth - width) > 0.002f;

                if (colorChanged || widthChanged)
                {
                    tg.LastColor = col;
                    tg.LastWidth = width;
                    ApplyVisuals(tg, col, width);
                }

                SetVisible(tg, true);
            }

            if (t >= 1f) EndScan();
        }

        private void AcquireTargetsUnscaled(bool force)
        {
            if (_player == null || _cam == null) return;
            Vector3 origin = _player.position;
            GeometryUtility.CalculateFrustumPlanes(_cam, _frustumPlanes);

            int hackCount = 0;
            int hackHitCount = Physics.OverlapSphereNonAlloc(origin, DetectionRadius, _hackHitsAlloc, HackableMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hackHitCount && hackCount < MaxHackableTargets; i++)
            {
                Collider c = _hackHitsAlloc[i];
                if (c == null) continue;
                Bounds b = c.bounds;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b)) continue;
                if (TryAddTarget(c, isHackable: true)) hackCount++;
            }

            _tempEnvList.Clear();
            int envHitCount = Physics.OverlapSphereNonAlloc(origin, DetectionRadius, _envHitsAlloc, EnvironmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < envHitCount; i++)
            {
                Collider c = _envHitsAlloc[i];
                if (c == null) continue;
                if (((1 << c.gameObject.layer) & HackableMask.value) != 0) continue;
                Bounds b = c.bounds;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b)) continue;
                if (PreferStaticEnvironment && !c.gameObject.isStatic) continue;
                if (b.size.magnitude < MinEnvironmentSize) continue;

                float d = Vector3.Distance(origin, b.center);
                _tempEnvList.Add((c, d));
            }

            if (_tempEnvList.Count == 0) return;
            _tempEnvList.Sort((a, b) => a.d.CompareTo(b.d));

            int envCount = 0;
            for (int i = 0; i < _tempEnvList.Count && envCount < MaxEnvironmentTargets; i++)
            {
                if (TryAddTarget(_tempEnvList[i].c, isHackable: false)) envCount++;
            }
        }

        private bool TryAddTarget(Collider c, bool isHackable)
        {
            if (c == null) return false;
            if (_targetsByCollider.TryGetValue(c, out var existing))
            {
                if (isHackable && !existing.IsHackable) existing.IsHackable = true;
                return false;
            }

            var tg = GetWireTarget();
            tg.Col = c;
            tg.IsHackable = isHackable;
            tg.LastBounds = c.bounds;
            tg.NextOccCheckUnscaled = Time.unscaledTime;

            _targetsByCollider.Add(c, tg);
            _targets.Add(tg);

            DrawTarget(tg);
            SetVisible(tg, false);
            return true;
        }

        private void DrawTarget(WireTarget tg)
        {
            if (tg.Col == null) return;
            if (tg.Col is BoxCollider bc) { DrawBoxColliderOBB(tg, bc); return; }
            if (tg.Col is SphereCollider sc) { DrawSphereCollider(tg, sc); return; }
            if (tg.Col is CapsuleCollider cc) { DrawCapsuleCollider(tg, cc); return; }
            if (tg.Col is MeshCollider mc) { DrawMeshColliderHardEdges(tg, mc); return; }

            Bounds b = tg.Col.bounds;
            if (tg.IsHackable) DrawBoxBounds(tg, b);
            else DrawBuilding(tg, b);
        }

        private void RedrawTarget(WireTarget tg)
        {
            ReleaseLines(tg);
            DrawTarget(tg);
            tg.LastColor = Color.clear;
            SetVisible(tg, false);
        }

        private void DrawBoxColliderOBB(WireTarget tg, BoxCollider bc)
        {
            Transform t = bc.transform;
            Vector3 half = bc.size * 0.5f;
            Vector3 cLocal = bc.center;

            _tempCorners[0] = t.TransformPoint(cLocal + new Vector3(-half.x, -half.y, -half.z));
            _tempCorners[1] = t.TransformPoint(cLocal + new Vector3(+half.x, -half.y, -half.z));
            _tempCorners[2] = t.TransformPoint(cLocal + new Vector3(+half.x, -half.y, +half.z));
            _tempCorners[3] = t.TransformPoint(cLocal + new Vector3(-half.x, -half.y, +half.z));
            _tempCorners[4] = t.TransformPoint(cLocal + new Vector3(-half.x, +half.y, -half.z));
            _tempCorners[5] = t.TransformPoint(cLocal + new Vector3(+half.x, +half.y, -half.z));
            _tempCorners[6] = t.TransformPoint(cLocal + new Vector3(+half.x, +half.y, +half.z));
            _tempCorners[7] = t.TransformPoint(cLocal + new Vector3(-half.x, +half.y, +half.z));

            for (int i = 0; i < _boxEdges.Length; i += 2)
                AddLine(tg, _tempCorners[_boxEdges[i]], _tempCorners[_boxEdges[i + 1]]);
        }

        private void DrawSphereCollider(WireTarget tg, SphereCollider sc)
        {
            Transform t = sc.transform;
            Vector3 center = t.TransformPoint(sc.center);
            Vector3 s = t.lossyScale;
            float r = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            const int segments = 18;
            DrawRingWorld(tg, center, t.up, r, segments);
            DrawRingWorld(tg, center, t.right, r, segments);
            DrawRingWorld(tg, center, t.forward, r, segments);
        }

        private void DrawCapsuleCollider(WireTarget tg, CapsuleCollider cc)
        {
            Transform t = cc.transform;
            Vector3 axisLocal = cc.direction == 0 ? Vector3.right : (cc.direction == 1 ? Vector3.up : Vector3.forward);
            Vector3 axisWorld = t.TransformDirection(axisLocal).normalized;
            Vector3 s = t.lossyScale;
            float radiusScale = cc.direction == 0 ? Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))
                              : cc.direction == 1 ? Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z))
                              : Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            float radius = cc.radius * radiusScale;
            float heightScale = cc.direction == 0 ? Mathf.Abs(s.x) : (cc.direction == 1 ? Mathf.Abs(s.y) : Mathf.Abs(s.z));
            float height = Mathf.Max(cc.height * heightScale, radius * 2f);
            Vector3 center = t.TransformPoint(cc.center);
            float halfHeight = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = center + axisWorld * halfHeight;
            Vector3 bot = center - axisWorld * halfHeight;

            Vector3 p1 = Vector3.Cross(axisWorld, Vector3.up);
            if (p1.sqrMagnitude < 0.001f) p1 = Vector3.Cross(axisWorld, Vector3.right);
            p1.Normalize();
            Vector3 p2 = Vector3.Cross(axisWorld, p1).normalized;

            const int segments = 16;
            DrawRingWorldBasis(tg, top, p1, p2, radius, segments);
            DrawRingWorldBasis(tg, bot, p1, p2, radius, segments);

            for (int i = 0; i < 8; i++)
            {
                float ang = (i / 8f) * Mathf.PI * 2f;
                Vector3 off = (Mathf.Cos(ang) * p1 + Mathf.Sin(ang) * p2) * radius;
                AddLine(tg, top + off, bot + off);
            }
        }

        private void DrawRingWorld(WireTarget tg, Vector3 center, Vector3 axis, float radius, int segments)
        {
            Vector3 a = axis.normalized;
            Vector3 p1 = Vector3.Cross(a, Vector3.up);
            if (p1.sqrMagnitude < 0.001f) p1 = Vector3.Cross(a, Vector3.right);
            p1.Normalize();
            Vector3 p2 = Vector3.Cross(a, p1).normalized;
            DrawRingWorldBasis(tg, center, p1, p2, radius, segments);
        }

        private void DrawRingWorldBasis(WireTarget tg, Vector3 center, Vector3 p1, Vector3 p2, float radius, int segments)
        {
            Vector3 prev = center + (p1 * radius);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + (Mathf.Cos(ang) * p1 + Mathf.Sin(ang) * p2) * radius;
                AddLine(tg, prev, next);
                prev = next;
            }
        }

        private void DrawBoxBounds(WireTarget tg, Bounds b)
        {
            Vector3 c = b.center;
            Vector3 e = b.extents;

            _tempCorners[0] = c + new Vector3(-e.x, -e.y, -e.z);
            _tempCorners[1] = c + new Vector3(+e.x, -e.y, -e.z);
            _tempCorners[2] = c + new Vector3(+e.x, -e.y, +e.z);
            _tempCorners[3] = c + new Vector3(-e.x, -e.y, +e.z);
            _tempCorners[4] = c + new Vector3(-e.x, +e.y, -e.z);
            _tempCorners[5] = c + new Vector3(+e.x, +e.y, -e.z);
            _tempCorners[6] = c + new Vector3(+e.x, +e.y, +e.z);
            _tempCorners[7] = c + new Vector3(-e.x, +e.y, +e.z);

            for (int i = 0; i < _boxEdges.Length; i += 2)
                AddLine(tg, _tempCorners[_boxEdges[i]], _tempCorners[_boxEdges[i + 1]]);
        }

        // 【核心修复】直接使用已经抓取好的本地坐标线段，无需再查点
        private void DrawMeshColliderHardEdges(WireTarget tg, MeshCollider mc)
        {
            Mesh m = mc.sharedMesh;
            if (m == null)
            {
                if (tg.IsHackable) DrawBoxBounds(tg, mc.bounds);
                else DrawBuilding(tg, mc.bounds);
                return;
            }

            Vector3[] lines = GetHardEdgeLinesCached(m);
            if (lines == null || lines.Length < 2)
            {
                if (tg.IsHackable) DrawBoxBounds(tg, mc.bounds);
                else DrawBuilding(tg, mc.bounds);
                return;
            }

            Transform t = mc.transform;
            for (int i = 0; i < lines.Length; i += 2)
            {
                Vector3 p0 = t.TransformPoint(lines[i]);
                Vector3 p1 = t.TransformPoint(lines[i + 1]);
                AddLine(tg, p0, p1);
            }
        }

        // 【核心修复】将原来存点的缓存系统，改为直接存线段坐标
        private Vector3[] GetHardEdgeLinesCached(Mesh m)
        {
            if (m == null) return null;
            if (_meshEdgeCache.TryGetValue(m, out var cache))
            {
                if (Mathf.Approximately(cache.HardAngleDeg, MeshHardEdgeAngle)
                    && cache.IncludeBoundary == MeshIncludeBoundaryEdges
                    && cache.MaxEdges == MaxMeshEdges
                    && cache.LocalLines != null)
                {
                    return cache.LocalLines;
                }
            }

            Vector3[] lines = BuildHardEdgeLines(m, MeshHardEdgeAngle, MeshIncludeBoundaryEdges, MaxMeshEdges);
            _meshEdgeCache[m] = new MeshEdgeCache
            {
                HardAngleDeg = MeshHardEdgeAngle,
                IncludeBoundary = MeshIncludeBoundaryEdges,
                MaxEdges = MaxMeshEdges,
                LocalLines = lines
            };
            return lines;
        }

        private static Vector3[] BuildHardEdgeLines(Mesh m, float hardAngleDeg, bool includeBoundaryEdges, int maxEdges)
        {
            _tempTriangles.Clear();
            _tempVertices.Clear();
            m.GetTriangles(_tempTriangles, 0);
            m.GetVertices(_tempVertices);

            if (_tempTriangles.Count < 3 || _tempVertices.Count == 0) return null;

            float cosThreshold = Mathf.Cos(hardAngleDeg * Mathf.Deg2Rad);
            var edges = new Dictionary<ulong, EdgeAccum>(Mathf.Min(1024, _tempTriangles.Count / 3));

            for (int i = 0; i < _tempTriangles.Count; i += 3)
            {
                int a = _tempTriangles[i], b = _tempTriangles[i + 1], c = _tempTriangles[i + 2];
                Vector3 vA = _tempVertices[a];
                Vector3 n = Vector3.Cross(_tempVertices[b] - vA, _tempVertices[c] - vA);
                float mag = n.magnitude;
                if (mag > 1e-6f) n /= mag; else n = Vector3.up;
                AccumEdge(edges, a, b, n); AccumEdge(edges, b, c, n); AccumEdge(edges, c, a, n);
            }

            var picked = new List<Vector3>(Mathf.Min(maxEdges * 2, edges.Count * 2));
            foreach (var kv in edges)
            {
                EdgeAccum e = kv.Value;
                bool keep = e.FaceCount == 1 ? includeBoundaryEdges : Vector3.Dot(e.N0, e.N1) <= cosThreshold;
                if (!keep) continue;

                int v0 = (int)(kv.Key >> 32);
                int v1 = (int)(kv.Key & 0xFFFFFFFF);

                // 直接把真实的坐标推进去保存
                picked.Add(_tempVertices[v0]);
                picked.Add(_tempVertices[v1]);

                if (picked.Count >= maxEdges * 2) break;
            }
            return picked.Count > 0 ? picked.ToArray() : null;
        }

        private struct EdgeAccum { public int FaceCount; public Vector3 N0; public Vector3 N1; }

        private static void AccumEdge(Dictionary<ulong, EdgeAccum> edges, int i0, int i1, Vector3 n)
        {
            int min = i0 < i1 ? i0 : i1, max = i0 < i1 ? i1 : i0;
            ulong key = ((ulong)(uint)min << 32) | (uint)max;
            if (!edges.TryGetValue(key, out var e)) { e.FaceCount = 1; e.N0 = n; e.N1 = n; edges[key] = e; return; }
            if (e.FaceCount == 1) { e.FaceCount = 2; e.N1 = n; } else e.N1 = (e.N1 + n).normalized;
            edges[key] = e;
        }

        private void DrawBuilding(WireTarget tg, Bounds b)
        {
            DrawBoxBounds(tg, b);
            if (EnvironmentInteriorDetailLevel <= 0) return;
            Vector3 c = b.center, e = b.extents;
            int linesAdded = 0;
            int floorCount = Mathf.Clamp(Mathf.CeilToInt((e.y * 2f) / 4f), 1, 4);
            for (int i = 1; i <= floorCount && linesAdded < MaxInteriorLinesPerBuilding; i++)
            {
                float y = c.y - e.y + (e.y * 2f * (i / (float)(floorCount + 1)));
                AddLine(tg, new Vector3(c.x - e.x, y, c.z - e.z), new Vector3(c.x + e.x, y, c.z - e.z)); linesAdded++;
                if (linesAdded >= MaxInteriorLinesPerBuilding) break;
                AddLine(tg, new Vector3(c.x - e.x, y, c.z + e.z), new Vector3(c.x + e.x, y, c.z + e.z)); linesAdded++;
            }
            for (int i = 1; i <= 2 && linesAdded < MaxInteriorLinesPerBuilding; i++)
            {
                float x = c.x - e.x + (e.x * 2f * (i / 3f));
                AddLine(tg, new Vector3(x, c.y - e.y, c.z - e.z), new Vector3(x, c.y + e.y, c.z - e.z)); linesAdded++;
                if (linesAdded >= MaxInteriorLinesPerBuilding) break;
                AddLine(tg, new Vector3(x, c.y - e.y, c.z + e.z), new Vector3(x, c.y + e.y, c.z + e.z)); linesAdded++;
            }
        }

        private void AddLine(WireTarget tg, Vector3 start, Vector3 end)
        {
            int idx = GetLineSlot();
            tg.LineIndices.Add(idx);
            var slot = _pool[idx];
            slot.InUse = true;
            slot.Lr.positionCount = 2;
            slot.Lr.SetPosition(0, start);
            slot.Lr.SetPosition(1, end);
            slot.Go.SetActive(false);
        }

        private int GetLineSlot()
        {
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].InUse) return i;
            GameObject go = new GameObject("WireframeLine");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = WireframeMaterial != null ? WireframeMaterial : _cachedFallbackMat;
            lr.sortingOrder = 5000;
            _pool.Add(new LineSlot { Go = go, Lr = lr, InUse = true });
            return _pool.Count - 1;
        }

        private void ApplyVisuals(WireTarget tg, Color color, float width)
        {
            for (int i = 0; i < tg.LineIndices.Count; i++)
            {
                int idx = tg.LineIndices[i];
                if (idx < 0 || idx >= _pool.Count) continue;
                var lr = _pool[idx].Lr;
                lr.startColor = color; lr.endColor = color;
                lr.startWidth = width; lr.endWidth = width;
            }
        }

        private void SetVisible(WireTarget tg, bool visible)
        {
            if (tg.IsVisible == visible) return;

            tg.IsVisible = visible;
            for (int i = 0; i < tg.LineIndices.Count; i++)
            {
                int idx = tg.LineIndices[i];
                if (idx < 0 || idx >= _pool.Count) continue;
                _pool[idx].Go.SetActive(visible);
            }
        }

        private void ReleaseLines(WireTarget tg)
        {
            tg.IsVisible = false;
            for (int i = 0; i < tg.LineIndices.Count; i++)
            {
                int idx = tg.LineIndices[i];
                if (idx < 0 || idx >= _pool.Count) continue;
                _pool[idx].InUse = false;
                _pool[idx].Go.SetActive(false);
            }
        }

        private void RemoveTargetAt(int i)
        {
            WireTarget tg = _targets[i];
            if (tg != null && tg.Col != null) _targetsByCollider.Remove(tg.Col);
            if (tg != null) { ReleaseLines(tg); ReleaseWireTarget(tg); }
            _targets.RemoveAt(i);
        }

        private void ClearAll()
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                WireTarget tg = _targets[i];
                ReleaseLines(tg);
                ReleaseWireTarget(tg);
            }
            _targets.Clear();
            _targetsByCollider.Clear();
        }

        private bool ComputeOccluded(Vector3 targetPoint, Collider targetCol)
        {
            if (_cam == null) return false;
            Vector3 eye = _cam.transform.position, dir = targetPoint - eye;
            float len = dir.magnitude;
            if (len < 0.01f) return false;
            if (Physics.Raycast(eye, dir / len, out RaycastHit hit, len, OcclusionMask, QueryTriggerInteraction.Ignore))
                return hit.collider != targetCol;
            return false;
        }

        private static bool BoundsApproximatelyEqual(Bounds a, Bounds b)
        {
            const float eps = 0.05f;
            return (a.center - b.center).sqrMagnitude < eps && (a.extents - b.extents).sqrMagnitude < eps;
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _pool.Count; i++) if (_pool[i].Go != null) Destroy(_pool[i].Go);
            _pool.Clear(); _meshEdgeCache.Clear();
            if (_cachedFallbackMat != null) Destroy(_cachedFallbackMat);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    /// <summary>
    /// ScanColliderWireframeFX
    /// - Unscaled-time radial pulse centered on player
    /// - Band-pass rendering: visible only while wave band passes over target
    /// - Frustum-based target set (in view), not limited by scan radius
    /// - X-ray style: intended to be visible through walls (use a material/shader with ZTest Always)
    /// - Occlusion dimming: still visible through walls, but dim when occluded
    /// - Moving targets supported: bounds changes trigger a redraw so lines follow motion
    /// </summary>
    public class ScanColliderWireframeFX : MonoBehaviour
    {
        [Header("Wave (Unscaled Time)")]
        [Tooltip("How far the wave can reach. This is NOT your gameplay scan radius; it's the visual/logic reach for frustum targets.")]
        public float DetectionRadius = 900f;

        [Tooltip("Pulse duration in seconds (unscaled). Recommended 0.3 ~ 0.6.")]
        public float WaveDuration = 0.45f;

        [Tooltip("Wave band thickness (meters). Smaller = faster/narrower look.")]
        public float WaveWidth = 1.6f;

        [Tooltip("Extra distance used for distance falloff so far targets aren't instantly invisible.")]
        public float FalloffRadiusExtra = 3f;

        [Header("Acquisition")]
        [Tooltip("How often we refresh candidate colliders (unscaled seconds).")]
        public float AcquireInterval = 0.15f;

        [Tooltip("Max hackable colliders to render per pulse.")]
        public int MaxHackableTargets = 20;

        [Tooltip("Max environment colliders to render per pulse.")]
        public int MaxEnvironmentTargets = 12;

        [Tooltip("Environment colliders smaller than this bounds-size magnitude will be ignored.")]
        public float MinEnvironmentSize = 8f;

        [Tooltip("If enabled, environment colliders must be marked Static.")]
        public bool PreferStaticEnvironment = false;

        [Header("Masks")]
        public LayerMask HackableMask = ~0;
        public LayerMask EnvironmentMask = ~0;

        [Header("Material")]
        [Tooltip("Use an Unlit/Additive material. For true X-ray, use a shader that sets ZTest Always (or equivalent).")]
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
        [Tooltip("0 = bounds only, 1 = add some interior lines (Watch Dogs vibe).")]
        [Range(0, 1)] public int EnvironmentInteriorDetailLevel = 1;

        [Tooltip("Cap interior lines per environment target to avoid spaghetti.")]
        public int MaxInteriorLinesPerBuilding = 18;

        [Header("MeshCollider Wireframe (Watch Dogs Style)")]
        [Tooltip("Max edges to draw per MeshCollider (prevents heavy meshes from exploding lines).")]
        public int MaxMeshEdges = 1200;

        [Tooltip("Only draw 'hard edges' above this angle (degrees). Higher = fewer lines / cleaner.")]
        [Range(0f, 180f)]
        public float MeshHardEdgeAngle = 35f;

        [Tooltip("If true: always include boundary edges (edges used by only 1 triangle).")]
        public bool MeshIncludeBoundaryEdges = true;

        // --- runtime state ---
        private Transform _player;
        private Camera _cam;
        private bool _isScanning;
        private float _scanStartUnscaled;
        private float _nextAcquireUnscaled;

        private Plane[] _frustumPlanes;

        private readonly Dictionary<Collider, WireTarget> _targetsByCollider = new Dictionary<Collider, WireTarget>(128);
        private readonly List<WireTarget> _targets = new List<WireTarget>(128);

        private readonly List<LineSlot> _pool = new List<LineSlot>(256);

        // Mesh edge cache (per mesh + settings) to avoid recomputation every pulse
        private readonly Dictionary<Mesh, MeshEdgeCache> _meshEdgeCache = new Dictionary<Mesh, MeshEdgeCache>(64);

        private class WireTarget
        {
            public Collider Col;
            public bool IsHackable;

            public Bounds LastBounds;

            public bool Occluded;
            public float NextOccCheckUnscaled;

            public readonly List<int> LineIndices = new List<int>(32);
        }

        private class LineSlot
        {
            public GameObject Go;
            public LineRenderer Lr;
            public bool InUse;
        }

        private struct MeshEdgeCache
        {
            public float HardAngleDeg;
            public bool IncludeBoundary;
            public int MaxEdges;
            public int[] EdgePairs; // packed pairs: v0,v1,v0,v1...
        }

        /// <summary>
        /// Start a single pulse. Call when pressing V.
        /// </summary>
        public void BeginScan(Transform player, Camera mainCamera)
        {
            _player = player;
            _cam = mainCamera;

            _isScanning = true;
            _scanStartUnscaled = Time.unscaledTime;
            _nextAcquireUnscaled = 0f;

            ClearAll(); // ensure no stale lines remain
            AcquireTargetsUnscaled(force: true);
        }

        /// <summary>
        /// Stop immediately and clear all visuals (no lingering).
        /// Call when leaving scan (V again) or when you want to cancel.
        /// </summary>
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

            // Acquire candidates periodically (unscaled)
            if (now >= _nextAcquireUnscaled)
            {
                _nextAcquireUnscaled = now + Mathf.Max(0.02f, AcquireInterval);
                AcquireTargetsUnscaled(force: false);
            }

            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_cam);

            Vector3 origin = _player.position;
            float falloffRadius = Mathf.Max(1f, DetectionRadius + FalloffRadiusExtra);

            // Update all targets
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                WireTarget tg = _targets[i];
                if (tg.Col == null)
                {
                    RemoveTargetAt(i);
                    continue;
                }

                Bounds b = tg.Col.bounds;

                // Optional: only operate on objects in view
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b))
                {
                    SetVisible(tg, false);
                    continue;
                }

                float dist = Vector3.Distance(origin, b.center);

                // Band-pass: visible only while wave band passes over it
                float inner = waveRadius - WaveWidth;
                bool inBand = (dist >= inner && dist <= waveRadius);

                if (!inBand)
                {
                    SetVisible(tg, false);
                    continue;
                }

                // Update occlusion periodically (unscaled)
                if (now >= tg.NextOccCheckUnscaled)
                {
                    tg.NextOccCheckUnscaled = now + Mathf.Max(0.02f, OcclusionCheckInterval);
                    tg.Occluded = ComputeOccluded(b.center, tg.Col);
                }

                // Rebuild geometry if bounds changed (moving objects / rotating OBB causes bounds to change too)
                if (!BoundsApproximatelyEqual(tg.LastBounds, b))
                {
                    tg.LastBounds = b;
                    RedrawTarget(tg);
                }

                // Bell-shaped band intensity (smooth in/out inside band)
                float a = Mathf.InverseLerp(inner, waveRadius, dist); // 0..1
                float s = Smooth01(a);
                float band = 4f * s * (1f - s); // peak at 0.5

                // Distance boost (closer = brighter/thicker)
                float dist01 = Mathf.Clamp01(dist / falloffRadius);
                float nearBoost = Mathf.Pow(1f - dist01, NearPower);

                float occlMul = tg.Occluded ? OccludedDimFactor : 1f;

                // Slight flash-to-white at wave front (fast/narrow pulse feel)
                float front01 = Mathf.Clamp01((dist - inner) / Mathf.Max(0.0001f, WaveWidth)); // 0..1 (back->front)
                float flash = Mathf.Pow(front01, 6f); // sharper near front
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
                ApplyVisuals(tg, col, width);
                SetVisible(tg, true);
            }

            // Auto-end pulse at completion (single pulse behavior)
            if (t >= 1f)
                EndScan();
        }

        // ---------------------------
        // Acquisition
        // ---------------------------

        private void AcquireTargetsUnscaled(bool force)
        {
            if (_player == null || _cam == null) return;

            Vector3 origin = _player.position;
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_cam);

            // Hackables (always)
            int hackCount = 0;
            Collider[] hacks = Physics.OverlapSphere(origin, DetectionRadius, HackableMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hacks.Length && hackCount < MaxHackableTargets; i++)
            {
                Collider c = hacks[i];
                if (c == null) continue;

                Bounds b = c.bounds;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b)) continue;

                if (TryAddTarget(c, isHackable: true))
                    hackCount++;
            }

            // Environment (large buildings/walls only)
            List<(Collider c, float d)> env = null;

            Collider[] envCols = Physics.OverlapSphere(origin, DetectionRadius, EnvironmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < envCols.Length; i++)
            {
                Collider c = envCols[i];
                if (c == null) continue;

                // Skip if also hackable (hackable wins)
                if (((1 << c.gameObject.layer) & HackableMask.value) != 0)
                    continue;

                Bounds b = c.bounds;

                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, b)) continue;
                if (PreferStaticEnvironment && !c.gameObject.isStatic) continue;
                if (b.size.magnitude < MinEnvironmentSize) continue;

                float d = Vector3.Distance(origin, b.center);
                env ??= new List<(Collider, float)>(64);
                env.Add((c, d));
            }

            if (env == null || env.Count == 0) return;

            env.Sort((a, b) => a.d.CompareTo(b.d));

            int envCount = 0;
            for (int i = 0; i < env.Count && envCount < MaxEnvironmentTargets; i++)
            {
                if (TryAddTarget(env[i].c, isHackable: false))
                    envCount++;
            }
        }

        private bool TryAddTarget(Collider c, bool isHackable)
        {
            if (c == null) return false;

            if (_targetsByCollider.TryGetValue(c, out var existing))
            {
                // If it was environment but now should be hackable, upgrade
                if (isHackable && !existing.IsHackable)
                {
                    existing.IsHackable = true;
                }
                return false;
            }

            var tg = new WireTarget
            {
                Col = c,
                IsHackable = isHackable,
                LastBounds = c.bounds,
                NextOccCheckUnscaled = Time.unscaledTime
            };

            _targetsByCollider.Add(c, tg);
            _targets.Add(tg);

            DrawTarget(tg);
            SetVisible(tg, false); // only show when band passes

            return true;
        }

        // ---------------------------
        // Drawing / Pool
        // ---------------------------

        private void DrawTarget(WireTarget tg)
        {
            if (tg.Col == null) return;

            // Prefer accurate collider-based wireframes so they don't look "skewed"
            if (tg.Col is BoxCollider bc)
            {
                DrawBoxColliderOBB(tg, bc);
                return;
            }

            if (tg.Col is SphereCollider sc)
            {
                DrawSphereCollider(tg, sc);
                return;
            }

            if (tg.Col is CapsuleCollider cc)
            {
                DrawCapsuleCollider(tg, cc);
                return;
            }

            if (tg.Col is MeshCollider mc)
            {
                // Watch Dogs style: hard edges / boundary edges only (cleaner than full triangle wireframe)
                DrawMeshColliderHardEdges(tg, mc);
                return;
            }

            // Fallback: bounds-based
            Bounds b = tg.Col.bounds;
            if (tg.IsHackable)
                DrawBoxBounds(tg, b);
            else
                DrawBuilding(tg, b);
        }

        private void RedrawTarget(WireTarget tg)
        {
            ReleaseLines(tg);
            DrawTarget(tg);
            SetVisible(tg, false);
        }

        // --- BoxCollider: oriented box (no "bounds skew") ---
        private void DrawBoxColliderOBB(WireTarget tg, BoxCollider bc)
        {
            Transform t = bc.transform;

            Vector3 half = bc.size * 0.5f;
            Vector3 cLocal = bc.center;

            Vector3[] local =
            {
                cLocal + new Vector3(-half.x, -half.y, -half.z),
                cLocal + new Vector3(+half.x, -half.y, -half.z),
                cLocal + new Vector3(+half.x, -half.y, +half.z),
                cLocal + new Vector3(-half.x, -half.y, +half.z),
                cLocal + new Vector3(-half.x, +half.y, -half.z),
                cLocal + new Vector3(+half.x, +half.y, -half.z),
                cLocal + new Vector3(+half.x, +half.y, +half.z),
                cLocal + new Vector3(-half.x, +half.y, +half.z),
            };

            Vector3[] w = new Vector3[8];
            for (int i = 0; i < 8; i++)
                w[i] = t.TransformPoint(local[i]);

            int[,] edges =
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };

            for (int i = 0; i < edges.GetLength(0); i++)
                AddLine(tg, w[edges[i, 0]], w[edges[i, 1]]);
        }

        // --- SphereCollider: accurate center/radius with transform ---
        private void DrawSphereCollider(WireTarget tg, SphereCollider sc)
        {
            Transform t = sc.transform;
            Vector3 center = t.TransformPoint(sc.center);

            // Radius uses the largest axis scale so it encloses nicely
            Vector3 s = t.lossyScale;
            float r = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));

            const int segments = 18;
            DrawRingWorld(tg, center, t.up, r, segments);
            DrawRingWorld(tg, center, t.right, r, segments);
            DrawRingWorld(tg, center, t.forward, r, segments);
        }

        // --- CapsuleCollider: support direction X/Y/Z and scale ---
        private void DrawCapsuleCollider(WireTarget tg, CapsuleCollider cc)
        {
            Transform t = cc.transform;

            // direction: 0=X, 1=Y, 2=Z
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

            // ring basis perpendicular to axisWorld
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

        // --- Bounds-based box fallback (kept from your original) ---
        private void DrawBoxBounds(WireTarget tg, Bounds b)
        {
            Vector3 c = b.center;
            Vector3 e = b.extents;

            Vector3[] corners =
            {
                c + new Vector3(-e.x,-e.y,-e.z),
                c + new Vector3(+e.x,-e.y,-e.z),
                c + new Vector3(+e.x,-e.y,+e.z),
                c + new Vector3(-e.x,-e.y,+e.z),
                c + new Vector3(-e.x,+e.y,-e.z),
                c + new Vector3(+e.x,+e.y,-e.z),
                c + new Vector3(+e.x,+e.y,+e.z),
                c + new Vector3(-e.x,+e.y,+e.z),
            };

            int[,] edges =
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };

            for (int i = 0; i < edges.GetLength(0); i++)
                AddLine(tg, corners[edges[i, 0]], corners[edges[i, 1]]);
        }

        // --- MeshCollider (Watch Dogs style): hard edges + boundary edges only ---
        private void DrawMeshColliderHardEdges(WireTarget tg, MeshCollider mc)
        {
            Mesh m = mc.sharedMesh;
            if (m == null)
            {
                // fallback
                if (tg.IsHackable) DrawBoxBounds(tg, mc.bounds);
                else DrawBuilding(tg, mc.bounds);
                return;
            }

            int[] pairs = GetHardEdgePairsCached(m);
            if (pairs == null || pairs.Length < 2)
            {
                if (tg.IsHackable) DrawBoxBounds(tg, mc.bounds);
                else DrawBuilding(tg, mc.bounds);
                return;
            }

            Vector3[] verts = m.vertices;
            Transform t = mc.transform;

            for (int i = 0; i < pairs.Length; i += 2)
            {
                int i0 = pairs[i];
                int i1 = pairs[i + 1];
                if ((uint)i0 >= (uint)verts.Length || (uint)i1 >= (uint)verts.Length) continue;

                Vector3 p0 = t.TransformPoint(verts[i0]);
                Vector3 p1 = t.TransformPoint(verts[i1]);
                AddLine(tg, p0, p1);
            }
        }

        private int[] GetHardEdgePairsCached(Mesh m)
        {
            if (m == null) return null;

            if (_meshEdgeCache.TryGetValue(m, out var cache))
            {
                if (Mathf.Approximately(cache.HardAngleDeg, MeshHardEdgeAngle)
                    && cache.IncludeBoundary == MeshIncludeBoundaryEdges
                    && cache.MaxEdges == MaxMeshEdges
                    && cache.EdgePairs != null)
                {
                    return cache.EdgePairs;
                }
            }

            int[] pairs = BuildHardEdgePairs(m, MeshHardEdgeAngle, MeshIncludeBoundaryEdges, MaxMeshEdges);

            _meshEdgeCache[m] = new MeshEdgeCache
            {
                HardAngleDeg = MeshHardEdgeAngle,
                IncludeBoundary = MeshIncludeBoundaryEdges,
                MaxEdges = MaxMeshEdges,
                EdgePairs = pairs
            };

            return pairs;
        }

        private static int[] BuildHardEdgePairs(Mesh m, float hardAngleDeg, bool includeBoundaryEdges, int maxEdges)
        {
            int[] tris = m.triangles;
            Vector3[] v = m.vertices;
            if (tris == null || tris.Length < 3 || v == null || v.Length == 0) return null;

            float cosThreshold = Mathf.Cos(hardAngleDeg * Mathf.Deg2Rad);

            // edge key: (min<<32) | max
            var edges = new Dictionary<ulong, EdgeAccum>(Mathf.Min(1024, tris.Length));

            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];

                // face normal in local space
                Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                float mag = n.magnitude;
                if (mag > 1e-6f) n /= mag;
                else n = Vector3.up;

                AccumEdge(edges, a, b, n);
                AccumEdge(edges, b, c, n);
                AccumEdge(edges, c, a, n);
            }

            // pick edges
            var picked = new List<int>(Mathf.Min(maxEdges * 2, edges.Count * 2));

            foreach (var kv in edges)
            {
                EdgeAccum e = kv.Value;

                bool keep = false;

                if (e.FaceCount == 1)
                {
                    keep = includeBoundaryEdges;
                }
                else
                {
                    // compare averaged normals of the two sides (approx)
                    // If dot is small => angle large => hard edge
                    float d = Vector3.Dot(e.N0, e.N1);
                    keep = d <= cosThreshold;
                }

                if (!keep) continue;

                // decode key -> v0,v1
                int v0 = (int)(kv.Key >> 32);
                int v1 = (int)(kv.Key & 0xFFFFFFFF);

                picked.Add(v0);
                picked.Add(v1);

                if (picked.Count >= maxEdges * 2)
                    break;
            }

            return picked.Count > 0 ? picked.ToArray() : null;
        }

        private struct EdgeAccum
        {
            public int FaceCount;
            public Vector3 N0;
            public Vector3 N1;
        }

        private static void AccumEdge(Dictionary<ulong, EdgeAccum> edges, int i0, int i1, Vector3 n)
        {
            int min = i0 < i1 ? i0 : i1;
            int max = i0 < i1 ? i1 : i0;
            ulong key = ((ulong)(uint)min << 32) | (uint)max;

            if (!edges.TryGetValue(key, out var e))
            {
                e.FaceCount = 1;
                e.N0 = n;
                e.N1 = n;
                edges[key] = e;
                return;
            }

            // store second face normal
            if (e.FaceCount == 1)
            {
                e.FaceCount = 2;
                e.N1 = n;
            }
            else
            {
                // more than 2 faces share an edge (non-manifold) - just blend a bit
                e.N1 = (e.N1 + n).normalized;
            }

            edges[key] = e;
        }

        private void DrawBuilding(WireTarget tg, Bounds b)
        {
            // Always bounds box
            DrawBoxBounds(tg, b);

            if (EnvironmentInteriorDetailLevel <= 0) return;

            Vector3 c = b.center;
            Vector3 e = b.extents;

            int linesAdded = 0;

            // A few "floors"
            int floorCount = Mathf.Clamp(Mathf.CeilToInt((e.y * 2f) / 4f), 1, 4);
            for (int i = 1; i <= floorCount && linesAdded < MaxInteriorLinesPerBuilding; i++)
            {
                float t = i / (float)(floorCount + 1);
                float y = c.y - e.y + (e.y * 2f * t);

                Vector3 a = new Vector3(c.x - e.x, y, c.z - e.z);
                Vector3 b1 = new Vector3(c.x + e.x, y, c.z - e.z);
                Vector3 c1 = new Vector3(c.x + e.x, y, c.z + e.z);
                Vector3 d = new Vector3(c.x - e.x, y, c.z + e.z);

                AddLine(tg, a, b1); linesAdded++;
                if (linesAdded >= MaxInteriorLinesPerBuilding) break;
                AddLine(tg, d, c1); linesAdded++;
            }

            // A few vertical grid hints
            int verticalCount = 2;
            for (int i = 1; i <= verticalCount && linesAdded < MaxInteriorLinesPerBuilding; i++)
            {
                float tx = i / (float)(verticalCount + 1);
                float x = c.x - e.x + (e.x * 2f * tx);

                Vector3 p0 = new Vector3(x, c.y - e.y, c.z - e.z);
                Vector3 p1 = new Vector3(x, c.y + e.y, c.z - e.z);
                AddLine(tg, p0, p1); linesAdded++;
                if (linesAdded >= MaxInteriorLinesPerBuilding) break;

                Vector3 q0 = new Vector3(x, c.y - e.y, c.z + e.z);
                Vector3 q1 = new Vector3(x, c.y + e.y, c.z + e.z);
                AddLine(tg, q0, q1); linesAdded++;
            }
        }

        private void AddLine(WireTarget tg, Vector3 start, Vector3 end)
        {
            int idx = GetLineSlot();
            tg.LineIndices.Add(idx);

            var slot = _pool[idx];
            slot.InUse = true;

            var lr = slot.Lr;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            // default visuals (will be overridden when active)
            lr.startWidth = BaseWidth;
            lr.endWidth = BaseWidth;
            lr.startColor = BaseBlue;
            lr.endColor = BaseBlue;

            slot.Go.SetActive(false);
        }

        private int GetLineSlot()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].InUse)
                    return i;
            }

            GameObject go = new GameObject("WireframeLine");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View; // keep your current look (thin glowing scan feel)
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            if (WireframeMaterial != null)
            {
                lr.material = WireframeMaterial;
            }
            else
            {
                // fallback (not true x-ray). Replace with your additive ZTestAlways shader material for best results.
                lr.material = new Material(Shader.Find("Unlit/Color"));
            }

            // Encourage drawing on top; true X-ray still needs a proper shader (ZTest Always).
            lr.sortingOrder = 5000;
            if (lr.material != null) lr.material.renderQueue = 5000;

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

                lr.startColor = color;
                lr.endColor = color;
                lr.startWidth = width;
                lr.endWidth = width;
            }
        }

        private void SetVisible(WireTarget tg, bool visible)
        {
            for (int i = 0; i < tg.LineIndices.Count; i++)
            {
                int idx = tg.LineIndices[i];
                if (idx < 0 || idx >= _pool.Count) continue;
                _pool[idx].Go.SetActive(visible);
            }
        }

        private void ReleaseLines(WireTarget tg)
        {
            for (int i = 0; i < tg.LineIndices.Count; i++)
            {
                int idx = tg.LineIndices[i];
                if (idx < 0 || idx >= _pool.Count) continue;

                _pool[idx].InUse = false;
                _pool[idx].Go.SetActive(false);
            }
            tg.LineIndices.Clear();
        }

        private void RemoveTargetAt(int i)
        {
            WireTarget tg = _targets[i];
            if (tg != null && tg.Col != null)
                _targetsByCollider.Remove(tg.Col);

            if (tg != null)
                ReleaseLines(tg);

            _targets.RemoveAt(i);
        }

        private void ClearAll()
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                ReleaseLines(_targets[i]);
            }
            _targets.Clear();
            _targetsByCollider.Clear();
        }

        private bool ComputeOccluded(Vector3 targetPoint, Collider targetCol)
        {
            if (_cam == null) return false;

            Vector3 eye = _cam.transform.position;
            Vector3 dir = targetPoint - eye;
            float len = dir.magnitude;
            if (len < 0.01f) return false;

            dir /= len;

            if (Physics.Raycast(eye, dir, out RaycastHit hit, len, OcclusionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider != targetCol;
            }
            return false;
        }

        private static bool BoundsApproximatelyEqual(Bounds a, Bounds b)
        {
            const float eps = 0.0004f;
            return (a.center - b.center).sqrMagnitude < eps && (a.extents - b.extents).sqrMagnitude < eps;
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            // cubic smoothstep
            return x * x * (3f - 2f * x);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].Go != null)
                    Destroy(_pool[i].Go);
            }
            _pool.Clear();
            _meshEdgeCache.Clear();
        }
    }
}

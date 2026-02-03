using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public class ScanColliderWireframeFX : MonoBehaviour
    {
        [Header("Detection Range")]
        [Tooltip("物理检测半径：决定了能扫出多远的东西")]
        public float DetectionRadius = 300f;

        [Header("Targets Limiter")]
        public int MaxHackableTargets = 100;
        public int MaxEnvironmentTargets = 800;
        public float MinEnvironmentSize = 0.5f;

        [Header("Material")]
        public Material WireframeMaterial;

        [Header("Colors")]
        public Color BaseBlue = new Color(0.0f, 0.7f, 1f, 1f);
        public Color HackableOrange = new Color(1f, 0.35f, 0.0f, 1f);
        public Color WaveFrontColor = new Color(1f, 1f, 1f, 1f);

        [Header("Wave Physics (The Pulse)")]
        [Tooltip("波浪扩散速度 (米/秒)")]
        public float WaveSpeed = 60f;

        [Tooltip("波浪的厚度 (米)。环境物体只有处在这个厚度带里才会显示")]
        public float WaveWidth = 25f;


        [Header("Line Settings")]
        [Range(0f, 1f)] public float MaxAlpha = 0.8f;
        public float BaseWidth = 0.01f;
        public float HackableWidthMultiplier = 2.0f;

        [Header("Occlusion")]
        public LayerMask OcclusionMask = ~0;
        public float OccludedDimFactor = 0.2f;
        // ★★★ 修复：补回了漏掉的变量定义 ★★★
        public float OcclusionCheckInterval = 0.2f;

        // 内部状态
        private Transform _playerTransform;
        private Vector3 _scanOrigin;
        private float _scanStartTime;
        private bool _isScanning;
        private LayerMask _hackableMask;
        private LayerMask _environmentMask;
        private Camera _mainCamera;

        private readonly List<WireframeTarget> _activeTargets = new List<WireframeTarget>();
        private readonly List<LineRendererPool> _linePool = new List<LineRendererPool>();

        private class WireframeTarget
        {
            public Collider Collider;
            public Transform Trans;
            public Vector3 CenterPoint;
            public bool IsHackable;

            // 状态控制
            public bool HasHit;
            public float DistToOrigin;

            public bool IsOccluded;
            public float NextOcclusionCheck;
            public List<int> LineIndices = new List<int>();
        }

        private class LineRendererPool
        {
            public GameObject GameObject;
            public LineRenderer Renderer;
            public bool InUse;
        }

        public void BeginScan(Transform playerTransform, float scanRadius_Unused, LayerMask hackableMask, LayerMask environmentMask, ScanScreenFX screenFX, Camera mainCamera)
        {
            _playerTransform = playerTransform;
            _scanOrigin = playerTransform.position; // 锁定中心
            _scanStartTime = Time.time;

            _hackableMask = hackableMask;
            _environmentMask = environmentMask;
            _mainCamera = mainCamera;
            _isScanning = true;

            AcquireTargets();
        }

        public void EndScan()
        {
            _isScanning = false;
            // 立即清理所有目标和可视化
            foreach (var t in _activeTargets) ReleaseTarget(t);
            _activeTargets.Clear();
        }

        private void Update()
        {
            if (!_isScanning)
            {
                // 非扫描状态：清理所有目标
                if (_activeTargets.Count > 0)
                {
                    foreach (var t in _activeTargets) ReleaseTarget(t);
                    _activeTargets.Clear();
                }
                return;
            }

            // 1. 计算波浪当前位置
            float timeElapsed = Time.time - _scanStartTime;
            float waveRadius = timeElapsed * WaveSpeed;

            // 2. 构建视锥体平面（用于剔除视野外物体）
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);

            for (int i = _activeTargets.Count - 1; i >= 0; i--)
            {
                var target = _activeTargets[i];
                if (target.Collider == null)
                {
                    ReleaseTarget(target);
                    _activeTargets.RemoveAt(i);
                    continue;
                }

                // 3. 更新移动物体的位置和线段几何
                Bounds currentBounds = target.Collider.bounds;
                bool boundsChanged = Vector3.Distance(target.CenterPoint, currentBounds.center) > 0.01f;

                if (boundsChanged)
                {
                    target.CenterPoint = currentBounds.center;
                    ReleaseTarget(target); // 释放旧线段
                    bool success = DrawWireframe(target); // 重新绘制
                    if (!success)
                    {
                        _activeTargets.RemoveAt(i);
                        continue;
                    }
                }

                // 4. 计算当前距离（每帧更新，支持移动物体）
                float dist = Vector3.Distance(_scanOrigin, target.CenterPoint);
                target.DistToOrigin = dist;

                // 5. 视锥剔除：不在视野内的物体不显示
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, currentBounds))
                {
                    UpdateTargetVisuals(target, Color.clear, 0f);
                    continue;
                }

                // 6. 核心逻辑：计算物体与波浪的关系
                float distBehindWave = waveRadius - dist; // >0 表示波浪已经扫过去了

                float finalAlpha = 0f;
                float widthScale = 1f;
                Color targetColor = BaseBlue;

                // Case A: 波浪还没到
                if (distBehindWave < 0)
                {
                    finalAlpha = 0f;
                }
                // Case B: 波浪正在经过 (Active Pulse) - 唯一可见区域
                else if (distBehindWave >= 0 && distBehindWave < WaveWidth)
                {
                    target.HasHit = true;

                    // 在波浪带中的位置 (0 = 波头, 1 = 波尾)
                    float waveProgress = distBehindWave / WaveWidth;

                    // 平滑衰减曲线：前半段快速上升，后半段缓慢下降
                    float frontFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - waveProgress) * 2f));
                    float backFade = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(waveProgress));
                    finalAlpha = Mathf.Max(frontFade, backFade * 0.5f);

                    // Color: 波头是白色高亮
                    targetColor = target.IsHackable ? HackableOrange : BaseBlue;
                    float colorBlend = Mathf.Pow(waveProgress, 0.7f);
                    targetColor = Color.Lerp(WaveFrontColor, targetColor, colorBlend);

                    // Width: 波头稍微粗一点
                    widthScale = Mathf.Lerp(1.5f, 1.0f, waveProgress);
                }
                // Case C: 波浪已经过去 - 立刻消失（无论是否hackable）
                else
                {
                    finalAlpha = 0f;
                }

                // 7. 应用遮挡剔除
                if (finalAlpha > 0.01f)
                {
                    UpdateOcclusion(target);
                    if (target.IsOccluded) finalAlpha *= OccludedDimFactor;
                }

                // 8. 最终应用
                finalAlpha *= MaxAlpha;

                if (target.IsHackable && finalAlpha > 0.001f)
                {
                    finalAlpha = Mathf.Min(1f, finalAlpha * 1.5f);
                    widthScale *= HackableWidthMultiplier;
                }

                if (finalAlpha < 0.01f)
                {
                    targetColor = Color.clear;
                }
                else
                {
                    targetColor.a = finalAlpha;
                }

                UpdateTargetVisuals(target, targetColor, BaseWidth * widthScale);
            }
        }


        private void AcquireTargets()
        {
            Vector3 origin = _scanOrigin;

            // 1. Hackables
            var hackables = Physics.OverlapSphere(origin, DetectionRadius, _hackableMask, QueryTriggerInteraction.Ignore);
            int hCount = 0;
            foreach (var col in hackables)
            {
                if (hCount >= MaxHackableTargets) break;
                CreateWireframeTarget(col, true);
                hCount++;
            }

            // 2. Environment
            var environment = Physics.OverlapSphere(origin, DetectionRadius, _environmentMask, QueryTriggerInteraction.Ignore);

            System.Array.Sort(environment, (a, b) => {
                float da = Vector3.SqrMagnitude(a.transform.position - origin);
                float db = Vector3.SqrMagnitude(b.transform.position - origin);
                return da.CompareTo(db);
            });

            int eCount = 0;
            foreach (var col in environment)
            {
                if (eCount >= MaxEnvironmentTargets) break;
                if (col.bounds.size.magnitude < MinEnvironmentSize) continue;
                CreateWireframeTarget(col, false);
                eCount++;
            }
        }

        private void CreateWireframeTarget(Collider col, bool isHackable)
        {
            // 防止重复添加相同的 collider
            foreach (var existing in _activeTargets)
            {
                if (existing.Collider == col) return;
            }

            var target = new WireframeTarget
            {
                Collider = col,
                Trans = col.transform,
                CenterPoint = col.bounds.center,
                IsHackable = isHackable,
                HasHit = false,
                DistToOrigin = Vector3.Distance(_scanOrigin, col.bounds.center),
                IsOccluded = false,
                NextOcclusionCheck = Time.time + Random.Range(0f, 0.2f)
            };

            bool success = DrawWireframe(target);
            if (success)
            {
                UpdateTargetVisuals(target, Color.clear, 0f);
                _activeTargets.Add(target);
            }
        }


        private bool DrawWireframe(WireframeTarget target)
        {
            if (target.Collider is BoxCollider box) { DrawRotatedBox(target, box); return true; }
            else if (target.Collider is SphereCollider sphere) { DrawRotatedSphere(target, sphere); return true; }
            else if (target.Collider is CapsuleCollider capsule) { DrawRotatedCapsule(target, capsule); return true; }

            MeshFilter mf = target.Collider.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { DrawMeshOBB(target, mf.sharedMesh); return true; }

            if (target.Collider is MeshCollider) { DrawAABB(target, target.Collider.bounds); return true; }

            return false;
        }

        private void DrawRotatedBox(WireframeTarget target, BoxCollider box)
        {
            Transform t = target.Trans;
            Vector3 c = box.center; Vector3 s = box.size * 0.5f;
            Vector3[] pts = GetBoxCorners(c, s);
            for (int i = 0; i < 8; i++) pts[i] = t.TransformPoint(pts[i]);
            DrawBoxFromCorners(target, pts);
            if (!target.IsHackable && box.size.y > 3f) DrawBuildingInterior(target, pts, 3);
        }

        private void DrawMeshOBB(WireframeTarget target, Mesh mesh)
        {
            Transform t = target.Trans;
            Bounds b = mesh.bounds;
            Vector3[] pts = GetBoxCorners(b.center, b.extents);
            for (int i = 0; i < 8; i++) pts[i] = t.TransformPoint(pts[i]);
            DrawBoxFromCorners(target, pts);
        }

        private void DrawAABB(WireframeTarget target, Bounds b)
        {
            DrawBoxFromCorners(target, GetBoxCornersWorld(b));
        }

        private Vector3[] GetBoxCorners(Vector3 c, Vector3 s)
        {
            return new Vector3[8] {
                c + new Vector3(-s.x, -s.y, -s.z), c + new Vector3( s.x, -s.y, -s.z),
                c + new Vector3( s.x, -s.y,  s.z), c + new Vector3(-s.x, -s.y,  s.z),
                c + new Vector3(-s.x,  s.y, -s.z), c + new Vector3( s.x,  s.y, -s.z),
                c + new Vector3( s.x,  s.y,  s.z), c + new Vector3(-s.x,  s.y,  s.z)
            };
        }

        private Vector3[] GetBoxCornersWorld(Bounds b)
        {
            Vector3 c = b.center; Vector3 s = b.extents;
            return new Vector3[8] {
                c + new Vector3(-s.x, -s.y, -s.z), c + new Vector3( s.x, -s.y, -s.z),
                c + new Vector3( s.x, -s.y,  s.z), c + new Vector3(-s.x, -s.y,  s.z),
                c + new Vector3(-s.x,  s.y, -s.z), c + new Vector3( s.x,  s.y, -s.z),
                c + new Vector3( s.x,  s.y,  s.z), c + new Vector3(-s.x,  s.y,  s.z)
            };
        }

        private void DrawRotatedSphere(WireframeTarget target, SphereCollider sphere)
        {
            Transform t = target.Trans;
            float r = sphere.radius * Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z));
            Vector3 worldC = t.TransformPoint(sphere.center);
            DrawCircle(target, worldC, t.up, t.right, r);
            DrawCircle(target, worldC, t.right, t.forward, r);
            DrawCircle(target, worldC, t.forward, t.up, r);
        }

        private void DrawRotatedCapsule(WireframeTarget target, CapsuleCollider cap)
        {
            Transform t = target.Trans;
            float maxScale = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z));
            float r = cap.radius * maxScale; float h = cap.height * maxScale;
            Vector3 worldUp = (cap.direction == 1) ? t.up : (cap.direction == 0 ? t.right : t.forward);
            Vector3 worldC = t.TransformPoint(cap.center);
            float cylinderHalfH = Mathf.Max(0, h * 0.5f - r);
            Vector3 top = worldC + worldUp * cylinderHalfH;
            Vector3 bottom = worldC - worldUp * cylinderHalfH;

            Vector3 cross = Vector3.Cross(worldUp, Vector3.up);
            if (cross.magnitude < 0.01f) cross = Vector3.Cross(worldUp, Vector3.right);
            cross.Normalize();
            Vector3 cross2 = Vector3.Cross(worldUp, cross).normalized;

            AddLine(target, top + cross * r, bottom + cross * r);
            AddLine(target, top - cross * r, bottom - cross * r);
            AddLine(target, top + cross2 * r, bottom + cross2 * r);
            AddLine(target, top - cross2 * r, bottom - cross2 * r);
            DrawCircle(target, top, worldUp, cross, r);
            DrawCircle(target, bottom, worldUp, cross, r);
        }

        private void DrawBoxFromCorners(WireframeTarget target, Vector3[] p)
        {
            AddLine(target, p[0], p[1]); AddLine(target, p[1], p[2]); AddLine(target, p[2], p[3]); AddLine(target, p[3], p[0]);
            AddLine(target, p[4], p[5]); AddLine(target, p[5], p[6]); AddLine(target, p[6], p[7]); AddLine(target, p[7], p[4]);
            AddLine(target, p[0], p[4]); AddLine(target, p[1], p[5]); AddLine(target, p[2], p[6]); AddLine(target, p[3], p[7]);
        }

        private void DrawBuildingInterior(WireframeTarget target, Vector3[] p, int cuts)
        {
            for (int i = 1; i <= cuts; i++)
            {
                float t = (float)i / (cuts + 1);
                AddLine(target, Vector3.Lerp(p[0], p[4], t), Vector3.Lerp(p[1], p[5], t));
                AddLine(target, Vector3.Lerp(p[1], p[5], t), Vector3.Lerp(p[2], p[6], t));
                AddLine(target, Vector3.Lerp(p[2], p[6], t), Vector3.Lerp(p[3], p[7], t));
                AddLine(target, Vector3.Lerp(p[3], p[7], t), Vector3.Lerp(p[0], p[4], t));
            }
        }

        private void DrawCircle(WireframeTarget target, Vector3 center, Vector3 axis, Vector3 perp, float radius)
        {
            int segments = 12;
            Vector3 lastP = center + perp.normalized * radius;
            for (int i = 1; i <= segments; i++)
            {
                Quaternion q = Quaternion.AngleAxis(360f * (i / (float)segments), axis);
                Vector3 currentP = center + (q * perp.normalized) * radius;
                AddLine(target, lastP, currentP);
                lastP = currentP;
            }
        }

        private void AddLine(WireframeTarget target, Vector3 start, Vector3 end)
        {
            int index = GetLineFromPool();
            target.LineIndices.Add(index);
            var pool = _linePool[index];
            pool.InUse = true;
            pool.Renderer.positionCount = 2;
            pool.Renderer.SetPosition(0, start);
            pool.Renderer.SetPosition(1, end);
            pool.GameObject.SetActive(true);
        }

        private int GetLineFromPool()
        {
            for (int i = 0; i < _linePool.Count; i++) if (!_linePool[i].InUse) return i;
            var go = new GameObject("WireframeLine");
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = WireframeMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;
            lr.numCapVertices = 0;
            lr.alignment = LineAlignment.View;
            _linePool.Add(new LineRendererPool { GameObject = go, Renderer = lr, InUse = true });
            return _linePool.Count - 1;
        }

        private void UpdateTargetVisuals(WireframeTarget target, Color color, float width)
        {
            bool visible = color.a > 0.005f;
            foreach (int index in target.LineIndices)
            {
                var pool = _linePool[index];
                if (pool.GameObject.activeSelf != visible) pool.GameObject.SetActive(visible);
                if (visible)
                {
                    pool.Renderer.startColor = color; pool.Renderer.endColor = color;
                    pool.Renderer.startWidth = width; pool.Renderer.endWidth = width;
                }
            }
        }

        private void UpdateOcclusion(WireframeTarget target)
        {
            if (Time.time < target.NextOcclusionCheck) return;
            // ★这里现在可以正常访问了
            target.NextOcclusionCheck = Time.time + OcclusionCheckInterval;

            if (_mainCamera == null) return;

            Vector3 eye = _mainCamera.transform.position;
            Vector3 dir = target.CenterPoint - eye;

            if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dir.magnitude, OcclusionMask, QueryTriggerInteraction.Ignore))
            {
                target.IsOccluded = (hit.collider != target.Collider);
            }
            else target.IsOccluded = false;
        }

        private void ReleaseTarget(WireframeTarget target)
        {
            foreach (int index in target.LineIndices)
            {
                _linePool[index].InUse = false;
                _linePool[index].GameObject.SetActive(false);
            }
            target.LineIndices.Clear();
        }

        private void OnDestroy()
        {
            foreach (var pool in _linePool) if (pool.GameObject) Destroy(pool.GameObject);
            _linePool.Clear();
        }
    }
}
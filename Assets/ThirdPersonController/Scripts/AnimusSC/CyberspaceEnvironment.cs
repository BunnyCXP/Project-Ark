using System.Collections;
using UnityEngine;

namespace TheGlitch
{
    public class CyberspaceEnvironment : MonoBehaviour
    {
        [Header("经典 Animus 纯白空间")]
        public GameObject MatrixBox;
        public float AbyssDepth = -50f;

        [Header("Animus 视觉参数 (冰蓝/苍白色调)")]
        public Color BackgroundColor = new Color(0.92f, 0.94f, 0.98f, 1f);
        public Color GridLineColor = new Color(0.7f, 0.82f, 0.95f, 1f);
        public float GridScrollSpeed = 0.02f;

        [Header("【1】空中全息大扫描指令")]
        [ColorUsage(true, false)]
        public Color ScanLinesColor = new Color(0.3f, 0.6f, 0.9f, 0.5f);
        public float ScanLineWidth = 1.0f;
        public float ScanLineMaxLength = 100f;

        [Header("【2】刺客信条：记忆数据流")]
        [ColorUsage(true, false)]
        public Color MemoryStreamColor = new Color(0.2f, 0.5f, 0.8f, 0.4f);
        public float StreamLineWidth = 0.08f;
        public float StreamLineMaxLength = 8f;
        public float StreamSpawnRateMin = 0.02f;
        public float StreamSpawnRateMax = 0.06f;
        public int StreamBurstCount = 4;
        public float StreamFlowSpeed = -12f;

        [Header("【3】墙壁回路流光")]
        [ColorUsage(true, false)]
        public Color WallCircuitColor = new Color(0.4f, 0.7f, 1.0f, 0.5f);
        public float WallFlowLineWidth = 0.8f;
        public float WallFlowSpawnRate = 0.05f;
        public int WallFlowBurstCount = 5;
        public float WallOffset = 3.0f;

        // 【已修复：新增光流速度控制！】
        [Tooltip("墙壁流光的最小速度")]
        [Range(10f, 300f)] public float WallFlowSpeedMin = 80f;
        [Tooltip("墙壁流光的最大速度")]
        [Range(10f, 300f)] public float WallFlowSpeedMax = 150f;

        [Header("【4】淡入淡出调节")]
        [Range(0.01f, 0.5f)] public float ScanFadeInPercent = 0.1f;
        [Range(0.01f, 0.5f)] public float ScanFadeOutPercent = 0.3f;
        [Space(5)]
        [Range(0.01f, 0.5f)] public float StreamFadeInPercent = 0.15f;
        [Range(0.01f, 0.5f)] public float StreamFadeOutPercent = 0.3f;
        [Space(5)]
        [Range(0.01f, 0.5f)] public float WallFadeInPercent = 0.05f;
        [Range(0.01f, 0.5f)] public float WallFadeOutPercent = 0.2f;

        private Material _matrixMaterial;
        private GameObject _abyssPlane;
        private float _pulseTimer = 0f;

        private Vector2 _sharedGridScale = new Vector2(30, 30);
        private int _gridThickness = 3;
        private int _textureSize = 128;

        private Bounds _localBounds;
        private bool _isDataBursting = true;

        private void Start()
        {
            if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = BackgroundColor; }

            if (MatrixBox != null)
            {
                Renderer boxRenderer = MatrixBox.GetComponent<Renderer>();
                MeshFilter mf = MatrixBox.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) _localBounds = mf.sharedMesh.bounds;
                else _localBounds = new Bounds(Vector3.zero, Vector3.one);

                if (boxRenderer != null)
                {
                    Shader baseShader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    _matrixMaterial = new Material(baseShader);
                    _matrixMaterial.mainTexture = GenerateGridTexture();
                    _matrixMaterial.mainTextureScale = _sharedGridScale;

                    if (_matrixMaterial.HasProperty("_BaseColor"))
                        _matrixMaterial.SetColor("_BaseColor", BackgroundColor);
                    else
                        _matrixMaterial.color = BackgroundColor;

                    boxRenderer.material = _matrixMaterial;
                }
            }

            _abyssPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(_abyssPlane.GetComponent<Collider>());
            _abyssPlane.transform.SetParent(this.transform);

            Vector3 bottomCenter = MatrixBox.transform.TransformPoint(new Vector3(_localBounds.center.x, _localBounds.min.y, _localBounds.center.z));
            _abyssPlane.transform.position = bottomCenter + new Vector3(0, AbyssDepth, 0);
            _abyssPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _abyssPlane.transform.localScale = new Vector3(3000f, 3000f, 1f);

            Material abyssMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            abyssMat.color = BackgroundColor;
            _abyssPlane.GetComponent<Renderer>().material = abyssMat;

            StartCoroutine(SpawnIntersectingLinesRoutine());
            StartCoroutine(SpawnMemoryStreamRoutine());
            StartCoroutine(SpawnWallGapLinesRoutine());
        }

        private void Update()
        {
            if (_matrixMaterial == null) return;

            Vector2 offset = _matrixMaterial.mainTextureOffset;
            offset.x += GridScrollSpeed * Time.deltaTime;
            offset.y += GridScrollSpeed * Time.deltaTime;
            _matrixMaterial.mainTextureOffset = offset;

            _pulseTimer += Time.deltaTime * 1.0f;
            float pulseRatio = (Mathf.Sin(_pulseTimer) + 1f) / 2f;
            Color glowColor = Color.Lerp(BackgroundColor, BackgroundColor * 1.08f, pulseRatio);

            if (_matrixMaterial.HasProperty("_BaseColor"))
                _matrixMaterial.SetColor("_BaseColor", glowColor);
            else
                _matrixMaterial.color = glowColor;
        }

        public void TriggerDataBurst() { _isDataBursting = true; }

        private Texture2D GenerateGridTexture()
        {
            Texture2D tex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Repeat;
            for (int x = 0; x < _textureSize; x++) for (int y = 0; y < _textureSize; y++)
                    if (x < _gridThickness || y < _gridThickness) tex.SetPixel(x, y, GridLineColor); else tex.SetPixel(x, y, BackgroundColor);
            tex.Apply(); return tex;
        }

        private void GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            Vector3 scale = MatrixBox.transform.lossyScale; float inX = scale.x != 0 ? Mathf.Abs(WallOffset / scale.x) : 0.001f; float inY = scale.y != 0 ? Mathf.Abs(WallOffset / scale.y) : 0.001f; float inZ = scale.z != 0 ? Mathf.Abs(WallOffset / scale.z) : 0.001f;
            minX = _localBounds.min.x + inX; maxX = _localBounds.max.x - inX; minY = _localBounds.min.y + inY; maxY = _localBounds.max.y - inY; minZ = _localBounds.min.z + inZ; maxZ = _localBounds.max.z - inZ;
        }

        private Vector3 ClampToRoom(Vector3 worldPos)
        {
            if (MatrixBox == null) return worldPos; Vector3 localPos = MatrixBox.transform.InverseTransformPoint(worldPos); GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);
            localPos.x = Mathf.Clamp(localPos.x, minX, maxX); localPos.y = Mathf.Clamp(localPos.y, minY, maxY); localPos.z = Mathf.Clamp(localPos.z, minZ, maxZ); return MatrixBox.transform.TransformPoint(localPos);
        }

        private float CalculateNaturalAlpha(float progress, float fadeInPercent, float fadeOutPercent)
        {
            float linearAlpha = 1.0f; if (progress < fadeInPercent) linearAlpha = progress / fadeInPercent; else if (progress > (1.0f - fadeOutPercent)) linearAlpha = 1.0f - (progress - (1.0f - fadeOutPercent)) / fadeOutPercent;
            return Mathf.SmoothStep(0.0f, 1.0f, linearAlpha);
        }

        private IEnumerator SpawnWallGapLinesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(WallFlowSpawnRate, WallFlowSpawnRate * 2f));
                if (MatrixBox == null || !_isDataBursting) continue;
                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);
                for (int i = 0; i < WallFlowBurstCount; i++)
                {
                    int face = Random.Range(0, 6); bool moveAlongPrimary = Random.value > 0.5f; float SnapLocal(float min, float max) { int count = (int)_sharedGridScale.x; return min + ((float)Random.Range(1, count) / count) * (max - min); }
                    Vector3 localA = Vector3.zero, localB = Vector3.zero;
                    if (face == 0) { if (moveAlongPrimary) { localA = new Vector3(minX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, minY, localA.z); } else { localA = new Vector3(SnapLocal(minX, maxX), minY, minZ); localB = new Vector3(localA.x, minY, maxZ); } } else if (face == 1) { if (moveAlongPrimary) { localA = new Vector3(minX, maxY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, maxY, localA.z); } else { localA = new Vector3(SnapLocal(minX, maxX), maxY, minZ); localB = new Vector3(localA.x, maxY, maxZ); } } else if (face == 2) { if (moveAlongPrimary) { localA = new Vector3(minX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(minX, maxY, localA.z); } else { localA = new Vector3(minX, SnapLocal(minY, maxY), minZ); localB = new Vector3(minX, localA.y, maxZ); } } else if (face == 3) { if (moveAlongPrimary) { localA = new Vector3(maxX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, maxY, localA.z); } else { localA = new Vector3(maxX, SnapLocal(minY, maxY), minZ); localB = new Vector3(maxX, localA.y, maxZ); } } else if (face == 4) { if (moveAlongPrimary) { localA = new Vector3(minX, SnapLocal(minY, maxY), minZ); localB = new Vector3(maxX, localA.y, minZ); } else { localA = new Vector3(SnapLocal(minX, maxX), minY, minZ); localB = new Vector3(localA.x, maxY, minZ); } } else if (face == 5) { if (moveAlongPrimary) { localA = new Vector3(minX, SnapLocal(minY, maxY), maxZ); localB = new Vector3(maxX, localA.y, maxZ); } else { localA = new Vector3(SnapLocal(minX, maxX), minY, maxZ); localB = new Vector3(localA.x, maxY, maxZ); } }
                    if (Random.value > 0.5f) { Vector3 temp = localA; localA = localB; localB = temp; }
                    StartCoroutine(AnimateWallFlowLine(MatrixBox.transform.TransformPoint(localA), MatrixBox.transform.TransformPoint(localB)));
                }
            }
        }

        private IEnumerator AnimateWallFlowLine(Vector3 worldA, Vector3 worldB)
        {
            LineRenderer lr = CreateSingleDataLine(WallFlowLineWidth); float totalDist = Vector3.Distance(worldA, worldB); Vector3 moveDir = (worldB - worldA).normalized;

            // 【已修复：现在可以在面板自定义墙壁光流的速度！】
            float speed = Random.Range(WallFlowSpeedMin, WallFlowSpeedMax);
            float length = Random.Range(15f, 60f), lifeTime = totalDist / speed, t = 0;

            while (t < lifeTime)
            {
                t += Time.deltaTime; float progress = Mathf.Clamp01(t / lifeTime); float headDist = Mathf.Clamp(speed * t, 0, totalDist), tailDist = Mathf.Clamp(headDist - length, 0, totalDist); lr.SetPosition(0, worldA + moveDir * tailDist); lr.SetPosition(1, worldA + moveDir * headDist); float alpha = CalculateNaturalAlpha(progress, WallFadeInPercent, WallFadeOutPercent);

                // 【👑 终极发黑 Bug 修复】：去掉了 RGB 乘以 Alpha 的逻辑！现在颜色将保持纯正，只会渐渐变透明，绝不变黑！
                Color finalColor = new Color(WallCircuitColor.r, WallCircuitColor.g, WallCircuitColor.b, alpha * WallCircuitColor.a);
                lr.material.SetColor("_BaseColor", finalColor);
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        private IEnumerator SpawnIntersectingLinesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
                if (MatrixBox == null || !_isDataBursting) continue;
                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ); Vector3 worldCenter = MatrixBox.transform.TransformPoint(new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), Random.Range(minZ, maxZ))); Vector3[] localDirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back }; Vector3 localMoveDir = localDirs[Random.Range(0, localDirs.Length)]; Vector3 localAxis1, localAxis2;
                if (localMoveDir == Vector3.up || localMoveDir == Vector3.down) { localAxis1 = Vector3.right; localAxis2 = Vector3.forward; } else if (localMoveDir == Vector3.left || localMoveDir == Vector3.right) { localAxis1 = Vector3.up; localAxis2 = Vector3.forward; } else { localAxis1 = Vector3.up; localAxis2 = Vector3.right; }
                Vector3 moveDir = MatrixBox.transform.TransformDirection(localMoveDir).normalized;
                StartCoroutine(AnimateCrossLines(worldCenter, moveDir, MatrixBox.transform.TransformDirection(localAxis1).normalized, MatrixBox.transform.TransformDirection(localAxis2).normalized, Random.value > 0.6f ? moveDir : Vector3.zero));
            }
        }

        private IEnumerator AnimateCrossLines(Vector3 startCenter, Vector3 moveDir, Vector3 axis1, Vector3 axis2, Vector3 axis3)
        {
            LineRenderer lr1 = CreateSingleDataLine(ScanLineWidth), lr2 = CreateSingleDataLine(ScanLineWidth), lr3 = axis3 != Vector3.zero ? CreateSingleDataLine(ScanLineWidth) : null; float lifeTime = Random.Range(2.5f, 4.5f), moveSpeed = Random.Range(15f, 40f), targetSize = Random.Range(ScanLineMaxLength * 0.4f, ScanLineMaxLength), t = 0;

            while (t < lifeTime)
            {
                t += Time.deltaTime; float progress = Mathf.Clamp01(t / lifeTime); Vector3 currentCenter = startCenter + moveDir * (moveSpeed * t); float sizeProgress = progress < ScanFadeInPercent ? progress / ScanFadeInPercent : (progress > (1.0f - ScanFadeOutPercent) ? 1.0f - (progress - (1.0f - ScanFadeOutPercent)) / ScanFadeOutPercent : 1.0f); float currentSize = Mathf.SmoothStep(0f, targetSize, sizeProgress); float alpha = CalculateNaturalAlpha(progress, ScanFadeInPercent, ScanFadeOutPercent);

                // 【👑 终极发黑 Bug 修复】
                Color currentColor = new Color(ScanLinesColor.r, ScanLinesColor.g, ScanLinesColor.b, alpha * ScanLinesColor.a);

                if (lr1 != null) { lr1.SetPosition(0, ClampToRoom(currentCenter - axis1 * currentSize)); lr1.SetPosition(1, ClampToRoom(currentCenter + axis1 * currentSize)); lr1.material.SetColor("_BaseColor", currentColor); }
                if (lr2 != null) { lr2.SetPosition(0, ClampToRoom(currentCenter - axis2 * currentSize)); lr2.SetPosition(1, ClampToRoom(currentCenter + axis2 * currentSize)); lr2.material.SetColor("_BaseColor", currentColor); }
                if (lr3 != null) { lr3.SetPosition(0, ClampToRoom(currentCenter - axis3 * currentSize)); lr3.SetPosition(1, ClampToRoom(currentCenter + axis3 * currentSize)); lr3.material.SetColor("_BaseColor", currentColor); }
                yield return null;
            }
            if (lr1 != null) Destroy(lr1.gameObject); if (lr2 != null) Destroy(lr2.gameObject); if (lr3 != null) Destroy(lr3.gameObject);
        }

        private IEnumerator SpawnMemoryStreamRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(StreamSpawnRateMin, StreamSpawnRateMax));
                if (MatrixBox == null || !_isDataBursting) continue;
                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);
                for (int i = 0; i < StreamBurstCount; i++) { Vector3 worldCenter = MatrixBox.transform.TransformPoint(new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), Random.Range(minZ, maxZ))); StartCoroutine(AnimateMemoryStreamLine(worldCenter, Vector3.up)); }
            }
        }

        private IEnumerator AnimateMemoryStreamLine(Vector3 startCenter, Vector3 axis)
        {
            LineRenderer lr = CreateSingleDataLine(StreamLineWidth); float lifeTime = Random.Range(1.5f, 3.5f), length = Random.Range(StreamLineMaxLength * 0.2f, StreamLineMaxLength), flowSpeed = StreamFlowSpeed * Random.Range(0.7f, 1.3f), t = 0;

            Vector3 currentCenter = startCenter;

            while (t < lifeTime)
            {
                t += Time.deltaTime; float progress = Mathf.Clamp01(t / lifeTime); currentCenter += axis * (flowSpeed * Time.deltaTime); float sizeProgress = progress < StreamFadeInPercent ? progress / StreamFadeInPercent : (progress > (1.0f - StreamFadeOutPercent) ? 1.0f - (progress - (1.0f - StreamFadeOutPercent)) / StreamFadeOutPercent : 1.0f); float currentLength = Mathf.SmoothStep(0f, length, sizeProgress); float alpha = CalculateNaturalAlpha(progress, StreamFadeInPercent, StreamFadeOutPercent);

                float pulseWave = (Mathf.Sin(progress * Mathf.PI * 4f) + 1f) / 2f;
                float pulse = Mathf.Lerp(1.0f, 1.15f, pulseWave);

                // 【👑 终极发黑 Bug 修复】
                Color finalColor = new Color(MemoryStreamColor.r * pulse, MemoryStreamColor.g * pulse, MemoryStreamColor.b * pulse, alpha * MemoryStreamColor.a);
                lr.material.SetColor("_BaseColor", finalColor);

                lr.SetPosition(0, ClampToRoom(currentCenter - axis * (currentLength / 2f))); lr.SetPosition(1, ClampToRoom(currentCenter + axis * (currentLength / 2f)));
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        private LineRenderer CreateSingleDataLine(float width)
        {
            GameObject go = new GameObject("DataLine"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); lr.useWorldSpace = true; lr.positionCount = 2; lr.startWidth = width; lr.endWidth = width;

            Shader accurateUnlitTransparent = Shader.Find("Universal Render Pipeline/Unlit");
            Material accurateMat = new Material(accurateUnlitTransparent);

            accurateMat.SetFloat("_Surface", 1);
            accurateMat.SetFloat("_Blend", 0);
            accurateMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            accurateMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            accurateMat.SetInt("_ZWrite", 0);
            accurateMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;

            lr.material = accurateMat; lr.numCapVertices = 4; return lr;
        }
    }
}
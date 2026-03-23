using System.Collections;
using UnityEngine;

namespace TheGlitch
{
    public class CyberspaceEnvironment : MonoBehaviour
    {
        [Header("经典 Animus 纯白空间")]
        [Tooltip("把你那个【已反转法线】的方块拖进来")]
        public GameObject MatrixBox;

        [Tooltip("深渊的高度（在地图下方多深）")]
        public float AbyssDepth = -50f;

        [Header("Animus 视觉参数 (已优化对比度)")]
        public Color BackgroundColor = new Color(0.85f, 0.85f, 0.88f, 1f);
        public Color GridLineColor = new Color(0.5f, 0.55f, 0.6f, 1f);

        public float GridScrollSpeed = 0.02f;

        [Header("【1】空中全息线条调节")]
        public float ScanLineWidth = 1.0f;
        public float GlitchLineWidth = 0.05f;

        [Header("【2】墙壁回路流光 (满负荷并发版)")]
        [ColorUsage(true, true)]
        public Color WallFlowColor = new Color(2f, 3.5f, 5.5f, 1f);
        public float WallFlowLineWidth = 0.8f;
        public float WallFlowSpawnRate = 0.05f;
        public int WallFlowBurstCount = 5;

        [Tooltip("【防穿模关键】：让墙壁流光凸出墙面多少米？在巨型空间内，调大它（比如10.0）能完美防破碎！")]
        public float WallOffset = 3.0f;

        private Material _matrixMaterial;
        private GameObject _abyssPlane;
        private float _pulseTimer = 0f;

        private Vector2 _sharedGridScale = new Vector2(30, 30);
        private int _gridThickness = 3;
        private int _textureSize = 128;

        private Bounds _localBounds;

        private void Start()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = BackgroundColor;
            }

            if (MatrixBox != null)
            {
                Renderer boxRenderer = MatrixBox.GetComponent<Renderer>();
                MeshFilter mf = MatrixBox.GetComponent<MeshFilter>();

                if (mf != null && mf.sharedMesh != null)
                {
                    _localBounds = mf.sharedMesh.bounds;
                }
                else
                {
                    _localBounds = new Bounds(Vector3.zero, Vector3.one);
                }

                if (boxRenderer != null)
                {
                    Shader stabilUnlitShader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    _matrixMaterial = new Material(stabilUnlitShader);
                    _matrixMaterial.mainTexture = GenerateGridTexture();
                    _matrixMaterial.mainTextureScale = _sharedGridScale;
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

            Material abyssMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            abyssMat.color = BackgroundColor;
            _abyssPlane.GetComponent<Renderer>().material = abyssMat;

            StartCoroutine(SpawnIntersectingLinesRoutine());
            StartCoroutine(SpawnGlitchLinesRoutine());
            StartCoroutine(SpawnWallGapLinesRoutine());
        }

        private void Update()
        {
            if (_matrixMaterial != null)
            {
                Vector2 offset = _matrixMaterial.mainTextureOffset;
                offset.x += GridScrollSpeed * Time.deltaTime;
                offset.y += GridScrollSpeed * Time.deltaTime;
                _matrixMaterial.mainTextureOffset = offset;

                _pulseTimer += Time.deltaTime * 1.5f;
                float glowIntensity = Mathf.Pow(Mathf.Sin(_pulseTimer), 8f);
                Color glowColor = Color.Lerp(Color.white, new Color(1.2f, 1.2f, 1.2f, 1f), glowIntensity);
                _matrixMaterial.color = glowColor;
            }
        }

        private Texture2D GenerateGridTexture()
        {
            Texture2D tex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int x = 0; x < _textureSize; x++)
            {
                for (int y = 0; y < _textureSize; y++)
                {
                    if (x < _gridThickness || y < _gridThickness) tex.SetPixel(x, y, GridLineColor);
                    else tex.SetPixel(x, y, BackgroundColor);
                }
            }
            tex.Apply();
            return tex;
        }

        private void GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            Vector3 scale = MatrixBox.transform.lossyScale;
            float inX = scale.x != 0 ? Mathf.Abs(WallOffset / scale.x) : 0.001f;
            float inY = scale.y != 0 ? Mathf.Abs(WallOffset / scale.y) : 0.001f;
            float inZ = scale.z != 0 ? Mathf.Abs(WallOffset / scale.z) : 0.001f;

            minX = _localBounds.min.x + inX; maxX = _localBounds.max.x - inX;
            minY = _localBounds.min.y + inY; maxY = _localBounds.max.y - inY;
            minZ = _localBounds.min.z + inZ; maxZ = _localBounds.max.z - inZ;
        }

        private Vector3 ClampToRoom(Vector3 worldPos)
        {
            if (MatrixBox == null) return worldPos;
            Vector3 localPos = MatrixBox.transform.InverseTransformPoint(worldPos);
            GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

            localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
            localPos.y = Mathf.Clamp(localPos.y, minY, maxY);
            localPos.z = Mathf.Clamp(localPos.z, minZ, maxZ);
            return MatrixBox.transform.TransformPoint(localPos);
        }

        private IEnumerator SpawnWallGapLinesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(WallFlowSpawnRate, WallFlowSpawnRate * 2f));
                if (MatrixBox == null) continue;

                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

                for (int i = 0; i < WallFlowBurstCount; i++)
                {
                    int face = Random.Range(0, 6);
                    bool moveAlongPrimary = Random.value > 0.5f;

                    float SnapLocal(float min, float max)
                    {
                        int count = (int)_sharedGridScale.x;
                        int idx = Random.Range(1, count);
                        return min + ((float)idx / count) * (max - min);
                    }

                    Vector3 localA = Vector3.zero;
                    Vector3 localB = Vector3.zero;

                    if (face == 0)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(minX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, minY, localA.z); }
                        else { localA = new Vector3(SnapLocal(minX, maxX), minY, minZ); localB = new Vector3(localA.x, minY, maxZ); }
                    }
                    else if (face == 1)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(minX, maxY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, maxY, localA.z); }
                        else { localA = new Vector3(SnapLocal(minX, maxX), maxY, minZ); localB = new Vector3(localA.x, maxY, maxZ); }
                    }
                    else if (face == 2)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(minX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(minX, maxY, localA.z); }
                        else { localA = new Vector3(minX, SnapLocal(minY, maxY), minZ); localB = new Vector3(minX, localA.y, maxZ); }
                    }
                    else if (face == 3)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(maxX, minY, SnapLocal(minZ, maxZ)); localB = new Vector3(maxX, maxY, localA.z); }
                        else { localA = new Vector3(maxX, SnapLocal(minY, maxY), minZ); localB = new Vector3(maxX, localA.y, maxZ); }
                    }
                    else if (face == 4)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(minX, SnapLocal(minY, maxY), minZ); localB = new Vector3(maxX, localA.y, minZ); }
                        else { localA = new Vector3(SnapLocal(minX, maxX), minY, minZ); localB = new Vector3(localA.x, maxY, minZ); }
                    }
                    else if (face == 5)
                    {
                        if (moveAlongPrimary) { localA = new Vector3(minX, SnapLocal(minY, maxY), maxZ); localB = new Vector3(maxX, localA.y, maxZ); }
                        else { localA = new Vector3(SnapLocal(minX, maxX), minY, maxZ); localB = new Vector3(localA.x, maxY, maxZ); }
                    }

                    if (Random.value > 0.5f) { Vector3 temp = localA; localA = localB; localB = temp; }

                    Vector3 worldA = MatrixBox.transform.TransformPoint(localA);
                    Vector3 worldB = MatrixBox.transform.TransformPoint(localB);

                    StartCoroutine(AnimateWallFlowLine(worldA, worldB));
                }
            }
        }

        private IEnumerator AnimateWallFlowLine(Vector3 worldA, Vector3 worldB)
        {
            LineRenderer lr = CreateSingleDataLine(WallFlowLineWidth);
            float totalDist = Vector3.Distance(worldA, worldB);
            Vector3 moveDir = (worldB - worldA).normalized;

            float speed = Random.Range(80f, 150f);
            float length = Random.Range(15f, 60f);
            float lifeTime = totalDist / speed;

            float t = 0;
            while (t < lifeTime)
            {
                t += Time.deltaTime;
                float progress = t / lifeTime;

                float headDist = speed * t;
                float tailDist = headDist - length;

                headDist = Mathf.Clamp(headDist, 0, totalDist);
                tailDist = Mathf.Clamp(tailDist, 0, totalDist);

                lr.SetPosition(0, worldA + moveDir * tailDist);
                lr.SetPosition(1, worldA + moveDir * headDist);

                float alpha = Mathf.Sin(progress * Mathf.PI);
                lr.material.color = new Color(WallFlowColor.r, WallFlowColor.g, WallFlowColor.b, alpha);

                yield return null;
            }
            Destroy(lr.gameObject);
        }

        private IEnumerator SpawnIntersectingLinesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
                if (MatrixBox == null) continue;

                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

                Vector3 localCenter = new Vector3(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY),
                    Random.Range(minZ, maxZ)
                );
                Vector3 worldCenter = MatrixBox.transform.TransformPoint(localCenter);

                Vector3[] localDirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
                Vector3 localMoveDir = localDirs[Random.Range(0, localDirs.Length)];

                Vector3 localAxis1, localAxis2;
                // 【已修复：去除乱码手误】
                if (localMoveDir == Vector3.up || localMoveDir == Vector3.down) { localAxis1 = Vector3.right; localAxis2 = Vector3.forward; }
                else if (localMoveDir == Vector3.left || localMoveDir == Vector3.right) { localAxis1 = Vector3.up; localAxis2 = Vector3.forward; }
                else { localAxis1 = Vector3.up; localAxis2 = Vector3.right; }

                Vector3 moveDir = MatrixBox.transform.TransformDirection(localMoveDir).normalized;
                Vector3 axis1 = MatrixBox.transform.TransformDirection(localAxis1).normalized;
                Vector3 axis2 = MatrixBox.transform.TransformDirection(localAxis2).normalized;

                bool useThirdAxis = Random.value > 0.6f;
                StartCoroutine(AnimateCrossLines(worldCenter, moveDir, axis1, axis2, useThirdAxis ? moveDir : Vector3.zero));
            }
        }

        private IEnumerator AnimateCrossLines(Vector3 startCenter, Vector3 moveDir, Vector3 axis1, Vector3 axis2, Vector3 axis3)
        {
            LineRenderer lr1 = CreateSingleDataLine(ScanLineWidth);
            LineRenderer lr2 = CreateSingleDataLine(ScanLineWidth);
            LineRenderer lr3 = axis3 != Vector3.zero ? CreateSingleDataLine(ScanLineWidth) : null;

            float lifeTime = Random.Range(2.5f, 4.5f);
            float moveSpeed = Random.Range(15f, 40f);
            float targetSize = 500f;

            Color hdrGlow = new Color(2f, 3f, 4f, 1f);

            float t = 0;
            while (t < lifeTime)
            {
                t += Time.deltaTime;
                float progress = t / lifeTime;

                Vector3 currentCenter = startCenter + moveDir * (moveSpeed * t);
                float currentSize = Mathf.Lerp(0f, targetSize, Mathf.Clamp01(progress * 4.0f));
                float alpha = Mathf.Sin(progress * Mathf.PI);
                Color currentColor = new Color(hdrGlow.r, hdrGlow.g, hdrGlow.b, alpha);

                if (lr1 != null) { lr1.SetPosition(0, ClampToRoom(currentCenter - axis1 * currentSize)); lr1.SetPosition(1, ClampToRoom(currentCenter + axis1 * currentSize)); lr1.material.color = currentColor; }
                if (lr2 != null) { lr2.SetPosition(0, ClampToRoom(currentCenter - axis2 * currentSize)); lr2.SetPosition(1, ClampToRoom(currentCenter + axis2 * currentSize)); lr2.material.color = currentColor; }
                if (lr3 != null) { lr3.SetPosition(0, ClampToRoom(currentCenter - axis3 * currentSize)); lr3.SetPosition(1, ClampToRoom(currentCenter + axis3 * currentSize)); lr3.material.color = currentColor; }

                yield return null;
            }
            if (lr1 != null) Destroy(lr1.gameObject);
            if (lr2 != null) Destroy(lr2.gameObject);
            if (lr3 != null) Destroy(lr3.gameObject);
        }

        private IEnumerator SpawnGlitchLinesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
                if (MatrixBox == null) continue;

                GetSafeLocalBounds(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

                Vector3 localCenter = new Vector3(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY),
                    Random.Range(minZ, maxZ)
                );
                Vector3 worldCenter = MatrixBox.transform.TransformPoint(localCenter);

                Vector3[] localAxes = { Vector3.up, Vector3.right, Vector3.forward };
                Vector3 localAxis = localAxes[Random.Range(0, localAxes.Length)];
                Vector3 axis = MatrixBox.transform.TransformDirection(localAxis).normalized;

                StartCoroutine(AnimateGlitchLine(worldCenter, axis));
            }
        }

        private IEnumerator AnimateGlitchLine(Vector3 center, Vector3 axis)
        {
            LineRenderer lr = CreateSingleDataLine(GlitchLineWidth);
            float lifeTime = Random.Range(0.5f, 2.0f);
            float length = Random.Range(3f, 15f);
            Color hdrGlow = new Color(1.5f, 2.0f, 2.5f, 1f);

            lr.SetPosition(0, ClampToRoom(center - axis * (length / 2f)));
            lr.SetPosition(1, ClampToRoom(center + axis * (length / 2f)));

            float t = 0;
            while (t < lifeTime)
            {
                t += Time.deltaTime;
                float progress = t / lifeTime;
                float alpha = Mathf.Sin(progress * Mathf.PI);
                lr.material.color = new Color(hdrGlow.r, hdrGlow.g, hdrGlow.b, alpha);
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        private LineRenderer CreateSingleDataLine(float width)
        {
            GameObject go = new GameObject("DataLine");
            go.transform.SetParent(this.transform);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;

            Shader preciseShader = Shader.Find("Unlit/Transparent");
            if (preciseShader == null) preciseShader = Shader.Find("Universal Render Pipeline/Unlit");

            Material preciseMat = new Material(preciseShader);
            preciseMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;

            lr.material = preciseMat;
            lr.numCapVertices = 4;
            return lr;
        }
    }
}
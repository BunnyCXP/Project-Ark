using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RailMeshDeformer : MonoBehaviour
    {
        [Header("Settings")]
        public int Segments = 16;
        public float Width = 1.0f;
        public float Height = 1.0f;

        [Header("References")]
        public BillboardAvatarController2D Controller;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _originalVertices;

        private void Start()
        {
            if (Controller == null)
                Controller = GetComponentInParent<BillboardAvatarController2D>();

            GenerateFlexibleMesh();
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Rail == null) return;
            DeformMesh();
        }

        private void GenerateFlexibleMesh()
        {
            _mesh = new Mesh();
            _mesh.name = "FlexiblePaper";

            int xCount = Segments + 1;
            int yCount = 2;
            int numVerts = xCount * yCount;

            _vertices = new Vector3[numVerts];
            _originalVertices = new Vector3[numVerts];
            Vector2[] uvs = new Vector2[numVerts];
            int[] tris = new int[Segments * 6];

            // 【修复2】计算 Y 轴偏移，让中心点回到物体中心 (0,0)
            // 这样就不会比原来的 Quad 高了
            float yBottom = -Height * 0.5f;
            float yTop = Height * 0.5f;

            for (int i = 0; i < xCount; i++)
            {
                float t = (float)i / Segments;
                float xPos = (t - 0.5f) * Width;

                // 下排顶点 
                _vertices[i] = new Vector3(xPos, yBottom, 0);
                _originalVertices[i] = _vertices[i];
                uvs[i] = new Vector2(t, 0);

                // 上排顶点 
                _vertices[i + xCount] = new Vector3(xPos, yTop, 0);
                _originalVertices[i + xCount] = _vertices[i + xCount];
                uvs[i + xCount] = new Vector2(t, 1);
            }

            // 生成三角形 (不变)
            for (int i = 0; i < Segments; i++)
            {
                int baseIdx = i * 6;
                int v0 = i;
                int v1 = i + xCount;
                int v2 = i + 1;
                int v3 = i + 1 + xCount;

                tris[baseIdx] = v0;
                tris[baseIdx + 1] = v1;
                tris[baseIdx + 2] = v2;
                tris[baseIdx + 3] = v2;
                tris[baseIdx + 4] = v1;
                tris[baseIdx + 5] = v3;
            }

            _mesh.vertices = _vertices;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();

            GetComponent<MeshFilter>().mesh = _mesh;
        }

        private void DeformMesh()
        {
            float centerDist = Controller.CurrentDistance;

            // 【修复1】获取世界空间的缩放比例
            // 这样如果你的 Scale 是 1.5，采样距离也会扩大 1.5 倍，防止图像变窄
            float worldScaleX = transform.lossyScale.x;

            // 缓存一下原始高度数据
            float yBottom = -Height * 0.5f;
            float yTop = Height * 0.5f;

            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                float xLocalOffset = (t - 0.5f) * Width;

                // 将本地宽度偏移 转换为 世界距离偏移
                // 必须乘上 worldScaleX，否则采样范围不够大
                float worldDistOffset = xLocalOffset * Mathf.Abs(worldScaleX);

                // 处理镜像翻转 (如果 Scale 为负，我们需要反向采样)
                float vertDist = centerDist + (worldDistOffset * Mathf.Sign(worldScaleX));

                // 采样轨道
                Vector3 railPos, railTan;
                Controller.Rail.Sample(vertDist, out railPos, out railTan);

                // 转回本地坐标
                Vector3 localRailPos = transform.InverseTransformPoint(railPos);

                // 赋值 (保持 Y 轴高度不变，只弯曲 X 和 Z)
                _vertices[i] = new Vector3(localRailPos.x, yBottom, localRailPos.z);
                _vertices[i + (Segments + 1)] = new Vector3(localRailPos.x, yTop, localRailPos.z);
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }
    }
}
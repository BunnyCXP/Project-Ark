using UnityEngine;
using System.Linq;

namespace TheGlitch
{
    public class Billboard2DRail : MonoBehaviour
    {
        // 如果你不手动拖，它会自动抓取所有子物体作为路径点
        public Transform[] Waypoints;

        private float[] _segLen;
        private float _totalLen;

        private void Awake()
        {
            // 自动构建：如果没有手动赋值，就找子物体
            if (Waypoints == null || Waypoints.Length == 0)
            {
                Waypoints = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++)
                {
                    Waypoints[i] = transform.GetChild(i);
                }
            }
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (Waypoints == null || Waypoints.Length < 2)
            {
                _segLen = null;
                _totalLen = 0f;
                return;
            }

            _segLen = new float[Waypoints.Length - 1];
            _totalLen = 0f;

            for (int i = 0; i < Waypoints.Length - 1; i++)
            {
                float l = Vector3.Distance(Waypoints[i].position, Waypoints[i + 1].position);
                _segLen[i] = Mathf.Max(0.0001f, l);
                _totalLen += _segLen[i];
            }
        }

        public float TotalLength => _totalLen;

        // 核心：输入距离 s，返回世界坐标 point 和 切线 tangent
        public void Sample(float s, out Vector3 point, out Vector3 tangent)
        {
            point = Waypoints[0].position;
            tangent = (Waypoints[1].position - Waypoints[0].position).normalized;

            if (Waypoints == null || Waypoints.Length < 2) return;
            if (_segLen == null) Rebuild();

            // 限制 s 在轨道范围内
            s = Mathf.Clamp(s, 0f, _totalLen);

            float acc = 0f;
            for (int i = 0; i < _segLen.Length; i++)
            {
                float l = _segLen[i];
                if (s <= acc + l || i == _segLen.Length - 1)
                {
                    float t = l <= 0.00001f ? 0f : (s - acc) / l;
                    Vector3 a = Waypoints[i].position;
                    Vector3 b = Waypoints[i + 1].position;
                    point = Vector3.Lerp(a, b, t);
                    tangent = (b - a).normalized;
                    return;
                }
                acc += l;
            }
        }

        // 找到离 3D 玩家最近的轨道位置（用于刚进入时定位）
        public void ClosestPoint(Vector3 worldPos, out float s)
        {
            if (_segLen == null || Waypoints == null || Waypoints.Length == 0)
            {
                Rebuild();
            }

            s = 0f;
            if (Waypoints == null || Waypoints.Length < 2) return;

            float bestD2 = float.PositiveInfinity;
            float acc = 0f;

            for (int i = 0; i < Waypoints.Length - 1; i++)
            {
                Vector3 a = Waypoints[i].position;
                Vector3 b = Waypoints[i + 1].position;
                Vector3 ab = b - a;
                float abLen2 = ab.sqrMagnitude;

                float t = 0f;
                if (abLen2 > 0.00001f)
                    t = Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / abLen2);

                Vector3 p = a + ab * t;
                float d2 = (worldPos - p).sqrMagnitude;

                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    s = acc + _segLen[i] * t;
                }
                acc += _segLen[i];
            }
        }
    }
}

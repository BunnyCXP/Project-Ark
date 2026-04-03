using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(LineRenderer))]
    public class PowerWire : MonoBehaviour
    {
        [Header("连接的开关")]
        public QChargerHackable SourceNode; // 拖入你要监听的那个 Charge Node

        [Header("电线颜色 (HDR 发光)")]
        [ColorUsage(true, true)] public Color UnchargedColor = Color.red;
        [ColorUsage(true, true)] public Color ChargedColor = new Color(0.2f, 0.8f, 1f, 1f); // 青蓝色
        public float ColorLerpSpeed = 8f;

        [Header("电线走向 (可选)")]
        [Tooltip("如果留空，会自动在【开关】和【当前物体】之间画一条直线。如果有拐弯，把拐弯点（空物体）按顺序拖进这里。")]
        public Transform[] PathPoints;

        private LineRenderer _lr;
        private Color _currentColor;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _currentColor = UnchargedColor;

            // 初始化点的数量
            if (PathPoints == null || PathPoints.Length == 0)
            {
                _lr.positionCount = 2;
            }
            else
            {
                _lr.positionCount = PathPoints.Length;
            }
        }

        private void Update()
        {
            if (SourceNode == null) return;

            // 1. 实时更新线条位置
            if (PathPoints == null || PathPoints.Length == 0)
            {
                _lr.SetPosition(0, SourceNode.transform.position);
                _lr.SetPosition(1, transform.position); // 连向挂着这个脚本的物体
            }
            else
            {
                for (int i = 0; i < PathPoints.Length; i++)
                {
                    if (PathPoints[i] != null)
                        _lr.SetPosition(i, PathPoints[i].position);
                }
            }

            // 2. 核心：根据开关状态，平滑过渡颜色
            // 读取你写在 charger.cs 里的 IsCharged 属性
            Color targetColor = SourceNode.IsCharged ? ChargedColor : UnchargedColor;
            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * ColorLerpSpeed);

            // 应用颜色
            _lr.startColor = _currentColor;
            _lr.endColor = _currentColor;

            // 如果材质使用了 Emission，顺手把材质的发光颜色也改了
            if (_lr.material != null && _lr.material.HasProperty("_EmissionColor"))
            {
                _lr.material.SetColor("_EmissionColor", _currentColor);
            }
            if (_lr.material != null && _lr.material.HasProperty("_BaseColor"))
            {
                _lr.material.SetColor("_BaseColor", _currentColor);
            }
        }
    }
}
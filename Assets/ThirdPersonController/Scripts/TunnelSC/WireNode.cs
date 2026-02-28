using UnityEngine;

namespace TheGlitch
{
    public class WireNode : MonoBehaviour
    {
        public enum NodeType { Straight, Corner, Cross, TShape, Start, End }

        [Header("节点配置")]
        public NodeType Type;
        public WirePuzzleManager PuzzleManager;
        public int GridX;
        public int GridY;

        [Header("旋转状态")]
        public bool IsRotatable = true;

        [Tooltip("0=0度, 1=90度, 2=180度, 3=270度")]
        [Range(0, 3)]
        public int CurrentRotation = 0;
        public int CorrectRotation = 0;

        [Header("动画设置")]
        public float RotationSpeed = 15f;
        public float ScaleSpeed = 12f;

        [Header("视觉与材质")]
        public bool IsGhostNode = false;
        public bool IsPowered = false;
        public Material UnpoweredMat;
        public Material PoweredMat;
        public Transform MeshRoot;    // 必须拖入 Pivot！

        // --- 自动抓取的渲染器数组 ---
        private Renderer[] _allRenderers;

        [HideInInspector] public int IncomingPowerCount = 0;
        [HideInInspector] public int PreviousIncomingCount = 0;

        // 【新增】：用来记住你在 Inspector 里设置的初始角度
        [HideInInspector] public int InitialRotation = 0;

        private float _targetZAngle = 0f;
        private float _currentZAngle = 0f;

        private float _targetScale = 0f;
        private float _currentScale = 0f;
        private float _hoverScaleMultiplier = 1f;
        private float _pulseScaleOffset = 0f;

        private void Start()
        {
            // 【核心补充】：游戏一运行，立刻把当前的初始角度记在小本本上！
            InitialRotation = CurrentRotation;

            if (MeshRoot != null)
            {
                _allRenderers = MeshRoot.GetComponentsInChildren<Renderer>(true);
            }

            UpdateVisuals(instant: true);
            _targetScale = 0f;
            _currentScale = 0f;
            if (MeshRoot != null) MeshRoot.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (MeshRoot != null && Application.isPlaying)
            {
                // 1. 旋转平滑 (保留原始 XYZ 轴)
                _currentZAngle = Mathf.Lerp(_currentZAngle, _targetZAngle, Time.deltaTime * RotationSpeed);
                Vector3 currentEuler = MeshRoot.localEulerAngles;
                MeshRoot.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, _currentZAngle);

                // 2. 缩放平滑与跳跃反馈
                _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * ScaleSpeed);
                _pulseScaleOffset = Mathf.Lerp(_pulseScaleOffset, 0f, Time.deltaTime * 8f);

                float breathingScale = 0f;
                if (Type == NodeType.Cross && !IsPowered && IncomingPowerCount > 0)
                {
                    breathingScale = Mathf.Sin(Time.time * 8f) * 0.08f * IncomingPowerCount;
                }

                float finalScale = (_currentScale * _hoverScaleMultiplier) + _pulseScaleOffset + breathingScale;
                MeshRoot.localScale = Vector3.one * Mathf.Max(0, finalScale);

                // 统一控制所有正反面贴图的隐身/显示
                if (_allRenderers != null)
                {
                    bool shouldRender = _currentScale >= 0.01f;
                    foreach (var r in _allRenderers)
                    {
                        if (r != null && r.enabled != shouldRender) r.enabled = shouldRender;
                    }
                }
            }
        }

        public void ApplyVisualState()
        {
            if (_allRenderers != null)
            {
                foreach (var r in _allRenderers)
                {
                    if (r != null) r.material = IsPowered ? PoweredMat : UnpoweredMat;
                }
            }

            if (IncomingPowerCount > PreviousIncomingCount)
            {
                if (Type == NodeType.Cross)
                {
                    if (IsPowered) _pulseScaleOffset = 0.6f;
                    else _pulseScaleOffset = 0.35f;
                }
                else
                {
                    _pulseScaleOffset = 0.25f;
                }
            }
        }

        public void SetVisible(bool visible) { _targetScale = visible ? 1f : 0f; }
        public void SetHovered(bool hovered) { _hoverScaleMultiplier = hovered ? 1.25f : 1f; }

        public bool HasPort(int direction)
        {
            int localDir = (direction - CurrentRotation + 4) % 4;
            switch (Type)
            {
                case NodeType.Straight: return localDir == 0 || localDir == 2;
                case NodeType.Corner: return localDir == 0 || localDir == 1;
                case NodeType.Cross: return true;
                case NodeType.TShape: return localDir != 2;
                case NodeType.Start: return localDir == 0;
                case NodeType.End: return localDir == 2;
            }
            return false;
        }

        public void RotateNode()
        {
            if (!IsRotatable || Type == NodeType.Start || Type == NodeType.End) return;
            CurrentRotation = (CurrentRotation + 1) % 4;
            _targetZAngle -= 90f;
        }

        // 【新增】：失败重置时调用的回档方法
        public void ResetToInitial()
        {
            if (!IsRotatable || Type == NodeType.Start || Type == NodeType.End) return;

            CurrentRotation = InitialRotation;

            // 每次回档重置时，让管子带有动画地“唰”一下转回最初的角度
            float absoluteTarget = -CurrentRotation * 90f;
            float diff = Mathf.DeltaAngle(_targetZAngle, absoluteTarget);
            _targetZAngle += diff;
        }

        public void UpdateVisuals(bool instant = false)
        {
            float absoluteTarget = -CurrentRotation * 90f;
            float diff = Mathf.DeltaAngle(_targetZAngle, absoluteTarget);
            _targetZAngle += diff;

            if (instant && MeshRoot != null)
            {
                _currentZAngle = _targetZAngle;
                Vector3 currentEuler = MeshRoot.localEulerAngles;
                MeshRoot.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, _currentZAngle);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (MeshRoot != null && !Application.isPlaying)
            {
                Vector3 currentEuler = MeshRoot.localEulerAngles;
                MeshRoot.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, -CurrentRotation * 90f);
            }
        }
#endif
    }
}
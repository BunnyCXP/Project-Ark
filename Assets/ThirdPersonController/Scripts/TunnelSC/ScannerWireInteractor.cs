using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class ScannerWireInteractor : MonoBehaviour
    {
        public Camera MainCamera;
        public float InteractRange = 15f;

        [Tooltip("拖入包含左右两侧 Puzzle_System 的总父物体")]
        public GameObject HiddenCircuitRoot;

        [Header("UI 自由准心与手感")]
        public RectTransform CustomReticleUI;
        public float ReticleMoveSpeed = 1.0f;

        [HideInInspector]
        public bool IsInTunnel = false;

        private WireNode[] _allNodes; // 改为直接获取节点引用，方便调用动画
        private bool _isCurrentlyScanning = false;
        private WireNode _lastAimedNode;
        private Vector2 _reticlePos;

        private void Start()
        {
            if (MainCamera == null) MainCamera = Camera.main;

            if (HiddenCircuitRoot != null)
            {
                _allNodes = HiddenCircuitRoot.GetComponentsInChildren<WireNode>(true);
                // 游戏开始，全员缩小隐藏
                SetCircuitsVisible(false);
            }

            if (CustomReticleUI != null) CustomReticleUI.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            if (!IsInTunnel)
            {
                if (_isCurrentlyScanning) StopScanning();
                return;
            }

            bool shouldScan = Keyboard.current.vKey.isPressed;

            if (shouldScan != _isCurrentlyScanning)
            {
                _isCurrentlyScanning = shouldScan;

                // 【动画】：按下V，通知所有节点放大出现；松开V，缩小隐藏
                SetCircuitsVisible(_isCurrentlyScanning);

                if (_isCurrentlyScanning)
                {
                    _reticlePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    if (CustomReticleUI != null)
                    {
                        CustomReticleUI.gameObject.SetActive(true);
                        CustomReticleUI.position = _reticlePos;
                    }
                }
                else
                {
                    StopScanning();
                }
            }

            if (_isCurrentlyScanning)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                _reticlePos += mouseDelta * ReticleMoveSpeed;

                _reticlePos.x = Mathf.Clamp(_reticlePos.x, 0, Screen.width);
                _reticlePos.y = Mathf.Clamp(_reticlePos.y, 0, Screen.height);

                if (CustomReticleUI != null) CustomReticleUI.position = _reticlePos;

                Ray ray = MainCamera.ScreenPointToRay(_reticlePos);

                WireNode currentNode = null;
                if (Physics.Raycast(ray, out RaycastHit hit, InteractRange))
                {
                    currentNode = hit.collider.GetComponentInParent<WireNode>();
                }

                // 【动画】：瞄准放大反馈
                if (currentNode != _lastAimedNode)
                {
                    if (_lastAimedNode != null) _lastAimedNode.SetHovered(false); // 取消上一个的放大

                    if (currentNode != null && !currentNode.IsGhostNode && currentNode.IsRotatable)
                    {
                        currentNode.SetHovered(true); // 放大当前的
                    }

                    _lastAimedNode = currentNode;
                }

                if (Keyboard.current.eKey.wasPressedThisFrame && _lastAimedNode != null && !_lastAimedNode.IsGhostNode && _lastAimedNode.IsRotatable)
                {
                    _lastAimedNode.RotateNode();
                    _lastAimedNode.PuzzleManager.EvaluatePower();
                }
            }
        }

        private void StopScanning()
        {
            _isCurrentlyScanning = false;
            SetCircuitsVisible(false);

            if (CustomReticleUI != null) CustomReticleUI.gameObject.SetActive(false);

            if (_lastAimedNode != null)
            {
                _lastAimedNode.SetHovered(false);
                _lastAimedNode = null;
            }
        }

        private void SetCircuitsVisible(bool visible)
        {
            if (_allNodes == null) return;
            foreach (var node in _allNodes)
            {
                if (node != null) node.SetVisible(visible);
            }
        }
    }
}
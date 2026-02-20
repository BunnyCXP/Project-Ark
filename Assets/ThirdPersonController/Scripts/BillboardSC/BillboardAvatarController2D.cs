using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    [RequireComponent(typeof(CharacterController))]
    public class BillboardAvatarController2D : MonoBehaviour
    {
        [Header("Rail System")]
        public Billboard2DRail Rail;
        public float HeightOffset = 0.0f;

        [Header("Movement")]
        public float MoveSpeed = 4.0f;
        public float JumpHeight = 1.2f;
        public float Gravity = -20f;

        [Header("Bounds (新功能)")]
        // 【新增】边缘安全距离。
        // 如果你的 Scale 是 1.5，建议设为 0.75 (宽度的一半)
        // 这样角色就不会有一半身体卡在起点或终点外面了
        public float EdgeMargin = 0.5f;

        [Header("Visual")]
        public Transform VisualRoot;
        public PaperAvatarFlipbook Flipbook;

        // 改为 public 属性，方便外部读取（比如变形脚本）
        public float CurrentDistance => _currentRailDistance;

        private float _currentRailDistance = 0f;
        private float _verticalVelocity = 0f;
        private float _currentVerticalPos = 0f;
        private bool _isGrounded = true;

        public void InitOnRail(Billboard2DRail rail, float startDist)
        {
            Rail = rail;

            // 【新增】初始化时也应用边距，防止一生成就卡一半
            if (Rail != null)
            {
                float min = EdgeMargin;
                float max = Mathf.Max(EdgeMargin, Rail.TotalLength - EdgeMargin);
                _currentRailDistance = Mathf.Clamp(startDist, min, max);
            }
            else
            {
                _currentRailDistance = startDist;
            }
        }

        private void Update()
        {
            if (Rail == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // 1. 获取输入
            float axis = 0f;
            if (kb.aKey.isPressed) axis -= 1f;
            if (kb.dKey.isPressed) axis += 1f;

            // 2. 更新轨道距离
            if (Mathf.Abs(axis) > 0.01f)
            {
                _currentRailDistance += axis * MoveSpeed * Time.deltaTime;

                // 【修改】限制移动范围，加上安全边距
                float minLimit = EdgeMargin;
                // 确保 max 至少不小于 min (防止轨道太短出bug)
                float maxLimit = Mathf.Max(minLimit, Rail.TotalLength - EdgeMargin);

                _currentRailDistance = Mathf.Clamp(_currentRailDistance, minLimit, maxLimit);

                // 处理翻转
                if (VisualRoot != null)
                {
                    float scaleX = Mathf.Abs(VisualRoot.localScale.x);
                    VisualRoot.localScale = new Vector3(axis > 0 ? scaleX : -scaleX, VisualRoot.localScale.y, VisualRoot.localScale.z);
                }

                if (Flipbook != null) Flipbook.SetMoving(true);
            }
            else
            {
                if (Flipbook != null) Flipbook.SetMoving(false);
            }

            // 3. 处理跳跃 / 重力
            if (_isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f;
            }

            if (kb.spaceKey.wasPressedThisFrame && _isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _verticalVelocity += Gravity * Time.deltaTime;
            _currentVerticalPos += _verticalVelocity * Time.deltaTime;

            if (_currentVerticalPos <= 0f)
            {
                _currentVerticalPos = 0f;
                _isGrounded = true;
            }
            else
            {
                _isGrounded = false;
            }

            // 4. 最终定位
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            Vector3 railPoint, tangent;
            Rail.Sample(_currentRailDistance, out railPoint, out tangent);

            Vector3 finalPos = railPoint + Vector3.up * (_currentVerticalPos + HeightOffset);
            transform.position = finalPos;

            Vector3 forward = Vector3.Cross(tangent, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }
}
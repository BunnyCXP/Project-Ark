using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    [RequireComponent(typeof(CharacterController))]
    public class BillboardAvatarController2D : MonoBehaviour
    {
        [Header("Rail System")]
        public Billboard2DRail Rail; // 【重要】轨道引用
        public float HeightOffset = 0.0f; // 贴图如果中心在脚底，这里设为0；如果在中心，设为0.5之类的

        [Header("Movement")]
        public float MoveSpeed = 4.0f;
        public float JumpHeight = 1.2f;
        public float Gravity = -20f;

        [Header("Visual")]
        public Transform VisualRoot; // 那个带 Sprite 的子物体
        public PaperAvatarFlipbook Flipbook; // 引用动画脚本

        private float _currentRailDistance = 0f; // 当前在轨道上的位置 (0 ~ TotalLen)
        private float _verticalVelocity = 0f;    // 垂直速度 (跳跃用)
        private float _currentVerticalPos = 0f;  // 垂直高度偏移
        private bool _isGrounded = true;

        // 初始化位置用
        public void InitOnRail(Billboard2DRail rail, float startDist)
        {
            Rail = rail;
            _currentRailDistance = startDist;
        }

        private void Update()
        {
            if (Rail == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // 1. 获取输入 (A/D)
            float axis = 0f;
            if (kb.aKey.isPressed) axis -= 1f;
            if (kb.dKey.isPressed) axis += 1f;

            // 2. 更新轨道距离
            if (Mathf.Abs(axis) > 0.01f)
            {
                _currentRailDistance += axis * MoveSpeed * Time.deltaTime;
                // 限制不要走出轨道头尾
                _currentRailDistance = Mathf.Clamp(_currentRailDistance, 0f, Rail.TotalLength);

                // 处理左右翻转 (修改 Scale X)
                if (VisualRoot != null)
                {
                    float scaleX = Mathf.Abs(VisualRoot.localScale.x);
                    VisualRoot.localScale = new Vector3(axis > 0 ? scaleX : -scaleX, VisualRoot.localScale.y, VisualRoot.localScale.z);
                }

                // 通知动画脚本
                if (Flipbook != null) Flipbook.SetMoving(true);
            }
            else
            {
                if (Flipbook != null) Flipbook.SetMoving(false);
            }

            // 3. 处理跳跃 / 重力
            if (_isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; // 贴地
            }

            if (kb.spaceKey.wasPressedThisFrame && _isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _verticalVelocity += Gravity * Time.deltaTime;
            _currentVerticalPos += _verticalVelocity * Time.deltaTime;

            // 简单的地面碰撞检测
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
            // 从轨道获取这一点的 世界坐标 和 切线方向
            Vector3 railPoint, tangent;
            Rail.Sample(_currentRailDistance, out railPoint, out tangent);

            // 计算最终坐标 = 轨道点 + 向上偏移(跳跃) + 高度修正
            Vector3 finalPos = railPoint + Vector3.up * (_currentVerticalPos + HeightOffset);
            transform.position = finalPos;

            // 计算朝向：
            // 我们希望人物的“右边”对着切线方向，同时保持垂直
            // Cross(tangent, up) 得到法线(也就是面朝方向)，LookRotation让人物正对相机
            Vector3 forward = Vector3.Cross(tangent, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }
}
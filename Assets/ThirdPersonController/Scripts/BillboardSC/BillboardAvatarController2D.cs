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
        public float JumpHeight = 1.5f;
        public float Gravity = -30f;

        [Header("Bounds & Collision")]
        public float EdgeMargin = 0.5f;

        [Header("Visual")]
        public Transform VisualRoot;
        public PaperAvatarFlipbook Flipbook;

        public float CurrentDistance => _currentRailDistance;

        private float _currentRailDistance = 0f;
        private float _verticalVelocity = 0f;
        private CharacterController _cc;

        private float _spawnDistance;
        private float _spawnHeight;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        public void InitOnRail(Billboard2DRail rail, float startDist)
        {
            Rail = rail;
            _currentRailDistance = Mathf.Max(startDist, EdgeMargin);

            _spawnDistance = _currentRailDistance;
            _spawnHeight = transform.position.y;
        }

        private void Update()
        {
            if (Rail == null || _cc == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            float axis = 0f;
            if (kb.aKey.isPressed) axis -= 1f;
            if (kb.dKey.isPressed) axis += 1f;

            // 1. 预计算目标点
            float targetDistance = _currentRailDistance;
            if (Mathf.Abs(axis) > 0.01f)
            {
                targetDistance += axis * MoveSpeed * Time.deltaTime;
                float minLimit = EdgeMargin;
                float maxLimit = Mathf.Max(minLimit, Rail.TotalLength - EdgeMargin);
                targetDistance = Mathf.Clamp(targetDistance, minLimit, maxLimit);

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

            Vector3 targetRailPos, intendedTangent;
            Rail.Sample(targetDistance, out targetRailPos, out intendedTangent);

            // 2. 真实的重力与跳跃检测
            if (_cc.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; // 保持一个向下的微小速度，确保 isGrounded 始终为 true
            }

            if (kb.spaceKey.wasPressedThisFrame && _cc.isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _verticalVelocity += Gravity * Time.deltaTime;

            // 3. 执行真正的物理移动 (拉力移动)
            Vector3 currentPos = transform.position;
            Vector3 moveDelta = new Vector3(targetRailPos.x - currentPos.x, 0, targetRailPos.z - currentPos.z);
            moveDelta.y = _verticalVelocity * Time.deltaTime;

            CollisionFlags flags = _cc.Move(moveDelta);

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0)
            {
                _verticalVelocity = 0f;
            }

            // 4. 防漂移校正 (找回在轨道上的真实距离)
            float actualS;
            Rail.ClosestPoint(transform.position, out actualS);
            _currentRailDistance = Mathf.Clamp(actualS, EdgeMargin, Mathf.Max(EdgeMargin, Rail.TotalLength - EdgeMargin));

            // 【修复关键点：删除了强行覆盖 transform.position 的代码！】
            // 让 CharacterController 保留它的物理落地状态

            // 5. 朝向更新
            Vector3 finalRailPoint, finalTangent;
            Rail.Sample(_currentRailDistance, out finalRailPoint, out finalTangent);
            Vector3 forward = Vector3.Cross(finalTangent, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        public void ResetToStart()
        {
            TeleportTo(_spawnDistance, _spawnHeight);
        }

        // 传送到指定距离和高度
        public void TeleportTo(float targetDistance, float targetHeight)
        {
            _currentRailDistance = Mathf.Clamp(targetDistance, EdgeMargin, Rail.TotalLength - EdgeMargin);
            Rail.Sample(_currentRailDistance, out Vector3 p, out Vector3 t);

            // 1. 玩家瞬移
            _cc.enabled = false;
            transform.position = new Vector3(p.x, targetHeight, p.z);

            // 强行把朝向也转过去，防止落地瞬间转身
            Vector3 forward = Vector3.Cross(t, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            _cc.enabled = true;
            _verticalVelocity = 0f;

            // 2. 【修复渲染 Bug】强行重置平滑替身 (如果存在)
            var smoother = Object.FindFirstObjectByType<CameraTargetSmoother>();
            if (smoother != null)
            {
                smoother.transform.position = transform.position + smoother.HeightOffset;
                smoother.transform.rotation = transform.rotation;
            }

            // 3. 【修复渲染 Bug】强行打断 Cinemachine 的平滑飞行，让它瞬间切镜
            var allCams = Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam.gameObject.activeInHierarchy)
                {
                    // 这句代码的意思是：上一帧的位置作废，不要做阻尼插值，直接把镜头拉过来！
                    cam.PreviousStateIsValid = false;
                }
            }
        }
    }
}
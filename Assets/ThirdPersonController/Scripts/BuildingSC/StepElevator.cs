using UnityEngine;

namespace TheGlitch
{
    // 强制自动添加刚体组件，防呆设计
    [RequireComponent(typeof(Rigidbody))]
    public class StepElevator : MonoBehaviour
    {
        [Header("电梯设置")]
        public float LiftHeight = 5f;
        public float MoveSpeed = 3f;

        [Header("音效 (可选)")]
        public AudioSource ElevatorMoveSound;
        public AudioSource ElevatorStopSound;

        // --- 内部状态变量 ---
        private Vector3 _bottomPos;
        private Vector3 _topPos;
        private Vector3 _currentTarget;

        private bool _isAtTop = false;
        private bool _isMoving = false;

        private Rigidbody _rb;

        private void Start()
        {
            // 1. 获取并强行设置刚体属性
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;  // 必须是运动学刚体（代码控制，不受掉落重力影响）
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; // 开启插值，让移动极致丝滑

            _bottomPos = transform.position;
            _topPos = _bottomPos + Vector3.up * LiftHeight;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (!_isMoving)
                {
                    _currentTarget = _isAtTop ? _bottomPos : _topPos;
                    _isMoving = true;
                    if (ElevatorMoveSound) ElevatorMoveSound.Play();
                }
            }
        }

        // 【终极绝杀】：抛弃 Update，改用物理引擎专属的 FixedUpdate！
        private void FixedUpdate()
        {
            if (_isMoving)
            {
                // 使用物理专用的 MovePosition，它会优雅地推动站在上面的所有物体，绝不卡脚！
                Vector3 nextPos = Vector3.MoveTowards(_rb.position, _currentTarget, MoveSpeed * Time.fixedDeltaTime);
                _rb.MovePosition(nextPos);

                if (Vector3.Distance(_rb.position, _currentTarget) < 0.001f)
                {
                    // 同样使用 MovePosition 归位
                    _rb.MovePosition(_currentTarget);
                    _isAtTop = !_isAtTop;
                    _isMoving = false;

                    if (ElevatorMoveSound) ElevatorMoveSound.Stop();
                    if (ElevatorStopSound) ElevatorStopSound.Play();
                }
            }
        }
    }
}
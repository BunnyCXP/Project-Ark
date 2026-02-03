using UnityEngine;
using Unity.Cinemachine;


namespace TheGlitch
{
    /// <summary>
    /// 根据玩家移动速度调节 vCam 的噪点强度（走路微晃，跑步更晃）
    /// 挂在 VCam_Player 上即可
    /// </summary>
    public class CameraMotionFX : MonoBehaviour
    {
        [Header("Refs")]
        public CinemachineVirtualCamera VCam;   // 拖 vCam_Player 自己
        public Transform Player;               // 拖 PlayerArmature（或玩家根节点）

        [Header("Speed → 摇晃强度")]
        public float MaxSpeed = 6f;            // 这个速度以上就认为是“疾跑”
        public float IdleAmplitude = 0f;       // 原地
        public float WalkAmplitude = 0.4f;     // 慢走
        public float RunAmplitude = 0.9f;      // 疾跑

        [Header("平滑参数")]
        public float AmplitudeLerpSpeed = 6f;  // 越大切换越快

        private CinemachineBasicMultiChannelPerlin _noise;
        private CharacterController _cc;
        private Rigidbody _rb;
        private Vector3 _lastPos;
        private bool _hasLastPos;

        private void Awake()
        {
            // vCam 引用
            if (VCam == null)
                VCam = GetComponent<CinemachineVirtualCamera>();

            if (VCam != null)
                _noise = VCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            // 找玩家
            if (Player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) Player = p.transform;
            }

            if (Player != null)
            {
                _cc = Player.GetComponent<CharacterController>();
                _rb = Player.GetComponent<Rigidbody>();
                _lastPos = Player.position;
                _hasLastPos = true;
            }
        }

        private void Update()
        {
            if (_noise == null || Player == null) return;

            // ===== 1. 计算水平速度 =====
            float speed = 0f;

            if (_cc != null)
            {
                // Starter Assets 默认用 CharacterController
                Vector3 v = _cc.velocity;
                v.y = 0;
                speed = v.magnitude;
            }
            else if (_rb != null)
            {
                Vector3 v = _rb.linearVelocity;
                v.y = 0;
                speed = v.magnitude;
            }
            else
            {
                // 没有 CC / RB，就用“上一帧位置差”估算速度
                if (_hasLastPos)
                {
                    Vector3 delta = Player.position - _lastPos;
                    delta.y = 0;
                    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    speed = delta.magnitude / dt;
                }
                _lastPos = Player.position;
                _hasLastPos = true;
            }

            // ===== 2. 映射速度 → 目标噪点强度 =====
            float t = Mathf.Clamp01(speed / Mathf.Max(0.01f, MaxSpeed));

            float targetAmp;
            if (t < 0.2f)        // 慢到基本不动
                targetAmp = IdleAmplitude;
            else if (t < 0.6f)   // 走路
                targetAmp = WalkAmplitude;
            else                 // 跑步
                targetAmp = RunAmplitude;

            // ===== 3. 平滑插值到目标值 =====
            float current = _noise.AmplitudeGain;
            float next = Mathf.Lerp(current, targetAmp, Time.deltaTime * AmplitudeLerpSpeed);
            _noise.AmplitudeGain = next;
        }
    }
}


using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering;

namespace TheGlitch
{
    public class HackCameraFX : MonoBehaviour
    {
        [Header("Refs")]
        public CinemachineVirtualCamera VCam; // 拖 vCam_Player 自己进来
        public Volume HackVolume;             // 拖刚刚建的 HackVolume 进来

        [Header("FOV")]
        public float HackFOV = 35f;           // 进入 Hack 时的视角（比平时小一点）
        public float FovLerpSpeed = 6f;       // 越大切换越快

        [Header("DOF")]
        public float DofLerpSpeed = 6f;       // 景深权重插值速度

        private float _baseFOV;
        private bool _hackOn;

        private void Awake()
        {
            if (VCam == null)
                VCam = GetComponent<CinemachineVirtualCamera>();

            if (VCam != null)
                _baseFOV = VCam.m_Lens.FieldOfView;

            // 确保初始景深关掉
            if (HackVolume != null)
                HackVolume.weight = 0f;
        }

        /// <summary>
        /// 从外面调用：true = 进入 Hack，false = 退出 Hack
        /// </summary>
        public void SetHack(bool on)
        {
            _hackOn = on;
        }

        private void Update()
        {
            if (VCam == null) return;

            // ===== FOV 过渡 =====
            float targetFov = _hackOn ? HackFOV : _baseFOV;
            VCam.m_Lens.FieldOfView =
                Mathf.Lerp(VCam.m_Lens.FieldOfView, targetFov, Time.deltaTime * FovLerpSpeed);

            // ===== 景深权重过渡 =====
            if (HackVolume != null)
            {
                float targetWeight = _hackOn ? 1f : 0f;
                HackVolume.weight =Mathf.Lerp(HackVolume.weight, targetWeight, Time.deltaTime * DofLerpSpeed);
            }
        }
    }
}

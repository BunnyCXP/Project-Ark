using UnityEngine;

namespace TheGlitch
{
    public class SearchLight : MonoBehaviour
    {
        [Header("探照灯设置")]
        [Tooltip("扫射的速度")]
        public float SwingSpeed = 2.0f;

        [Tooltip("向左/向右扫射的最大角度 (例如45度代表总共扫射90度)")]
        public float SwingAngle = 45.0f;

        [Tooltip("围绕哪个轴旋转？(通常是 Y 轴 或 Z 轴)")]
        public Vector3 RotationAxis = Vector3.up;

        // 记录灯刚开始的原始朝向
        private Quaternion _startRotation;

        private void Awake()
        {
            // 游戏开始时，记住灯放在场景里的初始角度
            _startRotation = transform.localRotation;
        }

        private void Update()
        {
            // Mathf.Sin 就像一个钟摆，会随着时间在 -1 到 1 之间平滑过渡
            float currentAngleOffset = Mathf.Sin(Time.time * SwingSpeed) * SwingAngle;

            // 在初始角度的基础上，叠加这个摆动角度
            transform.localRotation = _startRotation * Quaternion.AngleAxis(currentAngleOffset, RotationAxis);
        }
    }
}
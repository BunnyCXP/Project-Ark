using UnityEngine;

namespace TheGlitch
{
    public class CameraTargetSmoother : MonoBehaviour
    {
        [Header("Target")]
        public Transform PlayerRoot; // 拖入你的 PlayerArmature

        [Header("Settings")]
        public float RotationSpeed = 1.5f; // 越小转得越慢，越平滑
        public Vector3 HeightOffset = new Vector3(0, 0f, 0); // 替身的高度（建议设在肩膀位置）

        private void LateUpdate()
        {
            if (PlayerRoot == null) return;

            // 1. 位置：瞬间同步（绝对不掉队）
            transform.position = PlayerRoot.position + HeightOffset;

            // 2. 旋转：慢吞吞地插值（核心魔法）
            // Slerp 会在当前角度和目标角度之间平滑过渡
            transform.rotation = Quaternion.Slerp(transform.rotation, PlayerRoot.rotation, Time.deltaTime * RotationSpeed);
        }
    }
}
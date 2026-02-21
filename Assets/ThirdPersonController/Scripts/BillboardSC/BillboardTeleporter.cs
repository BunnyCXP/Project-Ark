using UnityEngine;

namespace TheGlitch
{
    public class BillboardTeleporter : MonoBehaviour
    {
        [Header("Teleport Destination")]
        [Tooltip("传送到轨道上的哪一段距离 (比如填 15 就是传送到轨道 15米处)")]
        public float TargetDistance = 10f;

        [Tooltip("传送到哪个高度 (拖一个空物体作为高度参考，或者手动填)")]
        public Transform TargetHeightRef;
        public float FallbackHeight = 1.0f;

        private void OnTriggerEnter(Collider other)
        {
            var avatar = other.GetComponent<BillboardAvatarController2D>();
            if (avatar != null)
            {
                float h = TargetHeightRef != null ? TargetHeightRef.position.y : FallbackHeight;
                avatar.TeleportTo(TargetDistance, h);
            }
        }
    }
}
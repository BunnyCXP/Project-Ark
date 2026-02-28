using UnityEngine;

namespace TheGlitch
{
    public class BillboardTeleporter : MonoBehaviour
    {
        [Header("Teleport Destination")]
        [Tooltip("把你想传送到的位置（创建一个空物体）拖到这里")]
        public Transform DestinationPoint;

        private void OnTriggerEnter(Collider other)
        {
            var avatar = other.GetComponent<BillboardAvatarController2D>();

            // 如果碰到的 2D 玩家，并且你设置了目标点
            if (avatar != null && DestinationPoint != null)
            {
                // 1. 自动计算这个目标点在轨道上的距离
                float targetDistance;
                avatar.Rail.ClosestPoint(DestinationPoint.position, out targetDistance);

                // 2. 直接使用这个目标点的真实高度
                float targetHeight = DestinationPoint.position.y;

                // 3. 执行传送
                avatar.TeleportTo(targetDistance, targetHeight);

                Debug.Log($"[Teleporter] 成功传送到距离: {targetDistance}, 高度: {targetHeight}");
            }
            else if (DestinationPoint == null)
            {
                Debug.LogWarning("[Teleporter] 传送失败！你没有在 Inspector 里拖入 DestinationPoint！");
            }
        }
    }
}
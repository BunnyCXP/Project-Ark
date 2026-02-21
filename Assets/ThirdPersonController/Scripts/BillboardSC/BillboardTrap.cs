using UnityEngine;

namespace TheGlitch
{
    public class BillboardTrap : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // 检测是否是 2D 玩家踩到了陷阱
            var avatar = other.GetComponent<BillboardAvatarController2D>();
            if (avatar != null)
            {
                Debug.Log("踩到陷阱！回到起点！");
                avatar.ResetToStart();
            }
        }
    }
}
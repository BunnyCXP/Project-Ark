using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class BillboardPortalExit : MonoBehaviour
    {
        public BillboardPortalEnter EnterSystem; // 引用入口脚本
        public Transform ExitPoint3D;            // 3D 玩家出来的位置
        public GameObject PromptUI;

        private bool _canExit;

        private void OnTriggerEnter(Collider other)
        {
            // 这里检测的是 2D Avatar，它的 Tag 最好也设为 Player，或者检测 Layer
            // 为了保险，我们可以不检测 Tag，只要 OnTriggerEnter 说明 Avatar 到了
            _canExit = true;
            if (PromptUI) PromptUI.SetActive(true);
        }

        private void OnTriggerExit(Collider other)
        {
            _canExit = false;
            if (PromptUI) PromptUI.SetActive(false);
        }

        private void Update()
        {
            // 必须是已经生成了 Avatar 才能退出
            if (_canExit && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Exit2DMode();
            }
        }

        private void Exit2DMode()
        {
            // 1. 把 3D 玩家瞬移到出口
            if (EnterSystem.PlayerRoot != null && ExitPoint3D != null)
            {
                //CharacterController 如果开启可能会干扰瞬移，建议先关再移再开，或者直接移
                var cc = EnterSystem.PlayerRoot.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;

                EnterSystem.PlayerRoot.position = ExitPoint3D.position;
                EnterSystem.PlayerRoot.rotation = ExitPoint3D.rotation;

                if (cc) cc.enabled = true;
            }

            // 2. 恢复状态
            EnterSystem.Restore3DPlayer();

            if (PromptUI) PromptUI.SetActive(false);
        }
    }
}
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
            if (PromptUI) PromptUI.SetActive(false);

            // 【核心修复】：把瞬移目标 (ExitPoint3D) 发送给 EnterSystem，
            // 剩下的瞬移、溶解重组动画全由 EnterSystem 统筹执行！
            if (EnterSystem != null)
            {
                EnterSystem.Restore3DPlayer(ExitPoint3D);
            }
        }
    }
}
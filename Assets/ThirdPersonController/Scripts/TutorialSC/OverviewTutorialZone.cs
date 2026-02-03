using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class OverviewTutorialZone : MonoBehaviour
    {
        [Header("Refs")]
        public CinemachineVirtualCamera TutorialVCam;      // VCam_Overview_Tutorial
        public CameraModeSwitcher ModeSwitcher;            // 拖你场景里的 CameraModeSwitcher

        [Header("Priorities")]
        public int TutorialPriority = 50;
        public int OffPriority = 0;

        [Header("Hint")]
        public GameObject TabHintRoot;

        [Header("Behavior")]
        public bool AutoSwitchOnEnterOnce = true;
        public bool HideHintAfterFirstTab = true;

        private bool _inside;
        private bool _usedOnce;
        private bool _tabLearned;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _inside = true;

            if (AutoSwitchOnEnterOnce && _usedOnce)
                return;

            _usedOnce = true;

            // 1) 让全局切换器“以为当前处在 Overview”
            // 这样玩家第一次按 Tab 就会切回 Follow（只需按一次）
            if (ModeSwitcher != null)
                ModeSwitcher.SyncModeAsOverview();

            // 2) 抢相机：切到教程镜头
            if (TutorialVCam != null)
                TutorialVCam.Priority = TutorialPriority;

            // 3) 显示提示
            if (TabHintRoot != null)
                TabHintRoot.SetActive(true);

            _tabLearned = false;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _inside = false;

            // 离开后释放教程镜头
            if (TutorialVCam != null)
                TutorialVCam.Priority = OffPriority;

            // 离开后提示隐藏
            if (TabHintRoot != null)
                TabHintRoot.SetActive(false);
        }

        private void Update()
        {
            if (!_inside || _tabLearned) return;
            if (Keyboard.current == null) return;

            // 观察 Tab：玩家按一次就算学会
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                _tabLearned = true;

                // 关键：立刻释放 TutorialCam
                // 让 CameraModeSwitcher 这一次 Tab 的结果马上生效（回 Follow）
                if (TutorialVCam != null)
                    TutorialVCam.Priority = OffPriority;

                if (HideHintAfterFirstTab && TabHintRoot != null)
                    TabHintRoot.SetActive(false);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // 确保引入了 Cinemachine

namespace TheGlitch
{
    public class SimpleTeleporter : MonoBehaviour
    {
        [Header("传送设置")]
        [Tooltip("要把玩家传送到哪里？拖入目标位置的空物体")]
        public Transform TeleportTarget;

        [Header("UI 设置")]
        public GameObject PromptUI;

        private CharacterController _playerCC;
        private bool _canTeleport = false;

        private void Start()
        {
            if (PromptUI != null) PromptUI.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerCC = other.GetComponent<CharacterController>();
                _canTeleport = true;
                if (PromptUI != null) PromptUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_playerCC != null && other.gameObject == _playerCC.gameObject) _playerCC = null;
                _canTeleport = false;
                if (PromptUI != null) PromptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (_canTeleport && _playerCC != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ExecuteTeleport();
            }
        }

        private void ExecuteTeleport()
        {
            if (TeleportTarget == null) return;

            if (PromptUI != null) PromptUI.SetActive(false);
            _canTeleport = false;

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    PerformTeleportCore();
                });
            }
            else
            {
                PerformTeleportCore();
            }
        }

        // 将核心传送逻辑单独提出来，保持代码干净
        private void PerformTeleportCore()
        {
            // 1. 关闭物理碰撞，防止瞬移时卡墙
            _playerCC.enabled = false;

            // 2. 传送身体位置和基础朝向
            _playerCC.transform.position = TeleportTarget.position;
            _playerCC.transform.rotation = TeleportTarget.rotation;

            // ==========================================
            // 【核心修复 A】：用“黑客手段(反射)”修改鼠标视角记忆
            // 完美兼容 Unity 官方 Starter Assets 第三方控制器
            // ==========================================
            var playerScript = _playerCC.GetComponent<MonoBehaviour>();
            // 寻找带有 Controller 字样的脚本
            foreach (var comp in _playerCC.GetComponents<MonoBehaviour>())
            {
                if (comp.GetType().Name.Contains("Controller"))
                {
                    playerScript = comp;
                    break;
                }
            }

            if (playerScript != null)
            {
                var type = playerScript.GetType();
                // 强行覆写底层的水平视角变量 (_cinemachineTargetYaw)
                var yawField = type.GetField("_cinemachineTargetYaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (yawField != null) yawField.SetValue(playerScript, TeleportTarget.eulerAngles.y);

                // 强行覆写底层的垂直视角变量 (_cinemachineTargetPitch)
                var pitchField = type.GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pitchField != null) pitchField.SetValue(playerScript, 0f); // 传过去后默认视线水平
            }

            // 3. 恢复物理碰撞
            _playerCC.enabled = true;

            // ==========================================
            // 【核心修复 B】：切断 Cinemachine 的平滑移动，让镜头瞬间瞬移！
            // 防止相机从旧地图“嗖”地一下飞到新地图穿模
            // ==========================================
            var allCams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam.gameObject.activeInHierarchy)
                {
                    // 这一句的意思是：“别管上一秒你在哪，现在直接给我闪现过去！”
                    cam.PreviousStateIsValid = false;
                }
            }
        }
    }
}
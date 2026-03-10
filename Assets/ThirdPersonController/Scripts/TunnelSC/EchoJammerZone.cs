using UnityEngine;

namespace TheGlitch
{
    public class EchoJammerZone : MonoBehaviour
    {
        [Header("UI 提示 (可选)")]
        public GameObject JammerWarningUI;

        // 记录玩家当前是否在区域内，防止重复执行
        private bool _isPlayerInside = false;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !_isPlayerInside)
            {
                ApplyJammerState(other.gameObject, true);
            }
        }

        // 【终极防漏补救】：如果玩家是直接瞬移进来的导致 Enter 没触发，
        // Stay 会在下一帧立刻发现玩家并补上一刀！
        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player") && !_isPlayerInside)
            {
                ApplyJammerState(other.gameObject, true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && _isPlayerInside)
            {
                ApplyJammerState(other.gameObject, false);
            }
        }

        // 把逻辑抽离出来，统一管理
        private void ApplyJammerState(GameObject player, bool isJamming)
        {
            _isPlayerInside = isJamming;

            // 1. 开关原版时空回溯
            var recorder = player.GetComponent<PlayerEchoRecorder>();
            if (recorder != null) recorder.enabled = !isJamming;

            // 2. 开关原版扫描仪 (彻底封死旧版的溶解和特效)
            var scanner = Object.FindFirstObjectByType<ScannerController>();
            if (scanner != null) scanner.enabled = !isJamming;

            // 3. 开关通道电线扫描系统
            var wireScanner = Object.FindFirstObjectByType<ScannerWireInteractor>();
            if (wireScanner != null) wireScanner.IsInTunnel = isJamming;

            if (JammerWarningUI) JammerWarningUI.SetActive(isJamming);
        }
    }
}
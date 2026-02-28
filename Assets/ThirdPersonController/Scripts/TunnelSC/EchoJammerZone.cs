using UnityEngine;

namespace TheGlitch
{
    public class EchoJammerZone : MonoBehaviour
    {
        [Header("UI 提示 (可选)")]
        public GameObject JammerWarningUI;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 1. 禁用原版时空回溯
                var recorder = other.GetComponent<PlayerEchoRecorder>();
                if (recorder != null) recorder.enabled = false;

                // 2. 禁用原版扫描仪 (干掉慢动作和变色)
                var scanner = Object.FindFirstObjectByType<ScannerController>();
                if (scanner != null) scanner.enabled = false;

                // 3. 【隔离修复】只在这个通道内，才唤醒解密电线扫描系统！
                var wireScanner = Object.FindFirstObjectByType<ScannerWireInteractor>();
                if (wireScanner != null) wireScanner.IsInTunnel = true;

                if (JammerWarningUI) JammerWarningUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 1. 恢复时空回溯
                var recorder = other.GetComponent<PlayerEchoRecorder>();
                if (recorder != null) recorder.enabled = true;

                // 2. 恢复原版扫描仪
                var scanner = Object.FindFirstObjectByType<ScannerController>();
                if (scanner != null) scanner.enabled = true;

                // 3. 【隔离修复】离开通道，彻底休眠解密电线系统，绝对不干扰外面！
                var wireScanner = Object.FindFirstObjectByType<ScannerWireInteractor>();
                if (wireScanner != null) wireScanner.IsInTunnel = false;

                if (JammerWarningUI) JammerWarningUI.SetActive(false);
            }
        }
    }
}
using System.Collections;
using TMPro;
using UnityEngine;

namespace TheGlitch
{
    public class EchoJammerZone : MonoBehaviour
    {
        [Header("UI 提示 (需包含 TextMeshProUGUI 组件)")]
        public GameObject JammerWarningUI;

        [Header("CRT 警告文本自定义")]
        public string TopText = "/// SIGNAL JAMMED ///";
        public string MidText = "[ ABILITIES OFFLINE ]";
        public string BotText = "CONNECTION LOST...";

        // 记录玩家当前是否在区域内
        private bool _isPlayerInside = false;

        // 记录是否已经展示过 UI，保证只出现一次
        private bool _hasShownUI = false;

        // UI 动画相关变量
        private Vector3 _uiOriginalScale = Vector3.one;
        private Coroutine _crtRoutine;

        private void Start()
        {
            if (JammerWarningUI != null)
            {
                // 记录 UI 的原始比例，用于动画还原
                _uiOriginalScale = JammerWarningUI.transform.localScale;
                JammerWarningUI.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !_isPlayerInside)
            {
                ApplyJammerState(other.gameObject, true);
            }
        }

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

        private void ApplyJammerState(GameObject player, bool isJamming)
        {
            _isPlayerInside = isJamming;

            // ==========================================
            // 1. 物理层：能力依然严格跟随进出状态封锁/解封
            // ==========================================
            var recorder = player.GetComponent<PlayerEchoRecorder>();
            if (recorder != null) recorder.enabled = !isJamming;

            var scanner = Object.FindFirstObjectByType<ScannerController>();
            if (scanner != null) scanner.enabled = !isJamming;

            var wireScanner = Object.FindFirstObjectByType<ScannerWireInteractor>();
            if (wireScanner != null) wireScanner.IsInTunnel = isJamming;

            // ==========================================
            // 2. 视觉层：UI 提示只在第一次进入时闪现
            // ==========================================
            if (isJamming && !_hasShownUI && JammerWarningUI != null)
            {
                if (_crtRoutine != null) StopCoroutine(_crtRoutine);
                _crtRoutine = StartCoroutine(PlayOneShotCRTWarning());
            }
        }

        private IEnumerator PlayOneShotCRTWarning()
        {
            _hasShownUI = true; // 永久锁死，这个区域再也不会弹 UI 了

            // 1. 展开 CRT
            yield return StartCoroutine(ShowCRTWarningRoutine());

            // 2. 悬停警告 1.2 秒钟 (稍作延长，让动画过度更自然)
            yield return new WaitForSecondsRealtime(1.2f);

            // 3. 断电收缩 CRT
            yield return StartCoroutine(HideCRTWarningRoutine());
        }

        // ==========================================
        // 【高级视觉特效】：CRT 屏幕丝滑展开
        // ==========================================
        private IEnumerator ShowCRTWarningRoutine()
        {
            CanvasGroup cg = JammerWarningUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = JammerWarningUI.AddComponent<CanvasGroup>();

            // 提前设为透明，防止激活的第一帧出现突然的色块闪烁
            cg.alpha = 0f;

            TextMeshProUGUI txt = JammerWarningUI.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.maxVisibleCharacters = 99999;
                txt.text = $"<color=red><size=30>{TopText}</size>\n<size=65><b>{MidText}</b></size>\n<size=25>{BotText}</size></color>";

                // 【👑 终极抗卡顿】：强制提前计算并生成网格，把排版压力释放在不可见的第一帧！
                txt.ForceMeshUpdate();
            }

            // 瞬间压扁，准备展开
            JammerWarningUI.transform.localScale = new Vector3(_uiOriginalScale.x, 0.01f * _uiOriginalScale.y, _uiOriginalScale.z);
            JammerWarningUI.SetActive(true);

            // CRT 纵向展开动画 (延长到 0.25 秒保证帧数充足)
            float duration = 0.25f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);

                // 【👑 丝滑秘诀】：Ease-Out Quart 曲线。爆发生长，然后极其顺滑地减速贴合目标！
                float easeOut = 1f - Mathf.Pow(1f - progress, 4f);

                float y = Mathf.Lerp(0.01f, 1f, easeOut);
                JammerWarningUI.transform.localScale = new Vector3(_uiOriginalScale.x, y * _uiOriginalScale.y, _uiOriginalScale.z);

                // 极速淡入，消除生硬感
                cg.alpha = Mathf.Lerp(0f, 1f, progress * 2.5f);

                yield return null;
            }
            JammerWarningUI.transform.localScale = _uiOriginalScale;
            cg.alpha = 1f;
        }

        // ==========================================
        // 【高级视觉特效】：CRT 断电收缩
        // ==========================================
        private IEnumerator HideCRTWarningRoutine()
        {
            if (!JammerWarningUI.activeSelf) yield break;

            CanvasGroup cg = JammerWarningUI.GetComponent<CanvasGroup>();
            if (cg == null) cg = JammerWarningUI.AddComponent<CanvasGroup>();

            // 阶段一：纵向压扁 (使用 Ease-In 越来越快的坠落感)
            float durationY = 0.15f;
            float t = 0f;
            while (t < durationY)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / durationY);
                float easeIn = progress * progress * progress; // Ease-In Cubic

                float y = Mathf.Lerp(1f, 0.02f, easeIn);
                JammerWarningUI.transform.localScale = new Vector3(_uiOriginalScale.x, y * _uiOriginalScale.y, _uiOriginalScale.z);
                yield return null;
            }

            // 阶段二：横向收缩断电成一个光点，同时伴随淡出
            float durationX = 0.1f;
            t = 0f;
            while (t < durationX)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / durationX);

                float x = Mathf.Lerp(1f, 0f, progress);
                JammerWarningUI.transform.localScale = new Vector3(x * _uiOriginalScale.x, 0.02f * _uiOriginalScale.y, _uiOriginalScale.z);

                cg.alpha = Mathf.Lerp(1f, 0f, progress); // 最后的一点点淡出光晕

                yield return null;
            }

            JammerWarningUI.SetActive(false);
            JammerWarningUI.transform.localScale = _uiOriginalScale;
            cg.alpha = 1f; // 恢复透明度备用
        }
    }
}
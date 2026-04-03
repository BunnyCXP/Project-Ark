using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheGlitch
{
    public class EchoCooldownUI : MonoBehaviour
    {
        public Image Icon;
        public Image Fill;
        public TMP_Text TimerText;
        public Image ReadyFlash;

        public float FlashDuration = 0.35f;

        private bool _wasReady = false;
        private float _flashT;
        private Color _flashColor;
        private Color _originalIconColor;

        private void Start()
        {
            if (ReadyFlash != null)
            {
                _flashColor = ReadyFlash.color;
                ReadyFlash.gameObject.SetActive(false);
            }
            if (Icon != null) _originalIconColor = Icon.color;
        }

        private void Update()
        {
            var rec = PlayerEchoRecorder.Instance;
            if (rec == null) return;

            bool isReady = rec.State == PlayerEchoRecorder.RecorderState.Ready;

            // --- 状态 1: 录制中 ---
            if (rec.State == PlayerEchoRecorder.RecorderState.Recording)
            {
                float remain = rec.RecordDuration - rec.CurrentRecordTime;
                float t = Mathf.Clamp01(remain / rec.RecordDuration);

                if (Fill != null) Fill.fillAmount = t;
                if (TimerText != null) TimerText.text = "REC " + remain.ToString("0.0");

                // 录制时图标变红，营造紧张感
                if (Icon != null) Icon.color = Color.red;
            }
            // --- 状态 2: 冷却中 ---
            else if (rec.State == PlayerEchoRecorder.RecorderState.Cooldown)
            {
                float remain = rec.CooldownRemain;
                float max = rec.EchoCooldown;
                float t = Mathf.Clamp01(1f - (remain / max));

                if (Fill != null) Fill.fillAmount = t;
                if (TimerText != null) TimerText.text = remain.ToString("0.0");

                if (Icon != null)
                {
                    Color c = _originalIconColor;
                    c.a = 0.4f;
                    Icon.color = c;
                }
            }
            // --- 状态 3: 准备就绪 ---
            else
            {
                if (Fill != null) Fill.fillAmount = 1f;
                if (TimerText != null) TimerText.text = "READY";

                if (Icon != null)
                {
                    Color c = _originalIconColor;
                    c.a = 1f;
                    Icon.color = c;
                }

                // 刚刚变成 Ready 时闪一下
                if (!_wasReady && ReadyFlash != null)
                {
                    _flashT = FlashDuration;
                    ReadyFlash.gameObject.SetActive(true);
                    ReadyFlash.color = _flashColor;
                }
            }

            // --- 更新闪光淡出 ---
            if (_flashT > 0f && ReadyFlash != null)
            {
                _flashT -= Time.deltaTime;
                float t = Mathf.Clamp01(_flashT / FlashDuration);

                var c = ReadyFlash.color;
                c.a = t;
                ReadyFlash.color = c;

                if (_flashT <= 0f) ReadyFlash.gameObject.SetActive(false);
            }

            _wasReady = isReady;
        }
    }
}
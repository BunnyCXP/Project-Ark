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

        private void Start()
        {
            if (ReadyFlash != null)
            {
                _flashColor = ReadyFlash.color;
                ReadyFlash.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            var rec = PlayerEchoRecorder.Instance;
            if (rec == null) return;

            float remain = rec.CooldownRemain;
            float max = rec.EchoCooldown;
            bool ready = remain <= 0f;

            // --- 冷却中 ---
            if (!ready)
            {
                float t = Mathf.Clamp01(1f - (remain / max));
                if (Fill != null) Fill.fillAmount = t;
                if (TimerText != null) TimerText.text = remain.ToString("0.0");

                if (Icon != null)
                {
                    Color c = Icon.color;
                    c.a = 0.4f;
                    Icon.color = c;
                }
            }
            // --- 冷却完成（ready == true）---
            else
            {
                if (Fill != null) Fill.fillAmount = 1f;
                if (TimerText != null) TimerText.text = "";

                if (Icon != null)
                {
                    Color c = Icon.color;
                    c.a = 1f;
                    Icon.color = c;
                }

                // ? 只在 “刚刚变成 Ready” 的那一帧触发闪光
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

                if (_flashT <= 0f)
                {
                    ReadyFlash.gameObject.SetActive(false);
                }
            }

            // 最后更新上一帧状态
            _wasReady = ready;
        }

    }
}


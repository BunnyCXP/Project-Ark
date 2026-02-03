using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheGlitch
{
    public class ScanScreenFX : MonoBehaviour
    {
        [Header("Setup")]
        public Image WaveImage;
        public float Duration = 0.35f;       // 动画时长
        public float StartScale = 0.2f;      // 从多小开始
        public float EndScale = 1.8f;        // 扩散到多大

        [Header("Alpha Curve")]
        public AnimationCurve AlphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Scan Line")]
        public Image LineImage;
        public float LineStartY = 550f;  // 从屏幕下方开始（anchoredPosition.y）
        public float LineEndY = -550f;     // 扫到上方

        public float Progress01 { get; private set; }

        private Coroutine _running;
        private Color _waveBaseColor;
        private Color _lineBaseColor;

        private void Awake()
        {
            if (WaveImage != null)
            {
                _waveBaseColor = WaveImage.color;
                WaveImage.gameObject.SetActive(false);
            }

            if (LineImage != null)
            {
                _lineBaseColor = LineImage.color;
                LineImage.gameObject.SetActive(false);
            }
        }


        public void Play()
        {
            if (WaveImage == null) return;

            if (_running != null)
                StopCoroutine(_running);

            _running = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (WaveImage != null)
            {
                WaveImage.gameObject.SetActive(true);
            }
            if (LineImage != null)
            {
                LineImage.gameObject.SetActive(true);
            }

            RectTransform waveRT = WaveImage != null ? WaveImage.rectTransform : null;
            RectTransform lineRT = LineImage != null ? LineImage.rectTransform : null;

            float t = 0f;

            while (t < Duration)
            {
                
                float u = t / Duration;
                Progress01 = u; // u = t/Duration

                // 1) Sonar 圆环：缩放 + 透明度
                if (waveRT != null)
                {
                    float s = Mathf.Lerp(StartScale, EndScale, u);
                    waveRT.localScale = Vector3.one * s;

                    float a = AlphaCurve != null ? AlphaCurve.Evaluate(u) : (1f - u);
                    WaveImage.color = new Color(_waveBaseColor.r, _waveBaseColor.g, _waveBaseColor.b, a);
                }

                // 2) 扫描线：从下往上移动，同时稍微淡入淡出
                if (lineRT != null)
                {
                    Vector2 pos = lineRT.anchoredPosition;
                    pos.y = Mathf.Lerp(LineStartY, LineEndY, u);
                    lineRT.anchoredPosition = pos;

                    // 淡入淡出（前 20% 淡入，后 20% 淡出）
                    float alphaFactor = 1f;
                    if (u < 0.2f) alphaFactor = Mathf.InverseLerp(0f, 0.2f, u);
                    else if (u > 0.8f) alphaFactor = Mathf.InverseLerp(1f, 0.8f, u);

                    LineImage.color = new Color(
                        _lineBaseColor.r,
                        _lineBaseColor.g,
                        _lineBaseColor.b,
                        _lineBaseColor.a * alphaFactor
                    );
                }
                

                t += Time.unscaledDeltaTime;  // 不受子弹时间影响
                yield return null;
            }
            Progress01 = 1f;
            // 结束时关掉
            if (WaveImage != null)
            {
                WaveImage.gameObject.SetActive(false);
                WaveImage.color = _waveBaseColor;
            }
            if (LineImage != null)
            {
                LineImage.gameObject.SetActive(false);
                LineImage.color = _lineBaseColor;
            }

            _running = null;
        }

    }
}


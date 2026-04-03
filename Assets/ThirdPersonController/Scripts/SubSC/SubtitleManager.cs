using System.Collections;
using TMPro;
using UnityEngine;

namespace TheGlitch
{
    public class SubtitleManager : MonoBehaviour
    {
        // 👑 单例模式：让全宇宙的触发器都能瞬间找到它，不需要拉线！
        public static SubtitleManager Instance { get; private set; }

        [Header("UI 引用 (自动获取)")]
        private TextMeshProUGUI _subtitleText;
        private CanvasGroup _canvasGroup;

        [Header("动画设置")]
        public float FadeDuration = 0.5f; // 淡入淡出的时间

        private Coroutine _currentRoutine;

        private void Awake()
        {
            // 单例初始化
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); }

            _subtitleText = GetComponent<TextMeshProUGUI>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            // 游戏开始时，确保字幕是完全透明隐藏的
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        // 供外部触发器调用的核心公开方法
        public void ShowSubtitle(string text, float duration)
        {
            // 如果上一句话还没说完，立刻打断它，防止两句话重叠打架 (抢话筒机制)
            if (_currentRoutine != null) StopCoroutine(_currentRoutine);

            _currentRoutine = StartCoroutine(SubtitleRoutine(text, duration));
        }

        private IEnumerator SubtitleRoutine(string text, float duration)
        {
            _subtitleText.text = text;

            // 1. 丝滑淡入
            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / FadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // 2. 保持显示指定的时间
            yield return new WaitForSeconds(duration);

            // 3. 丝滑淡出
            t = 0f;
            while (t < FadeDuration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / FadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }
    }
}
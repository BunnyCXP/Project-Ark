using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheGlitch
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance;

        [Header("黑屏设置")]
        public Image FadeImage;
        public float FadeSpeed = 2.0f; // 数值越大，黑屏和亮起的速度越快

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // 确保游戏开始时屏幕是透明的
            if (FadeImage != null)
            {
                Color c = FadeImage.color;
                c.a = 0f;
                FadeImage.color = c;
            }
        }

        // 万能呼叫接口：传入一个方法，它会在屏幕“全黑”的那一瞬间执行那个方法！
        public void DoFadeAndAction(Action midPointAction)
        {
            StartCoroutine(FadeRoutine(midPointAction));
        }

        private IEnumerator FadeRoutine(Action midPointAction)
        {
            if (FadeImage == null) yield break;

            // 1. 逐渐变黑
            Color c = FadeImage.color;
            while (c.a < 1f)
            {
                c.a += Time.deltaTime * FadeSpeed;
                FadeImage.color = c;
                yield return null;
            }

            // 2. 屏幕已经完全黑了！执行传送、扣血等逻辑
            midPointAction?.Invoke();

            // 在黑暗中停留一小会儿，给玩家一点心理缓冲
            yield return new WaitForSeconds(0.3f);

            // 3. 逐渐变亮
            while (c.a > 0f)
            {
                c.a -= Time.deltaTime * FadeSpeed;
                FadeImage.color = c;
                yield return null;
            }
        }
    }
}
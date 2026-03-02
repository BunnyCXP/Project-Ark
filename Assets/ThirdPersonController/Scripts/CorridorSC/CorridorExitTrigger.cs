using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 【新增】：引入 TextMeshPro 命名空间

public class CorridorExitTrigger : MonoBehaviour
{
    [Header("传送与黑屏设置")]
    public Transform TeleportTarget;
    public Image FadeImage;
    public float FadeInTime = 0.5f;  // 屏幕变黑的时间
    public float FadeOutTime = 0.5f; // 屏幕亮起的时间

    [Header("伪加载文字设置")]
    public TextMeshProUGUI LoadingText; // 拖入你新建的 TMP 文本控件

    [TextArea(2, 5)] // 在 Inspector 里提供一个大一点的输入框
    public string TextToType = "SYSTEM REBOOTING...\nCONNECTING TO NEW SECTOR...";

    public float TypewriterSpeed = 0.08f; // 每个字符敲击出来的间隔时间
    public float HoldTimeAfterText = 1.0f; // 文字全部打完后，在黑屏停留多久再亮起

    private bool _triggered;

    private void Start()
    {
        // 游戏开始时，确保黑屏和文字都是完全透明不可见的
        if (FadeImage != null)
        {
            Color c = FadeImage.color;
            c.a = 0f;
            FadeImage.color = c;
        }

        if (LoadingText != null)
        {
            LoadingText.text = ""; // 清空文字
            Color tc = LoadingText.color;
            tc.a = 0f;
            LoadingText.color = tc;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        StartCoroutine(DoTransition(other.transform));
    }

    private IEnumerator DoTransition(Transform player)
    {
        // ================= 1. 屏幕渐渐变黑 =================
        if (FadeImage != null)
        {
            Color c = FadeImage.color;
            float t = 0f;
            while (t < FadeInTime)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / FadeInTime);
                FadeImage.color = c;
                yield return null;
            }
            c.a = 1f;
            FadeImage.color = c;
        }

        // ================= 2. 打字机效果呈现文字 =================
        if (LoadingText != null)
        {
            // 先把文字的透明度设为 1（完全不透明），准备显示
            Color tc = LoadingText.color;
            tc.a = 1f;
            LoadingText.color = tc;

            LoadingText.text = ""; // 确保初始为空

            // 逐个字符添加到屏幕上
            foreach (char letter in TextToType)
            {
                LoadingText.text += letter;
                // 如果是空格，就不需要停顿那么久，显得更自然
                if (letter != ' ')
                    yield return new WaitForSeconds(TypewriterSpeed);
            }
        }

        // ================= 3. 后台悄悄传送玩家 =================
        // (此时玩家看着屏幕上的字，根本意识不到自己已经被传走了)
        if (TeleportTarget != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.position = TeleportTarget.position;
            player.rotation = TeleportTarget.rotation;

            if (cc != null) cc.enabled = true;
        }

        // ================= 4. 文字显示完后，额外停留一会儿 =================
        yield return new WaitForSeconds(HoldTimeAfterText);

        // ================= 5. 屏幕和文字一起渐渐变亮/消失 =================
        float fadeOutTimer = 0f;
        Color bgc = FadeImage != null ? FadeImage.color : Color.black;
        Color txtc = LoadingText != null ? LoadingText.color : Color.white;

        while (fadeOutTimer < FadeOutTime)
        {
            fadeOutTimer += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(fadeOutTimer / FadeOutTime);

            // 同时淡出背景图和文字
            if (FadeImage != null)
            {
                bgc.a = alpha;
                FadeImage.color = bgc;
            }
            if (LoadingText != null)
            {
                txtc.a = alpha;
                LoadingText.color = txtc;
            }
            yield return null;
        }

        // ================= 清理工作 =================
        if (FadeImage != null) { bgc.a = 0f; FadeImage.color = bgc; }
        if (LoadingText != null) { txtc.a = 0f; LoadingText.color = txtc; LoadingText.text = ""; }

        _triggered = false; // 允许以后回头再触发（如果不需要可以删掉这行）
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

public class CorridorExitTrigger : MonoBehaviour
{
    [Header("传送与黑屏设置")]
    public Transform TeleportTarget;
    public Image FadeImage;
    public float FadeInTime = 0.5f;
    public float FadeOutTime = 0.5f;

    [Header("伪加载文字设置")]
    public TextMeshProUGUI LoadingText;
    [TextArea(2, 5)]
    public string TextToType = "SYSTEM REBOOTING...\nCONNECTING TO NEW SECTOR...";
    public float TypewriterSpeed = 0.08f;
    public float HoldTimeAfterText = 1.0f;

    [Header("关卡切换：相机与模型升级")]
    public CameraModeSwitcher CamSwitcher;
    public CinemachineVirtualCamera NewOverviewCam;
    public GameObject OldPlayerModel;
    public GameObject NewPlayerModel;

    [Header("动画重绑 (骨骼修复)")]
    public Animator PlayerAnimator;
    public Avatar NewModelAvatar;

    [Header("【升级】粒子与多材质重组设置")]
    [Tooltip("把新模型身上的所有溶解材质(Top, Bottom等)都拖进这个列表里")]
    public Material[] HackerMaterials; // 【核心升级】：变成数组了！

    [Tooltip("Shader控制溶解的变量名(例如 _DissolveAmount)")]
    public string ShaderVariableName = "_DissolveAmount";

    [Tooltip("重组动画需要多久时间？")]
    public float ReassemblyDuration = 1.2f;

    [Tooltip("拖入场景中新模型脚底下的 VFX 粒子物体")]
    public GameObject ReassemblyVFX;

    [Tooltip("特效开始时的音效")]
    public AudioSource MaterializeSound;

    private bool _triggered;

    private void Start()
    {
        if (FadeImage != null) { Color c = FadeImage.color; c.a = 0f; FadeImage.color = c; }
        if (LoadingText != null) { LoadingText.text = ""; Color tc = LoadingText.color; tc.a = 0f; LoadingText.color = tc; }
        if (ReassemblyVFX != null) ReassemblyVFX.SetActive(false);
        SetAllMaterialsFloat(1f);
        // 确保所有材质初始都是透明的
    
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
        // 1 & 2. 黑屏与打字
        if (FadeImage != null)
        {
            Color c = FadeImage.color; float t = 0f;
            while (t < FadeInTime) { t += Time.deltaTime; c.a = Mathf.Clamp01(t / FadeInTime); FadeImage.color = c; yield return null; }
            c.a = 1f; FadeImage.color = c;
        }

        if (LoadingText != null)
        {
            Color tc = LoadingText.color; tc.a = 1f; LoadingText.color = tc; LoadingText.text = "";
            foreach (char letter in TextToType)
            {
                LoadingText.text += letter;
                if (letter != ' ') yield return new WaitForSeconds(TypewriterSpeed);
            }
        }

        // 3. 黑屏后台：传送、换相机、换骨骼
        if (TeleportTarget != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = TeleportTarget.position;
            player.rotation = TeleportTarget.rotation;
            if (cc != null) cc.enabled = true;
        }

        if (CamSwitcher != null && NewOverviewCam != null) CamSwitcher.SetNewOverviewCamera(NewOverviewCam);

        if (PlayerAnimator != null && NewModelAvatar != null)
        {
            PlayerAnimator.avatar = NewModelAvatar;
            PlayerAnimator.Rebind();
        }

        // 扒旧衣服，穿新衣服（全身透明状态）
        // 扒旧衣服，穿新衣服（全身透明状态）
        if (OldPlayerModel != null) OldPlayerModel.SetActive(false);
        if (NewPlayerModel != null) NewPlayerModel.SetActive(true);

        // 【修改这里】：把原本的 0f 改成 1f，让新衣服一穿上就是透明的
        SetAllMaterialsFloat(1f);

        yield return new WaitForSeconds(HoldTimeAfterText);

        // 4. 核心演出：粒子与全身多材质同步重组！
        if (ReassemblyVFX != null)
        {
            ReassemblyVFX.SetActive(true);
            // 强制获取粒子组件并按下播放键！
            ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play(true);
        }
        if (MaterializeSound != null) MaterializeSound.Play();

        StartCoroutine(FadeOutScreen());

        // 同步渐变所有材质
        if (HackerMaterials != null && HackerMaterials.Length > 0)
        {
            float t = 0f;
            while (t < ReassemblyDuration)
            {
                t += Time.deltaTime;
                float amount = 1f - Mathf.Clamp01(t / ReassemblyDuration);
                // 注意：如果你的Shader是 1=透明，0=实体，就把上面这句改成 float amount = 1f - Mathf.Clamp01(t / ReassemblyDuration);
                SetAllMaterialsFloat(amount);
                yield return null;
            }
            SetAllMaterialsFloat(0f); // 确保最后全是完美实体
        }
        else
        {
            yield return new WaitForSeconds(ReassemblyDuration);
        }
        // D. 停止发射粒子，让已经在半空的粒子自然消散
        if (ReassemblyVFX != null)
        {
            ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(); // 【核心】：停止喷射新数据流，但让天上的粒子继续飞完生命周期！
            }
            else
            {
                ReassemblyVFX.SetActive(false); // 防呆兜底
            }
        }
        
        if (LoadingText != null) { Color tc = LoadingText.color; tc.a = 0f; LoadingText.color = tc; LoadingText.text = ""; }
        _triggered = false;
    }

    private IEnumerator FadeOutScreen()
    {
        float fadeOutTimer = 0f;
        Color bgc = FadeImage != null ? FadeImage.color : Color.black;
        // 【新增】：获取文字的颜色
        Color txtc = LoadingText != null ? LoadingText.color : Color.white;

        while (fadeOutTimer < FadeOutTime)
        {
            fadeOutTimer += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(fadeOutTimer / FadeOutTime);

            // 黑屏逐渐变透明
            if (FadeImage != null)
            {
                bgc.a = alpha;
                FadeImage.color = bgc;
            }
            // 【新增】：文字也跟着黑屏一起平滑变透明！
            if (LoadingText != null)
            {
                txtc.a = alpha;
                LoadingText.color = txtc;
            }

            yield return null;
        }

        // 完全透明后的终极清理
        if (FadeImage != null) { bgc.a = 0f; FadeImage.color = bgc; }
        if (LoadingText != null) { txtc.a = 0f; LoadingText.color = txtc; LoadingText.text = ""; }
    }

    // 【新增的辅助方法】：一键统一修改所有材质的数值
    private void SetAllMaterialsFloat(float value)
    {
        if (HackerMaterials == null) return;
        foreach (Material mat in HackerMaterials)
        {
            if (mat != null)
            {
                mat.SetFloat(ShaderVariableName, value);
            }
        }
    }
}
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

    [Header("【新增】光照切换设置")]
    [Tooltip("黑屏传送时，想要【关闭】的旧关卡光源（或光源的父物体）")]
    public GameObject[] LightsToDisable;
    [Tooltip("黑屏传送时，想要【开启】的新关卡光源（或光源的父物体）")]
    public GameObject[] LightsToEnable;

    [Header("【新增】天空盒切换")]
    [Tooltip("新关卡的暗夜天空盒材质球。如果为空，则不切换。")]
    public Material NewSkyboxMaterial;

    [Header("【升级】粒子与多材质重组设置")]
    public Material[] HackerMaterials;
    public string ShaderVariableName = "_DissolveAmount";
    public float ReassemblyDuration = 1.2f;
    public GameObject ReassemblyVFX;
    public AudioSource MaterializeSound;

    private bool _triggered;

    private void Start()
    {
        if (FadeImage != null) { Color c = FadeImage.color; c.a = 0f; FadeImage.color = c; }
        if (LoadingText != null) { LoadingText.text = ""; Color tc = LoadingText.color; tc.a = 0f; LoadingText.color = tc; }
        if (ReassemblyVFX != null) ReassemblyVFX.SetActive(false);
        SetAllMaterialsFloat(1f);
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

        // 3. 黑屏后台：传送、换相机、换骨骼、换衣服、换光照、换天空！
        if (TeleportTarget != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 基础移动
            player.position = TeleportTarget.position;
            player.rotation = TeleportTarget.rotation;

            // 修改鼠标视角记忆 (反射)
            var playerScript = player.GetComponent<MonoBehaviour>();
            foreach (var comp in player.GetComponents<MonoBehaviour>())
            {
                if (comp.GetType().Name.Contains("Controller"))
                {
                    playerScript = comp;
                    break;
                }
            }

            if (playerScript != null)
            {
                var type = playerScript.GetType();
                var yawField = type.GetField("_cinemachineTargetYaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (yawField != null) yawField.SetValue(playerScript, TeleportTarget.eulerAngles.y);

                var pitchField = type.GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pitchField != null) pitchField.SetValue(playerScript, 0f);
            }

            if (cc != null) cc.enabled = true;

            // 切断相机平滑拖拽
            var allCams = Object.FindObjectsByType<CinemachineVirtualCamera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam.gameObject.activeInHierarchy)
                {
                    cam.PreviousStateIsValid = false;
                }
            }
        }

        if (CamSwitcher != null && NewOverviewCam != null) CamSwitcher.SetNewOverviewCamera(NewOverviewCam);

        if (PlayerAnimator != null && NewModelAvatar != null)
        {
            PlayerAnimator.avatar = NewModelAvatar;
            PlayerAnimator.Rebind();
        }

        if (OldPlayerModel != null) OldPlayerModel.SetActive(false);
        if (NewPlayerModel != null) NewPlayerModel.SetActive(true);

        SetAllMaterialsFloat(1f);

        // 切换光照
        if (LightsToDisable != null)
        {
            foreach (var lightObj in LightsToDisable)
            {
                if (lightObj != null) lightObj.SetActive(false);
            }
        }
        if (LightsToEnable != null)
        {
            foreach (var lightObj in LightsToEnable)
            {
                if (lightObj != null) lightObj.SetActive(true);
            }
        }

        // 【新增逻辑】：瞬间切换天空盒并刷新环境光
        if (NewSkyboxMaterial != null)
        {
            RenderSettings.skybox = NewSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        yield return new WaitForSeconds(HoldTimeAfterText);

        // 4. 核心演出
        if (ReassemblyVFX != null)
        {
            ReassemblyVFX.SetActive(true);
            ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play(true);
        }
        if (MaterializeSound != null) MaterializeSound.Play();

        StartCoroutine(FadeOutScreen());

        if (HackerMaterials != null && HackerMaterials.Length > 0)
        {
            float t = 0f;
            while (t < ReassemblyDuration)
            {
                t += Time.deltaTime;
                float amount = 1f - Mathf.Clamp01(t / ReassemblyDuration);
                SetAllMaterialsFloat(amount);
                yield return null;
            }
            SetAllMaterialsFloat(0f);
        }
        else
        {
            yield return new WaitForSeconds(ReassemblyDuration);
        }

        if (ReassemblyVFX != null)
        {
            ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop();
            else ReassemblyVFX.SetActive(false);
        }

        if (LoadingText != null) { Color tc = LoadingText.color; tc.a = 0f; LoadingText.color = tc; LoadingText.text = ""; }
        _triggered = false;
    }

    private IEnumerator FadeOutScreen()
    {
        float fadeOutTimer = 0f;
        Color bgc = FadeImage != null ? FadeImage.color : Color.black;
        Color txtc = LoadingText != null ? LoadingText.color : Color.white;

        while (fadeOutTimer < FadeOutTime)
        {
            fadeOutTimer += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(fadeOutTimer / FadeOutTime);

            if (FadeImage != null) { bgc.a = alpha; FadeImage.color = bgc; }
            if (LoadingText != null) { txtc.a = alpha; LoadingText.color = txtc; }

            yield return null;
        }

        if (FadeImage != null) { bgc.a = 0f; FadeImage.color = bgc; }
        if (LoadingText != null) { txtc.a = 0f; LoadingText.color = txtc; LoadingText.text = ""; }
    }

    private void SetAllMaterialsFloat(float value)
    {
        if (HackerMaterials == null) return;
        foreach (Material mat in HackerMaterials)
        {
            if (mat != null) mat.SetFloat(ShaderVariableName, value);
        }
    }
}
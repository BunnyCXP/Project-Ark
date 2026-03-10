using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Cinemachine;

public class CameraModeSwitcher : MonoBehaviour
{
    [Header("Cameras (兼容新旧版)")]
    public CinemachineVirtualCameraBase FollowCam;
    public CinemachineVirtualCameraBase OverviewCam;

    [Header("Priorities")]
    public int FollowPriority = 20;
    public int OverviewPriority = 30;

    [Header("智能防穿模设置")]
    public Image FadeImage;
    public float FadeSpeed = 0.15f;

    [Tooltip("检测遮挡的层级。千万不要选 Player(玩家) 或 IgnoreRaycast！建议只勾选 Default。")]
    public LayerMask ObstacleLayers = 1;

    [Tooltip("玩家对象。留空会自动通过标签寻找。")]
    public Transform PlayerTransform;

    private enum Mode { Follow, Overview }
    private Mode _mode = Mode.Follow;
    private bool _isTransitioning = false;

    private void Start()
    {
        if (PlayerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) PlayerTransform = p.transform;
        }

        if (FadeImage != null)
        {
            Color c = FadeImage.color;
            c.a = 0f;
            FadeImage.color = c;
            FadeImage.raycastTarget = false;
        }

        SetModeCore(Mode.Follow);
        ForceInstantCut();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame && !_isTransitioning)
        {
            Mode targetMode = (_mode == Mode.Follow) ? Mode.Overview : Mode.Follow;

            bool isBlocked = false;

            // ==========================================
            // 【终极修复】：永远只检测 玩家 和 上帝相机 之间是否被阻挡！
            // 不管是切过去还是切回来，只要这俩不在同一个房间，必须一来一回全黑屏！
            // ==========================================
            if (PlayerTransform != null && OverviewCam != null)
            {
                Vector3 rayStart = PlayerTransform.position + Vector3.up * 1.5f;
                // 目标永远是 OverviewCam 的位置
                Vector3 rayEnd = OverviewCam.transform.position;

                isBlocked = Physics.Linecast(rayStart, rayEnd, ObstacleLayers, QueryTriggerInteraction.Ignore);
            }

            if (isBlocked && FadeImage != null)
            {
                // 阻挡状态：切去和切回都是黑屏硬切
                StartCoroutine(BlackScreenTransition(targetMode));
            }
            else
            {
                // 视野开阔状态：切去和切回都是丝滑运镜
                SetModeCore(targetMode);
            }
        }
    }

    private IEnumerator BlackScreenTransition(Mode targetMode)
    {
        _isTransitioning = true;

        float t = 0;
        Color c = FadeImage.color;
        while (t < FadeSpeed)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(t / FadeSpeed);
            FadeImage.color = c;
            yield return null;
        }
        c.a = 1f;
        FadeImage.color = c;

        SetModeCore(targetMode);
        ForceInstantCut();

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        t = 0;
        while (t < FadeSpeed)
        {
            t += Time.deltaTime;
            c.a = 1f - Mathf.Clamp01(t / FadeSpeed);
            FadeImage.color = c;
            yield return null;
        }
        c.a = 0f;
        FadeImage.color = c;

        _isTransitioning = false;
    }

    public void SyncModeAsOverview() => _mode = Mode.Overview;
    public void SyncModeAsFollow() => _mode = Mode.Follow;

    private void SetModeCore(Mode m)
    {
        _mode = m;
        if (FollowCam == null || OverviewCam == null) return;

        if (m == Mode.Follow)
        {
            FollowCam.Priority = FollowPriority;
            OverviewCam.Priority = 0;
        }
        else
        {
            FollowCam.Priority = 0;
            OverviewCam.Priority = OverviewPriority;
        }
    }

    private void ForceInstantCut()
    {
        var brain = Object.FindFirstObjectByType<CinemachineBrain>();
        if (brain != null)
        {
            var oldBlend = brain.DefaultBlend;
            brain.DefaultBlend = default;
            StartCoroutine(RestoreBlend(brain, oldBlend));
        }

        if (FollowCam != null) FollowCam.PreviousStateIsValid = false;
        if (OverviewCam != null) OverviewCam.PreviousStateIsValid = false;
    }

    private IEnumerator RestoreBlend(CinemachineBrain brain, CinemachineBlendDefinition oldBlend)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        if (brain != null) brain.DefaultBlend = oldBlend;
    }

    public void SetNewOverviewCamera(CinemachineVirtualCameraBase newCam)
    {
        if (OverviewCam != null) OverviewCam.Priority = 0;
        OverviewCam = newCam;

        if (_mode == Mode.Overview && OverviewCam != null)
        {
            OverviewCam.Priority = OverviewPriority;
            ForceInstantCut();
        }
    }
}
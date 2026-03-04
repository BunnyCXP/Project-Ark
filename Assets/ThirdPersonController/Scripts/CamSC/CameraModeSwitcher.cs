using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraModeSwitcher : MonoBehaviour
{
    public CinemachineVirtualCamera FollowCam;    // VCam_Player
    public CinemachineVirtualCamera OverviewCam;  // VCam_Overview

    public int FollowPriority = 20;
    public int OverviewPriority = 30;

    private enum Mode { Follow, Overview }
    private Mode _mode = Mode.Follow;

    private void Start()
    {
        SetMode(Mode.Follow, true);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (_mode == Mode.Follow)
                SetMode(Mode.Overview);
            else
                SetMode(Mode.Follow);
        }
    }

    public void SyncModeAsOverview()
    {
        _mode = Mode.Overview;
    }

    public void SyncModeAsFollow()
    {
        _mode = Mode.Follow;
    }

    private void SetMode(Mode m, bool instant = false)
    {
        _mode = m;

        if (FollowCam == null || OverviewCam == null)
            return;

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

    // ✅ 【核心新增】：允许外部（比如传送门）把新的上帝视角相机塞进来！
    public void SetNewOverviewCamera(CinemachineVirtualCamera newCam)
    {
        // 1. 如果旧相机还在，先把它降级关掉
        if (OverviewCam != null)
        {
            OverviewCam.Priority = 0;
        }

        // 2. 换上新关卡的相机
        OverviewCam = newCam;

        // 3. 如果玩家此时正好处于上帝视角状态，立刻激活新相机！
        if (_mode == Mode.Overview && OverviewCam != null)
        {
            OverviewCam.Priority = OverviewPriority;
        }
    }
}
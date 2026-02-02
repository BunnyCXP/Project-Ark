using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

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

    // ✅ 新增：只同步“内部模式”，不改优先级
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
}

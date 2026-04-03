using UnityEngine;

namespace TheGlitch
{
    /// <summary>
    /// 录制用的一帧：记录玩家当时的位置/朝向/按键
    /// </summary>
    public struct GhostFrame
    {
        public Vector3 Pos;
        public Quaternion Rot;

        // 这一帧是否“按下了”某键（wasPressedThisFrame 或 isPressed）
        public bool PressV;
        public bool PressE;
        public bool PressQ;
        public bool ReleaseQ;

        // ★ 新增核心：这一帧是否真正成功执行了黑入？（不猜按键，只看结果）
        public bool ExecuteHack;
    }
}
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

        // 这一帧是否“按下了”某键（wasPressedThisFrame）
        public bool PressV;
        public bool PressE;
        public bool PressQ;
        public bool ReleaseQ;
    }
}


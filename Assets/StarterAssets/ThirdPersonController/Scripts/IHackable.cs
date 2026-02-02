using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public interface IHackable
    {
        string DisplayName { get; }
        Transform WorldTransform { get; }

        // 给 UI 用：展示哪些字段、当前值是什么
        List<HackField> GetFields();

        // UI 点击 Apply 后：把字段值写回去并生效
        void Apply(List<HackField> fields);

        void OnScannedOnce();
        void ResetScanFlag();

    }
}

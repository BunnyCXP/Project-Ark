using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public enum HackFieldType
    {
        Bool,
        Float,
        Enum
    }

    [Serializable]
    public class HackField
    {
        public string Id;           // 内部键：例如 "isLocked"
        public string DisplayName;  // UI显示：例如 "Door.isLocked"
        public HackFieldType Type;

        // 当前值（用字符串承载，UI输入也用字符串，最省事）
        public string Value;

        // Enum 选项（Type=Enum时用）
        public string[] Options;

        public HackField(string id, string displayName, bool v)
        {
            Id = id; DisplayName = displayName; Type = HackFieldType.Bool;
            Value = v ? "True" : "False";
            Options = Array.Empty<string>();
        }

        public HackField(string id, string displayName, float v)
        {
            Id = id; DisplayName = displayName; Type = HackFieldType.Float;
            Value = v.ToString("0.###");
            Options = Array.Empty<string>();
        }

        public HackField(string id, string displayName, string v, string[] options)
        {
            Id = id; DisplayName = displayName; Type = HackFieldType.Enum;
            Value = v;
            Options = options ?? Array.Empty<string>();
        }
    }
}

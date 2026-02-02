using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    // 挂在 Hint 的 Root 上（同一个 Root 上也挂你的 WallHintFade3D / Trigger Collider）
    public class WallHintKeyPressGlow : MonoBehaviour
    {
        [Header("Only Active In Trigger")]
        public bool RequirePlayerInTrigger = true;
        public string PlayerTag = "Player";

        [Header("Keys To Listen (match scene object name Key_XXX)")]
        public List<KeyListen> ListenKeys = new List<KeyListen>()
        {
            new KeyListen{ KeyObjectName = "Key_W", Key = Key.W },
            new KeyListen{ KeyObjectName = "Key_A", Key = Key.A },
            new KeyListen{ KeyObjectName = "Key_S", Key = Key.S },
            new KeyListen{ KeyObjectName = "Key_D", Key = Key.D },
            new KeyListen{ KeyObjectName = "Key_Shift", Key = Key.LeftShift, AlsoListen = Key.RightShift },
            new KeyListen{ KeyObjectName = "Key_Space", Key = Key.Space },
        };

        [Serializable]
        public class KeyListen
        {
            public string KeyObjectName;     // 场景对象名：Key_V / Key_E / Key_Q ...
            public Key Key;                  // 主键
            public Key AlsoListen = Key.None; // 可选：比如 Shift 右边
        }

        [Header("Hold Glow")]
        [Range(0f, 1.5f)] public float HoldIntensity = 1.0f;
        public float ReleaseReturnSpeed = 10f;
        public float PressUpSpeed = 18f;

        [Header("Material Boost")]
        public float BrightBoost = 0.55f;
        public float EmissionBoost = 2.2f;

        [Header("Text")]
        public bool AlsoAffectText = true;
        public float TextBrightBoost = 0.7f;

        [Header("Idle State")]
        [Range(0f, 0.5f)]
        public float IdleBaseIntensity = 0.15f;
        public bool ShowIdleInTrigger = true;

        [Serializable]
        private class KeyVisual
        {
            public string Name;
            public Renderer Rend;
            public TMP_Text Text;

            public Material[] Mats;
            public Color[] BaseColors;
            public Color[] BaseEmission;

            public Color TextBaseColor;

            public float Intensity01;
        }

        private readonly Dictionary<string, KeyVisual> _keys = new();
        private bool _playerInside;

        private void Awake()
        {
            BuildKeyMap();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!RequirePlayerInTrigger) return;
            if (other.CompareTag(PlayerTag))
                _playerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!RequirePlayerInTrigger) return;
            if (other.CompareTag(PlayerTag))
            {
                _playerInside = false;

                foreach (var kv in _keys.Values)
                {
                    kv.Intensity01 = 0f;
                    ApplyVisual(kv);
                }
            }
        }

        private void Update()
        {
            if (RequirePlayerInTrigger && !_playerInside)
                return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // 逐个驱动你在 Inspector 填的键
            for (int i = 0; i < ListenKeys.Count; i++)
            {
                var lk = ListenKeys[i];
                if (lk == null || string.IsNullOrEmpty(lk.KeyObjectName)) continue;

                bool held = false;

                if (lk.Key != Key.None)
                    held |= kb[lk.Key].isPressed;

                if (lk.AlsoListen != Key.None)
                    held |= kb[lk.AlsoListen].isPressed;

                DriveKey(lk.KeyObjectName, held);
            }

            foreach (var kv in _keys.Values)
                ApplyVisual(kv);
        }

        private void DriveKey(string keyName, bool held)
        {
            if (!_keys.TryGetValue(keyName, out var kv)) return;

            float idle = (ShowIdleInTrigger && _playerInside) ? IdleBaseIntensity : 0f;
            float target = held ? HoldIntensity : idle;

            float speed = held ? PressUpSpeed : ReleaseReturnSpeed;
            kv.Intensity01 = Mathf.MoveTowards(kv.Intensity01, target, Time.unscaledDeltaTime * speed);
            kv.Intensity01 = Mathf.Clamp(kv.Intensity01, 0f, 1.5f);
        }

        private void BuildKeyMap()
        {
            _keys.Clear();

            var rends = GetComponentsInChildren<Renderer>(true);
            var tmps = GetComponentsInChildren<TMP_Text>(true);

            foreach (var r in rends)
            {
                if (r == null) continue;
                string n = r.transform.name;
                if (!n.StartsWith("Key_", StringComparison.OrdinalIgnoreCase)) continue;

                if (!_keys.TryGetValue(n, out var kv))
                {
                    kv = new KeyVisual { Name = n };
                    _keys[n] = kv;
                }

                if (kv.Rend == null)
                    kv.Rend = r;
            }

            foreach (var t in tmps)
            {
                if (t == null) continue;

                Transform p = t.transform;
                string keyName = null;
                while (p != null)
                {
                    if (p.name.StartsWith("Key_", StringComparison.OrdinalIgnoreCase))
                    {
                        keyName = p.name;
                        break;
                    }
                    p = p.parent;
                }
                if (keyName == null) continue;

                if (!_keys.TryGetValue(keyName, out var kv))
                {
                    kv = new KeyVisual { Name = keyName };
                    _keys[keyName] = kv;
                }

                if (kv.Text == null)
                    kv.Text = t;
            }

            foreach (var kv in _keys.Values)
            {
                if (kv.Rend != null)
                {
                    kv.Mats = kv.Rend.materials;
                    kv.BaseColors = new Color[kv.Mats.Length];
                    kv.BaseEmission = new Color[kv.Mats.Length];

                    for (int i = 0; i < kv.Mats.Length; i++)
                    {
                        var m = kv.Mats[i];
                        if (m == null)
                        {
                            kv.BaseColors[i] = Color.white;
                            kv.BaseEmission[i] = Color.black;
                            continue;
                        }

                        kv.BaseColors[i] =
                            m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") :
                            m.HasProperty("_Color") ? m.GetColor("_Color") :
                            Color.white;

                        kv.BaseEmission[i] =
                            m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") :
                            Color.black;
                    }
                }

                if (kv.Text != null)
                    kv.TextBaseColor = kv.Text.color;

                kv.Intensity01 = 0f;
                ApplyVisual(kv);
            }
        }

        private void ApplyVisual(KeyVisual kv)
        {
            float t = Mathf.Clamp01(kv.Intensity01);
            float emissionMul = Mathf.Lerp(1f, EmissionBoost, Mathf.Clamp01(kv.Intensity01));

            if (kv.Mats != null && kv.BaseColors != null)
            {
                for (int i = 0; i < kv.Mats.Length; i++)
                {
                    var m = kv.Mats[i];
                    if (m == null) continue;

                    Color bc = kv.BaseColors[i];

                    float brightK = BrightBoost * t;
                    Color bright = Color.Lerp(bc, new Color(1f, 1f, 1f, bc.a), brightK);

                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", bright);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", bright);

                    if (m.HasProperty("_EmissionColor"))
                    {
                        Color be = kv.BaseEmission[i];
                        m.SetColor("_EmissionColor", be * emissionMul * Mathf.Max(0.2f, kv.Intensity01));
                    }
                }
            }

            if (AlsoAffectText && kv.Text != null)
            {
                Color bc = kv.TextBaseColor;
                Color bright = Color.Lerp(bc, Color.white, TextBrightBoost * t);
                kv.Text.color = new Color(bright.r, bright.g, bright.b, bc.a);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Key Map")]
        private void RebuildKeyMapInEditor() => BuildKeyMap();
#endif
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class HackableLaser : MonoBehaviour, IHackable, IQuickHackable
    {
        public enum LaserMode
        {
            Normal,
            LowDamage,
            Disabled
        }

        [Header("Visual")]
        public Renderer BeamRenderer;
        public Color NormalColor = Color.red;
        public Color LowDamageColor = Color.yellow;
        public Color DisabledColor = new Color(0.2f, 0.2f, 0.2f);

        [Header("State")]
        public LaserMode Mode = LaserMode.Normal;

        private Collider _col;
        private bool _scanTriggered;

        public string DisplayName => "Laser";
        public Transform WorldTransform => transform;

        private void Awake()
        {
            _col = GetComponent<Collider>();
            if (BeamRenderer == null)
                BeamRenderer = GetComponentInChildren<Renderer>();
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (BeamRenderer == null) return;

            Color c = NormalColor;
            switch (Mode)
            {
                case LaserMode.Normal: c = NormalColor; break;
                case LaserMode.LowDamage: c = LowDamageColor; break;
                case LaserMode.Disabled: c = DisabledColor; break;
            }

            var mat = BeamRenderer.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);

            // 碰撞是否启用
            if (_col != null)
                _col.enabled = (Mode != LaserMode.Disabled);
        }

        // ===== 扫描 glitch =====

        public void ResetScanFlag()
        {
            _scanTriggered = false;
        }

        public void OnScannedOnce()
        {
            if (_scanTriggered) return;
            _scanTriggered = true;
            StartCoroutine(ScanGlitchFX());
        }

        private IEnumerator ScanGlitchFX()
        {
            if (BeamRenderer == null) yield break;

            var mat = BeamRenderer.material;
            Color baseColor =
                mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                NormalColor;

            for (int i = 0; i < 2; i++)
            {
                SetMatColor(mat, Color.cyan);
                yield return new WaitForSecondsRealtime(0.05f);
                SetMatColor(mat, baseColor);
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }

        // ===== 深度字段 =====

        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("mode", "Laser.Mode", Mode.ToString(), new[]{"Normal","LowDamage","Disabled"})
            };
        }

        public void Apply(List<HackField> fields)
        {
            foreach (var f in fields)
            {
                if (f.Id == "mode")
                {
                    if (System.Enum.TryParse(f.Value, out LaserMode m))
                    {
                        Mode = m;
                        UpdateVisual();
                    }
                }
            }
        }

        // ===== Quick Hack 四个方向 =====

        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            // ↑：切换 Normal / LowDamage
            up = new QuickHackOption
            {
                Name = (Mode == LaserMode.Normal) ? "LowDamage" : "Normal",
                RequiresCharge = false,
                Execute = () =>
                {
                    Mode = (Mode == LaserMode.Normal) ? LaserMode.LowDamage : LaserMode.Normal;
                    UpdateVisual();
                }
            };

            // →：直接 Disable / Enable
            right = new QuickHackOption
            {
                Name = (Mode == LaserMode.Disabled) ? "Enable" : "Disable",
                RequiresCharge = false,
                Execute = () =>
                {
                    Mode = (Mode == LaserMode.Disabled) ? LaserMode.Normal : LaserMode.Disabled;
                    UpdateVisual();
                }
            };

            // ↓：短暂 Flicker（闪烁几秒）
            down = new QuickHackOption
            {
                Name = "Flicker",
                RequiresCharge = false,
                Execute = () =>
                {
                    StartCoroutine(FlickerRoutine());
                }
            };

            // ←：Overload：闪一下然后永久关掉
            left = new QuickHackOption
            {
                Name = "Overload",
                RequiresCharge = true,
                ChargeTime = 1.0f,
                Execute = () =>
                {
                    Mode = LaserMode.Disabled;
                    UpdateVisual();
                    Debug.Log("Laser Overloaded");
                }
            };
        }

        private IEnumerator FlickerRoutine()
        {
            float t = 0f;
            float duration = 2.0f;

            while (t < duration)
            {
                Mode = (Random.value > 0.5f) ? LaserMode.Disabled : LaserMode.Normal;
                UpdateVisual();

                yield return new WaitForSecondsRealtime(0.1f);
                t += 0.1f;
            }

            Mode = LaserMode.Normal;
            UpdateVisual();
        }
    }
}


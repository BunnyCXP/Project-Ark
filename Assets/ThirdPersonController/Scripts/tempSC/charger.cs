using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class QChargerHackable : MonoBehaviour, IHackable, IQuickHackable
    {
        [Header("UI")]
        public string ChargerName = "Charge Node";

        [Header("Charge")]
        [Tooltip("HackWheel 长按Q需要多久才算充满")]
        public float RequiredChargeTime = 0.8f;

        [Tooltip("充满后保持“已充能”的时间（用于门判断“同时”）")]
        public float KeepAlive = 10.0f;

        [Tooltip("是否允许在已充能时再次充能刷新时间")]
        public bool RefreshIfCharged = true;

        [Header("Scan FX (optional)")]
        public bool PlayScanFX = true;

        [Header("Visual")]
        public Color ChargedColor = new Color(0.2f, 0.8f, 1f); // 蓝色
        public float ChargeLerpSpeed = 10f;

        private Renderer _rend;
        private Material _mat;
        private Color _baseColor;
        private Color _currentColor;


        // ===== IHackable =====
        public string DisplayName => ChargerName;
        public Transform WorldTransform => transform;

        private bool _scanTriggered = false;

        // 充能状态
        private float _chargedTimer;

        public bool IsCharged => _chargedTimer > 0f;

        public void ResetScanFlag()
        {
            _scanTriggered = false;
        }

        public void OnScannedOnce()
        {
            if (!_scanTriggered && PlayScanFX)
            {
                _scanTriggered = true;
                StartCoroutine(ScanGlitchFX());
            }
        }
        private void UpdateVisual()
        {
            if (_mat == null) return;

            Color target = IsCharged ? ChargedColor : _baseColor;

            _currentColor = Color.Lerp(
                _currentColor,
                target,
                Time.deltaTime * ChargeLerpSpeed
            );

            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", _currentColor);
            if (_mat.HasProperty("_Color"))
                _mat.SetColor("_Color", _currentColor);
        }


        private void Update()
        {
            UpdateVisual();
            if (_chargedTimer > 0f)
                _chargedTimer -= Time.deltaTime;
        }

        private void MarkCharged()
        {
            if (IsCharged && !RefreshIfCharged) return;
            _chargedTimer = KeepAlive;
        }

        // ===== Quick Hacks =====
        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            // 你要的是“同时按Q蓄满”，所以就把唯一交互放 Up（你喜欢放哪都行）
            up = new QuickHackOption
            {
                Id = "Node_Charge",
                Name = "Charge",
                RequiresCharge = true,
                ChargeTime = RequiredChargeTime,
                Execute = () =>
                {
                    MarkCharged();
                }
            };

            right = null;
            down = null;
            left = null;
        }

        // ===== 深度 hack（给 HackPromptUI 预览用）=====
        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("charged", "Node.Charged", IsCharged),
                new HackField("remain", "Node.Remain", Mathf.Max(0f, _chargedTimer))
            };
        }

        public void Apply(List<HackField> fields)
        {
            // 暂时不需要深度修改，留空
        }

        // ===== Scan FX：照你 Box 的写法 =====
        private IEnumerator ScanGlitchFX()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            Material mat = rend.material;

            Color baseColor =
                mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                mat.color;

            for (int i = 0; i < 3; i++)
            {
                SetMatColor(mat, new Color(0.2f, 0.8f, 1f)); // 充能节点用偏蓝更像能量
                transform.localScale *= 1.05f;

                yield return new WaitForSecondsRealtime(0.04f);

                SetMatColor(mat, baseColor);
                transform.localScale /= 1.05f;

                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            if (_rend != null)
            {
                _mat = _rend.material;

                _baseColor =
                    _mat.HasProperty("_BaseColor") ? _mat.GetColor("_BaseColor") :
                    _mat.HasProperty("_Color") ? _mat.GetColor("_Color") :
                    Color.white;

                _currentColor = _baseColor;
            }
        }


        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }
    }
}

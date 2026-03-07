using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Rigidbody))]
    public class HackableBox : MonoBehaviour, IHackable, IQuickHackable
    {
        public float GravityScale = 1.0f;
        public float GravityStrength = 9.81f;

        [Header("Quick Hack Presets")]
        public float FloatUpScale = -0.3f;      // 低重力（上飘）
        public float HoverScale = 0f;           // 悬停
        public float LowFrictionDrag = 0.1f;    // 更容易推
        public float NormalDrag = 1.5f;

        [Header("Height Limit (新增高度限制)")]
        public float MaxFloatHeight = 5f;       // 允许箱子最高飘多高
        private float _startY;                  // 记录初始Y坐标

        private Rigidbody _rb;

        // defaults for undo
        private float _defaultGravityScale;
        private float _defaultDrag;

        public string DisplayName => "Box";
        public Transform WorldTransform => transform;
        private bool _scanTriggered = false;

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
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            Material mat = rend.material;

            Color baseColor =
                mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                mat.color;

            for (int i = 0; i < 3; i++)
            {
                SetMatColor(mat, new Color(1f, 0.2f, 0.2f));
                transform.localScale *= 1.05f;

                yield return new WaitForSecondsRealtime(0.04f);

                SetMatColor(mat, baseColor);
                transform.localScale /= 1.05f;

                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;

            _defaultGravityScale = GravityScale;
            _defaultDrag = NormalDrag;

            // 记录箱子出生时的高度
            _startY = transform.position.y;
        }

        private void FixedUpdate()
        {
            // 【新增】高度限制检测：如果在上飘状态且超出了最大高度
            if (GravityScale < 0 && transform.position.y >= _startY + MaxFloatHeight)
            {
                // 强制切为悬停状态
                GravityScale = HoverScale;
                // 消除向上冲的惯性，让它瞬间停住
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            }

            Vector3 g = Vector3.down * GravityStrength * GravityScale;
            _rb.AddForce(g, ForceMode.Acceleration);

            _rb.linearDamping = NormalDrag;
        }

        // ===== Wheel hacks =====
        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            up = new QuickHackOption
            {
                Id = "Box_LowG",
                Name = "Low G",
                RequiresCharge = false,
                Execute = () =>
                {
                    GravityScale = Mathf.Approximately(GravityScale, FloatUpScale) ? _defaultGravityScale : FloatUpScale;
                }
            };

            right = new QuickHackOption
            {
                Id = "Box_LowFriction",
                Name = "Low f",
                RequiresCharge = false,
                Execute = () =>
                {
                    NormalDrag = Mathf.Approximately(NormalDrag, LowFrictionDrag) ? _defaultDrag : LowFrictionDrag;
                }
            };

            down = new QuickHackOption
            {
                Id = "Box_Hover",
                Name = "In Air",
                RequiresCharge = false,
                Execute = () =>
                {
                    GravityScale = Mathf.Approximately(GravityScale, HoverScale) ? _defaultGravityScale : HoverScale;
                }
            };

            left = new QuickHackOption
            {
                Id = "Box_Crack",
                Name = "Crack (Charge)",
                RequiresCharge = true,
                ChargeTime = 0.8f,
                Execute = () =>
                {
                    Debug.Log("LOUD BOX DESTROYED");
                    gameObject.SetActive(false);
                }
            };
        }

        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("gravity", "Box.Gravity", GravityScale)
            };
        }

        public void Apply(List<HackField> fields)
        {
            foreach (var f in fields)
            {
                if (f.Id == "gravity" && float.TryParse(f.Value, out float v))
                    GravityScale = Mathf.Clamp(v, -2f, 2f);
            }
        }
    }
}
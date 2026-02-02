using UnityEngine;

namespace TheGlitch
{
    public class VoxelShard : MonoBehaviour
    {
        [Header("Lifetime")]
        public float Lifetime = 1.6f;      // 方块存在多久

        [Header("Fade & Scale")]
        public float MinScale = 0.45f;     // 缩到多小
        public float GravityDelay = 0.15f; // 延迟多久再掉落

        [Header("Internal")]
        public float FadeCurvePower = 2.0f; // 越大越慢开始，越快结束

        private Material _mat;
        private Rigidbody _rb;
        private float _t;
        private Color _baseColor;
        private Vector3 _startScale;
        private float _startAlpha = 1f;

        private void Start()
        {
            var rend = GetComponent<MeshRenderer>();
            if (rend != null)
            {
                _mat = rend.material;

                _baseColor =
                    _mat.HasProperty("_BaseColor") ? _mat.GetColor("_BaseColor") :
                    _mat.HasProperty("_Color") ? _mat.GetColor("_Color") :
                    Color.white;

                _startAlpha = _baseColor.a;
            }

            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
                _rb.useGravity = false;

            _startScale = transform.localScale;
        }

        private void Update()
        {
            if (Lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Lifetime);

            float fade = Mathf.Pow(k, FadeCurvePower);

            // 颜色淡出
            if (_mat != null)
            {
                float a = Mathf.Lerp(_startAlpha, 0f, fade);
                Color c = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);

                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", c);
                if (_mat.HasProperty("_Color"))
                    _mat.SetColor("_Color", c);

                if (_mat.HasProperty("_EmissionColor"))
                {
                    Color ec = _mat.GetColor("_EmissionColor");
                    _mat.SetColor("_EmissionColor", ec * (1f - fade));
                }
            }

            // 缩小
            float scaleMul = Mathf.Lerp(1f, MinScale, fade);
            transform.localScale = _startScale * scaleMul;

            // 延迟开启重力
            if (_rb != null && !_rb.useGravity && _t >= GravityDelay)
            {
                _rb.useGravity = true;
            }

            // 时间结束
            if (_t >= Lifetime)
                Destroy(gameObject);
        }
    }
}



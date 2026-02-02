using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public class AfterimageFade : MonoBehaviour
    {
        private float _life;
        private float _alpha;
        private float _t;

        private List<Material> _mats = new();

        public void Init(float lifetime, float alpha)
        {
            _life = Mathf.Max(0.01f, lifetime);
            _alpha = Mathf.Clamp01(alpha);

            var rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                // materials 会实例化：便于单独改 alpha
                _mats.AddRange(r.materials);
            }

            // 初始设置到目标 alpha
            SetAlpha(_alpha);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);

            // 从 _alpha -> 0
            float a = Mathf.Lerp(_alpha, 0f, k);
            SetAlpha(a);

            if (_t >= _life)
                Destroy(gameObject);
        }

        private void SetAlpha(float a)
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m == null) continue;

                // 只改颜色 alpha
                if (m.HasProperty("_BaseColor"))
                {
                    Color c = m.GetColor("_BaseColor");
                    c.a = a;
                    m.SetColor("_BaseColor", c);
                }
                if (m.HasProperty("_Color"))
                {
                    Color c = m.GetColor("_Color");
                    c.a = a;
                    m.SetColor("_Color", c);
                }
            }
        }
    }
}


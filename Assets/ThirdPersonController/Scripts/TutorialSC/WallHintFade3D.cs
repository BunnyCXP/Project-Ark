using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class WallHintFade3D : MonoBehaviour
    {
        [Header("Fade")]
        public float FadeInTime = 0.25f;
        public float FadeOutTime = 0.25f;

        [Header("Trigger")]
        public string PlayerTag = "Player";

        [Header("Optional")]
        public bool StartHidden = true;
        public bool IncludeInactiveChildren = true;

        // 自动收集
        private readonly List<Renderer> _renderers = new();
        private readonly List<TMP_Text> _tmps = new();

        // 记录原始颜色（用于恢复RGB，只改Alpha）
        private readonly List<Color[]> _rendererBaseColors = new(); // 每个 renderer 可能多个材质
        private readonly List<Color> _tmpBaseColors = new();

        private Coroutine _co;
        private float _alphaTarget = 0f;
        private float _alphaCurrent = 0f;

        private void Awake()
        {
            // 确保 trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            // 收集所有 Renderer（包含 Quad 的 MeshRenderer）
            _renderers.Clear();
            _renderers.AddRange(GetComponentsInChildren<Renderer>(IncludeInactiveChildren));

            // 收集所有 TMP（世界空间 / UI 都行）
            _tmps.Clear();
            _tmps.AddRange(GetComponentsInChildren<TMP_Text>(IncludeInactiveChildren));

            // 记录每个 Renderer 每个材质的 baseColor
            _rendererBaseColors.Clear();
            foreach (var r in _renderers)
            {
                if (r == null) { _rendererBaseColors.Add(new Color[0]); continue; }

                var mats = r.materials; // 注意：materials 会实例化，适合做淡入淡出
                var colors = new Color[mats.Length];

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) { colors[i] = Color.white; continue; }

                    colors[i] =
                        m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") :
                        m.HasProperty("_Color") ? m.GetColor("_Color") :
                        Color.white;
                }

                _rendererBaseColors.Add(colors);
            }

            // 记录 TMP baseColor
            _tmpBaseColors.Clear();
            foreach (var t in _tmps)
            {
                _tmpBaseColors.Add(t != null ? t.color : Color.white);
            }

            if (StartHidden)
            {
                _alphaCurrent = 0f;
                _alphaTarget = 0f;
                ApplyAlpha(0f);
            }
            else
            {
                _alphaCurrent = 1f;
                _alphaTarget = 1f;
                ApplyAlpha(1f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            FadeTo(1f);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            FadeTo(0f);
        }

        private void FadeTo(float a)
        {
            _alphaTarget = Mathf.Clamp01(a);

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(FadeRoutine());
        }

        private System.Collections.IEnumerator FadeRoutine()
        {
            float start = _alphaCurrent;
            float end = _alphaTarget;

            float dur = (end > start) ? FadeInTime : FadeOutTime;
            dur = Mathf.Max(0.0001f, dur);

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                _alphaCurrent = Mathf.Lerp(start, end, k);
                ApplyAlpha(_alphaCurrent);
                yield return null;
            }

            _alphaCurrent = end;
            ApplyAlpha(_alphaCurrent);
            _co = null;
        }

        private void ApplyAlpha(float a)
        {
            // Renderer
            for (int rIndex = 0; rIndex < _renderers.Count; rIndex++)
            {
                var r = _renderers[rIndex];
                if (r == null) continue;

                var mats = r.materials;
                var baseColors = _rendererBaseColors[rIndex];

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    Color bc = (i < baseColors.Length) ? baseColors[i] : Color.white;
                    Color c = new Color(bc.r, bc.g, bc.b, a);

                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", c);

                    // 可选：发光也跟着淡一点（看你喜好）
                    if (m.HasProperty("_EmissionColor"))
                    {
                        var ec = m.GetColor("_EmissionColor");
                        m.SetColor("_EmissionColor", ec * a);
                    }
                }
            }

            // TMP
            for (int i = 0; i < _tmps.Count; i++)
            {
                var t = _tmps[i];
                if (t == null) continue;

                Color bc = _tmpBaseColors[i];
                t.color = new Color(bc.r, bc.g, bc.b, a);
            }
        }
    }
}

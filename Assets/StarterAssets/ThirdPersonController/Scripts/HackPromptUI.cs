using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheGlitch
{
    public class HackPromptUI : MonoBehaviour
    {
        public TMP_Text Title;      // 名字
        public TMP_Text Hint;       // “Press E to Hack”

        [Header("Highlight")]
        public Image Background;
        public Color NormalColor = new Color(0, 0, 0, 0.55f);
        public Color HighlightColor = new Color(0.0f, 0.6f, 1.0f, 0.75f);

        [HideInInspector] public IHackable Target;
        [HideInInspector] public Camera Cam;
        public Vector3 WorldOffset = new Vector3(0, 2.0f, 0);

        // ===== 出场淡入 / 上飘 =====
        private CanvasGroup _group;
        private RectTransform _rt;
        private float _appearT = 0f;
        private float _appearDuration = 0.25f;
        private float _yOffset = 25f;
        private bool _wasVisibleLastFrame;

        private void Awake()
        {
            EnsureRefs();
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();

            _rt = GetComponent<RectTransform>();

            _group.alpha = 0f;
            _appearT = 0f;
            _wasVisibleLastFrame = false;
        }

        // 只显示名称 + 固定提示，不再显示字段预览
        public void Bind(IHackable target, Camera cam)
        {
            EnsureRefs();
            Target = target;
            Cam = cam;

            if (Title != null)
                Title.text = Target?.DisplayName ?? "";

            if (Hint != null)
                Hint.text = "E : Hack";

            SetHighlighted(false);
        }
        private Vector3 GetAnchorWorldPos()
        {
            if (Target == null || Target.WorldTransform == null)
                return Vector3.zero;

            // 1?? 优先用 Renderer 的 bounds.center
            var rend = Target.WorldTransform.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                return rend.bounds.center + WorldOffset;
            }

            // 2?? 实在没有 Renderer，就退回 transform.position
            return Target.WorldTransform.position + WorldOffset;
        }

        public void SetHighlighted(bool on)
        {
            if (Background != null)
                Background.color = on ? HighlightColor : NormalColor;

            if (Hint != null)
                Hint.color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
        private void EnsureRefs()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }

            if (_rt == null)
            {
                _rt = GetComponent<RectTransform>();
                // 如果这里还 null，说明你的 HackPromptUI 不在 UI 对象上（不是 RectTransform）
                // 那 prefab 结构就要修：必须挂在 Canvas 下的 UI 上
            }
        }


        public bool UpdateScreenPositionOnlyIfVisible()
        {
            // 这句非常关键：如果 _rt 还是 null，直接不更新，避免崩
            if (_rt == null) return false;
            if (Target == null || Target.WorldTransform == null || Cam == null)
            {
                _wasVisibleLastFrame = false;
                _group.alpha = 0f;
                return false;
            }

            Vector3 worldPos = GetAnchorWorldPos();

            Vector3 vp = Cam.WorldToViewportPoint(worldPos);

            bool inFront = vp.z > 0.01f;
            bool inScreenX = vp.x >= -0.05f && vp.x <= 1.05f;
            bool inScreenY = vp.y >= -0.05f && vp.y <= 1.05f;
            bool visible = inFront && inScreenX && inScreenY;

            if (!visible)
            {
                if (_wasVisibleLastFrame)
                {
                    _group.alpha = 0;
                    _appearT = 0;
                }
                _wasVisibleLastFrame = false;
                return false;
            }

            // 刚进入可见 → 播放淡入动画
            if (!_wasVisibleLastFrame)
            {
                _appearT = 0f;
                _group.alpha = 0f;

                Vector2 sp0 = Cam.WorldToScreenPoint(worldPos);
                _rt.position = sp0 + new Vector2(0, _yOffset);
            }

            _wasVisibleLastFrame = true;

            // 淡入插值
            if (_appearT < 1f)
            {
                _appearT += Time.unscaledDeltaTime / _appearDuration;
                if (_appearT > 1f) _appearT = 1f;
            }

            Vector2 sp = Cam.WorldToScreenPoint(worldPos);
            float yOffset = Mathf.Lerp(_yOffset, 0, _appearT);

            _rt.position = sp + new Vector2(0, yOffset);
            _group.alpha = _appearT;

            return true;
        }
    }
}


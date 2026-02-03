using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheGlitch
{
    public enum WheelDir { None, Up, Right, Down, Left }

    [System.Serializable]

    
    public class QuickHackOption
    {
        // ★ 新增：唯一 Id，用来让影子区分是哪个选项
        public string Id;

        public string Name;
        public System.Action Execute;

        // 是否需要充能
        public bool RequiresCharge = false;

        // 需要充能多久（秒）
        public float ChargeTime = 0.8f;
    }



    public class HackWheelUI : MonoBehaviour
    {
        [Header("Root")]
        public GameObject Root;

        [Header("Slices")]
        public Image Up;
        public Image Right;
        public Image Down;
        public Image Left;

        [Header("Labels")]
        public TMP_Text UpText;
        public TMP_Text RightText;
        public TMP_Text DownText;
        public TMP_Text LeftText;

        [Header("Tuning")]
        public float DeadZone = 35f;            // 鼠标推多远才算选中
        public float MaxRadius = 120f;          // 防止无限累计
        public Color NormalColor = new Color(0, 0, 0, 0.5f);
        public Color HighlightColor = new Color(0.0f, 0.7f, 1.0f, 0.85f);

        [Header("Follow Target")]
        public Vector3 FollowWorldOffset = new Vector3(0, 2.0f, 0);
        public Vector2 FollowScreenOffset = new Vector2(40, -20);

        private Transform _followTarget;
        private Camera _followCam;

        [Header("Charge UI")]
        public Image ChargeRing;



        private float _shakeTimer;
        private Vector2 _basePos;
        [Header("Charge Shake")]
        public float ChargeShakeMax = 22f;    // 满充时最大抖动像素
        public float ChargeShakeMin = 2f;     // 刚开始充能时抖动像素
        public float ChargeShakeFreq = 35f;   // 抖动频率（越大越抖）


        public bool UseRendererBoundsCenter = true;


        private Vector2 _accum;                // 累计鼠标 delta
        private WheelDir _currentDir = WheelDir.None;

        private QuickHackOption _up, _right, _down, _left;

        public WheelDir CurrentDir => _currentDir;

        public QuickHackOption GetSelectedOption()
        {
            return _currentDir switch
            {
                WheelDir.Up => _up,
                WheelDir.Right => _right,
                WheelDir.Down => _down,
                WheelDir.Left => _left,
                _ => null
            };
        }

        public void Open(QuickHackOption up, QuickHackOption right, QuickHackOption down, QuickHackOption left)
        {
            _up = up; _right = right; _down = down; _left = left;

            UpText.text = up?.Name ?? "-";
            RightText.text = right?.Name ?? "-";
            DownText.text = down?.Name ?? "-";
            LeftText.text = left?.Name ?? "-";

            _accum = Vector2.zero;
            SetDir(WheelDir.None);

            Root.SetActive(true);
            var rt = Root.transform as RectTransform;
            _basePos = rt.anchoredPosition;

            SetChargeProgress(0f);

        }

        public void Close()
        {
            Root.SetActive(false);
            _accum = Vector2.zero;
            SetDir(WheelDir.None);
            SetChargeProgress(0f);

        }

  


        // 每帧喂进鼠标delta（不需要解锁鼠标）
        public void FeedMouseDelta(Vector2 delta)
        {
            if (!Root.activeSelf) return;

            _accum += delta;

            // 限制半径
            if (_accum.magnitude > MaxRadius)
                _accum = _accum.normalized * MaxRadius;

            // 在 deadzone 里不选
            if (_accum.magnitude < DeadZone)
            {
                SetDir(WheelDir.None);
                return;
            }

            // 选四方向
            if (Mathf.Abs(_accum.x) > Mathf.Abs(_accum.y))
                SetDir(_accum.x > 0 ? WheelDir.Right : WheelDir.Left);
            else
                SetDir(_accum.y > 0 ? WheelDir.Up : WheelDir.Down);
        }

        public bool TryExecuteSelected()
        {
            if (!Root.activeSelf) return false;

            QuickHackOption opt = _currentDir switch
            {
                WheelDir.Up => _up,
                WheelDir.Right => _right,
                WheelDir.Down => _down,
                WheelDir.Left => _left,
                _ => null
            };

            if (opt?.Execute == null) return false;
            opt.Execute.Invoke();
            return true;
        }

        public void SetFollow(Transform target, Camera cam)
        {
            _followTarget = target;
            _followCam = cam;
        }

        public void ClearFollow()
        {
            _followTarget = null;
            _followCam = null;
        }

        private float _chargeT;

        public void SetChargeProgress(float t01)
        {
            _chargeT = Mathf.Clamp01(t01);

            if (ChargeRing != null)
            {
                ChargeRing.gameObject.SetActive(_chargeT > 0f);
                ChargeRing.fillAmount = _chargeT;
            }
        }


        private void LateUpdate()
        {
            if (Root == null || !Root.activeSelf) return;
            if (_followTarget == null || _followCam == null) return;

            var rt = Root.transform as RectTransform;

            Vector3 worldPos = _followTarget.position + FollowWorldOffset;

            if (UseRendererBoundsCenter)
            {
                var r = _followTarget.GetComponentInChildren<Renderer>();
                if (r != null) worldPos = r.bounds.center + FollowWorldOffset;
            }

            Vector3 vp = _followCam.WorldToViewportPoint(worldPos);

            bool behind = vp.z < 0.01f;
            bool offscreen = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;

            // 背后就翻到前面再贴边
            if (behind)
            {
                vp.x = 1f - vp.x;
                vp.y = 1f - vp.y;
                vp.z = 0.01f;
                offscreen = true;
            }

            // 目标出屏：贴边
            if (offscreen)
            {
                float padX = 0.06f;
                float padY = 0.08f;
                vp.x = Mathf.Clamp(vp.x, padX, 1f - padX);
                vp.y = Mathf.Clamp(vp.y, padY, 1f - padY);
            }

            Vector2 screen = _followCam.ViewportToScreenPoint(vp);
            screen += FollowScreenOffset;

            // 用尺寸保证不出屏（建议 Root Pivot = (0,1)）
            Vector2 size = rt.rect.size;
            float x = Mathf.Clamp(screen.x, 0f, Screen.width - size.x);
            float y = Mathf.Clamp(screen.y, size.y, Screen.height);
            rt.position = new Vector2(x, y);
            // 震动：在屏幕空间做一个小抖
            // 充能抖动：越接近满充越明显（用 unscaledTime 不受慢动作影响）
            // ===== 充能抖动：越接近满充越明显 =====
            if (_chargeT > 0f)
            {
                float strength = Mathf.Lerp(ChargeShakeMin, ChargeShakeMax, _chargeT);

                float jitterX =
                    (Mathf.PerlinNoise(Time.unscaledTime * ChargeShakeFreq, 0.13f) - 0.5f) * 2f;

                float jitterY =
                    (Mathf.PerlinNoise(0.27f, Time.unscaledTime * ChargeShakeFreq) - 0.5f) * 2f;

                var rtt = Root.transform as RectTransform;
                rtt.anchoredPosition += new Vector2(jitterX, jitterY) * strength;
            }



        }


        private void SetDir(WheelDir dir)
        {
            if (_currentDir == dir) return;
            _currentDir = dir;

            // reset
            Up.color = NormalColor;
            Right.color = NormalColor;
            Down.color = NormalColor;
            Left.color = NormalColor;

            switch (dir)
            {
                case WheelDir.Up: Up.color = HighlightColor; break;
                case WheelDir.Right: Right.color = HighlightColor; break;
                case WheelDir.Down: Down.color = HighlightColor; break;
                case WheelDir.Left: Left.color = HighlightColor; break;
            }
        }
    }
}

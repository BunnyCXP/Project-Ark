using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class TutorialPracticeHackable : MonoBehaviour, IHackable, IQuickHackable
    {
        [Header("UI")]
        public string DisplayNameOverride = "Practice Node";
        public string DisplayName => DisplayNameOverride;
        public Transform WorldTransform => transform;

        [Header("Short Press (Move)")]
        [Tooltip("滑动目标点（空物体）")]
        public Transform MoveTo;
        [Tooltip("滑动时长（秒）")]
        public float MoveDuration = 0.45f;
        [Tooltip("缓动曲线：0~1")]
        public AnimationCurve MoveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("滑动中是否禁止再次触发")]
        public bool LockWhileMoving = true;

        [Header("Charge (Vanish)")]
        [Tooltip("长按Q充能时间")]
        public float VanishChargeTime = 0.8f;

        [Tooltip("阶段1：快速淡到残影的时间")]
        public float FadeToGhostTime = 0.12f;

        [Tooltip("阶段2：残影慢慢消失的时间")]
        public float GhostFadeOutTime = 0.9f;

        private Vector3 _startPos;
        private bool _moved;

        [Range(0f, 1f)]
        [Tooltip("残影阶段的目标透明度")]
        public float GhostAlpha = 0.18f;

        [Tooltip("消失后是否禁用物体（推荐 true）")]
        public bool DisableOnEnd = true;

        [Header("Afterimage")]
        [Tooltip("是否生成淡淡残影")]
        public bool SpawnAfterimage = true;
        public float AfterimageInterval = 0.05f;
        public float AfterimageLifetime = 0.25f;
        [Range(0f, 1f)] public float AfterimageAlpha = 0.12f;

        // ===== internal =====
        private bool _scanTriggered;
        private bool _moving;
        private bool _vanishing;

        // 渲染器收集：Skinned + Mesh 都支持
        private SkinnedMeshRenderer[] _skinned;
        private MeshRenderer[] _meshes;

        private List<Material> _mats = new();
        private List<Color> _baseColors = new();

        private Coroutine _afterimageCo;

        private void Awake()
        {
            _skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _meshes = GetComponentsInChildren<MeshRenderer>(true);

            CacheMaterials();
            _startPos = transform.position;
            _moved = false;

            // 练习对象通常不需要物理阻挡玩家（看你要不要）
            // 如果要挡路就别关 collider
        }

        private void CacheMaterials()
        {
            _mats.Clear();
            _baseColors.Clear();

            foreach (var r in _skinned)
            {
                if (r == null) continue;
                _mats.AddRange(r.materials);
            }
            foreach (var r in _meshes)
            {
                if (r == null) continue;
                _mats.AddRange(r.materials);
            }

            foreach (var m in _mats)
            {
                if (m == null) { _baseColors.Add(Color.white); continue; }

                Color c =
                    m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") :
                    m.HasProperty("_Color") ? m.GetColor("_Color") :
                    Color.white;

                _baseColors.Add(c);
            }
        }

        // ===== IHackable =====
        public void ResetScanFlag() => _scanTriggered = false;

        public void OnScannedOnce()
        {
            if (_scanTriggered) return;
            _scanTriggered = true;
            StartCoroutine(ScanGlitchFX());
        }

        private IEnumerator ScanGlitchFX()
        {
            // 跟你 Box 风格一致：闪几下
            if (_mats.Count == 0) yield break;

            for (int i = 0; i < 3; i++)
            {
                SetAllMatColor(new Color(0.2f, 0.8f, 1f, 1f));
                transform.localScale *= 1.03f;
                yield return new WaitForSecondsRealtime(0.04f);

                RestoreAllMatColor();
                transform.localScale /= 1.03f;
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        // ===== IQuickHackable =====
        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            // 只显示两个：Up=Move（短按Q），Left=Vanish（长按Q）
            up = new QuickHackOption
            {
                Id = "Practice_Move",
                Name = "Slide",
                RequiresCharge = false,
                Execute = () =>
                {
                    if (_vanishing) return;
                    if (LockWhileMoving && _moving) return;
                    if (MoveTo == null) return;

                    // ★ Toggle：去 MoveTo / 回 start
                    Vector3 target = _moved ? _startPos : MoveTo.position;
                    _moved = !_moved;

                    StartCoroutine(MoveRoutine(target, MoveDuration));
                }

            };

            left = new QuickHackOption
            {
                Id = "Practice_Vanish",
                Name = "Phase (Charge)",
                RequiresCharge = true,
                ChargeTime = VanishChargeTime,
                Execute = () =>
                {
                    if (_moving) return;
                    if (_vanishing) return;
                    StartCoroutine(VanishRoutine());
                }
            };

            right = null;
            down = null;
        }

        public List<HackField> GetFields() => new List<HackField>(); // 你现在不需要字段预览
        public void Apply(List<HackField> fields) { }

        // ===== Move =====
        private IEnumerator MoveRoutine(Vector3 targetWorld, float duration)
        {
            _moving = true;

            Vector3 start = transform.position;
            float t = 0f;

            // 滑动时给一点残影（可选）
            if (SpawnAfterimage && _afterimageCo == null)
                _afterimageCo = StartCoroutine(AfterimageRoutine());

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                float e = MoveEase != null ? MoveEase.Evaluate(k) : k;

                transform.position = Vector3.Lerp(start, targetWorld, e);
                yield return null;
            }
            transform.position = targetWorld;

            if (_afterimageCo != null)
            {
                StopCoroutine(_afterimageCo);
                _afterimageCo = null;
            }

            _moving = false;
        }

        // ===== Vanish (fast to ghost, then slow fade) =====
        private IEnumerator VanishRoutine()
        {
            _vanishing = true;

            // 如果你不想玩家碰它：这里关掉 Collider
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 残影阶段也生成 afterimage（很淡）
            if (SpawnAfterimage && _afterimageCo == null)
                _afterimageCo = StartCoroutine(AfterimageRoutine());

            // 阶段1：快速淡到 GhostAlpha
            yield return FadeAlphaRoutine(1f, GhostAlpha, FadeToGhostTime);

            // 阶段2：慢慢淡到 0
            yield return FadeAlphaRoutine(GhostAlpha, 0f, GhostFadeOutTime);

            if (_afterimageCo != null)
            {
                StopCoroutine(_afterimageCo);
                _afterimageCo = null;
            }

            // 彻底结束
            if (DisableOnEnd)
                gameObject.SetActive(false);

            _vanishing = false;
        }

        private IEnumerator FadeAlphaRoutine(float fromA, float toA, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
                float a = Mathf.Lerp(fromA, toA, k);
                SetAllMatAlpha(a);
                yield return null;
            }
            SetAllMatAlpha(toA);
        }

        // ===== Material helpers =====
        private void RestoreAllMatColor()
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m == null) continue;

                Color bc = _baseColors[i];
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", bc);
                if (m.HasProperty("_Color")) m.SetColor("_Color", bc);
            }
        }

        private void SetAllMatColor(Color c)
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m == null) continue;

                // 保留原 alpha
                Color bc = _baseColors[i];
                Color nc = new Color(c.r, c.g, c.b, bc.a);

                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", nc);
                if (m.HasProperty("_Color")) m.SetColor("_Color", nc);
            }
        }

        private void SetAllMatAlpha(float a)
        {
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m == null) continue;

                Color bc = _baseColors[i];
                Color nc = new Color(bc.r, bc.g, bc.b, Mathf.Clamp01(a));

                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", nc);
                if (m.HasProperty("_Color")) m.SetColor("_Color", nc);
            }
        }

        // ===== Afterimage =====
        private IEnumerator AfterimageRoutine()
        {
         
            float emitT = 0f;

            while (_moving || _vanishing)
            {
                emitT += Time.deltaTime;
                if (emitT >= AfterimageInterval)
                {
                    emitT = 0f;
                    SpawnOneAfterimage();
                }
                yield return null;
            }
        }

        private void SpawnOneAfterimage()
        {
            if (!SpawnAfterimage) return;

            // 用最简单方式：复制 MeshRenderer 的当前 mesh（白模也能用）
            // 如果是 SkinnedMesh，做 baked mesh
            foreach (var sr in _skinned)
            {
                if (sr == null) continue;
                var baked = new Mesh();
                sr.BakeMesh(baked);

                var go = new GameObject("Afterimage");
                go.transform.position = sr.transform.position;
                go.transform.rotation = sr.transform.rotation;
                go.transform.localScale = sr.transform.lossyScale;

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = baked;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sr.sharedMaterials;

                go.AddComponent<AfterimageFade>().Init(AfterimageLifetime, AfterimageAlpha);
            }

            foreach (var mr0 in _meshes)
            {
                if (mr0 == null) continue;

                var mf0 = mr0.GetComponent<MeshFilter>();
                if (mf0 == null || mf0.sharedMesh == null) continue;

                var go = new GameObject("Afterimage");
                go.transform.position = mr0.transform.position;
                go.transform.rotation = mr0.transform.rotation;
                go.transform.localScale = mr0.transform.lossyScale;

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mf0.sharedMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mr0.sharedMaterials;

                go.AddComponent<AfterimageFade>().Init(AfterimageLifetime, AfterimageAlpha);
            }
        }
    }
}


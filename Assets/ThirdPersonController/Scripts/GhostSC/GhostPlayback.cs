using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    /// <summary>
    /// 挂在 Ghost 上：按录制好的帧回放路径，
    /// 自己做“静默 Scan / Hack”，
    /// 出生：0.3s 抖动淡入现形
    /// 结束：全身溶解 + 抖动 + voxel 爆散 + 路径线淡出。
    /// </summary>
    public class GhostPlayback : MonoBehaviour
    {
        // ====== 轨迹回放相关 ======
        private List<GhostFrame> _frames;
        private float _interval;
        private int _index;
        private float _timer;

        [Header("Ghost Hack Settings")]
        [Tooltip("影子只对这些 Layer 的物体做扫描 / 黑入")]
        public LayerMask HackableMask = ~0;

        [Tooltip("影子扫描半径（只影响它自己）")]
        public float ScanRadius = 12f;

        [Tooltip("影子黑入时的射线距离（现在主要用记录的目标，不再依赖这个）")]
        public float AimDistance = 30f;

        [Tooltip("播放完轨迹后是否自动消失")]
        public bool DestroyOnEnd = true;

        // ★ 影子自己的 hack 目标 & 选项 Id（按 R 那一刻锁定）
        private IHackable _hackTarget;
        private string _hackOptionId;

        // ====== 出生 FX ======
        [Header("Spawn FX")]
        [Tooltip("幽灵生成时淡入+抖动的时长")]
        public float SpawnDuration = 0.3f;

        [Tooltip("生成时的 scale 抖动强度")]
        public float SpawnScaleJitter = 0.04f;

        private bool _spawnDone = false;     // 出生动画是否完成

        // ====== 溶解 + 体素爆散相关 ======
        [Header("Dissolve FX")]
        [Tooltip("溶解持续时间（秒）")]
        public float DissolveDuration = 0.4f;

        [Tooltip("溶解时整体轻微 scale 抖动强度")]
        public float DissolveScaleJitter = 0.05f;

        private SkinnedMeshRenderer[] _skinnedRenderers;
        private MeshRenderer[] _meshRenderers;
        private TrailRenderer[] _trails;
        private Vector3 _origScale;
        private bool _isEnding;

        [Header("Voxel Death FX")]
        [Tooltip("小方块 Prefab，必须挂有 VoxelShard 脚本 + Rigidbody")]
        public GameObject VoxelPrefab;

        [Tooltip("生成的 voxel 数量")]
        public int VoxelCount = 60;

        [Tooltip("在角色局部空间里的随机生成体积（x,y,z 半尺寸）")]
        public Vector3 VoxelSpawnBounds = new Vector3(0.7f, 1.8f, 0.4f);

        [Tooltip("爆散时的爆炸力")]
        public float VoxelExplosionForce = 4f;

        [Tooltip("爆散时的爆炸半径")]
        public float VoxelExplosionRadius = 2f;

        // ====== 路径线 ======
        [Header("Path Line")]
        [Tooltip("用来画整条时间轨迹的 LineRenderer")]
        public LineRenderer PathLine;

        [Tooltip("轨迹线整体抬高一点，避免扎进地板")]
        public float PathHeightOffset = 0.08f;

        private Gradient _pathLineBaseGradient;
        private bool _hasPathGradient;

        private void Awake()
        {
            _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            _meshRenderers = GetComponentsInChildren<MeshRenderer>();
            _trails = GetComponentsInChildren<TrailRenderer>();

            _origScale = transform.localScale;

            // 影子不参与物理
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            StartCoroutine(SpawnAppear());
        }

        public void SetupFrames(List<GhostFrame> frames, float interval, PlayerEchoRecorder.LastHackRecord lastHack)
        {
            _frames = frames;
            _interval = interval;
            _index = 0;
            _timer = 0f;

            if (_frames != null && _frames.Count > 0)
            {
                transform.position = _frames[0].Pos;
                transform.rotation = _frames[0].Rot;
            }

            if (lastHack != null)
            {
                _hackTarget = lastHack.Target;
                _hackOptionId = lastHack.OptionId;
            }
            else
            {
                _hackTarget = null;
                _hackOptionId = null;
            }

            // ====== 画出完整时间轨迹线 ======
            if (PathLine != null && _frames != null && _frames.Count > 1)
            {
                PathLine.positionCount = _frames.Count;
                for (int i = 0; i < _frames.Count; i++)
                {
                    Vector3 p = _frames[i].Pos + Vector3.up * PathHeightOffset;
                    PathLine.SetPosition(i, p);
                }

                if (WorldFXController.Instance != null)
                {
                    WorldFXController.Instance.PlayNoiseKick(0.3f, 0.7f);
                }

                _pathLineBaseGradient = PathLine.colorGradient;
                _hasPathGradient = true;

                // 先把线条调成“全透明版”（淡入起点）
                var baseG = _pathLineBaseGradient;
                var colorKeys = baseG.colorKeys;
                var alphaSrc = baseG.alphaKeys;
                var alphaKeys = new GradientAlphaKey[alphaSrc.Length];
                for (int i = 0; i < alphaSrc.Length; i++)
                {
                    alphaKeys[i] = new GradientAlphaKey(0f, alphaSrc[i].time);
                }
                var g0 = new Gradient();
                g0.SetKeys(colorKeys, alphaKeys);
                PathLine.colorGradient = g0;
            }
        }

        private void Update()
        {
            if (!_spawnDone) return;
            if (_isEnding) return;
            if (_frames == null || _frames.Count == 0) return;

            _timer += Time.deltaTime;
            if (_timer < _interval) return;
            _timer = 0f;

            if (_index < 0 || _index >= _frames.Count)
            {
                if (DestroyOnEnd && !_isEnding)
                {
                    BeginEndSequence();
                }
                return;
            }

            GhostFrame f = _frames[_index];

            // 影子走到这一帧的位置 / 朝向
            transform.position = f.Pos;
            transform.rotation = f.Rot;

            // 复刻 V：静默扫描
            if (f.PressV)
            {
                GhostScanOnce();
            }

            // ★ 核心修复：在这个绝对确定的时刻，执行黑入！
            if (f.ExecuteHack)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[Ghost] Frame {_index} ExecuteHack -> AutoHack");

                GhostAutoHack();
            }

            _index++;

            // 到最后一帧，启动结束动画
            if (_index >= _frames.Count && DestroyOnEnd)
            {
                if (!_isEnding)
                {
                    BeginEndSequence();
                }
            }
        }

        private IEnumerator SpawnAppear()
        {
            float t = 0f;
            var mats = new List<Material>();
            var baseColors = new List<Color>();

            foreach (var r in _skinnedRenderers)
            {
                if (r == null) continue;
                mats.AddRange(r.materials);
            }

            foreach (var r in _meshRenderers)
            {
                if (r == null) continue;
                mats.AddRange(r.materials);
            }

            foreach (var m in mats)
            {
                if (m == null)
                {
                    baseColors.Add(Color.white);
                    continue;
                }

                Color c =
                    m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") :
                    m.HasProperty("_Color") ? m.GetColor("_Color") :
                    Color.white;

                baseColors.Add(c);
            }

            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                Color bc = baseColors[i];
                Color start = new Color(bc.r, bc.g, bc.b, 0f);

                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", start);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", start);
            }

            transform.localScale = _origScale * 0.95f;

            while (t < SpawnDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, SpawnDuration));

                for (int i = 0; i < mats.Count; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    Color bc = baseColors[i];
                    float a = Mathf.Lerp(0f, bc.a, k);
                    Color nc = new Color(bc.r, bc.g, bc.b, a);

                    if (m.HasProperty("_BaseColor"))
                        m.SetColor("_BaseColor", nc);
                    if (m.HasProperty("_Color"))
                        m.SetColor("_Color", nc);
                }

                if (_hasPathGradient && PathLine != null)
                {
                    var baseG = _pathLineBaseGradient;
                    var colorKeys = baseG.colorKeys;
                    var alphaSrc = baseG.alphaKeys;
                    var alphaKeys = new GradientAlphaKey[alphaSrc.Length];

                    for (int i = 0; i < alphaSrc.Length; i++)
                    {
                        float a = Mathf.Lerp(0f, alphaSrc[i].alpha, k);
                        alphaKeys[i] = new GradientAlphaKey(a, alphaSrc[i].time);
                    }

                    var g = new Gradient();
                    g.SetKeys(colorKeys, alphaKeys);
                    PathLine.colorGradient = g;
                }

                float jitter = Mathf.Sin(Time.time * 40f) * SpawnScaleJitter * (1f - k);
                float s = Mathf.Lerp(0.95f, 1f, k) + jitter;
                transform.localScale = _origScale * s;

                yield return null;
            }

            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                Color bc = baseColors[i];
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", bc);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", bc);
            }

            if (_hasPathGradient && PathLine != null)
            {
                PathLine.colorGradient = _pathLineBaseGradient;
            }

            transform.localScale = _origScale;
            _spawnDone = true;
        }

        private void BeginEndSequence()
        {
            _isEnding = true;

            if (WorldFXController.Instance != null)
                WorldFXController.Instance.PlayGlitchKick(0.3f, 1.3f, 0.4f);

            StartCoroutine(DissolveAndDie());
        }

        private IEnumerator DissolveAndDie()
        {
            float t = 0f;

            foreach (var tr in _trails)
            {
                if (tr != null) tr.emitting = false;
            }

            var mats = new List<Material>();

            foreach (var r in _skinnedRenderers)
            {
                if (r == null) continue;
                mats.AddRange(r.materials);
            }

            foreach (var r in _meshRenderers)
            {
                if (r == null) continue;
                mats.AddRange(r.materials);
            }

            var baseColors = new List<Color>();
            foreach (var m in mats)
            {
                if (m == null)
                {
                    baseColors.Add(Color.white);
                    continue;
                }

                Color c =
                    m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") :
                    m.HasProperty("_Color") ? m.GetColor("_Color") :
                    Color.white;

                baseColors.Add(c);
            }

            while (t < DissolveDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, DissolveDuration));
                float fadeOut = 1f - k;

                for (int i = 0; i < mats.Count; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    Color bc = baseColors[i];
                    float a = Mathf.Lerp(1f, 0f, k);
                    Color nc = new Color(bc.r, bc.g, bc.b, a);

                    if (m.HasProperty("_BaseColor"))
                        m.SetColor("_BaseColor", nc);
                    if (m.HasProperty("_Color"))
                        m.SetColor("_Color", nc);
                }

                if (_hasPathGradient && PathLine != null)
                {
                    var baseG = _pathLineBaseGradient;
                    var colorKeys = baseG.colorKeys;
                    var alphaSrc = baseG.alphaKeys;
                    var alphaKeys = new GradientAlphaKey[alphaSrc.Length];

                    for (int i = 0; i < alphaSrc.Length; i++)
                    {
                        float a = alphaSrc[i].alpha * fadeOut;
                        alphaKeys[i] = new GradientAlphaKey(a, alphaSrc[i].time);
                    }

                    var g = new Gradient();
                    g.SetKeys(colorKeys, alphaKeys);
                    PathLine.colorGradient = g;
                }

                float jitter = Mathf.Sin(Time.time * 40f) * DissolveScaleJitter * (1f - k);
                float s = 1f + jitter;
                transform.localScale = _origScale * s;

                yield return null;
            }

            foreach (var r in _skinnedRenderers)
            {
                if (r != null) r.enabled = false;
            }
            foreach (var r in _meshRenderers)
            {
                if (r != null) r.enabled = false;
            }

            if (PathLine != null)
                PathLine.enabled = false;

            foreach (var tr in _trails)
            {
                if (tr != null) tr.emitting = false;
            }

            SpawnVoxelPieces();

            Destroy(gameObject);
        }

        private void SpawnVoxelPieces()
        {
            if (VoxelPrefab == null || VoxelCount <= 0)
                return;

            for (int i = 0; i < VoxelCount; i++)
            {
                Vector3 localOffset = new Vector3(
                    Random.Range(-VoxelSpawnBounds.x, VoxelSpawnBounds.x),
                    Random.Range(0, VoxelSpawnBounds.y),
                    Random.Range(-VoxelSpawnBounds.z, VoxelSpawnBounds.z)
                );

                Vector3 spawnPos = transform.TransformPoint(localOffset);
                Quaternion rot = Random.rotation;

                GameObject piece = Object.Instantiate(VoxelPrefab, spawnPos, rot);

                Rigidbody rb = piece.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(
                        VoxelExplosionForce,
                        transform.position,
                        VoxelExplosionRadius
                    );
                }
            }
        }

        private void GhostScanOnce()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                ScanRadius,
                HackableMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (var c in hits)
            {
                var h = c.GetComponentInParent<IHackable>();
                if (h == null) continue;

                h.OnScannedOnce();
            }
        }

        private void GhostAutoHack()
        {
            if (_hackTarget == null || string.IsNullOrEmpty(_hackOptionId))
            {
                if (Debug.isDebugBuild)
                    Debug.Log("[Ghost] No recorded hack to replay");
                return;
            }

            IHackable target = _hackTarget;
            string optId = _hackOptionId;

            IQuickHackable qh = target as IQuickHackable;
            if (qh == null && target.WorldTransform != null)
            {
                qh = target.WorldTransform.GetComponentInParent<IQuickHackable>();
            }

            if (qh == null)
            {
                if (Debug.isDebugBuild)
                    Debug.Log("[Ghost] Target is not IQuickHackable anymore");
                return;
            }

            qh.GetQuickHacks(out var up, out var right, out var down, out var left);

            QuickHackOption chosen = null;
            QuickHackOption[] all = { up, right, down, left };
            foreach (var o in all)
            {
                if (o != null && o.Id == optId)
                {
                    chosen = o;
                    break;
                }
            }

            if (chosen == null || chosen.Execute == null)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[Ghost] No QuickHackOption with Id={optId} on target");
                return;
            }

            if (Debug.isDebugBuild)
                Debug.Log($"[Ghost] Replaying hack on {target.DisplayName} with option {chosen.Name} ({optId})");

            chosen.Execute.Invoke();
        }
    }
}
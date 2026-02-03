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
        public float SpawnDuration = 0.3f;   // 你说 0.3s 闪现

        [Tooltip("生成时的 scale 抖动强度")]
        public float SpawnScaleJitter = 0.04f;

        private bool _spawnDone = false;     // 出生动画是否完成

        // ====== 溶解 + 体素爆散相关 ======
        [Header("Dissolve FX")]
        [Tooltip("溶解持续时间（秒）")]
        public float DissolveDuration = 0.4f;

        [Tooltip("溶解时整体轻微 scale 抖动强度")]
        public float DissolveScaleJitter = 0.05f;

        // 上半身：SkinnedMeshRenderer
        private SkinnedMeshRenderer[] _skinnedRenderers;
        // 下半身 / 其它：MeshRenderer
        private MeshRenderer[] _meshRenderers;
        private TrailRenderer[] _trails;
        private Vector3 _origScale;
        private bool _isEnding;   // 已经进入结束动画，不再更新轨迹

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

        // 记录 PathLine 原始渐变，方便淡入淡出
        private Gradient _pathLineBaseGradient;
        private bool _hasPathGradient;


        private void Awake()
        {
            // 找到所有 skinned mesh（身体、衣服）
            _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            // 找到所有普通 MeshRenderer（靴子、配件、胶囊等）
            _meshRenderers = GetComponentsInChildren<MeshRenderer>();
            // 找到所有尾巴
            _trails = GetComponentsInChildren<TrailRenderer>();

            _origScale = transform.localScale;

            // 影子不参与物理
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            // 一出生就先做“现形”特效（路径不动，只在第 0 帧位置）
            StartCoroutine(SpawnAppear());
        }

        /// <summary>
        /// 外部由 PlayerEchoRecorder 调用：
        /// 传入轨迹帧 + 采样间隔 + 按 R 那一刻的 LastHack 记录
        /// </summary>
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
                // ★ 影子出现时：屏幕噪点冲击一下（0.3s）
                if (WorldFXController.Instance != null)
                {
                    WorldFXController.Instance.PlayNoiseKick(0.3f, 0.7f);
                }

                // 记录原始渐变
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
            // 出生动画没结束，不推进轨迹（只原地闪现）
            if (!_spawnDone) return;

            // 正在做结束溶解动画，就不再推进轨迹
            if (_isEnding) return;

            if (_frames == null || _frames.Count == 0) return;

            _timer += Time.deltaTime;
            if (_timer < _interval) return;
            _timer = 0f;

            // 确保索引安全
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
                if (Debug.isDebugBuild)
                    Debug.Log($"[Ghost] Frame {_index} PressV -> Scan");
                GhostScanOnce();
            }

            // 复刻 E：在这个时刻，对记录好的目标/选项执行一次 hack
            if (f.PressE)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[Ghost] Frame {_index} PressE -> AutoHack");
                GhostAutoHack();
            }

            _index++;


            // 到最后一帧，启动结束动画（溶解 + voxel）
            if (_index >= _frames.Count && DestroyOnEnd)
            {
                if (!_isEnding)
                {
                    BeginEndSequence();
                }
            }
        }

        // =========================================================
        // 出生：由透明 → 实体，带一点 scale 抖动 + PathLine 淡入
        // =========================================================
        private IEnumerator SpawnAppear()
        {
            float t = 0f;

            // 收集所有材质 + 原始颜色
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

            // 初始：完全透明、略微缩小
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

                // 1）角色透明度 0 → 1
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

                // 2）PathLine 同步淡入
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

                // 3）轻微 scale 抖动：靠近结束越平稳
                float jitter = Mathf.Sin(Time.time * 40f) * SpawnScaleJitter * (1f - k);
                float s = Mathf.Lerp(0.95f, 1f, k) + jitter;
                transform.localScale = _origScale * s;

                yield return null;
            }

            // 最终对齐：避免数值累积问题
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

            // PathLine 恢复为原始渐变（完全不透明）
            if (_hasPathGradient && PathLine != null)
            {
                PathLine.colorGradient = _pathLineBaseGradient;
            }

            transform.localScale = _origScale;
            _spawnDone = true; // ★ 现在才允许 Update 推进轨迹
        }

        /// <summary>
        /// 开始“结尾”：先停止轨迹更新，再溶解 & voxel 爆散
        /// </summary>
        private void BeginEndSequence()
        {
            _isEnding = true;


            // ★ Ghost 消失：再来一次 glitch
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.PlayGlitchKick(0.3f, 1.3f, 0.4f);

            StartCoroutine(DissolveAndDie());
        }


        /// <summary>
        /// 溶解 + scale 抖动 + PathLine 淡出 + 等尾巴消失 + spawn voxel + 销毁
        /// </summary>
        private IEnumerator DissolveAndDie()
        {
            float t = 0f;

            // 关掉 trail 的 emitting，让它自然收尾
            foreach (var tr in _trails)
            {
                if (tr != null) tr.emitting = false;
            }

            // 收集所有 Renderer 的材质和原始颜色（上半身 + 下半身）
            var mats = new List<Material>();

            foreach (var r in _skinnedRenderers)
            {
                if (r == null) continue;
                mats.AddRange(r.materials);  // materials 会实例化
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

            // 溶解过程
            while (t < DissolveDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, DissolveDuration));
                float fadeOut = 1f - k;

                // 1) Alpha 从 1 -> 0（角色）
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

                // 2) PathLine 同步淡出
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

                // 3) 轻微 scale 抖动：像“被一点点抽走”
                float jitter = Mathf.Sin(Time.time * 40f) * DissolveScaleJitter * (1f - k);
                float s = 1f + jitter;
                transform.localScale = _origScale * s;

                yield return null;
            }

            // ===== 溶解结束这一帧：立刻把本体 & 轨迹线关掉 =====

            // 关掉所有 renderer（上半身 Skinned + 下半身 Mesh）
            foreach (var r in _skinnedRenderers)
            {
                if (r != null) r.enabled = false;
            }
            foreach (var r in _meshRenderers)
            {
                if (r != null) r.enabled = false;
            }

            // 关掉路径线
            if (PathLine != null)
                PathLine.enabled = false;

            // 关掉尾巴发射（尾巴已有的残影会消失，因为 ghost 等会被 Destroy）
            foreach (var tr in _trails)
            {
                if (tr != null) tr.emitting = false;
            }

            // 角色完全不可见后，生成 voxel 体素碎片
            SpawnVoxelPieces();

            // 直接销毁 ghost 本体（方块是独立 GameObject，会自己按 VoxelShard 的脚本慢慢消失）
            Destroy(gameObject);
        }

        /// <summary>
        /// 生成一堆小方块，在角色附近随机位置爆散
        /// </summary>
        private void SpawnVoxelPieces()
        {
            if (VoxelPrefab == null || VoxelCount <= 0)
                return;

            for (int i = 0; i < VoxelCount; i++)
            {
                // 在角色局部空间的一个盒子里随机生成
                Vector3 localOffset = new Vector3(
                    Random.Range(-VoxelSpawnBounds.x, VoxelSpawnBounds.x),
                    Random.Range(0, VoxelSpawnBounds.y),
                    Random.Range(-VoxelSpawnBounds.z, VoxelSpawnBounds.z)
                );

                Vector3 spawnPos = transform.TransformPoint(localOffset);
                Quaternion rot = Random.rotation;

                GameObject piece = Object.Instantiate(VoxelPrefab, spawnPos, rot);

                // 给点爆炸力（从 Ghost 的中心往外）
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

        /// <summary>
        /// 影子自己的 Scan：只调 OnScannedOnce，不出 UI、不改时间
        /// </summary>
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

                h.OnScannedOnce();   // 每个 h 自己用 _scanTriggered 防重复
            }
        }

        /// <summary>
        /// 影子自己的 Hack：严格复刻按 R 那一刻记录的目标 + 选项
        /// </summary>
        private void GhostAutoHack()
        {
            // 没有任何记录：只是不做 hack
            if (_hackTarget == null || string.IsNullOrEmpty(_hackOptionId))
            {
                if (Debug.isDebugBuild)
                    Debug.Log("[Ghost] No recorded hack to replay");
                return;
            }

            IHackable target = _hackTarget;
            string optId = _hackOptionId;

            // 尝试找 IQuickHackable
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

            // 拿四个选项并按 Id 匹配
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


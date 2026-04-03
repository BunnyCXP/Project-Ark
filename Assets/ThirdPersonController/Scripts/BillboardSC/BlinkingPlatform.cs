using System.Collections;
using UnityEngine;

namespace TheGlitch
{
    // 自动确保物体上有 Renderer 和 Collider，防止脚本报错
    [RequireComponent(typeof(Collider))]
    public class BlinkingPlatformGroup : MonoBehaviour
    {
        [Header("⏱️ 循环周期设置")]
        [Tooltip("【👑 核心设置】一个平台从出现到消失的总时长。")]
        public float ActiveCycleTime = 3.0f;
        [Tooltip("平台消失后，整个循环要等待多久才重启？(多平台交替时设为0)")]
        public float GlobalCooldown = 0.0f;

        [Header("⚠️ 闪烁警告设置")]
        [Tooltip("在消失前的最后几秒开始闪烁？")]
        [Range(0.5f, 3.0f)] public float WarningDuration = 1.0f;
        [Tooltip("闪烁的高频速度。数值越小，闪得越快，压迫感越强。")]
        [Range(0.05f, 0.3f)] public float FlickerRate = 0.1f;

        [Header("🔀 多平台链式同步 (3个平台交替填这里)")]
        [Tooltip("【👑 核心设置】如果你有3个平台，平台A设为0，平台B设为1，平台C设为2。")]
        public int SyncIndex = 0;

        private Collider _collider;
        private Renderer[] _renderers;
        private bool _isInitialized = false;

        private void Start()
        {
            Initialize();
            // 所有平台都在同一帧启动协程，它们会通过 SyncIndex 自动错开时间轴
            StartCoroutine(SynchronizedBlinkRoutine());
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _collider = GetComponent<Collider>();
            // 获取包括子物体在内的所有渲染器，确保整个模型一起闪
            _renderers = GetComponentsInChildren<Renderer>();

            // 安全检查：防止警告时间比总Active时间还长
            if (WarningDuration >= ActiveCycleTime)
            {
                WarningDuration = ActiveCycleTime * 0.5f;
            }
            _isInitialized = true;
        }

        private IEnumerator SynchronizedBlinkRoutine()
        {
            // ------------------------------------------------------------
            // 【👑 链式同步核心】：通过 Index 错开启动时间。
            // 假设 ActiveCycleTime 是 3s。
            // 平台0：等待 0s。立刻开工。
            // 平台1：等待 3s。当平台0准备消失时，平台1刚好出现。
            // 平台2：等待 6s。完美的 ABC 循环链。
            // ------------------------------------------------------------
            float initialDelay = SyncIndex * ActiveCycleTime;

            // 开局先全部物理隐藏，等待自己的回合
            SetPlatformVisuals(false);
            SetPlatformPhysics(false);

            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            // 进入无限循环
            while (true)
            {
                // 1. 状态：完全实体出现 (Visible & Solid)
                SetPlatformVisuals(true);
                SetPlatformPhysics(true);

                // 等待到该闪烁的时间点 (比如 Active 3s，警告 1s，那就安全站立 2s)
                yield return new WaitForSeconds(ActiveCycleTime - WarningDuration);

                // 2. 状态：闪烁警告 (Solid & Flickering)
                // 玩家此时还能踩在上面，但视觉在疯狂提示快跑！
                float flickerTimer = 0f;
                bool flickerState = true;

                while (flickerTimer < WarningDuration)
                {
                    flickerState = !flickerState; // 切换可见性
                    SetPlatformVisuals(flickerState);

                    yield return new WaitForSeconds(FlickerRate);
                    flickerTimer += FlickerRate;
                }

                // 3. 状态：完全消失 (Hidden & Empty)
                SetPlatformVisuals(false);
                SetPlatformPhysics(false);

                // 4. 【👑 链式同步计算】：等待其他平台轮流写
                // 如果是AB交替，等待 1 个周期的 Active 时间。
                // 如果是ABC交替，等待 2 个周期的 Active 时间。
                float chainWaitTime = SyncIndex * ActiveCycleTime + GlobalCooldown;

                // 这里我们简化逻辑：所有平台消失后，等待 "ActiveCycleTime * (平台总数 - 1)" 
                // 但因为脚本是独立的，我们用简化的数学方法控制：
                // 消失时间 = 玩家踩的时间 * 1 + Cooldown
                yield return new WaitForSeconds(ActiveCycleTime + GlobalCooldown);
            }
        }

        // 分开控制视觉和物理，防止闪烁时玩家掉下去
        private void SetPlatformVisuals(bool visible)
        {
            foreach (var r in _renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        private void SetPlatformPhysics(bool solid)
        {
            if (_collider != null) _collider.enabled = solid;
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    /// <summary>
    /// 挂在玩家身上：持续录制最近 N 秒，然后按 R 生成影子回放这些操作
    /// </summary>
    public class PlayerEchoRecorder : MonoBehaviour
    {
        public static PlayerEchoRecorder Instance { get; private set; }

        [Header("Record Settings")]
        [Tooltip("录制多长时间的历史（秒）")]
        public float RecordDuration = 10f;

        [Tooltip("多久采样一帧（秒），越小越精细")]
        public float RecordInterval = 0.05f;

        [Header("Ghost")]
        [Tooltip("用来播放影子的预制体")]
        public GameObject GhostPrefab;

        [Header("Echo Cooldown")]
        [Tooltip("回溯冷却时间（秒），建议跟 RecordDuration 一样")]
        public float EchoCooldown = 10f;

        // 剩余冷却时间（<=0 表示已经冷却好）
        public float CooldownRemain { get; private set; }

        // 给 UI 用：是否可以再次放影子
        public bool EchoReady => CooldownRemain <= 0f;

        [Serializable]
        public class LastHackRecord
        {
            public IHackable Target;
            public string OptionId;
            public float Time;   // 这个 hack 发生的世界时间
        }

        public LastHackRecord LastHack { get; private set; }

        private readonly List<GhostFrame> _frames = new();
        private float _recordTimer;

        private GameObject _currentGhost;

        // ★ 按下 R 之后锁定 LastHack，不再更新
        private bool _lockHackRecord = false;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // 1. 冷却计时
            if (CooldownRemain > 0f)
                CooldownRemain -= Time.deltaTime;

            // 2. 持续录制轨迹 + 按键
            RecordFrames();

            // 3. 按 R 生成影子（只有冷却好才响应）
            if (Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame &&
                EchoReady)
            {
                // 锁定当前这一次的 hack 记录
                _lockHackRecord = true;

                SpawnGhostFromHistory();
            }

            // 冷却走完后，允许下一轮重新记录 hack
            if (EchoReady && _lockHackRecord)
            {
                // 下一次 hack 又可以更新 LastHack
                _lockHackRecord = false;
                // 不清掉 LastHack，这样你可以看到上次 ghost 复刻的是什么
            }
        }

        /// <summary>
        /// 在执行 QuickHack 的时候调用，记录「最后一次」黑入
        /// </summary>
        public void RecordLastHack(IHackable target, QuickHackOption opt)
        {
            if (target == null || opt == null) return;
            if (string.IsNullOrEmpty(opt.Id)) return;

            // ★ 已经锁定的这一轮，不再覆盖（按下 R 之后的 hack 不影响当前 ghost）
            if (_lockHackRecord) return;

            LastHack = new LastHackRecord
            {
                Target = target,
                OptionId = opt.Id,
                Time = Time.time
            };

            //Debug.Log($"[Recorder] LastHack = {target.DisplayName} / {opt.Id}");
        }

        /// <summary>
        /// 每隔 RecordInterval 录一帧：位置 + 朝向 + 是否在这一帧按键
        /// </summary>
        private void RecordFrames()
        {
            _recordTimer += Time.deltaTime;
            if (_recordTimer < RecordInterval) return;
            _recordTimer = 0f;

            if (Keyboard.current == null) return;

            // 用 isPressed 增大录到的概率
            bool vPressed = Keyboard.current.vKey.isPressed;
            bool ePressed = Keyboard.current.eKey.isPressed;
            bool qPressed = Keyboard.current.qKey.isPressed;
            bool qReleased = Keyboard.current.qKey.wasReleasedThisFrame;

            GhostFrame f = new GhostFrame
            {
                Pos = transform.position,
                Rot = transform.rotation,

                PressV = vPressed,
                PressE = ePressed,
                PressQ = qPressed,
                ReleaseQ = qReleased
            };

            _frames.Add(f);

            int maxFrames = Mathf.CeilToInt(RecordDuration / RecordInterval);
            if (_frames.Count > maxFrames)
            {
                _frames.RemoveAt(0);
            }
        }

        /// <summary>
        /// 用当前这段历史生成一个影子，让它回放
        /// </summary>
        private void SpawnGhostFromHistory()
        {
            // 冷却中就不放影子
            if (!EchoReady) return;

            if (GhostPrefab == null) return;
            if (_frames.Count < 2) return;

            // 有旧 ghost 就先干掉
            if (_currentGhost != null)
                Destroy(_currentGhost);

            _currentGhost = Instantiate(GhostPrefab);

            // 起点位置
            _currentGhost.transform.position = _frames[0].Pos;
            _currentGhost.transform.rotation = _frames[0].Rot;

            var playback = _currentGhost.GetComponent<GhostPlayback>();
            if (playback != null)
            {
                // ★ 在按 R 那一刻，把当前 LastHack 拷贝一份快照
                LastHackRecord snapshot = null;
                if (LastHack != null)
                {
                    snapshot = new LastHackRecord
                    {
                        Target = LastHack.Target,
                        OptionId = LastHack.OptionId
                    };
                }

                // 把帧 + 间隔 + 那一刻的 hack 快照 一起塞给影子
                playback.SetupFrames(new List<GhostFrame>(_frames), RecordInterval, snapshot);
            }


            // ★ Ghost 出生：强一点的 glitch 撕裂
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.PlayGlitchKick(0.3f, 1.3f, 0.4f);

            // 进入冷却
            CooldownRemain = EchoCooldown;
        }

    }
}


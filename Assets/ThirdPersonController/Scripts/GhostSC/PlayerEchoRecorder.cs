using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class PlayerEchoRecorder : MonoBehaviour
    {
        public static PlayerEchoRecorder Instance { get; private set; }

        public enum RecorderState
        {
            Ready,
            Recording,
            Cooldown
        }

        [Header("状态 (供查看)")]
        public RecorderState State { get; private set; } = RecorderState.Ready;

        [Header("Record Settings")]
        [Tooltip("最多录制多长时间（秒）")]
        public float RecordDuration = 10f;
        [Tooltip("多久采样一帧（秒），越小越精细")]
        public float RecordInterval = 0.05f;

        [Header("Ghost")]
        public GameObject GhostPrefab;

        [Header("Echo Cooldown")]
        [Tooltip("影子消失后多久可以再次录制")]
        public float EchoCooldown = 8f;

        public float CooldownRemain { get; private set; }
        public float CurrentRecordTime { get; private set; }

        public bool EchoReady => State == RecorderState.Ready;

        [Serializable]
        public class LastHackRecord
        {
            public IHackable Target;
            public string OptionId;
            public float Time;
        }

        public LastHackRecord LastHack { get; private set; }

        private readonly List<GhostFrame> _frames = new();
        private float _recordTimer;
        private GameObject _currentGhost;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            switch (State)
            {
                case RecorderState.Ready:
                    // 按 R 开始录制
                    if (Keyboard.current.rKey.wasPressedThisFrame)
                    {
                        StartRecording();
                    }
                    break;

                case RecorderState.Recording:
                    CurrentRecordTime += Time.deltaTime;
                    RecordFrames();

                    // 时间结束，或者玩家再次按 R 提前打断录制！
                    if (CurrentRecordTime >= RecordDuration || Keyboard.current.rKey.wasPressedThisFrame)
                    {
                        StopRecordingAndSpawnGhost();
                    }
                    break;

                case RecorderState.Cooldown:
                    CooldownRemain -= Time.deltaTime;
                    if (CooldownRemain <= 0f)
                    {
                        State = RecorderState.Ready;
                    }
                    break;
            }
        }

        private void StartRecording()
        {
            State = RecorderState.Recording;
            CurrentRecordTime = 0f;
            _recordTimer = 0f;
            _frames.Clear();
            LastHack = null;

            // 录下第一帧
            RecordSingleFrame();

            // 屏幕给个特效，提示玩家“摄像机开启了”
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.PlayNoiseKick(0.2f, 0.5f);
        }

        private void RecordFrames()
        {
            _recordTimer += Time.deltaTime;
            if (_recordTimer >= RecordInterval)
            {
                _recordTimer = 0f;
                RecordSingleFrame();
            }
        }

        private void RecordSingleFrame()
        {
            if (Keyboard.current == null) return;

            GhostFrame f = new GhostFrame
            {
                Pos = transform.position,
                Rot = transform.rotation,
                PressV = Keyboard.current.vKey.isPressed,
                PressE = Keyboard.current.eKey.isPressed,
                PressQ = Keyboard.current.qKey.isPressed,
                ReleaseQ = Keyboard.current.qKey.wasReleasedThisFrame,
                ExecuteHack = false // 默认没执行
            };
            _frames.Add(f);
        }

        private void StopRecordingAndSpawnGhost()
        {
            State = RecorderState.Cooldown;
            CooldownRemain = EchoCooldown;

            SpawnGhostFromHistory();
        }

        // 仅在录制期间才允许记录黑客操作
        public void RecordLastHack(IHackable target, QuickHackOption opt)
        {
            if (State != RecorderState.Recording) return;

            if (target == null || opt == null) return;
            if (string.IsNullOrEmpty(opt.Id)) return;

            LastHack = new LastHackRecord
            {
                Target = target,
                OptionId = opt.Id,
                Time = Time.time
            };

            // ★ 核心修复：精准打标！
            // 找到录像带的最后一帧，给它盖上“在这里执行黑客操作”的章
            if (_frames.Count > 0)
            {
                GhostFrame f = _frames[_frames.Count - 1];
                f.ExecuteHack = true;
                _frames[_frames.Count - 1] = f; // 结构体需要重新赋值
            }
        }

        private void SpawnGhostFromHistory()
        {
            if (GhostPrefab == null || _frames.Count < 2) return;

            if (_currentGhost != null)
                Destroy(_currentGhost);

            _currentGhost = Instantiate(GhostPrefab);
            // 影子诞生在录制的起点
            _currentGhost.transform.position = _frames[0].Pos;
            _currentGhost.transform.rotation = _frames[0].Rot;

            var playback = _currentGhost.GetComponent<GhostPlayback>();
            if (playback != null)
            {
                LastHackRecord snapshot = null;
                if (LastHack != null)
                {
                    snapshot = new LastHackRecord { Target = LastHack.Target, OptionId = LastHack.OptionId };
                }
                // 派影子去干活
                playback.SetupFrames(new List<GhostFrame>(_frames), RecordInterval, snapshot);
            }

            if (WorldFXController.Instance != null)
                WorldFXController.Instance.PlayGlitchKick(0.3f, 1.3f, 0.4f);
        }
    }
}
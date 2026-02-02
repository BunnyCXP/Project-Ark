using System.Collections;
using UnityEngine;
using Cinemachine;

namespace TheGlitch
{
    public class CameraTutorialSequenceZone : MonoBehaviour
    {
        [Header("Cameras")]
        public CinemachineVirtualCamera DefaultVCam;   // 跟随玩家的
        public CinemachineVirtualCamera NodeVCam;      // 看 TrainingNode
        public CinemachineVirtualCamera WallVCam;      // 看 教学墙

        [Header("Priority")]
        public int ActivePriority = 30;
        public int InactivePriority = 0;
        public int DefaultPriority = 10;

        [Header("Timing")]
        [Tooltip("进区先看 Node 多久（秒）")]
        public float LookNodeTime = 0.8f;

        [Tooltip("从 Node 切到 Wall 时可额外等待一点点（可为 0）")]
        public float GapTime = 0.0f;

        [Header("One Shot")]
        [Tooltip("是否只触发一次（教学常用）")]
        public bool TriggerOnce = false;

        private bool _inside;
        private bool _played;
        private Coroutine _co;

        private void Awake()
        {
            // 初始：确保默认镜头在用
            SetVCamPriority(DefaultVCam, DefaultPriority);
            SetVCamPriority(NodeVCam, InactivePriority);
            SetVCamPriority(WallVCam, InactivePriority);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (TriggerOnce && _played) return;

            _inside = true;

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Sequence());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _inside = false;

            if (_co != null) StopCoroutine(_co);
            _co = null;

            // 出区：回到玩家视角
            SetVCamPriority(NodeVCam, InactivePriority);
            SetVCamPriority(WallVCam, InactivePriority);

            // 让 DefaultVCam 稳稳赢
            SetVCamPriority(DefaultVCam, ActivePriority);

            if (TriggerOnce) _played = true;
        }

        private IEnumerator Sequence()
        {
            // 进入区：第一段看 Node
            SetVCamPriority(WallVCam, InactivePriority);
            SetVCamPriority(NodeVCam, ActivePriority);
            SetVCamPriority(DefaultVCam, DefaultPriority);

            float t = 0f;
            while (t < LookNodeTime)
            {
                // 玩家提前出区就停止
                if (!_inside) yield break;
                t += Time.deltaTime;
                yield return null;
            }

            if (GapTime > 0f)
            {
                t = 0f;
                while (t < GapTime)
                {
                    if (!_inside) yield break;
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            // 第二段看教学墙：保持到出区
            SetVCamPriority(NodeVCam, InactivePriority);
            SetVCamPriority(WallVCam, ActivePriority);
            SetVCamPriority(DefaultVCam, DefaultPriority);

            _co = null;
        }

        private void SetVCamPriority(CinemachineVirtualCamera vcam, int p)
        {
            if (vcam != null) vcam.Priority = p;
        }
    }
}

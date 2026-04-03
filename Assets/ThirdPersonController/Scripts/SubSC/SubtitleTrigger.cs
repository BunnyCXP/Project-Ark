using UnityEngine;

namespace TheGlitch
{
    public class SubtitleTrigger : MonoBehaviour
    {
        [Header("字幕设置")]
        [TextArea(2, 5)] // 让面板上的输入框变大，方便写长句子
        public string SubtitleText = "这里输入你想说的台词...";

        [Tooltip("这句话在屏幕上停留几秒？")]
        public float DisplayDuration = 3.0f;

        [Header("触发控制")]
        [Tooltip("勾选后，玩家反复进出也不会重复触发这句话")]
        public bool TriggerOnlyOnce = true;

        private bool _hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            // 防御机制 1：如果只能触发一次且已经触发过，直接拦截
            if (_hasTriggered && TriggerOnlyOnce) return;

            // 防御机制 2：必须是玩家碰到才算数
            if (other.CompareTag("Player"))
            {
                _hasTriggered = true;

                // 👑 呼叫大管家：不需要在面板里拖拽UI，直接一行代码送达！
                if (SubtitleManager.Instance != null)
                {
                    SubtitleManager.Instance.ShowSubtitle(SubtitleText, DisplayDuration);
                }
                else
                {
                    Debug.LogWarning("未找到 SubtitleManager！请确保 Canvas 里有字幕管家。");
                }
            }
        }
    }
}
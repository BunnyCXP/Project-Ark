using UnityEngine;

namespace TheGlitch
{
    public class EscapeEventTrigger : MonoBehaviour
    {
        [Header("触发设置")]
        [Tooltip("是否只触发一次？（逃生陷阱通常都是一次性的）")]
        public bool TriggerOnce = true;
        private bool _hasTriggered = false;

        [Header("物理灾难 (落石/塌陷)")]
        [Tooltip("把需要掉下来的石头或地板拖进来，它们必须带有 Rigidbody 组件")]
        public Rigidbody[] ObjectsToDrop;

        [Header("场景切换 (瞬间开关)")]
        [Tooltip("触发时要显示的东西 (比如：灰尘粒子特效、阻挡退路的隐形空气墙)")]
        public GameObject[] ObjectsToEnable;

        [Tooltip("触发时要隐藏的东西 (比如：原本完好无损的地板)")]
        public GameObject[] ObjectsToDisable;

        [Header("特效与声音 (可选)")]
        public AudioSource EventSound; // 比如石头砸下的轰隆声

        private void OnTriggerEnter(Collider other)
        {
            // 确保只有玩家能触发，且没被触发过
            if (!_hasTriggered && other.CompareTag("Player"))
            {
                TriggerDisaster();

                if (TriggerOnce) _hasTriggered = true;
            }
        }

        private void TriggerDisaster()
        {
            // 1. 让石头或地板受重力掉落
            foreach (var rb in ObjectsToDrop)
            {
                if (rb != null)
                {
                    rb.isKinematic = false; // 取消悬空锁定
                    rb.useGravity = true;   // 开启重力
                    // 给一点随机向下的初始力量，让掉落更猛烈
                    rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
                }
            }

            // 2. 开启新东西 (灰尘、空气墙)
            foreach (var obj in ObjectsToEnable)
            {
                if (obj != null) obj.SetActive(true);
            }

            // 3. 关掉旧东西 (原本的完好地板)
            foreach (var obj in ObjectsToDisable)
            {
                if (obj != null) obj.SetActive(false);
            }

            // 4. 播放音效
            if (EventSound != null) EventSound.Play();
        }
    }
}
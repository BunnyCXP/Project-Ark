using UnityEngine;

namespace TheGlitch
{
    public class CorridorTextTrigger : MonoBehaviour
    {
        public TypewriterTMP Typewriter;

        [Tooltip("只触发一次")]
        public bool Once = true;

        bool _used;

        void Reset()
        {
            // 尝试自动找同场景的 Typewriter
            Typewriter = FindAnyObjectByType<TypewriterTMP>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_used && Once) return;
            if (!other.CompareTag("Player")) return;

            if (Typewriter != null)
            {
                Typewriter.Begin();
                _used = true;
            }
        }
    }
}

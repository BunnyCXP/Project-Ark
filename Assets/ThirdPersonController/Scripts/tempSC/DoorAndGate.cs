using UnityEngine;

namespace TheGlitch
{
    public class DualChargerGate : MonoBehaviour
    {
        public QChargerHackable Left;
        public QChargerHackable Right;
        public DoorSimple Door;

        [Tooltip("两边都充满后，需要同时成立的最短时间（更有“同时”的感觉）")]
        public float BothChargedHold = 10.0f;

        private float _bothT;
        private void Update()
        {
            if (Left == null || Right == null || Door == null) return;

            bool both = Left.IsCharged && Right.IsCharged;

            Debug.Log($"[Gate] L={Left.IsCharged} R={Right.IsCharged} both={both} t={_bothT:F2}");

            if (both) _bothT += Time.unscaledDeltaTime;   // ★ 用 unscaled 更直观
            else _bothT = 0f;

            Door.SetOpen(_bothT >= BothChargedHold);
        }

    }
}

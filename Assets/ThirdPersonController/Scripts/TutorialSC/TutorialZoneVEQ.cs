using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class TutorialZoneVEQ : MonoBehaviour
    {
        [Header("Refs")]
        public HackWheelUI HackWheel;             // 玩家身上的 HackWheelUI
        public QChargerHackable TrainingNode;     // 训练节点（用于 Q 完成判定，可空）

        [Header("Keycaps")]
        public GameObject KeyV;
        public GameObject KeyE;
        public GameObject KeyQ;

        [Header("Timing (optional)")]
        [Tooltip("进入 Trigger 后延迟多久才允许 V 显示（想做“拐角出现”可以用）")]
        public float ShowDelayV = 0f;

        [Tooltip("V 完成后延迟多久才显示 E")]
        public float ShowDelayE = 0f;

        [Tooltip("E 完成后延迟多久才显示 Q")]
        public float ShowDelayQ = 0f;

        private bool _inside;

        // 累积完成状态（在 trigger 内不会回退）
        private bool _vDone;
        private bool _eDone;
        private bool _qDone;

        // 控制“按顺序出现”的解锁时间点
        private float _enterTime;
        private float _vDoneTime;
        private float _eDoneTime;

        private void Start()
        {
            SetAllOff();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _inside = true;
            _enterTime = Time.time;

            // 进入时先刷新（可能 ShowDelayV = 0 直接显示 V）
            RefreshUI();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _inside = false;

            // ★ 出 trigger：全部隐藏（跟 WASD 一样）
            SetAllOff();
        }

        private void Update()
        {
            if (!_inside) return;
            if (Keyboard.current == null) return;

            // ---- 1) V：按到一次就算完成 ----
            if (!_vDone && Keyboard.current.vKey.wasPressedThisFrame)
            {
                _vDone = true;
                _vDoneTime = Time.time;
                RefreshUI();
            }

            // ---- 2) E：进入 HackWheel（Root.activeSelf）才算完成 ----
            if (_vDone && !_eDone && HackWheel != null && HackWheel.Root != null && HackWheel.Root.activeSelf)
            {
                _eDone = true;
                _eDoneTime = Time.time;
                RefreshUI();
            }

            // ---- 3) Q：训练节点充能成功才算完成（可选）----
            if (_eDone && !_qDone && TrainingNode != null && TrainingNode.IsCharged)
            {
                _qDone = true;
                RefreshUI();
            }

            // 这帧如果延迟条件刚好满足，也要刷新显示
            //（比如 ShowDelayE/ShowDelayQ 用了延迟）
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (!_inside)
                return; // 出 trigger 已经 SetAllOff 了

            float now = Time.time;

            // V 何时允许显示
            bool allowV = now >= _enterTime + ShowDelayV;

            // E 何时允许显示：V 完成后 + delay
            bool allowE = _vDone && now >= _vDoneTime + ShowDelayE;

            // Q 何时允许显示：E 完成后 + delay
            bool allowQ = _eDone && now >= _eDoneTime + ShowDelayQ;

            // ★ 关键：在 trigger 内“累积出现，不会消失”
            // 规则：只要允许显示，就一直显示；不会因为玩家没按而关掉
            if (KeyV != null) KeyV.SetActive(allowV);
            if (KeyE != null) KeyE.SetActive(allowE);
            if (KeyQ != null) KeyQ.SetActive(allowQ);

            // 如果你想 Q 完成后也还显示（你说“不要消失”）——现在就是会一直显示
            // 如果你想 Q 完成后变“完成态”（变色/更亮），那是 Keycap 自己材质或脚本做
        }

        private void SetAllOff()
        {
            if (KeyV != null) KeyV.SetActive(false);
            if (KeyE != null) KeyE.SetActive(false);
            if (KeyQ != null) KeyQ.SetActive(false);
        }
    }
}

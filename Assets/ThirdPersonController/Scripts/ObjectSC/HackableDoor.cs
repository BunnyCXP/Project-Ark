using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Collider))]
    public class HackableDoor : MonoBehaviour, IHackable, IQuickHackable
    {
        [Header("Door Parts")]
        public Transform DoorMesh;       // 真正的门（可以是子物体 Box）
        public float OpenAngle = 90f;    // 开门角度
        public float OpenSpeed = 5f;

        [Header("State")]
        public bool IsLocked = true;
        public bool IsOpen = false;

        private Quaternion _closedRot;
        private Quaternion _openRot;
        private bool _scanTriggered;

        public string DisplayName => "Door";
        public Transform WorldTransform => transform;

        private void Awake()
        {
            if (DoorMesh == null)
                DoorMesh = transform;

            _closedRot = DoorMesh.localRotation;
            _openRot = _closedRot * Quaternion.Euler(0, OpenAngle, 0);
        }

        private void Update()
        {
            // 平滑旋转到目标开门角度
            Quaternion target = IsOpen ? _openRot : _closedRot;
            DoorMesh.localRotation = Quaternion.Slerp(DoorMesh.localRotation, target, Time.deltaTime * OpenSpeed);
        }

        // ===== 扫描 Glitch =====

        public void ResetScanFlag()
        {
            _scanTriggered = false;
        }

        public void OnScannedOnce()
        {
            if (_scanTriggered) return;
            _scanTriggered = true;
            StartCoroutine(ScanGlitchFX());
        }

        private IEnumerator ScanGlitchFX()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            Material mat = rend.material;
            Color baseColor =
                mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                Color.white;

            for (int i = 0; i < 2; i++)
            {
                SetMatColor(mat, new Color(0.2f, 0.8f, 1f));
                yield return new WaitForSecondsRealtime(0.05f);
                SetMatColor(mat, baseColor);
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }

        // ===== 深度面板字段 =====

        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("locked", "Door.Locked", IsLocked),
                new HackField("open",   "Door.Open",   IsOpen)
            };
        }

        public void Apply(List<HackField> fields)
        {
            foreach (var f in fields)
            {
                if (f.Id == "locked" && bool.TryParse(f.Value, out bool b1))
                    IsLocked = b1;

                if (f.Id == "open" && bool.TryParse(f.Value, out bool b2))
                    IsOpen = b2;
            }
        }

        // ===== Quick Hack 轮盘 =====

        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            // ↑：解锁 / 上锁
            up = new QuickHackOption
            {
                Name = IsLocked ? "Unlock" : "Lock",
                RequiresCharge = false,
                Execute = () =>
                {
                    IsLocked = !IsLocked;
                }
            };

            // →：开 / 关（静音开门）
            right = new QuickHackOption
            {
                Name = IsOpen ? "Close" : "Open",
                RequiresCharge = false,
                Execute = () =>
                {
                    if (!IsLocked)
                        IsOpen = !IsOpen;
                }
            };

            // ↓：强制敞开（卡死在 Open 状态）
            down = new QuickHackOption
            {
                Name = "Jam Open",
                RequiresCharge = false,
                Execute = () =>
                {
                    IsLocked = true;
                    IsOpen = true;
                }
            };

            // ←：Overload（爆门，高风险）
            left = new QuickHackOption
            {
                Name = "Overload",
                RequiresCharge = true,
                ChargeTime = 1.0f,
                Execute = () =>
                {
                    // 爆门：碰撞体失效，门定格在半开状态
                    var col = GetComponent<Collider>();
                    if (col) col.enabled = false;

                    IsLocked = true;
                    IsOpen = true;
                    DoorMesh.localRotation = _openRot;

                    // 这里还能加一个巨响+吸引敌人，后面再接
                    Debug.Log("Door Overloaded (loud)");
                }
            };
        }
    }
}


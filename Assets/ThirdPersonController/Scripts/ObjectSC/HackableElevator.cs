using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Rigidbody))]
    public class HackableElevator : MonoBehaviour, IHackable, IQuickHackable
    {
        [Header("Elevator Settings")]
        public float LiftHeight = 5f;
        public float MoveSpeed = 3f;

        [Header("Audio (可选)")]
        public AudioSource ElevatorSound;

        private Rigidbody _rb;
        private Vector3 _bottomPos;
        private Vector3 _topPos;
        private Vector3 _targetPos;

        private bool _isAtTop = false;
        private bool _isMoving = false;

        // 状态锁
        private bool _isTransitioning = false;

        // 【新增】：冷却计时器，防止关闭开启CC引起的无限死循环！
        private float _triggerCooldown = 0f;

        // 记录站在电梯上的玩家
        private CharacterController _playerCC;

        public string DisplayName => "Elevator";
        public Transform WorldTransform => transform;
        private bool _scanTriggered = false;

        public void ResetScanFlag() => _scanTriggered = false;

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
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                              mat.HasProperty("_Color") ? mat.GetColor("_Color") : mat.color;

            for (int i = 0; i < 3; i++)
            {
                SetMatColor(mat, new Color(0.2f, 1f, 0.2f));
                yield return new WaitForSecondsRealtime(0.04f);
                SetMatColor(mat, baseColor);
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.None;

            _bottomPos = transform.position;
            _topPos = _bottomPos + Vector3.up * LiftHeight;
            _targetPos = _bottomPos;
        }

        private void Update()
        {
            // 【新增】：随着时间推移减少冷却计时
            if (_triggerCooldown > 0f)
            {
                _triggerCooldown -= Time.deltaTime;
            }

            // 只有当玩家不在电梯上时，才会执行视觉上的平滑移动
            if (_isMoving && !_isTransitioning)
            {
                transform.position = Vector3.MoveTowards(transform.position, _targetPos, MoveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPos) < 0.001f)
                {
                    transform.position = _targetPos;
                    _isMoving = false;
                }
            }
        }

        // --- 玩家上下电梯的感应区 ---
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerCC = other.GetComponent<CharacterController>();

                // 【核心修改】：只有不在冷却期、不在转场、不在移动时，踩上去才会触发！
                if (_triggerCooldown <= 0f && !_isTransitioning && !_isMoving)
                {
                    TriggerElevatorLogic();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_playerCC != null && other.gameObject == _playerCC.gameObject) _playerCC = null;
            }
        }

        // ==========================================
        // 【核心提取】：通用的电梯触发器
        // ==========================================
        private void TriggerElevatorLogic()
        {
            if (_isTransitioning || _isMoving) return;

            _isAtTop = !_isAtTop;
            _targetPos = _isAtTop ? _topPos : _bottomPos;

            // 判断玩家是不是踩在电梯上
            if (_playerCC != null && ScreenFader.Instance != null)
            {
                _isTransitioning = true;

                if (ElevatorSound) ElevatorSound.Play();

                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    Vector3 oldElevatorPos = transform.position;
                    Vector3 playerOffset = _playerCC.transform.position - oldElevatorPos;

                    // 1. 瞬间移动电梯
                    transform.position = _targetPos;
                    _isMoving = false;

                    // 2. 提前开启冷却锁！防止重启CC时触发 OnTriggerEnter
                    _triggerCooldown = 1.5f;

                    // 3. 瞬间极其精准地移动玩家
                    _playerCC.enabled = false;
                    _playerCC.transform.position = _targetPos + playerOffset;
                    _playerCC.enabled = true; // 此时就算触发了 OnTriggerEnter，也会被上面的冷却锁挡住！

                    _isTransitioning = false;
                });
            }
            else
            {
                if (ElevatorSound) ElevatorSound.Play();
                _isMoving = true;
            }
        }

        // --- 轮盘 Hack 功能 ---
        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            up = new QuickHackOption
            {
                Id = "Elevator_Toggle",
                Name = _isAtTop ? "Go Down" : "Go Up",
                RequiresCharge = false,
                Execute = () =>
                {
                    // 黑客轮盘远程执行时，如果冷却完毕也可以强行调用
                    if (_triggerCooldown <= 0f)
                    {
                        TriggerElevatorLogic();
                    }
                }
            };
            right = null; down = null; left = null;
        }

        // --- 深度 Hack（保留） ---
        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("state", "Elevator.State", _isAtTop ? "TOP" : "BOTTOM", new[] { "TOP", "BOTTOM" })
            };
        }

        public void Apply(List<HackField> fields)
        {
            foreach (var f in fields)
            {
                if (f.Id == "state")
                {
                    bool wantTop = (f.Value == "TOP");
                    if (wantTop != _isAtTop)
                    {
                        _isAtTop = wantTop;
                        _targetPos = _isAtTop ? _topPos : _bottomPos;
                        _isMoving = true;
                    }
                }
            }
        }
    }
}
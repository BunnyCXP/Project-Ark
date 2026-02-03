using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class ScannerController : MonoBehaviour
    {
        private enum Mode { Normal, Scan, Hack }

        [Header("Refs")]
        public Camera MainCamera;

        [Header("Center Crosshair")]
        public RectTransform CrosshairUI; // 屏幕中心准心（一直在中心）

        [Header("Scan")]
        public LayerMask HackableMask;
        public LayerMask EnvironmentMask;
        public float ScanRadius = 12f;
        public float RefreshInterval = 0.25f;
        public float AimDistance = 30f;

        [Header("Prompt UI (near objects)")]
        public Transform PromptsParent;     // Canvas 下 ScanPrompts
        public HackPromptUI PromptPrefab;   // HackPrompt prefab
        public Vector3 PromptWorldOffset = new Vector3(0, 2.0f, 0);

        [Header("Hack Panel")]

        public HackWheelUI HackWheel;

        [Header("Camera FX")]
        public HackCameraFX HackCamFX;

        [Header("Scan Screen FX")]
        public ScanScreenFX ScreenScanFX;
        public float ScanFXDelay = 0.35f;   // 和 ScanScreenFX.Duration 保持一致
        public GameObject ScanOverlay;
        public ScanColliderWireframeFX WireframeFX;

        private bool _scanStarting;




        private Coroutine _maskFadeCo;


        private Mode _mode = Mode.Normal;
        private float _refreshTimer;

        public float QChargeTime = 0.8f;
        private float _qHold;
        private bool _qCharging;
        private QuickHackOption _chargingOption; // 当前正在充能的选项
        private IHackable _hackTarget;

        private readonly List<IHackable> _inRange = new();
        private readonly Dictionary<IHackable, HackPromptUI> _prompts = new();
        private IHackable _aimed;
        private Vector2 _reticlePos;   // 虚拟光标（用于 ScanReticle）

        private void Reset()
        {
            if (MainCamera == null) MainCamera = Camera.main;
        }

        private void Start()
        {
            BulletTime.Init();
            EnterNormal();

        }


        private void Update()
        {
            if (Keyboard.current == null) return;

            // ===== V：切换 Normal <-> Scan（Normal 进入 Scan 时加扫描动画） =====
            if (Keyboard.current.vKey.wasPressedThisFrame && !_scanStarting)
            {
                if (_mode == Mode.Normal)
                {
                    // 先播全屏扫描动画，再真正进入 Scan
                    StartCoroutine(StartScanWithFX());
                }
                else if (_mode == Mode.Scan)
                {
                    // 从 Scan 退回 Normal 还是直接退
                    EnterNormal();
                }
                // Hack 模式下按 V：可以选择无效或以后加别的功能，先不处理
            }

            // ===== Scan 模式：持续 Tick 扫描 + E 进入 Hack =====
            if (_mode == Mode.Scan)
            {
                TickScan();

                if (_aimed != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    EnterHack(_aimed);
                }
            }
            // ===== Hack 模式：轮盘 + Q Hack =====
            else if (_mode == Mode.Hack)
            {
                // 鼠标 delta 驱动轮盘方向
                if (Mouse.current != null)
                {
                    HackWheel.FeedMouseDelta(Mouse.current.delta.ReadValue());
                }

                var opt = HackWheel.GetSelectedOption();

                // ——短按 Q：非高风险 Hack，立即执行，留在轮盘——
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    if (opt != null && opt.Execute != null && !opt.RequiresCharge)
                    {
                        // 执行玩家选择的 Hack
                        opt.Execute.Invoke();

                        // ★ 记录给影子：这一次对谁、用哪个选项
                        if (PlayerEchoRecorder.Instance != null && _hackTarget != null)
                        {
                            PlayerEchoRecorder.Instance.RecordLastHack(_hackTarget, opt);
                        }

                        // 非高风险：不抖、不退出
                        HackWheel.SetChargeProgress(0f);
                        _chargingOption = null;
                        _qHold = 0f;
                    }

                    else
                    {
                        // 高风险 Hack：准备充能
                        _chargingOption = (opt != null && opt.RequiresCharge) ? opt : null;
                        _qHold = 0f;
                        HackWheel.SetChargeProgress(0f);
                    }
                }

                // ——按住 Q：只对 RequiresCharge 的 Hack 充能 + 抖动——
                if (Keyboard.current.qKey.isPressed && _chargingOption != null)
                {
                    // 中途换方向 → 取消充能
                    if (opt != _chargingOption)
                    {
                        _chargingOption = null;
                        _qHold = 0f;
                        HackWheel.SetChargeProgress(0f);
                    }
                    else
                    {
                        _qHold += Time.unscaledDeltaTime;

                        float t01 = _qHold / Mathf.Max(0.01f, _chargingOption.ChargeTime);
                        HackWheel.SetChargeProgress(t01); // ★ 抖动就在这里发生

                        if (_qHold >= _chargingOption.ChargeTime)
                        {
                            PlayerEchoRecorder.Instance?.RecordLastHack(_aimed, _chargingOption);

                            // ★ 满充那一帧：抖动已到最大
                            _chargingOption.Execute?.Invoke();

                            // ★ 同样记录给影子
                            if (PlayerEchoRecorder.Instance != null && _hackTarget != null)
                            {
                                PlayerEchoRecorder.Instance.RecordLastHack(_hackTarget, _chargingOption);
                            }

                            // 高风险 Hack：立刻退出
                            ExitHackToNormal();

                            _chargingOption = null;
                            _qHold = 0f;
                        }

                    }
                }

                // ——松开 Q：取消充能——
                if (!Keyboard.current.qKey.isPressed && _chargingOption != null)
                {
                    _chargingOption = null;
                    _qHold = 0f;
                    HackWheel.SetChargeProgress(0f);
                }

                // E：手动退出 Hack
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    ExitHackToNormal();
                }
            }
        }



        // ---------- Modes ----------

        private void EnterNormal()
        {
            BulletTime.Set(false);
            if (WireframeFX != null)
                WireframeFX.EndScan();

            if (ScanOverlay != null)
                ScanOverlay.SetActive(false);

            _mode = Mode.Normal;

            // Normal 模式：不显示 ScanReticle
            if (CrosshairUI != null)
                CrosshairUI.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;



            ClearAllPrompts();
            _inRange.Clear();
            _aimed = null;

            // ★ 世界恢复彩色
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.SetNormal();
        }


        private IEnumerator StartScanWithFX()
        {
            _scanStarting = true;

            // 预收集一次 inRange
            RefreshInRange();
            foreach (var h in _inRange)
                h?.ResetScanFlag();

            // 动画期间先整体隐藏 Prompt
            if (PromptsParent != null)
                PromptsParent.gameObject.SetActive(false);

            if (ScreenScanFX != null)
                ScreenScanFX.Play();
            if (WireframeFX != null) WireframeFX.BeginScan(transform, MainCamera);


            float t = 0f;
            while (t < ScanFXDelay)
            {
                t += Time.unscaledDeltaTime;

                // === 扫描期间：所有在屏幕里的 hackable 都触发一次 OnScannedOnce ===
                if (MainCamera != null)
                {
                    foreach (var h in _inRange)
                    {
                        if (h == null) continue;

                        Vector3 vp = MainCamera.WorldToViewportPoint(h.WorldTransform.position);
                        bool inFront = vp.z > 0.01f;
                        bool inScreenX = vp.x >= 0f && vp.x <= 1f;
                        bool inScreenY = vp.y >= 0f && vp.y <= 1f;

                        if (inFront && inScreenX && inScreenY)
                        {
                            h.OnScannedOnce();   // 每个 h 自己用 _scanTriggered 防止多次触发
                        }
                    }
                }

                yield return null;
            }

            // 动画结束 → 真正进入 Scan 模式
            EnterScan();

            RefreshInRange();
            if (PromptsParent != null)
                PromptsParent.gameObject.SetActive(true);

            _scanStarting = false;
        }




        private void EnterScan()
        {
            BulletTime.Set(true, 0.2f);
            if (ScanOverlay != null)
                ScanOverlay.SetActive(true);

            _mode = Mode.Scan;

            if (CrosshairUI != null)
            {
                CrosshairUI.gameObject.SetActive(true);

                // ★ 扫描开始时，把虚拟光标放在屏幕中心
                _reticlePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                CrosshairUI.position = _reticlePos;
            }

            // ★ 扫描时仍然锁鼠标 & 隐藏系统光标（只看得到 UI 准星）
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;



            _refreshTimer = 0f;
            TickScan(forceRefresh: true);
            foreach (var h in _inRange)
                h.ResetScanFlag();

            // ★ Scan 模式：世界变黑白
            // Scan：黑白 + 中等噪点
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.SetScanMode();
        }


        private void UpdateReticleUI()
        {
            if (CrosshairUI == null) return;
            if (Mouse.current == null) return;

            // 读取这一帧鼠标移动量（不受 timeScale 影响）
            Vector2 delta = Mouse.current.delta.ReadValue();

            // 可以加个灵敏度，比如 1.0f 不变，0.5f 更慢
            float sensitivity = 1.0f;
            _reticlePos += delta * sensitivity;

            // 限制在屏幕内
            _reticlePos.x = Mathf.Clamp(_reticlePos.x, 0f, Screen.width);
            _reticlePos.y = Mathf.Clamp(_reticlePos.y, 0f, Screen.height);

            // 更新 UI reticle 位置
            CrosshairUI.position = _reticlePos;
        }


        private void EnterHack(IHackable target)
        {
            // ★ 记录当前 hack 的目标（给影子用）
            _hackTarget = target;
            BulletTime.Set(true, 0.2f);
            if (ScanOverlay != null)
                ScanOverlay.SetActive(true);

            _mode = Mode.Hack;
            if (CrosshairUI != null) CrosshairUI.gameObject.SetActive(false);

            // 暂停扫描提示
            SetAllPromptsActive(false);
            DisableAllOutlines();

            // 鼠标不弹出：保持锁定 + 不显示
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;



            // 从目标拿四个选项
            QuickHackOption up = null, right = null, down = null, left = null;
            if (target is IQuickHackable qh)
                qh.GetQuickHacks(out up, out right, out down, out left);

            HackWheel.Open(up, right, down, left);
            HackWheel.SetFollow(target.WorldTransform, MainCamera);
            HackWheel.SetChargeProgress(0f);

            // ★ Hack 模式：同样保持黑白世界
            // Hack：黑白 + 更重噪点 + 更高对比
            if (WorldFXController.Instance != null)
                WorldFXController.Instance.SetHackMode();

            // ★ 打开 Hack 镜头效果
            if (HackCamFX != null)
                HackCamFX.SetHack(true);

        }





        private void ExitHackToNormal()
        {
            if (HackWheel != null)
            {
                HackWheel.SetChargeProgress(0f);
                HackWheel.ClearFollow();
                HackWheel.Close();
            }
            // ★ 离开 Hack 模式，把当前目标清掉
            _hackTarget = null;
            // ★ 打开 Hack 镜头效果
            if (HackCamFX != null)
                HackCamFX.SetHack(false);
            WireframeFX.EndScan();

            EnterNormal();
        }



        // ---------- Scan ----------

        private void TickScan(bool forceRefresh = false)
        {
            _refreshTimer -= Time.deltaTime;
            if (forceRefresh || _refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                RefreshInRange();
            }

            // ★ 鼠标 delta → 推动虚拟光标 → 更新 ScanReticle 位置
            UpdateReticleUI();

            // ★ 用 ScanReticle 位置做射线选目标
            UpdateAimedByReticle();

            UpdatePromptAndOutlineVisuals();
        }








        private void RefreshInRange()
        {
            // 1) 收集范围内唯一 hackable（用 HashSet 去重最稳）
            var set = new HashSet<IHackable>();

            var hits = Physics.OverlapSphere(transform.position, ScanRadius, HackableMask, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var h = c.GetComponentInParent<IHackable>();
                if (h != null) set.Add(h);
            }

            // 2) 更新 _inRange
            _inRange.Clear();
            _inRange.AddRange(set);

            // 3) 移除离开范围的 prompt
            var remove = new List<IHackable>();
            foreach (var kv in _prompts)
            {
                if (!set.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    remove.Add(kv.Key);
                }
            }
            foreach (var k in remove) _prompts.Remove(k);

            // 4) 为新进入范围的创建 prompt；已有的只更新，不 Instantiate
            foreach (var h in _inRange)
            {
                if (!_prompts.TryGetValue(h, out var ui) || ui == null)
                {
                    if (PromptPrefab == null || PromptsParent == null) continue;

                    ui = Instantiate(PromptPrefab, PromptsParent);
                    ui.name = $"HackPrompt_{h.DisplayName}";
                    ui.WorldOffset = PromptWorldOffset;
                    ui.Bind(h, MainCamera);
                    _prompts[h] = ui;
                }
                else
                {
                    ui.WorldOffset = PromptWorldOffset;
                    ui.Bind(h, MainCamera);
                }
            }

        }


        private void UpdateAimedByReticle()
        {
            _aimed = null;
            if (MainCamera == null) return;

            // 用虚拟光标位置发射屏幕射线
            Ray ray = MainCamera.ScreenPointToRay(_reticlePos);

            if (Physics.Raycast(ray, out RaycastHit hit, AimDistance, HackableMask, QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<IHackable>();
                if (h != null && _inRange.Contains(h))
                    _aimed = h;
            }
        }


        private void UpdatePromptAndOutlineVisuals()
        {
            foreach (var kv in _prompts)
            {
                var h = kv.Key;
                var ui = kv.Value;
                if (ui == null) continue;

                // 只显示视野内卡片
                bool visible = ui.UpdateScreenPositionOnlyIfVisible();
                if (!visible)
                {
                    // 视野外：不高亮，不描边
                    ui.SetHighlighted(false);
                    SetOutline(h.WorldTransform, false);
                    continue;
                }

                bool aimed = (h == _aimed);
                ui.SetHighlighted(aimed);
                SetOutline(h.WorldTransform, aimed);
            }

            if (_aimed == null)
            {
                foreach (var kv in _prompts)
                    SetOutline(kv.Key.WorldTransform, false);
            }
        }

        private void SetOutline(Transform root, bool on)
        {
            if (root == null) return;
            var outline = root.GetComponentInChildren<OutlineTarget>();
            if (outline != null) outline.SetOutlined(on);
        }

        private void SetAllPromptsActive(bool on)
        {
            foreach (var kv in _prompts)
            {
                if (kv.Value != null)
                    kv.Value.gameObject.SetActive(on);
            }
        }

        private void DisableAllOutlines()
        {
            foreach (var kv in _prompts)
            {
                SetOutline(kv.Key.WorldTransform, false);
            }
        }








        private void ClearAllPrompts()
        {
            foreach (var kv in _prompts)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _prompts.Clear();

            foreach (var h in _inRange)
                SetOutline(h.WorldTransform, false);
        }
    }
}


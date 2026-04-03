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
        public RectTransform CrosshairUI;

        [Header("Scan")]
        public LayerMask HackableMask;
        public LayerMask EnvironmentMask;
        public float ScanRadius = 12f;
        public float RefreshInterval = 0.25f;
        public float AimDistance = 30f;

        [Header("Prompt UI (near objects)")]
        public Transform PromptsParent;
        public HackPromptUI PromptPrefab;
        public Vector3 PromptWorldOffset = new Vector3(0, 2.0f, 0);

        [Header("Hack Panel")]
        public HackWheelUI HackWheel;

        [Header("Camera FX")]
        public HackCameraFX HackCamFX;

        [Header("Scan Screen FX")]
        public ScanScreenFX ScreenScanFX;
        public float ScanFXDelay = 0.35f;
        public GameObject ScanOverlay;
        public ScanColliderWireframeFX WireframeFX;

        private bool _scanStarting;
        public static bool IsScanOrHackActive { get; private set; }

        private Mode _mode = Mode.Normal;
        private float _refreshTimer;

        public float QChargeTime = 0.8f;
        private float _qHold;
        private QuickHackOption _chargingOption;
        private IHackable _hackTarget;

        private readonly List<IHackable> _inRange = new List<IHackable>(64);
        private readonly Dictionary<IHackable, HackPromptUI> _prompts = new Dictionary<IHackable, HackPromptUI>(64);
        private IHackable _aimed;
        private Vector2 _reticlePos;

        private static readonly Collider[] _scannerHitsAlloc = new Collider[100];
        private readonly HashSet<IHackable> _tempSet = new HashSet<IHackable>();
        private readonly List<IHackable> _tempRemove = new List<IHackable>(32);

        // 【CPU 优化核心】缓存已经被扫过的组件，杜绝每次都往复 GetComponentInParent 寻址
        private static readonly Dictionary<Collider, IHackable> _hackableCache = new Dictionary<Collider, IHackable>(256);

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

            if (Keyboard.current.vKey.wasPressedThisFrame && !_scanStarting)
            {
                if (_mode == Mode.Normal) StartCoroutine(StartScanWithFX());
                else if (_mode == Mode.Scan) EnterNormal();
            }

            if (_mode == Mode.Scan)
            {
                TickScan();
                if (_aimed != null && Keyboard.current.eKey.wasPressedThisFrame) EnterHack(_aimed);
            }
            else if (_mode == Mode.Hack)
            {
                if (Mouse.current != null) HackWheel.FeedMouseDelta(Mouse.current.delta.ReadValue());

                var opt = HackWheel.GetSelectedOption();

                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    if (opt != null && opt.Execute != null && !opt.RequiresCharge)
                    {
                        opt.Execute.Invoke();
                        if (PlayerEchoRecorder.Instance != null && _hackTarget != null)
                            PlayerEchoRecorder.Instance.RecordLastHack(_hackTarget, opt);

                        HackWheel.SetChargeProgress(0f);
                        _chargingOption = null;
                        _qHold = 0f;
                    }
                    else
                    {
                        _chargingOption = (opt != null && opt.RequiresCharge) ? opt : null;
                        _qHold = 0f;
                        HackWheel.SetChargeProgress(0f);
                    }
                }

                if (Keyboard.current.qKey.isPressed && _chargingOption != null)
                {
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
                        HackWheel.SetChargeProgress(t01);

                        if (_qHold >= _chargingOption.ChargeTime)
                        {
                            PlayerEchoRecorder.Instance?.RecordLastHack(_aimed, _chargingOption);
                            _chargingOption.Execute?.Invoke();

                            if (PlayerEchoRecorder.Instance != null && _hackTarget != null)
                                PlayerEchoRecorder.Instance.RecordLastHack(_hackTarget, _chargingOption);

                            ExitHackToNormal();
                            _chargingOption = null;
                            _qHold = 0f;
                        }
                    }
                }

                if (!Keyboard.current.qKey.isPressed && _chargingOption != null)
                {
                    _chargingOption = null;
                    _qHold = 0f;
                    HackWheel.SetChargeProgress(0f);
                }

                if (Keyboard.current.eKey.wasPressedThisFrame) ExitHackToNormal();
            }
        }

        private void EnterNormal()
        {
            BulletTime.Set(false);
            if (WireframeFX != null) WireframeFX.EndScan();
            if (ScanOverlay != null) ScanOverlay.SetActive(false);

            _mode = Mode.Normal;
            if (CrosshairUI != null) CrosshairUI.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ClearAllPrompts();
            _inRange.Clear();
            _aimed = null;

            if (WorldFXController.Instance != null) WorldFXController.Instance.SetNormal();
            IsScanOrHackActive = false;
        }

        private IEnumerator StartScanWithFX()
        {
            _scanStarting = true;

            RefreshInRange();
            foreach (var h in _inRange) h?.ResetScanFlag();

            if (PromptsParent != null) PromptsParent.gameObject.SetActive(false);
            if (ScreenScanFX != null) ScreenScanFX.Play();
            if (WireframeFX != null) WireframeFX.BeginScan(transform, MainCamera);

            float t = 0f;
            while (t < ScanFXDelay)
            {
                t += Time.unscaledDeltaTime;

                if (MainCamera != null)
                {
                    foreach (var h in _inRange)
                    {
                        if (h == null) continue;
                        Vector3 vp = MainCamera.WorldToViewportPoint(h.WorldTransform.position);
                        bool inFront = vp.z > 0.01f;
                        bool inScreenX = vp.x >= 0f && vp.x <= 1f;
                        bool inScreenY = vp.y >= 0f && vp.y <= 1f;

                        if (inFront && inScreenX && inScreenY) h.OnScannedOnce();
                    }
                }

                yield return null;
            }

            EnterScan();
            RefreshInRange();
            if (PromptsParent != null) PromptsParent.gameObject.SetActive(true);

            _scanStarting = false;
        }

        private void EnterScan()
        {
            BulletTime.Set(true, 0.2f);
            if (ScanOverlay != null) ScanOverlay.SetActive(true);

            _mode = Mode.Scan;
            if (CrosshairUI != null)
            {
                CrosshairUI.gameObject.SetActive(true);
                _reticlePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                CrosshairUI.position = _reticlePos;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _refreshTimer = 0f;
            TickScan(forceRefresh: true);
            foreach (var h in _inRange) h.ResetScanFlag();

            if (WorldFXController.Instance != null) WorldFXController.Instance.SetScanMode();
            IsScanOrHackActive = true;
        }

        private void UpdateReticleUI()
        {
            if (CrosshairUI == null || Mouse.current == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            float sensitivity = 1.0f;
            _reticlePos += delta * sensitivity;

            _reticlePos.x = Mathf.Clamp(_reticlePos.x, 0f, Screen.width);
            _reticlePos.y = Mathf.Clamp(_reticlePos.y, 0f, Screen.height);

            CrosshairUI.position = _reticlePos;
        }

        private void EnterHack(IHackable target)
        {
            _hackTarget = target;
            BulletTime.Set(true, 0.2f);
            if (ScanOverlay != null) ScanOverlay.SetActive(true);

            _mode = Mode.Hack;
            if (CrosshairUI != null) CrosshairUI.gameObject.SetActive(false);

            SetAllPromptsActive(false);
            DisableAllOutlines();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            QuickHackOption up = null, right = null, down = null, left = null;
            if (target is IQuickHackable qh)
                qh.GetQuickHacks(out up, out right, out down, out left);

            HackWheel.Open(up, right, down, left);
            HackWheel.SetFollow(target.WorldTransform, MainCamera);
            HackWheel.SetChargeProgress(0f);

            if (WorldFXController.Instance != null) WorldFXController.Instance.SetHackMode();
            if (HackCamFX != null) HackCamFX.SetHack(true);
            IsScanOrHackActive = true;
        }

        private void ExitHackToNormal()
        {
            if (HackWheel != null)
            {
                HackWheel.SetChargeProgress(0f);
                HackWheel.ClearFollow();
                HackWheel.Close();
            }
            _hackTarget = null;

            if (HackCamFX != null) HackCamFX.SetHack(false);
            WireframeFX.EndScan();

            EnterNormal();
        }

        private void TickScan(bool forceRefresh = false)
        {
            _refreshTimer -= Time.deltaTime;
            if (forceRefresh || _refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                RefreshInRange();
            }

            UpdateReticleUI();
            UpdateAimedByReticle();
            UpdatePromptAndOutlineVisuals();
        }

        private void RefreshInRange()
        {
            _tempSet.Clear();
            _tempRemove.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, _scannerHitsAlloc, HackableMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _scannerHitsAlloc[i];
                if (c == null) continue;

                // 【CPU 优化核心】从缓存拿组件，如果没有再耗时查询并缓存
                if (!_hackableCache.TryGetValue(c, out IHackable h))
                {
                    h = c.GetComponentInParent<IHackable>();
                    if (c != null) _hackableCache[c] = h;
                }

                if (h != null) _tempSet.Add(h);
            }

            _inRange.Clear();
            foreach (var h in _tempSet) _inRange.Add(h);

            foreach (var kv in _prompts)
            {
                if (!_tempSet.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    _tempRemove.Add(kv.Key);
                }
            }
            foreach (var k in _tempRemove) _prompts.Remove(k);

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

            Ray ray = MainCamera.ScreenPointToRay(_reticlePos);

            if (Physics.Raycast(ray, out RaycastHit hit, AimDistance, HackableMask, QueryTriggerInteraction.Ignore))
            {
                Collider c = hit.collider;
                if (!_hackableCache.TryGetValue(c, out IHackable h))
                {
                    h = c.GetComponentInParent<IHackable>();
                    if (c != null) _hackableCache[c] = h;
                }

                if (h != null && _inRange.Contains(h)) _aimed = h;
            }
        }

        private void UpdatePromptAndOutlineVisuals()
        {
            foreach (var kv in _prompts)
            {
                var h = kv.Key;
                var ui = kv.Value;
                if (ui == null) continue;

                bool visible = ui.UpdateScreenPositionOnlyIfVisible();
                if (!visible)
                {
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
                foreach (var kv in _prompts) SetOutline(kv.Key.WorldTransform, false);
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
                if (kv.Value != null) kv.Value.gameObject.SetActive(on);
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

            foreach (var h in _inRange) SetOutline(h.WorldTransform, false);
        }
    }
}
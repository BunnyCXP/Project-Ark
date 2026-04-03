using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

namespace TheGlitch
{
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class AnimusLevelConstructor : MonoBehaviour
    {
        [Header("🎥 镜头组 (GameObject 必须包含 CM Camera 组件)")]
        public GameObject VirtualCamera1;
        public GameObject VirtualCamera2;
        public GameObject PlayerVirtualCamera;

        [Header("🎥 电影级微小运镜")]
        public bool EnableCameraDollyMove = true;
        [Space(5)]
        public Transform Cam1DollyTarget;
        [Range(0.1f, 3.0f)] public float Cam1MoveSpeedMultiplier = 1.0f;
        [Space(5)]
        public Transform Cam2DollyTarget;
        [Range(0.1f, 3.0f)] public float Cam2MoveSpeedMultiplier = 1.0f;

        [Header("🏗️ 空间大小与扫描控制")]
        public Transform BuildOrigin;
        public Transform LevelParent;
        public bool AutoCalculateSize = true;
        public float ManualMaxRadius = 80f;
        [Range(5f, 200f)] public float BuildWaveSpeed = 25f;

        [Header("🎬 镜头切换时间")]
        public float StartBuildDelay = 0.5f;
        public float Camera1Duration = 3.0f;
        public float Camera2Duration = 3.0f;

        [Header("✨ 视觉特效与闪回")]
        public bool ShowDiffusionSphere = true;
        public Color SphereColor = new Color(0.1f, 0.4f, 1.0f, 0.3f);
        public float IntroWhiteFadeDuration = 1.5f;
        public float FlashHalfDuration = 0.15f;
        [Range(0.1f, 1.0f)] public float MaxFlashAlpha = 0.6f;

        [Header("⚡ Animus 故障闪烁 (Glitch)")]
        public Color GlitchFlashColor = new Color(0.8f, 0.95f, 1.0f, 0.1f);
        [Range(0.5f, 10f)] public float FovTwitchAmplitude = 3.0f;
        [Range(0.1f, 0.9f)] public float GlitchFrequency = 0.4f;

        [Header("💻 终端启动代码 & 全息卡片 (Terminal UI)")]
        public bool ShowBootTerminal = true;
        public Color TerminalTextColor = new Color(0.4f, 0.8f, 1.0f, 0.8f);

        public MonoBehaviour[] PlayerScriptsToFreeze;

        private float _maxDistance = 0f;
        private float _currentBuildRadius = 0f;
        private bool _isBuilding = false;
        private bool _isGlitchingLoop = false;

        private Image _whiteScreen;
        private TextMeshProUGUI _terminalText;
        private TextMeshProUGUI[] _holographicPanelTexts;
        private CinemachineImpulseSource _impulseSource;
        private GameObject _diffusionSphere;
        private CinemachineBrain _cameraBrain;

        private GameObject _currentActiveCamObj;

        private Vector3 _cam1StartPos, _cam2StartPos;
        private Quaternion _cam1StartRot, _cam2StartRot;

        private void Start()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();

            SetupUI();
            InitializeLevelRadius();
            SetupDiffusionSphereVisual();

            StoreInitialCameraTransforms();

            _cameraBrain = FindObjectOfType<CinemachineBrain>();
            if (_cameraBrain == null && Camera.main != null)
            {
                _cameraBrain = Camera.main.gameObject.AddComponent<CinemachineBrain>();
            }

            StartCoroutine(PlayIntroSequence());
        }

        private void StoreInitialCameraTransforms()
        {
            if (VirtualCamera1 != null) { _cam1StartPos = VirtualCamera1.transform.position; _cam1StartRot = VirtualCamera1.transform.rotation; }
            if (VirtualCamera2 != null) { _cam2StartPos = VirtualCamera2.transform.position; _cam2StartRot = VirtualCamera2.transform.rotation; }
        }

        private void SetupUI()
        {
            GameObject canvasObj = new GameObject("AnimusIntroCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            // 1. 白屏遮罩
            GameObject imageObj = new GameObject("WhiteImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            _whiteScreen = imageObj.AddComponent<Image>();
            _whiteScreen.color = new Color(1, 1, 1, 0);
            RectTransform rect = _whiteScreen.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;

            // 2. 终端代码文本
            GameObject textObj = new GameObject("TerminalText");
            textObj.transform.SetParent(canvasObj.transform, false);
            _terminalText = textObj.AddComponent<TextMeshProUGUI>();
            _terminalText.font = TMP_Settings.defaultFontAsset;
            _terminalText.fontSize = 20;
            _terminalText.color = TerminalTextColor;
            _terminalText.alignment = TextAlignmentOptions.BottomLeft;
            _terminalText.lineSpacing = -20f;

            RectTransform txtRect = _terminalText.rectTransform;
            txtRect.sizeDelta = new Vector2(500, 300);
            txtRect.pivot = new Vector2(0f, 0f);
            txtRect.anchoredPosition = Vector2.zero;
            _terminalText.text = "";

            // 3. 全息数据流卡片
            if (ShowBootTerminal)
            {
                SetupHolographicPanels(canvasObj);
            }
        }

        private void SetupHolographicPanels(GameObject canvas)
        {
            _holographicPanelTexts = new TextMeshProUGUI[1];
            string panelName = "SYS_MONITOR";

            GameObject panelObj = new GameObject($"HoloPanel_{panelName}");
            panelObj.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI txt = panelObj.AddComponent<TextMeshProUGUI>();
            txt.font = TMP_Settings.defaultFontAsset;
            txt.fontSize = 18;
            txt.color = TerminalTextColor;
            txt.alignment = TextAlignmentOptions.TopLeft;
            txt.lineSpacing = -15f;
            txt.text = "";

            RectTransform r = txt.rectTransform;
            r.sizeDelta = new Vector2(300, 150);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;

            panelObj.AddComponent<CanvasGroup>();
            _holographicPanelTexts[0] = txt;
        }

        private void InitializeLevelRadius()
        {
            if (AutoCalculateSize && LevelParent != null && BuildOrigin != null)
            {
                _maxDistance = 0f;
                MeshRenderer[] renderers = LevelParent.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    float dist1 = Vector3.Distance(BuildOrigin.position, renderer.bounds.min);
                    float dist2 = Vector3.Distance(BuildOrigin.position, renderer.bounds.max);
                    float maxD = Mathf.Max(dist1, dist2);
                    if (maxD > _maxDistance) _maxDistance = maxD;
                }
                _maxDistance *= 1.1f;
            }
            else
            {
                _maxDistance = ManualMaxRadius;
            }

            if (LevelParent != null)
            {
                MeshRenderer[] renderers = LevelParent.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers) renderer.enabled = true;
            }

            Shader.SetGlobalFloat("_GlobalAnimusRadius", 0f);
            if (BuildOrigin != null) Shader.SetGlobalVector("_GlobalAnimusOrigin", BuildOrigin.position);
        }

        private void SetupDiffusionSphereVisual()
        {
            if (!ShowDiffusionSphere || BuildOrigin == null) return;
            _diffusionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_diffusionSphere.GetComponent<Collider>());
            _diffusionSphere.transform.position = BuildOrigin.position;
            _diffusionSphere.transform.localScale = Vector3.zero;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_Surface", 1);
            mat.color = SphereColor;
            _diffusionSphere.GetComponent<Renderer>().material = mat;
        }

        private IEnumerator PlayIntroSequence()
        {
            foreach (var script in PlayerScriptsToFreeze) { if (script != null) script.enabled = false; }

            _whiteScreen.color = new Color(1, 1, 1, MaxFlashAlpha);
            SwitchToCamera(VirtualCamera1);

            float totalCam1Time = IntroWhiteFadeDuration + StartBuildDelay + Camera1Duration;
            StartCoroutine(AnimateCameraDollyMove(VirtualCamera1, _cam1StartPos, _cam1StartRot, Cam1DollyTarget, totalCam1Time, Cam1MoveSpeedMultiplier));

            float totalCinematicTime = IntroWhiteFadeDuration + StartBuildDelay + Camera1Duration + (FlashHalfDuration * 2f) + Camera2Duration;

            if (ShowBootTerminal) StartCoroutine(BootTerminalRoutine(totalCinematicTime));

            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, IntroWhiteFadeDuration));
            yield return new WaitForSeconds(StartBuildDelay);

            _isBuilding = true;
            _isGlitchingLoop = true;
            StartCoroutine(GlitchLoopRoutine());

            yield return new WaitForSeconds(Camera1Duration);

            yield return StartCoroutine(FadeWhiteScreen(0f, MaxFlashAlpha, FlashHalfDuration));

            SwitchToCamera(VirtualCamera2);
            if (_impulseSource != null) _impulseSource.GenerateImpulse();

            CyberspaceEnvironment env = FindObjectOfType<CyberspaceEnvironment>();
            if (env != null) env.TriggerDataBurst();

            float totalCam2Time = FlashHalfDuration + Camera2Duration;
            StartCoroutine(AnimateCameraDollyMove(VirtualCamera2, _cam2StartPos, _cam2StartRot, Cam2DollyTarget, totalCam2Time, Cam2MoveSpeedMultiplier));

            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, FlashHalfDuration));
            yield return new WaitForSeconds(Camera2Duration);

            yield return StartCoroutine(FadeWhiteScreen(0f, MaxFlashAlpha, FlashHalfDuration));

            SwitchToCamera(PlayerVirtualCamera);
            _isBuilding = false;
            _isGlitchingLoop = false;

            if (_impulseSource != null) _impulseSource.GenerateImpulse();
            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, FlashHalfDuration));

            if (_diffusionSphere != null) Destroy(_diffusionSphere);

            Shader.SetGlobalFloat("_GlobalAnimusRadius", 99999f);

            foreach (var script in PlayerScriptsToFreeze) { if (script != null) script.enabled = true; }
        }

        private IEnumerator BootTerminalRoutine(float duration)
        {
            if (_terminalText == null) yield break;

            string[] logLines = {
                "INITIALIZING ANIMUS OS v9.42...", "BYPASSING SECURITY... [OK]", "ACCESSING GENETIC MEMORY BLOCK...",
                "LOADING GEOMETRY... 12%", "LOADING GEOMETRY... 45%", "LOADING GEOMETRY... 89%",
                "TEXTURE MAPS SYNCHRONIZED.", "PHYSICS ENGINE: ONLINE.", "TIMELINE: INSTABILITY DETECTED.",
                "COMPENSATING... NEURAL LINK...", "SYNCHRONIZATION COMPLETE."
            };

            string[] holoLines = {
                "RECONSTRUCTING...", "DATA_PACKETS: 14502/s", "CRC_CHECK...[OK]", "VECTOR_ARRAY_LOADED",
                "SYNC_BUFFER...14%", "TEMP_RECOVERY: 4%", "NEURAL_FEEDBACK: 120ms", "BYPASSING...",
                "RENDER_QUEUE: Busy"
            };

            string currentText = "";
            int lineIndex = 0;
            float t = 0;

            float nextLogTime = 0f;
            float nextTerminalJumpTime = 0f;
            float nextPanelUpdate = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;

                // 【👑 终极防遮挡】：主终端代码严格锁定在左下角极小范围内偏移
                if (t >= nextTerminalJumpTime)
                {
                    float rx = Random.Range(0.02f, 0.15f); // 绝对靠近左侧边缘
                    float ry = Random.Range(0.02f, 0.15f); // 绝对靠近下方边缘
                    _terminalText.rectTransform.anchorMin = new Vector2(rx, ry);
                    _terminalText.rectTransform.anchorMax = new Vector2(rx, ry);
                    _terminalText.rectTransform.anchoredPosition = Vector2.zero;

                    nextTerminalJumpTime = t + Random.Range(1.5f, 3.5f);
                }

                if (lineIndex < logLines.Length && t >= nextLogTime)
                {
                    currentText += logLines[lineIndex] + "\n";
                    string[] split = currentText.Split('\n');
                    if (split.Length > 6) currentText = string.Join("\n", split, split.Length - 6, 6);
                    _terminalText.text = currentText;
                    lineIndex++;
                    nextLogTime = t + Random.Range(0.3f, 1.0f);
                }

                // 【👑 终极防遮挡】：全息卡片空心化生成算法，绝不踏入屏幕中央！
                if (_holographicPanelTexts != null && _holographicPanelTexts.Length > 0)
                {
                    if (t >= nextPanelUpdate)
                    {
                        var txt = _holographicPanelTexts[0];
                        txt.text = $"{txt.gameObject.name.Substring(10)}\n------\n" +
                                   $"STATUS: {holoLines[Random.Range(0, holoLines.Length)]}\n" +
                                   $"VAL: {Random.Range(1000, 99999)}\n" +
                                   $"HASH: {Random.Range(100, 999)}";

                        float rx = 0f, ry = 0f;

                        // 50% 概率贴紧左右两边，50% 概率贴紧上下两边
                        if (Random.value > 0.5f)
                        {
                            rx = Random.value > 0.5f ? Random.Range(0.05f, 0.2f) : Random.Range(0.8f, 0.95f); // 最左 or 最右
                            ry = Random.Range(0.05f, 0.95f); // 上下随机
                        }
                        else
                        {
                            rx = Random.Range(0.05f, 0.95f); // 左右随机
                            ry = Random.value > 0.5f ? Random.Range(0.05f, 0.2f) : Random.Range(0.8f, 0.95f); // 最上 or 最下
                        }

                        txt.rectTransform.anchorMin = new Vector2(rx, ry);
                        txt.rectTransform.anchorMax = new Vector2(rx, ry);
                        txt.rectTransform.anchoredPosition = Vector2.zero;

                        txt.GetComponent<CanvasGroup>().alpha = Random.Range(0.5f, 1f);
                        nextPanelUpdate = t + Random.Range(0.2f, 0.8f);
                    }
                }
                yield return null;
            }

            _terminalText.color = new Color(1, 1, 1, 0.5f);
            yield return new WaitForSeconds(0.05f);
            _terminalText.text = "";
            if (_holographicPanelTexts != null && _holographicPanelTexts.Length > 0)
            {
                _holographicPanelTexts[0].text = "";
            }
        }

        private IEnumerator AnimateCameraDollyMove(GameObject camObj, Vector3 startPos, Quaternion startRot, Transform targetTrans, float duration, float speedMultiplier)
        {
            if (!EnableCameraDollyMove || camObj == null || targetTrans == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01((elapsed / duration) * speedMultiplier);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                camObj.transform.position = Vector3.Lerp(startPos, targetTrans.position, smoothT);
                camObj.transform.rotation = Quaternion.Lerp(startRot, targetTrans.rotation, smoothT);

                yield return null;
            }
        }

        private void Update()
        {
            if (_isBuilding && BuildOrigin != null)
            {
                _currentBuildRadius += BuildWaveSpeed * Time.deltaTime;
                if (_currentBuildRadius > _maxDistance) _currentBuildRadius = _maxDistance;

                if (_diffusionSphere != null)
                    _diffusionSphere.transform.localScale = Vector3.one * (_currentBuildRadius * 2f);

                Shader.SetGlobalFloat("_GlobalAnimusRadius", _currentBuildRadius);
                Shader.SetGlobalVector("_GlobalAnimusOrigin", BuildOrigin.position);
            }
        }

        private void SwitchToCamera(GameObject targetCamObj)
        {
            if (_cameraBrain != null) _cameraBrain.enabled = false;

            if (targetCamObj != null)
            {
                targetCamObj.SetActive(true);
                _currentActiveCamObj = targetCamObj;
            }

            if (VirtualCamera1 != null && VirtualCamera1 != targetCamObj) VirtualCamera1.SetActive(false);
            if (VirtualCamera2 != null && VirtualCamera2 != targetCamObj) VirtualCamera2.SetActive(false);
            if (PlayerVirtualCamera != null && PlayerVirtualCamera != targetCamObj) PlayerVirtualCamera.SetActive(false);

            StartCoroutine(RebootBrainNextFrame());
        }

        private IEnumerator RebootBrainNextFrame()
        {
            yield return null;
            if (_cameraBrain != null) _cameraBrain.enabled = true;
        }

        private IEnumerator FadeWhiteScreen(float startAlpha, float endAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _whiteScreen.color = new Color(1, 1, 1, Mathf.Lerp(startAlpha, endAlpha, elapsed / duration));
                yield return null;
            }
            _whiteScreen.color = new Color(1, 1, 1, endAlpha);
        }

        private IEnumerator GlitchLoopRoutine()
        {
            if (_whiteScreen == null) yield break;

            while (_isGlitchingLoop)
            {
                if (_currentActiveCamObj != null)
                {
                    CinemachineCamera cm3Cam = _currentActiveCamObj.GetComponent<CinemachineCamera>();
                    if (Random.value < GlitchFrequency)
                    {
                        if (cm3Cam != null)
                        {
                            float baseFov = cm3Cam.Lens.FieldOfView;
                            cm3Cam.Lens.FieldOfView += Random.Range(-FovTwitchAmplitude, FovTwitchAmplitude);
                            _whiteScreen.color = GlitchFlashColor;

                            yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));

                            cm3Cam.Lens.FieldOfView = baseFov;
                            _whiteScreen.color = new Color(1, 1, 1, 0f);
                        }
                        else
                        {
                            _whiteScreen.color = GlitchFlashColor;
                            yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
                            _whiteScreen.color = new Color(1, 1, 1, 0f);
                        }
                    }
                }
                yield return new WaitForSeconds(Random.Range(0.05f, 0.3f));
            }
        }

        public float GetCurrentBuildRadius() { return _currentBuildRadius; }
    }
}
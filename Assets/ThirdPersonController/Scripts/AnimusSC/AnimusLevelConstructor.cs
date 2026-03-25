using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;



namespace TheGlitch
{
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class AnimusLevelConstructor : MonoBehaviour
    {
        [Header("🎥 Cinemachine 镜头组")]
        public GameObject VirtualCamera1;
        public GameObject VirtualCamera2;
        public GameObject PlayerVirtualCamera;

        [Header("🏗️ 空间大小与扫描控制 (解决识别过大)")]
        [Tooltip("扫描波从哪个点开始扩散？")]
        public Transform BuildOrigin;
        public Transform LevelParent;
        [Space(5)]
        [Tooltip("【👑 核心修复】：如果自动算出来的范围太大，取消勾选这个！")]
        public bool AutoCalculateSize = true;
        [Tooltip("手动指定的场景最大半径 (米)")]
        public float ManualMaxRadius = 80f;
        [Space(5)]
        [Tooltip("【👑 独立扩散速度】：扫描波每秒往外扩散多少米？")]
        [Range(5f, 200f)] public float BuildWaveSpeed = 25f;

        [Header("🎬 镜头切换时间 (与速度彻底解绑)")]
        [Tooltip("开场死白消散后，等待多久开始扫描？")]
        public float StartBuildDelay = 0.5f;
        [Tooltip("【👑 独立时间】：镜头 1 (全景) 持续展示几秒后切走？")]
        public float Camera1Duration = 3.0f;
        [Tooltip("【👑 独立时间】：镜头 2 (近景) 持续展示几秒后切回玩家？")]
        public float Camera2Duration = 3.0f;

        [Header("✨ 视觉特效与闪回")]
        public bool ShowDiffusionSphere = true;
        public Color SphereColor = new Color(0.1f, 0.4f, 1.0f, 0.3f);
        public float IntroWhiteFadeDuration = 1.5f;
        public float FlashHalfDuration = 0.15f;
        [Range(0.1f, 1.0f)] public float MaxFlashAlpha = 0.6f;

        public MonoBehaviour[] PlayerScriptsToFreeze;

        private float _maxDistance = 0f;
        private float _currentBuildRadius = 0f;
        private bool _isBuilding = false;

        private Image _whiteScreen;
        private CinemachineImpulseSource _impulseSource;
        private GameObject _diffusionSphere;

        private void Start()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();

            SetupWhiteScreenUI();
            InitializeLevelRadius();
            SetupDiffusionSphereVisual();

            StartCoroutine(PlayIntroSequence());
        }

        private void SetupWhiteScreenUI()
        {
            GameObject canvasObj = new GameObject("AnimusFlashCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            GameObject imageObj = new GameObject("WhiteImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            _whiteScreen = imageObj.AddComponent<Image>();
            _whiteScreen.color = new Color(1, 1, 1, 0);

            RectTransform rect = _whiteScreen.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        }

        private void InitializeLevelRadius()
        {
            if (AutoCalculateSize && LevelParent != null && BuildOrigin != null)
            {
                _maxDistance = 0f;
                MeshRenderer[] renderers = LevelParent.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    // 更精确地计算边界最大值
                    float dist1 = Vector3.Distance(BuildOrigin.position, renderer.bounds.min);
                    float dist2 = Vector3.Distance(BuildOrigin.position, renderer.bounds.max);
                    float maxD = Mathf.Max(dist1, dist2);
                    if (maxD > _maxDistance) _maxDistance = maxD;
                }
                _maxDistance *= 1.1f;
            }
            else
            {
                // 直接采用你手动填写的数值！
                _maxDistance = ManualMaxRadius;
            }

            // 强制将所有物理模型显示，交由 Shader 裁剪
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

            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, IntroWhiteFadeDuration));
            yield return new WaitForSeconds(StartBuildDelay);

            // 【👑 逻辑解绑】：只管开始构建，不在此处控制速度
            _isBuilding = true;

            // 【镜头 1】：纯粹基于时间等待
            yield return new WaitForSeconds(Camera1Duration);

            // 【柔和闪回 1】
            yield return StartCoroutine(FadeWhiteScreen(0f, MaxFlashAlpha, FlashHalfDuration));

            SwitchToCamera(VirtualCamera2);
            if (_impulseSource != null) _impulseSource.GenerateImpulse();

            CyberspaceEnvironment env = FindObjectOfType<CyberspaceEnvironment>();
            if (env != null) env.TriggerDataBurst();

            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, FlashHalfDuration));

            // 【镜头 2】：纯粹基于时间等待
            yield return new WaitForSeconds(Camera2Duration);

            // 【柔和闪回 2：切回玩家】
            yield return StartCoroutine(FadeWhiteScreen(0f, MaxFlashAlpha, FlashHalfDuration));

            SwitchToCamera(PlayerVirtualCamera);
            _isBuilding = false;

            if (_impulseSource != null) _impulseSource.GenerateImpulse();
            yield return StartCoroutine(FadeWhiteScreen(MaxFlashAlpha, 0f, FlashHalfDuration));

            if (_diffusionSphere != null) Destroy(_diffusionSphere);

            // 演出结束，强制将半径设为极大，保证全部显现
            Shader.SetGlobalFloat("_GlobalAnimusRadius", 99999f);

            foreach (var script in PlayerScriptsToFreeze) { if (script != null) script.enabled = true; }
        }

        private void Update()
        {
            if (_isBuilding && BuildOrigin != null)
            {
                // 【👑 逻辑解绑】：扫描波按固定速度平滑扩散，跟镜头等了多少秒毫无关系！
                _currentBuildRadius += BuildWaveSpeed * Time.deltaTime;

                // 限制最大扩散范围
                if (_currentBuildRadius > _maxDistance) _currentBuildRadius = _maxDistance;

                if (_diffusionSphere != null)
                    _diffusionSphere.transform.localScale = Vector3.one * (_currentBuildRadius * 2f);

                Shader.SetGlobalFloat("_GlobalAnimusRadius", _currentBuildRadius);
                Shader.SetGlobalVector("_GlobalAnimusOrigin", BuildOrigin.position);
            }
        }

        private void SwitchToCamera(GameObject targetCam)
        {
            if (VirtualCamera1 != null) VirtualCamera1.SetActive(false);
            if (VirtualCamera2 != null) VirtualCamera2.SetActive(false);
            if (PlayerVirtualCamera != null) PlayerVirtualCamera.SetActive(false);
            if (targetCam != null) targetCam.SetActive(true);
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

        public float GetCurrentBuildRadius() { return _currentBuildRadius; }
    }
}
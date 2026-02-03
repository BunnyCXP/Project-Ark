using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheGlitch
{
    public class WorldFXController : MonoBehaviour
    {
        public static WorldFXController Instance { get; private set; }

        [Header("PostProcess Volume")]
        public Volume GlobalVolume;

        private ColorAdjustments _color;
        private FilmGrain _grain;
        private Vignette _vignette;
        private ChromaticAberration _chromatic;
        private LensDistortion _lens;

        // 当前模式的基准值
        private float _baseSaturation;
        private float _baseGrain;
        private float _baseExposure;

        private Coroutine _fxBlendCo;
        private Coroutine _noiseCo;

        [Header("Camera Glitch")]
        [Tooltip("要抖动的相机 Transform（一般拖 Main Camera 或其父节点）")]
        public Transform CameraRoot;

        [Tooltip("画面撕裂时的横向抖动幅度")]
        public float PosJitterX = 0.06f;

        [Tooltip("画面撕裂时的竖向抖动幅度")]
        public float PosJitterY = 0.03f;

        [Tooltip("画面撕裂时的 Z 轴旋转（度）")]
        public float RotJitterZ = 4f;

        private Vector3 _camBasePos;
        private Quaternion _camBaseRot;
        private bool _camBaseCaptured = false;

        private void Awake()
        {
            Instance = this;

            if (GlobalVolume != null && GlobalVolume.profile != null)
            {
                GlobalVolume.profile.TryGet(out _color);
                GlobalVolume.profile.TryGet(out _grain);
                GlobalVolume.profile.TryGet(out _vignette);
                GlobalVolume.profile.TryGet(out _chromatic);
                GlobalVolume.profile.TryGet(out _lens);
            }

            if (CameraRoot != null)
            {
                _camBasePos = CameraRoot.localPosition;
                _camBaseRot = CameraRoot.localRotation;
                _camBaseCaptured = true;
            }
        }

        private void OnDisable()
        {
            // 避免编辑器停播后相机姿势留在抖动状态
            RestoreCameraTransform();
        }

        // ========== 模式切换 ==========

        public void SetNormal(float blendTime = 0.18f)
        {
            BlendTo(
                saturation: 0f,
                contrast: 0f,
                exposure: 0f,
                grain: 0f,
                grainResponse: 0.8f,
                vignette: 0f,
                chromaIntensity: 0f,
                lensIntensity: 0f,
                blendTime: blendTime
            );
        }

        public void SetScanMode(float blendTime = 0.18f)
        {
            BlendTo(
                saturation: -100f,
                contrast: 5f,
                exposure: -0.35f,
                grain: 0.45f,
                grainResponse: 0.8f,
                vignette: 0.18f,
                chromaIntensity: 0.1f,
                lensIntensity: 0.05f,
                blendTime: blendTime
            );
        }

        public void SetHackMode(float blendTime = 0.22f)
        {
            BlendTo(
                saturation: -100f,
                contrast: 12f,
                exposure: -0.55f,
                grain: 0.7f,
                grainResponse: 1.0f,
                vignette: 0.35f,
                chromaIntensity: 0.18f,
                lensIntensity: 0.08f,
                blendTime: blendTime
            );
        }

        private void BlendTo(
            float saturation,
            float contrast,
            float exposure,
            float grain,
            float grainResponse,
            float vignette,
            float chromaIntensity,
            float lensIntensity,
            float blendTime
        )
        {
            if (_fxBlendCo != null)
                StopCoroutine(_fxBlendCo);

            _fxBlendCo = StartCoroutine(BlendFxCo(
                saturation,
                contrast,
                exposure,
                grain,
                grainResponse,
                vignette,
                chromaIntensity,
                lensIntensity,
                blendTime
            ));
        }

        private IEnumerator BlendFxCo(
            float targetSat,
            float targetContrast,
            float targetExposure,
            float targetGrain,
            float targetGrainResp,
            float targetVignette,
            float targetChroma,
            float targetLens,
            float duration
        )
        {
            duration = Mathf.Max(0.01f, duration);

            float startSat = _color ? _color.saturation.value : 0f;
            float startContrast = _color ? _color.contrast.value : 0f;
            float startExposure = _color ? _color.postExposure.value : 0f;
            float startGrain = _grain ? _grain.intensity.value : 0f;
            float startGrainResp = _grain ? _grain.response.value : 0f;
            float startVignette = _vignette ? _vignette.intensity.value : 0f;
            float startChroma = _chromatic ? _chromatic.intensity.value : 0f;
            float startLens = _lens ? _lens.intensity.value : 0f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                if (_color)
                {
                    _color.saturation.value = Mathf.Lerp(startSat, targetSat, k);
                    _color.contrast.value = Mathf.Lerp(startContrast, targetContrast, k);
                    _color.postExposure.value = Mathf.Lerp(startExposure, targetExposure, k);
                }

                if (_grain)
                {
                    _grain.intensity.value = Mathf.Lerp(startGrain, targetGrain, k);
                    _grain.response.value = Mathf.Lerp(startGrainResp, targetGrainResp, k);
                }

                if (_vignette)
                {
                    _vignette.intensity.value = Mathf.Lerp(startVignette, targetVignette, k);
                    _vignette.smoothness.value = 0.85f;
                }

                if (_chromatic)
                {
                    _chromatic.intensity.value = Mathf.Lerp(startChroma, targetChroma, k);
                }

                if (_lens)
                {
                    _lens.intensity.value = Mathf.Lerp(startLens, targetLens, k);
                }

                yield return null;
            }

            if (_color)
            {
                _color.saturation.value = targetSat;
                _color.contrast.value = targetContrast;
                _color.postExposure.value = targetExposure;
            }

            if (_grain)
            {
                _grain.intensity.value = targetGrain;
                _grain.response.value = targetGrainResp;
            }

            if (_vignette)
                _vignette.intensity.value = targetVignette;

            if (_chromatic)
                _chromatic.intensity.value = targetChroma;

            if (_lens)
                _lens.intensity.value = targetLens;

            _baseSaturation = targetSat;
            _baseGrain = targetGrain;
            _baseExposure = targetExposure;

            _fxBlendCo = null;
        }

        // ========== 轻微噪点冲击 ==========

        public void PlayNoiseKick(float duration = 0.25f, float peakIntensity = 1.0f)
        {
            if (_grain == null) return;

            if (_noiseCo != null)
                StopCoroutine(_noiseCo);

            _noiseCo = StartCoroutine(NoiseKickCo(duration, peakIntensity));
        }

        private IEnumerator NoiseKickCo(float duration, float peakIntensity)
        {
            duration = Mathf.Max(0.01f, duration);
            float t = 0f;

            float startGrain = _grain.intensity.value;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                float up = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 2f));
                float down = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (k - 0.5f) * 2f));

                float upVal = Mathf.Lerp(startGrain, peakIntensity, up);
                float downVal = Mathf.Lerp(peakIntensity, _baseGrain, down);
                float current = Mathf.Lerp(upVal, downVal, down);

                _grain.intensity.value = current;

                yield return null;
            }

            _grain.intensity.value = _baseGrain;
            _noiseCo = null;
        }

        // ========== 强 Glitch（Ghost 用） ==========

        public void PlayGlitchKick(
            float duration = 0.3f,
            float peakGrain = 1.3f,
            float exposureJitter = 0.4f
        )
        {
            if (_grain == null || _color == null) return;

            if (_noiseCo != null)
                StopCoroutine(_noiseCo);

            _noiseCo = StartCoroutine(GlitchKickCo(duration, peakGrain, exposureJitter));
        }

        private IEnumerator GlitchKickCo(float duration, float peakGrain, float exposureJitter)
        {
            duration = Mathf.Max(0.01f, duration);

            float t = 0f;
            float startGrain = _grain.intensity.value;
            float startExposure = _color.postExposure.value;

            // 记录相机原始姿势
            CaptureCameraBase();

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float inv = 1f - k;

                // 1) 粒子强度随机抖到 peakGrain 附近
                float rand = Random.Range(0.4f, 1.0f);
                float targetGrain = Mathf.Lerp(_baseGrain, peakGrain, rand * inv);
                _grain.intensity.value = targetGrain;

                // 2) 曝光上下 jitter，像亮度“撕裂”
                float jitterExp = Random.Range(-exposureJitter, exposureJitter) * inv;
                _color.postExposure.value = startExposure + jitterExp;

                // 3) 可选：色差和镜头扭曲也抖一抖
                if (_chromatic != null)
                {
                    _chromatic.intensity.value = Mathf.Lerp(0f, 0.4f, inv);
                }

                if (_lens != null)
                {
                    _lens.intensity.value = Mathf.Lerp(0f, 0.12f, inv);
                }

                // 4) 相机的屏幕空间抖动：横向为主 + 少量 Z 轴旋转 → 时间撕裂感
                if (CameraRoot != null && _camBaseCaptured)
                {
                    float offX = Random.Range(-PosJitterX, PosJitterX) * inv;
                    float offY = Random.Range(-PosJitterY, PosJitterY) * inv;

                    CameraRoot.localPosition = _camBasePos + new Vector3(offX, offY, 0f);

                    float rotZ = Random.Range(-RotJitterZ, RotJitterZ) * inv;
                    CameraRoot.localRotation =
                        Quaternion.Euler(_camBaseRot.eulerAngles + new Vector3(0f, 0f, rotZ));
                }

                yield return null;
            }

            // 复位
            _grain.intensity.value = _baseGrain;
            _color.postExposure.value = _baseExposure;

            if (_chromatic != null) _chromatic.intensity.value = 0f;
            if (_lens != null) _lens.intensity.value = 0f;

            RestoreCameraTransform();

            _noiseCo = null;
        }

        private void CaptureCameraBase()
        {
            if (CameraRoot == null) return;

            if (!_camBaseCaptured)
            {
                _camBasePos = CameraRoot.localPosition;
                _camBaseRot = CameraRoot.localRotation;
                _camBaseCaptured = true;
            }
        }

        private void RestoreCameraTransform()
        {
            if (CameraRoot == null || !_camBaseCaptured) return;

            CameraRoot.localPosition = _camBasePos;
            CameraRoot.localRotation = _camBaseRot;
        }
    }
}

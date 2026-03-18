using System.Collections;
using StarterAssets;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheGlitch
{
    [System.Serializable]
    public class P2_StageSet
    {
        public Transform PlayerStage;
    }

    public class MainframeBossManager : MonoBehaviour
    {
        // 【核心修改】：加入了第三阶段的所有电影化互动状态！
        private enum BossPhase
        {
            Waiting, Intro, P1_Dodging, P1_QTE, Transitioning,
            Phase2_Idle, Phase2_LaserAiming, Phase2_LaserFiring, Phase2_Stunned,
            Phase3_Cinematic, Phase3_Walk1, Phase3_Walk2, Phase3_Walk3, Phase3_Execution, Dead
        }
        private BossPhase _currentPhase = BossPhase.Waiting;

        public enum FirePattern { Simultaneous, Alternating, Random }
        public enum DanmakuSourceType { SpecificTransforms, RandomInArea, PlayerPosition }

        [Header("基础与场景设置")]
        public GameObject BossDoor;
        public Transform DoorClosedPos;
        public CinemachineCamera IntroDoorCamera;
        public CinemachineCamera P1_TopCamera;
        public CinemachineCamera QTE_CloseUpCamera;

        private CinemachineBrain _mainCameraBrain;
        private GameObject _player;
        private SimplePlayerLife _playerLife;
        private Animator _playerAnimator;
        private ThirdPersonController _thirdPerson;
        private StarterAssetsInputs _starterInputs;

        [Header("BGM 音乐设置")]
        public AudioClip Phase1_BGM;
        public AudioClip Phase2_BGM;
        public float BGMFadeDuration = 1.5f;
        [Range(0f, 1f)] public float MaxBGMVolume = 0.5f;
        private AudioSource[] _bgmSources = new AudioSource[2];
        private int _currentBgmSourceIndex = 0;
        private Coroutine _bgmCrossfadeRoutine;

        [Header("第一阶段：系统清洗 (静态红圈)")]
        public GameObject AoEWarningPrefab;
        public Transform[] BombLocations;
        public float WarningTime = 1.5f;
        public int BombRounds = 3;
        public int BombsPerRound = 3;
        public float BombRadius = 2.2f;
        public int BombDamage = 1;

        [Header("Phase 1 - 弱点与摄像机设置")]
        public GameObject[] P1_WeakPoints;
        public Transform[] P1_WeakPointCamAnchors;
        public Material WeakPointExposedMat;

        [Header("第一阶段：动态弹幕设置")]
        public GameObject ProjectilePrefab;
        public DanmakuSourceType ProjectileSourceType = DanmakuSourceType.SpecificTransforms;
        public Transform[] ProjectileSources;
        public BoxCollider ProjectileSpawnArea;
        public float PlayerYOffset = 0f;
        [Space(10)]
        public FirePattern P1_FirePattern = FirePattern.Alternating;
        [Range(0.1f, 2f)] public float ProjectileFireRate = 0.4f;
        [Range(1f, 20f)] public float ProjectileSpeed = 10f;

        [Header("P1 QTE：狂按突破")]
        public float P1_QTESlowMo = 0.08f;
        public float P1_GaugeMax = 100f;
        public float P1_GaugeDrainPerSecond = 20f;
        public float P1_GaugeGainPerPress = 15f;

        [Header("第二阶段：演出 1 - 护甲掉落")]
        public P2_StageSet Phase2Settings;
        public CinemachineCamera P2_ArmorDropCamera;
        public GameObject BossArmorParent;

        [Header("第二阶段：演出 2 - 场景重构(柱子)")]
        public CinemachineCamera P2_PillarPanCamera;
        public Transform PillarPanStartPos;
        public Transform PillarPanEndPos;
        public GameObject[] P2_Pillars;
        public float PillarRiseHeight = 15f;
        public float PillarRiseDuration = 4f;

        [Header("第二阶段：战斗视角与机制")]
        public CinemachineCamera P2_DynamicLockCamera;
        public Transform BossCore;

        [Header("--- 死亡扫描激光设置 ---")]
        public LineRenderer P2_LaserRenderer;
        public ParticleSystem LaserChargeFX; // <--- 新增的蓄力吸收粒子
        public float P2_LaserAimTime = 3f;
        public float P2_LaserFireTime = 0.15f;
        public float P2_LaserCooldown = 2f;
        public LayerMask P2_LaserHitMask = ~0;

        [Header("--- 逆向木马骇客机制 ---")]
        public Material PillarNormalMat;
        public Material PillarExposedMat;
        public Material PillarHackedMat;

        public float P2_HackRange = 4.0f;
        public float P2_HackTimeRequired = 2.0f;
        public float P2_StunDuration = 5.0f;

        private enum PillarState { Normal, Exposed, Hacked }
        private PillarState[] _pillarStates;
        private float _p2HackGauge = 0f;

        // ==========================================
        // 【新增】：第三阶段 电影化终极处决运镜
        // ==========================================
        [Header("第三阶段：终极处决演出")]
        [Tooltip("P3开始时，玩家被强行拉回的起始位置（建议离核心远一点）")]
        public Transform P3_PlayerStartPos;  // <--- 【加上这一行】
        [Header("第三阶段：风暴粒子特效")]
        [Tooltip("四周狂乱飞舞的狂风/速度线特效")]
        public ParticleSystem GlobalWindParticles;
        [Tooltip("核心向外喷射的能量粒子")]
        public ParticleSystem CoreEnergyParticles;

        [Tooltip("看核心升空发波的专属相机")]
        public CinemachineCamera P3_CoreAscendCamera;
        [Tooltip("核心最终悬浮的位置")]
        public Transform P3_CoreAscendPos;

        [Space(10)]
        public CinemachineCamera P3_WalkCamera_1;
        [Tooltip("按第一次 E 后玩家走到的位置")]
        public Transform P3_WalkTarget_1;

        public CinemachineCamera P3_WalkCamera_2;
        [Tooltip("按第二次 E 后玩家走到的位置")]
        public Transform P3_WalkTarget_2;

        public CinemachineCamera P3_WalkCamera_3;
        [Tooltip("按第三次 E 后玩家走到的核心脚下位置")]
        public Transform P3_WalkTarget_3;

        [Space(10)]
        [Tooltip("最后处决时的特写相机")]
        public CinemachineCamera P3_ExecutionCamera;
        [Tooltip("需要长按 E 多少秒才能彻底销毁")]
        public float P3_ExecutionTimeRequired = 3.0f;

        [Header("UI 设置")]
        public GameObject WarningTextUI;
        public GameObject QTE_PromptTextUI;
        public TextMeshProUGUI QTE_TextComponent;
        public Slider QTE_ProgressBar;
        [Tooltip("长按E时的能量虹吸特效")]
        public ParticleSystem SiphonParticles;

        [Tooltip("你刚做的声波圆柱材质")]
        public Material PulseWaveMat; // <--- 加上这一行
        // 用于实时追踪电磁波的物理半径
        private float _currentPulseRadius = -1f;
        private bool _pulseHandledThisRound = false;

        private Material[] _wpOriginalMats;
        private int _currentWeakPointIndex = 0;
        private float _p1Gauge = 0f;

        private Vector3 _qteUIPromptOriginalScale;
        private Vector3 _qteUIBarOriginalScale;
        private Vector3 _currentUIPromptScale;
        private Vector3 _currentUIBarScale;

        private Transform _dynamicCamAnchor;

        private float _p2StateTimer = 0f;
        private Vector3 _lockedLaserTargetPos;
        private bool _hasLaserDamagedPlayer = false;

        private void Awake()
        {
            if (P1_GaugeDrainPerSecond <= 0f) P1_GaugeDrainPerSecond = 20f;
            if (P1_GaugeGainPerPress <= 0f) P1_GaugeGainPerPress = 15f;

            for (int i = 0; i < 2; i++)
            {
                _bgmSources[i] = gameObject.AddComponent<AudioSource>();
                _bgmSources[i].loop = true;
                _bgmSources[i].playOnAwake = false;
                _bgmSources[i].volume = 0f;
                _bgmSources[i].spatialBlend = 0f;
            }
        }

        private void Start()
        {
            InitUI();
            CacheWeakPointMaterials();

            if (QTE_PromptTextUI != null) _qteUIPromptOriginalScale = QTE_PromptTextUI.transform.localScale;
            if (QTE_ProgressBar != null) _qteUIBarOriginalScale = QTE_ProgressBar.transform.localScale;

            if (Camera.main != null) _mainCameraBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (_mainCameraBrain == null) _mainCameraBrain = Object.FindFirstObjectByType<CinemachineBrain>();

            // 初始化所有相机权限
            if (IntroDoorCamera) IntroDoorCamera.Priority = 0;
            if (P1_TopCamera) P1_TopCamera.Priority = 10;
            if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 0;
            if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 0;
            if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 0;
            if (P2_DynamicLockCamera) P2_DynamicLockCamera.Priority = 0;
            if (P3_CoreAscendCamera) P3_CoreAscendCamera.Priority = 0;
            if (P3_WalkCamera_1) P3_WalkCamera_1.Priority = 0;
            if (P3_WalkCamera_2) P3_WalkCamera_2.Priority = 0;
            if (P3_WalkCamera_3) P3_WalkCamera_3.Priority = 0;
            if (P3_ExecutionCamera) P3_ExecutionCamera.Priority = 0;

            _dynamicCamAnchor = new GameObject("DynamicQTECamAnchor").transform;
            _dynamicCamAnchor.SetParent(this.transform);

            if (P2_LaserRenderer != null) P2_LaserRenderer.gameObject.SetActive(false);

            Time.timeScale = 1f;
        }

        private void OnDisable() => Time.timeScale = 1f;

        private void InitUI()
        {
            if (WarningTextUI) WarningTextUI.SetActive(false);
            if (QTE_PromptTextUI) QTE_PromptTextUI.SetActive(false);
            if (QTE_ProgressBar) QTE_ProgressBar.gameObject.SetActive(false);
        }

        private void CacheWeakPointMaterials()
        {
            if (P1_WeakPoints == null) return;
            _wpOriginalMats = new Material[P1_WeakPoints.Length];
            for (int i = 0; i < P1_WeakPoints.Length; i++)
            {
                Renderer r = GetWeakPointRenderer(i);
                if (r != null) _wpOriginalMats[i] = r.material;
            }
        }

        private Renderer GetWeakPointRenderer(int index)
        {
            if (index < 0 || index >= P1_WeakPoints.Length) return null;
            if (P1_WeakPoints[index] == null) return null;
            return P1_WeakPoints[index].GetComponentInChildren<Renderer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_currentPhase != BossPhase.Waiting) return;
            if (!other.CompareTag("Player")) return;

            _currentPhase = BossPhase.Intro;

            _player = other.transform.root.gameObject;
            _playerLife = _player.GetComponent<SimplePlayerLife>();
            if (_playerLife != null) _playerLife.IsInBossRoom = true;

            _playerAnimator = _player.GetComponentInChildren<Animator>();
            _thirdPerson = _player.GetComponent<ThirdPersonController>();
            _starterInputs = _player.GetComponent<StarterAssetsInputs>();

            LockAndDisableCustomAbilities();
            DisablePlayerAbilities(true);

            StartCoroutine(IntroRoutine());
        }

        private void LockAndDisableCustomAbilities()
        {
            if (_player == null) return;
            MonoBehaviour[] allScripts = _player.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in allScripts)
            {
                if (script == null) continue;
                string scriptName = script.GetType().Name;
                if (scriptName.Contains("Scanner") || scriptName.Contains("Echo") || scriptName.Contains("Rewind"))
                {
                    script.enabled = false;
                }
            }
        }

        #region BGM Management 
        private void PlayBGM(AudioClip newClip)
        {
            if (newClip == null) return;
            if (_bgmSources[_currentBgmSourceIndex].clip == newClip && _bgmSources[_currentBgmSourceIndex].isPlaying) return;

            if (_bgmCrossfadeRoutine != null) StopCoroutine(_bgmCrossfadeRoutine);
            _bgmCrossfadeRoutine = StartCoroutine(CrossfadeBGM(newClip));
        }

        private IEnumerator CrossfadeBGM(AudioClip newClip)
        {
            AudioSource activeSource = _bgmSources[_currentBgmSourceIndex];
            int nextIndex = 1 - _currentBgmSourceIndex;
            AudioSource nextSource = _bgmSources[nextIndex];

            nextSource.clip = newClip;
            nextSource.volume = 0f;
            nextSource.Play();

            float t = 0;
            while (t < BGMFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float fraction = t / BGMFadeDuration;
                if (activeSource.isPlaying) activeSource.volume = Mathf.Lerp(MaxBGMVolume, 0f, fraction);
                nextSource.volume = Mathf.Lerp(0f, MaxBGMVolume, fraction);
                yield return null;
            }

            activeSource.volume = 0f;
            activeSource.Stop();
            nextSource.volume = MaxBGMVolume;
            _currentBgmSourceIndex = nextIndex;
        }

        // =====================================
        // 【把下面这段补回来！】
        // =====================================
        private void StopAllBGM()
        {
            if (_bgmCrossfadeRoutine != null) StopCoroutine(_bgmCrossfadeRoutine);
            _bgmCrossfadeRoutine = StartCoroutine(FadeOutAllBGM());
        }

        private IEnumerator FadeOutAllBGM()
        {
            float t = 0;
            float startVol0 = _bgmSources[0].volume;
            float startVol1 = _bgmSources[1].volume;

            while (t < BGMFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float fraction = t / BGMFadeDuration;
                _bgmSources[0].volume = Mathf.Lerp(startVol0, 0f, fraction);
                _bgmSources[1].volume = Mathf.Lerp(startVol1, 0f, fraction);
                yield return null;
            }
            _bgmSources[0].Stop();
            _bgmSources[1].Stop();
        }
        // =====================================
        #endregion

        #region Intro + Phase 1 (省略不必要的内容，保持不变)
        private IEnumerator IntroRoutine()
        {
            CinemachineBlendDefinition oldBlend = default;
            if (_mainCameraBrain != null) oldBlend = _mainCameraBrain.DefaultBlend;

            if (BossDoor != null && DoorClosedPos != null)
            {
                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.DoFadeAndAction(() =>
                    {
                        PlayBGM(Phase1_BGM);
                        if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                        if (IntroDoorCamera != null) IntroDoorCamera.Priority = 200;
                        if (P1_TopCamera != null) P1_TopCamera.Priority = 0;
                    });
                    yield return new WaitForSeconds(1.0f);
                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
                }
                else
                {
                    PlayBGM(Phase1_BGM);
                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                    if (IntroDoorCamera != null) IntroDoorCamera.Priority = 200;
                    if (P1_TopCamera != null) P1_TopCamera.Priority = 0;
                    yield return new WaitForSeconds(0.5f);
                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
                }

                yield return StartCoroutine(AnimateDoorClosing());
                yield return new WaitForSeconds(1.0f);

                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.DoFadeAndAction(() =>
                    {
                        if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                        if (IntroDoorCamera != null) IntroDoorCamera.Priority = 0;
                        if (P1_TopCamera != null) P1_TopCamera.Priority = 100;
                        if (_playerLife != null) _playerLife.ShowPlayerHealthBar();
                    });
                    yield return new WaitForSeconds(1.0f);
                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
                }
            }
            else
            {
                PlayBGM(Phase1_BGM);
                if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                if (P1_TopCamera) P1_TopCamera.Priority = 100;
                if (_playerLife != null) _playerLife.ShowPlayerHealthBar();
                yield return new WaitForSeconds(0.5f);
                if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
            }

            if (WarningTextUI) WarningTextUI.SetActive(true);
            yield return new WaitForSeconds(2.0f);
            if (WarningTextUI) WarningTextUI.SetActive(false);

            DisablePlayerAbilities(false);
            StartCoroutine(P1_DodgingRoutine());
        }

        private IEnumerator AnimateDoorClosing()
        {
            Vector3 startPos = BossDoor.transform.position;
            Vector3 endPos = DoorClosedPos.position;
            float duration = 0.45f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / duration;
                float gravityCurve = progress * progress * progress;
                BossDoor.transform.position = Vector3.Lerp(startPos, endPos, gravityCurve);
                yield return null;
            }
            BossDoor.transform.position = endPos;
            RawCameraShake.Shake(0.5f, 0.35f);
        }

        private IEnumerator P1_DodgingRoutine()
        {
            _currentPhase = BossPhase.P1_Dodging;
            DisablePlayerAbilities(false);
            StartCoroutine(P1_BulletHellRoutine());

            for (int round = 0; round < BombRounds; round++)
            {
                if (_currentPhase != BossPhase.P1_Dodging) break;
                if (BombLocations != null && BombLocations.Length > 0)
                {
                    System.Collections.Generic.List<Transform> availableSpots = new System.Collections.Generic.List<Transform>(BombLocations);
                    int bombsThisRound = Mathf.Min(BombsPerRound, availableSpots.Count);
                    for (int i = 0; i < bombsThisRound; i++)
                    {
                        int randomIndex = Random.Range(0, availableSpots.Count);
                        Transform spawnPoint = availableSpots[randomIndex];
                        if (spawnPoint != null) StartCoroutine(SpawnAoE(spawnPoint.position));
                        availableSpots.RemoveAt(randomIndex);
                    }
                }
                yield return new WaitForSeconds(WarningTime + 0.7f);
            }

            _currentPhase = BossPhase.Transitioning;
            if (_currentWeakPointIndex < P1_WeakPoints.Length) yield return StartCoroutine(BeginP1WeakPointQTE());
        }

        private IEnumerator P1_BulletHellRoutine()
        {
            int sourceIndex = 0;
            while (_currentPhase == BossPhase.P1_Dodging)
            {
                if (ProjectilePrefab == null) yield break;
                if (P1_FirePattern == FirePattern.Simultaneous)
                {
                    if (ProjectileSourceType == DanmakuSourceType.SpecificTransforms && ProjectileSources != null)
                    {
                        foreach (Transform source in ProjectileSources) if (source != null) FireBullet(source.position, source.rotation);
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++) FireBullet(GetSpawnPosition(), Quaternion.identity);
                    }
                    yield return new WaitForSeconds(ProjectileFireRate);
                }
                else if (P1_FirePattern == FirePattern.Alternating)
                {
                    if (ProjectileSourceType == DanmakuSourceType.SpecificTransforms && ProjectileSources != null && ProjectileSources.Length > 0)
                    {
                        Transform source = ProjectileSources[sourceIndex % ProjectileSources.Length];
                        if (source != null) FireBullet(source.position, source.rotation);
                        sourceIndex++;
                        yield return new WaitForSeconds(ProjectileFireRate / ProjectileSources.Length);
                    }
                    else
                    {
                        FireBullet(GetSpawnPosition(), Quaternion.identity);
                        yield return new WaitForSeconds(ProjectileFireRate / 2f);
                    }
                }
            }
        }

        private Vector3 GetSpawnPosition()
        {
            switch (ProjectileSourceType)
            {
                case DanmakuSourceType.RandomInArea:
                    if (ProjectileSpawnArea != null)
                    {
                        Bounds bounds = ProjectileSpawnArea.bounds;
                        return new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z));
                    }
                    break;
                case DanmakuSourceType.PlayerPosition:
                    if (_player != null) return _player.transform.position + new Vector3(0, PlayerYOffset, 0);
                    break;
                case DanmakuSourceType.SpecificTransforms:
                    if (ProjectileSources != null && ProjectileSources.Length > 0) return ProjectileSources[Random.Range(0, ProjectileSources.Length)].position;
                    break;
            }
            return transform.position;
        }

        private void FireBullet(Vector3 position, Quaternion rotation)
        {
            if (ProjectileSourceType != DanmakuSourceType.SpecificTransforms && _player != null)
            {
                Vector3 direction = (_player.transform.position + Vector3.up * 1f) - position;
                rotation = Quaternion.LookRotation(direction);
            }
            GameObject bullet = Instantiate(ProjectilePrefab, position, rotation);
            BossProjectile projScript = bullet.GetComponent<BossProjectile>();
            if (projScript != null) projScript.Speed = ProjectileSpeed;
        }

        private IEnumerator SpawnAoE(Vector3 position)
        {
            GameObject warning = null;
            Renderer warningRenderer = null;
            float targetDiameter = BombRadius * 2f;

            if (AoEWarningPrefab != null)
            {
                warning = Instantiate(AoEWarningPrefab, position, Quaternion.identity);
                warning.transform.localScale = new Vector3(0, 0.1f, 0);
                warningRenderer = warning.GetComponent<Renderer>();
                if (warningRenderer != null) warningRenderer.material.color = new Color(1f, 0f, 0f, 0.4f);
            }

            float t = 0f;
            while (t < WarningTime)
            {
                t += Time.deltaTime;
                float progress = t / WarningTime;
                if (warning != null)
                {
                    float currentSize = Mathf.Lerp(0f, targetDiameter, progress);
                    warning.transform.localScale = new Vector3(currentSize, 0.1f, currentSize);
                }
                yield return null;
            }

            if (warning != null)
            {
                warning.transform.localScale = new Vector3(targetDiameter, 0.1f, targetDiameter);
                if (warningRenderer != null) warningRenderer.material.color = Color.red;
            }

            ApplyBombDamage(position);
            yield return new WaitForSeconds(0.15f);
            if (warning != null) Destroy(warning);
        }

        private void ApplyBombDamage(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, BombRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                SimplePlayerLife life = hit.GetComponentInParent<SimplePlayerLife>();
                if (life == null) continue;
                life.TakeDamage(BombDamage);
                break;
            }
        }

        private IEnumerator BeginP1WeakPointQTE()
        {
            _currentPhase = BossPhase.Transitioning;
            GameObject targetWP = P1_WeakPoints[_currentWeakPointIndex];
            if (targetWP == null) yield break;

            Renderer r = GetWeakPointRenderer(_currentWeakPointIndex);
            if (r != null && WeakPointExposedMat != null) r.material = WeakPointExposedMat;

            Transform camTarget = targetWP.transform;
            if (P1_WeakPointCamAnchors != null && _currentWeakPointIndex < P1_WeakPointCamAnchors.Length && P1_WeakPointCamAnchors[_currentWeakPointIndex] != null)
            {
                camTarget = P1_WeakPointCamAnchors[_currentWeakPointIndex];
            }
            else
            {
                _dynamicCamAnchor.position = targetWP.transform.position + targetWP.transform.forward * 2.5f + Vector3.up * 1f;
                _dynamicCamAnchor.LookAt(targetWP.transform);
                camTarget = _dynamicCamAnchor;
            }

            yield return StartCoroutine(CinematicCameraPan(camTarget, 2.2f, P1_QTESlowMo));
            yield return new WaitForSecondsRealtime(0.15f);

            _p1Gauge = P1_GaugeMax * 0.5f;
            if (QTE_ProgressBar != null)
            {
                QTE_ProgressBar.gameObject.SetActive(true);
                QTE_ProgressBar.maxValue = P1_GaugeMax;
                QTE_ProgressBar.value = _p1Gauge;
            }

            ShowQTEPrompt("[E] MASH!");
            _currentUIPromptScale = _qteUIPromptOriginalScale;
            _currentUIBarScale = _qteUIBarOriginalScale;
            _currentPhase = BossPhase.P1_QTE;
        }

        private void UpdateP1QTE()
        {
            _p1Gauge -= P1_GaugeDrainPerSecond * Time.unscaledDeltaTime;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                _p1Gauge += P1_GaugeGainPerPress;
                ApplyUIPunch(_p1Gauge / P1_GaugeMax);
            }
            _p1Gauge = Mathf.Clamp(_p1Gauge, 0f, P1_GaugeMax);
            if (QTE_ProgressBar != null) QTE_ProgressBar.value = _p1Gauge;

            if (_p1Gauge >= P1_GaugeMax) ResolveP1WeakPoint(true);
            else if (_p1Gauge <= 0f) ResolveP1WeakPoint(false);
            RecoverUIPunch();
        }

        private void ApplyUIPunch(float gaugePercent)
        {
            float punchMultiplier = Mathf.Lerp(1.1f, 1.6f, gaugePercent);
            if (QTE_PromptTextUI != null)
            {
                _currentUIPromptScale = _qteUIPromptOriginalScale * punchMultiplier;
                QTE_PromptTextUI.transform.localScale = _currentUIPromptScale;
            }
            if (QTE_ProgressBar != null)
            {
                _currentUIBarScale = _qteUIBarOriginalScale * punchMultiplier;
                QTE_ProgressBar.transform.localScale = _currentUIBarScale;
            }
        }

        private void RecoverUIPunch()
        {
            if (QTE_PromptTextUI != null)
            {
                _currentUIPromptScale = Vector3.Lerp(_currentUIPromptScale, _qteUIPromptOriginalScale, Time.unscaledDeltaTime * 15f);
                QTE_PromptTextUI.transform.localScale = _currentUIPromptScale;
            }
            if (QTE_ProgressBar != null)
            {
                _currentUIBarScale = Vector3.Lerp(_currentUIBarScale, _qteUIBarOriginalScale, Time.unscaledDeltaTime * 15f);
                QTE_ProgressBar.transform.localScale = _currentUIBarScale;
            }
        }

        private void ResolveP1WeakPoint(bool success)
        {
            if (success)
            {
                if (P1_WeakPoints[_currentWeakPointIndex] != null) P1_WeakPoints[_currentWeakPointIndex].SetActive(false);
                _currentWeakPointIndex++;
                if (_currentWeakPointIndex >= P1_WeakPoints.Length) StartCoroutine(TransitionToPhase2());
                else StartCoroutine(ReturnToP1Dodging());
            }
            else
            {
                Renderer r = GetWeakPointRenderer(_currentWeakPointIndex);
                if (r != null && _wpOriginalMats[_currentWeakPointIndex] != null) r.material = _wpOriginalMats[_currentWeakPointIndex];
                if (_playerLife != null) _playerLife.TakeDamage(1);
                StartCoroutine(ReturnToP1Dodging());
            }
        }

        private IEnumerator ReturnToP1Dodging()
        {
            _currentPhase = BossPhase.Transitioning;
            ExitQTEState(true);
            yield return new WaitForSeconds(2.0f);
            DisablePlayerAbilities(false);
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(P1_DodgingRoutine());
        }
        #endregion

        #region Phase 2 (运镜过渡)
        private IEnumerator TransitionToPhase2()
        {
            _currentPhase = BossPhase.Transitioning;
            Time.timeScale = 1f;

            HideQTEPrompt();
            if (QTE_ProgressBar != null) QTE_ProgressBar.gameObject.SetActive(false);

            PlayBGM(Phase2_BGM);

            CinemachineBlendDefinition oldBlend = default;
            if (_mainCameraBrain != null) oldBlend = _mainCameraBrain.DefaultBlend;

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    ExitQTEState(true);
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 0;
                    if (P1_TopCamera) P1_TopCamera.Priority = 0;

                    if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 100;
                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                });
                yield return new WaitForSeconds(1.0f);
                if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
            }

            yield return new WaitForSeconds(0.5f);

            if (BossArmorParent != null)
            {
                Rigidbody[] armorPieces = BossArmorParent.GetComponentsInChildren<Rigidbody>();
                foreach (Rigidbody rb in armorPieces)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    Vector3 forceDir = (rb.transform.position - transform.position).normalized;
                    rb.AddForce(forceDir * 2f, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 1f, ForceMode.Impulse);
                }
                RawCameraShake.Shake(0.6f, 0.3f);
            }

            yield return new WaitForSeconds(2.5f);

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    if (BossArmorParent != null) BossArmorParent.SetActive(false);
                    if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 0;

                    if (Phase2Settings != null && Phase2Settings.PlayerStage != null && _player != null)
                    {
                        CharacterController cc = _player.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        _player.transform.position = Phase2Settings.PlayerStage.position;
                        _player.transform.rotation = Phase2Settings.PlayerStage.rotation;
                        if (cc != null) cc.enabled = true;
                    }

                    if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 100;
                    if (PillarPanStartPos && P2_PillarPanCamera)
                    {
                        P2_PillarPanCamera.transform.position = PillarPanStartPos.position;
                        P2_PillarPanCamera.transform.rotation = PillarPanStartPos.rotation;
                    }

                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                });
                yield return new WaitForSeconds(1.0f);
                if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
            }

            _pillarStates = new PillarState[P2_Pillars != null ? P2_Pillars.Length : 0];
            Vector3[] originalPillarPos = new Vector3[_pillarStates.Length];
            for (int i = 0; i < _pillarStates.Length; i++)
            {
                _pillarStates[i] = PillarState.Normal;
                if (P2_Pillars[i] != null)
                {
                    originalPillarPos[i] = P2_Pillars[i].transform.position;
                    ChangePillarMaterial(i, PillarNormalMat);
                }
            }
            float individualRiseTime = 1.5f;
            float delayBetweenPillars = (PillarRiseDuration - individualRiseTime) / Mathf.Max(1, _pillarStates.Length - 1);

            float panTimer = 0f;
            while (panTimer < PillarRiseDuration)
            {
                panTimer += Time.deltaTime;
                float camProgress = Mathf.SmoothStep(0, 1, panTimer / PillarRiseDuration);

                if (P2_PillarPanCamera && PillarPanStartPos && PillarPanEndPos)
                {
                    P2_PillarPanCamera.transform.position = Vector3.Lerp(PillarPanStartPos.position, PillarPanEndPos.position, camProgress);
                    P2_PillarPanCamera.transform.rotation = Quaternion.Lerp(PillarPanStartPos.rotation, PillarPanEndPos.rotation, camProgress);
                }

                for (int i = 0; i < originalPillarPos.Length; i++)
                {
                    if (P2_Pillars[i] != null)
                    {
                        float startTime = i * delayBetweenPillars;
                        float pillarProgress = Mathf.Clamp01((panTimer - startTime) / individualRiseTime);
                        float smoothPillarProgress = Mathf.SmoothStep(0, 1, pillarProgress);

                        P2_Pillars[i].transform.position = originalPillarPos[i] + Vector3.up * (PillarRiseHeight * smoothPillarProgress);
                    }
                }

                RawCameraShake.Shake(0.04f, 0.05f);
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 0;

                    if (_player != null && BossCore != null && _dynamicCamAnchor != null)
                    {
                        _dynamicCamAnchor.position = _player.transform.position;
                        Vector3 lookDir = BossCore.position - _player.transform.position;
                        lookDir.y = 0;
                        if (lookDir != Vector3.zero) _dynamicCamAnchor.rotation = Quaternion.LookRotation(lookDir);
                    }

                    if (P2_DynamicLockCamera)
                    {
                        if (_dynamicCamAnchor != null) P2_DynamicLockCamera.Follow = _dynamicCamAnchor;
                        if (BossCore != null) P2_DynamicLockCamera.LookAt = BossCore;

                        if (_dynamicCamAnchor != null)
                        {
                            P2_DynamicLockCamera.transform.position = _dynamicCamAnchor.position;
                            P2_DynamicLockCamera.transform.rotation = _dynamicCamAnchor.rotation;
                        }

                        P2_DynamicLockCamera.PreviousStateIsValid = false;
                        P2_DynamicLockCamera.Priority = 100;
                    }

                    if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;
                });
                yield return new WaitForSeconds(1.0f);
            }

            DisablePlayerAbilities(false);

            _p2StateTimer = 2f;
            _currentPhase = BossPhase.Phase2_Idle;
            Debug.Log("<color=green>【P2 演出结束】玩家已就位，木马协议准备启动！</color>");
        }
        #endregion

        #region Update (含所有机制循环)
        private void Update()
        {
            if (_currentPhase == BossPhase.Phase2_Idle || _currentPhase == BossPhase.Phase2_LaserAiming || _currentPhase == BossPhase.Phase2_LaserFiring)
            {
                HandleP2Hacking();
            }

            switch (_currentPhase)
            {
                case BossPhase.P1_QTE:
                    UpdateP1QTE();
                    break;

                case BossPhase.Phase2_Idle:
                    MaintainLockOnCamera();

                    _p2StateTimer -= Time.deltaTime;
                    if (_p2StateTimer <= 0f)
                    {
                        _p2StateTimer = P2_LaserAimTime;
                        _currentPhase = BossPhase.Phase2_LaserAiming;

                        if (BossCore != null && P2_LaserRenderer != null && _player != null)
                        {
                            Vector3 startPos = BossCore.position;
                            Vector3 targetDir = (_player.transform.position + Vector3.up * 1.2f) - startPos;

                            if (Physics.Raycast(startPos, targetDir, out RaycastHit hit, 100f, P2_LaserHitMask))
                                _lockedLaserTargetPos = hit.point;
                            else
                                _lockedLaserTargetPos = startPos + targetDir.normalized * 100f;

                            P2_LaserRenderer.SetPosition(0, startPos);
                            P2_LaserRenderer.SetPosition(1, _lockedLaserTargetPos);

                            P2_LaserRenderer.gameObject.SetActive(true);

                            // ==========================================
                            // 【动画起点】：蓄力刚开始时，激光宽度为 0，几乎看不见！
                            P2_LaserRenderer.startWidth = 0f;
                            P2_LaserRenderer.endWidth = 0f;
                            P2_LaserRenderer.material.color = new Color(1f, 0f, 0f, 0f);
                        }

                        // 【启动粒子】：打开引力场，开始吸收四周的能量！
                        if (LaserChargeFX != null)
                        {
                            LaserChargeFX.Play();
                        }
                        // ==========================================
                    }
                    break;

                case BossPhase.Phase2_LaserAiming:
                    MaintainLockOnCamera();

                    if (BossCore != null && P2_LaserRenderer != null && _player != null)
                    {
                        Vector3 startPos = BossCore.position;
                        P2_LaserRenderer.SetPosition(0, startPos);
                        Vector3 targetDir = (_player.transform.position + Vector3.up * 1.2f) - startPos;

                        RaycastHit hit;
                        if (Physics.Raycast(startPos, targetDir, out hit, 100f, P2_LaserHitMask))
                            _lockedLaserTargetPos = hit.point;
                        else
                            _lockedLaserTargetPos = startPos + targetDir.normalized * 100f;

                        P2_LaserRenderer.SetPosition(1, _lockedLaserTargetPos);

                        // ==========================================
                        // 【核心动画】：随着时间推移的蓄力演出！
                        // chargeProgress 会在 3 秒内从 0 平滑涨到 1
                        float chargeProgress = 1f - (_p2StateTimer / P2_LaserAimTime);

                        // 1. 让粒子吸收管永远精准指向目标点
                        if (LaserChargeFX != null)
                        {
                            LaserChargeFX.transform.LookAt(_lockedLaserTargetPos);
                        }

                        // 2. 射线宽度慢慢变宽 (0 -> 0.1)
                        float baseWidth = Mathf.Lerp(0f, 0.1f, chargeProgress);

                        // 3. 狂暴闪烁：蓄力过半后，射线开始发生高频的不稳定跳动！
                        float jitter = chargeProgress > 0.6f ? Random.Range(-0.04f, 0.04f) : 0f;
                        float currentWidth = Mathf.Max(0.01f, baseWidth + jitter);

                        P2_LaserRenderer.startWidth = currentWidth;
                        P2_LaserRenderer.endWidth = currentWidth;

                        // 4. 颜色由暗红慢慢变得极度刺眼发白
                        P2_LaserRenderer.material.color = new Color(1f, chargeProgress * 0.8f, chargeProgress * 0.8f, chargeProgress);
                        // ==========================================
                    }

                    _p2StateTimer -= Time.deltaTime;
                    if (_p2StateTimer <= 0f)
                    {
                        _p2StateTimer = P2_LaserFireTime;
                        _currentPhase = BossPhase.Phase2_LaserFiring;
                        _hasLaserDamagedPlayer = false;

                        // ==========================================
                        // 【爆发】：蓄力结束，停止吸气！
                        if (LaserChargeFX != null) LaserChargeFX.Stop();

                        // 射线瞬间膨胀为毁灭光柱！
                        if (P2_LaserRenderer != null)
                        {
                            P2_LaserRenderer.startWidth = 1.2f;
                            P2_LaserRenderer.endWidth = 1.2f;
                            P2_LaserRenderer.material.color = Color.white;
                        }
                        // ==========================================
                        RawCameraShake.Shake(0.5f, 0.4f);
                    }
                    break;

                case BossPhase.Phase2_LaserFiring:
                    MaintainLockOnCamera();

                    if (BossCore != null && P2_LaserRenderer != null)
                    {
                        P2_LaserRenderer.SetPosition(0, BossCore.position);
                        P2_LaserRenderer.SetPosition(1, _lockedLaserTargetPos);

                        // 光柱开火后的余晖衰减
                        float progress = 1f - (_p2StateTimer / P2_LaserFireTime);
                        float currentWidth = Mathf.Lerp(1.2f, 0f, progress);
                        P2_LaserRenderer.startWidth = currentWidth;
                        P2_LaserRenderer.endWidth = currentWidth;

                        if (!_hasLaserDamagedPlayer)
                        {
                            Collider[] hits = Physics.OverlapCapsule(BossCore.position, _lockedLaserTargetPos, 0.5f, P2_LaserHitMask);
                            foreach (Collider col in hits)
                            {
                                SimplePlayerLife life = col.GetComponentInParent<SimplePlayerLife>();
                                if (life != null)
                                {
                                    life.TakeDamage(1);
                                    _hasLaserDamagedPlayer = true;
                                }

                                for (int i = 0; i < P2_Pillars.Length; i++)
                                {
                                    if (P2_Pillars[i] != null && _pillarStates[i] == PillarState.Normal)
                                    {
                                        if (col.transform.IsChildOf(P2_Pillars[i].transform))
                                        {
                                            _pillarStates[i] = PillarState.Exposed;
                                            ChangePillarMaterial(i, PillarExposedMat);
                                            Debug.Log($"【暴露】机房 {i} 被激光击穿！");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    _p2StateTimer -= Time.deltaTime;
                    if (_p2StateTimer <= 0f)
                    {
                        _p2StateTimer = P2_LaserCooldown;
                        _currentPhase = BossPhase.Phase2_Idle;
                        if (P2_LaserRenderer != null) P2_LaserRenderer.gameObject.SetActive(false);
                    }
                    break;


                // ==========================================
                // 【修改】：第三阶段互动逻辑 (A/D 步步逼近核心)
                // ==========================================
                case BossPhase.Phase3_Walk1:
                    if (Keyboard.current != null && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
                        StartCoroutine(CinematicWalkRoutine(P3_WalkTarget_1, P3_WalkCamera_2, BossPhase.Phase3_Walk2, Keyboard.current.aKey.wasPressedThisFrame));
                    break;

                case BossPhase.Phase3_Walk2:
                    if (Keyboard.current != null && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
                        StartCoroutine(CinematicWalkRoutine(P3_WalkTarget_2, P3_WalkCamera_3, BossPhase.Phase3_Walk3, Keyboard.current.aKey.wasPressedThisFrame));
                    break;

                case BossPhase.Phase3_Walk3:
                    if (Keyboard.current != null && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
                        StartCoroutine(CinematicWalkRoutine(P3_WalkTarget_3, P3_ExecutionCamera, BossPhase.Phase3_Execution, Keyboard.current.aKey.wasPressedThisFrame));
                    break;

                case BossPhase.Phase3_Execution:
                    // ==========================================
                    // 无论按不按E，黑洞中心和发射区必须永远死死锁定在玩家胸口和Boss身上！
                    if (SiphonParticles != null && BossCore != null && _player != null)
                    {
                        // 完美贴合胸口的位置
                        Vector3 chestPos = _player.transform.position + Vector3.up * 0.8f - _player.transform.right * 0f;

                        SiphonParticles.transform.position = chestPos;
                        SiphonParticles.transform.rotation = Quaternion.identity;

                        var shape = SiphonParticles.shape;
                        shape.position = BossCore.position - chestPos;
                    }
                    // ==========================================

                    if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
                    {
                        if (QTE_ProgressBar != null && !QTE_ProgressBar.gameObject.activeSelf)
                        {
                            QTE_ProgressBar.gameObject.SetActive(true);
                            QTE_ProgressBar.maxValue = P3_ExecutionTimeRequired;
                        }

                        _p2HackGauge += Time.deltaTime;
                        if (QTE_ProgressBar != null) QTE_ProgressBar.value = _p2HackGauge;
                        ApplyUIPunch(_p2HackGauge / P3_ExecutionTimeRequired);

                        float pullProgress = Mathf.Clamp01(_p2HackGauge / P3_ExecutionTimeRequired);

                        // 1. 核心的拉扯位移
                        if (BossCore != null && P3_CoreAscendPos != null && _player != null)
                        {
                            Vector3 startPos = P3_CoreAscendPos.position;
                            Vector3 targetPos = _player.transform.position + Vector3.up * 0.8f + _player.transform.forward * 0.5f;
                            BossCore.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, pullProgress));
                        }

                        // 2. 空间扭曲 (Dolly Zoom)
                        if (P3_ExecutionCamera != null)
                        {
                            P3_ExecutionCamera.Lens.FieldOfView = Mathf.Lerp(60f, 25f, pullProgress);
                        }

                        // 3. 【优化】：大幅削弱镜头摇晃！(从 0.5f 降到 0.08f) 让你的特效清晰可见！
                        float heavyShake = Mathf.Lerp(0f, 0.08f, pullProgress);
                        if (heavyShake > 0) RawCameraShake.Shake(heavyShake, 0.1f);

                        // 4. 【完美修复特效断档】：通过控制 Emission(发射器) 来实现无缝续传！
                        if (SiphonParticles != null)
                        {
                            // 如果系统处于休眠，先唤醒它
                            if (!SiphonParticles.isPlaying) SiphonParticles.Play();

                            // 打开水龙头，开始疯狂发射！
                            var em = SiphonParticles.emission;
                            em.enabled = true;
                        }

                        if (_p2HackGauge >= P3_ExecutionTimeRequired)
                        {
                            _currentPhase = BossPhase.Dead;
                            HideQTEPrompt();
                            if (QTE_ProgressBar) QTE_ProgressBar.gameObject.SetActive(false);

                            StopAllBGM();

                            if (_playerAnimator != null)
                            {
                                _playerAnimator.enabled = true;
                                _playerAnimator.speed = 1f;
                                _playerAnimator.CrossFade("Grounded", 0.5f);
                            }

                            if (GlobalWindParticles != null) GlobalWindParticles.Stop();
                            if (CoreEnergyParticles != null) CoreEnergyParticles.Stop();
                            if (SiphonParticles != null) SiphonParticles.Stop();

                            StartCoroutine(WhiteoutEndingRoutine());
                        }
                    }
                    else
                    {
                        // 如果松开 E 键，进度条倒退，核心像有弹力一样飘回天上
                        _p2HackGauge = Mathf.Max(0, _p2HackGauge - Time.deltaTime * 3f);
                        if (QTE_ProgressBar != null) QTE_ProgressBar.value = _p2HackGauge;

                        float pullProgress = Mathf.Clamp01(_p2HackGauge / P3_ExecutionTimeRequired);

                        if (BossCore != null && P3_CoreAscendPos != null && _player != null)
                        {
                            Vector3 targetPos = _player.transform.position + Vector3.up * 0.8f + _player.transform.forward * 0.5f;
                            BossCore.position = Vector3.Lerp(P3_CoreAscendPos.position, targetPos, Mathf.SmoothStep(0f, 1f, pullProgress));
                        }

                        if (P3_ExecutionCamera != null)
                        {
                            P3_ExecutionCamera.Lens.FieldOfView = Mathf.Lerp(60f, 25f, pullProgress);
                        }

                        // 【完美修复特效断档】：松手时，仅仅关闭“水龙头(发射器)”，不关闭系统本身。
                        // 这样残留的粒子会继续飞向玩家，且下次按 E 瞬间就能续上！
                        if (SiphonParticles != null)
                        {
                            var em = SiphonParticles.emission;
                            em.enabled = false;
                        }
                    }
                    RecoverUIPunch();
                    break;
            }
        }
        #endregion

        #region Phase 2 Hacking Logic
        private void HandleP2Hacking()
        {
            if (_player == null || _pillarStates == null) return;

            int nearestExposedIndex = -1;
            float minDistance = P2_HackRange;

            for (int i = 0; i < P2_Pillars.Length; i++)
            {
                if (_pillarStates[i] == PillarState.Exposed && P2_Pillars[i] != null)
                {
                    float dist = Vector3.Distance(_player.transform.position, P2_Pillars[i].transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestExposedIndex = i;
                    }
                }
            }

            if (nearestExposedIndex != -1)
            {
                ShowQTEPrompt("[HOLD E] INJECT MALWARE");

                if (QTE_ProgressBar != null && !QTE_ProgressBar.gameObject.activeSelf)
                {
                    QTE_ProgressBar.gameObject.SetActive(true);
                    QTE_ProgressBar.maxValue = P2_HackTimeRequired;
                    QTE_ProgressBar.value = _p2HackGauge;
                }

                if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
                {
                    _p2HackGauge += Time.deltaTime;
                    if (QTE_ProgressBar != null) QTE_ProgressBar.value = _p2HackGauge;
                    ApplyUIPunch(_p2HackGauge / P2_HackTimeRequired);

                    if (_p2HackGauge >= P2_HackTimeRequired) ExecuteHack(nearestExposedIndex);
                }
                else
                {
                    _p2HackGauge = Mathf.Max(0, _p2HackGauge - Time.deltaTime * 3f);
                    if (QTE_ProgressBar != null) QTE_ProgressBar.value = _p2HackGauge;
                }
            }
            else
            {
                _p2HackGauge = 0f;
                HideQTEPrompt();
                if (QTE_ProgressBar != null) QTE_ProgressBar.gameObject.SetActive(false);
            }

            RecoverUIPunch();
        }

        private void ExecuteHack(int pillarIndex)
        {
            _pillarStates[pillarIndex] = PillarState.Hacked;
            ChangePillarMaterial(pillarIndex, PillarHackedMat);

            _p2HackGauge = 0f;
            HideQTEPrompt();
            if (QTE_ProgressBar != null) QTE_ProgressBar.gameObject.SetActive(false);

            StartCoroutine(BossStunRoutine());
        }

        private IEnumerator BossStunRoutine()
        {
            _currentPhase = BossPhase.Phase2_Stunned;
            if (P2_LaserRenderer != null) P2_LaserRenderer.gameObject.SetActive(false);

            bool allHacked = true;
            for (int i = 0; i < _pillarStates.Length; i++)
            {
                if (_pillarStates[i] != PillarState.Hacked)
                {
                    allHacked = false;
                    break;
                }
            }

            if (allHacked)
            {
                // 【绝杀】：最后一根柱子被黑，直接跳入最终升空处决动画！
                RawCameraShake.Shake(1.2f, 0.8f);
                StartCoroutine(TransitionToPhase3());
                yield break; // 后面的宕机代码不跑了
            }
            else
            {
                RawCameraShake.Shake(0.3f, 0.2f);
            }

            Vector3 originalCorePos = BossCore.position;
            float spasmTime = 1f;
            while (spasmTime > 0)
            {
                spasmTime -= Time.deltaTime;
                BossCore.position = originalCorePos + (Random.insideUnitSphere * 0.3f);
                yield return null;
            }
            BossCore.position = originalCorePos;

            yield return new WaitForSeconds(P2_StunDuration - 1f);

            _p2StateTimer = 1.5f;
            _currentPhase = BossPhase.Phase2_Idle;
            Debug.Log("【重启】Boss 杀毒完毕，开始下一轮扫描！");
        }

        private void ChangePillarMaterial(int index, Material mat)
        {
            if (P2_Pillars == null || index < 0 || index >= P2_Pillars.Length || P2_Pillars[index] == null || mat == null) return;
            Renderer r = P2_Pillars[index].GetComponentInChildren<Renderer>();
            if (r != null) r.material = mat;
        }

        private void MaintainLockOnCamera()
        {
            if (_player != null && BossCore != null && _dynamicCamAnchor != null)
            {
                _dynamicCamAnchor.position = _player.transform.position;
                Vector3 lookDir = BossCore.position - _player.transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero) _dynamicCamAnchor.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        #endregion

        #region Phase 3 (最终电影化处决演出)
        private IEnumerator TransitionToPhase3()
        {
            _currentPhase = BossPhase.Phase3_Cinematic;

            // 1. 等待反噬的疯狂抽搐表现
            Vector3 originalCorePos = BossCore.position;
            float spasmTime = 1.5f;
            while (spasmTime > 0)
            {
                spasmTime -= Time.deltaTime;
                BossCore.position = originalCorePos + (Random.insideUnitSphere * 0.4f);
                yield return null;
            }
            BossCore.position = originalCorePos;

            // 2. 黑屏剥夺操作权，切到核心升空特写
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    if (P2_DynamicLockCamera) P2_DynamicLockCamera.Priority = 0;
                    if (P3_CoreAscendCamera) P3_CoreAscendCamera.Priority = 100;
                    DisablePlayerAbilities(true); // 剥夺操作
                });
                yield return new WaitForSeconds(1.0f);
            }

            // 3. 核心升空 + 散发高压电波 (全损震动)

            // ==========================================
            // 【新增】：狂风起！能量爆发！
            if (GlobalWindParticles != null) GlobalWindParticles.Play();
            if (CoreEnergyParticles != null) CoreEnergyParticles.Play();
            // ==========================================


            Vector3 ascendTarget = P3_CoreAscendPos != null ? P3_CoreAscendPos.position : originalCorePos + Vector3.up * 8f;
            float ascendDuration = 4f;
            float t = 0;
            while (t < ascendDuration)
            {
                t += Time.deltaTime;
                BossCore.position = Vector3.Lerp(originalCorePos, ascendTarget, Mathf.SmoothStep(0, 1, t / ascendDuration));

                // 代表核心在散发让人无法靠近的威压，屏幕疯狂颤抖
                RawCameraShake.Shake(0.08f, 0.15f);
                yield return null;
            }

            yield return new WaitForSeconds(1f);

            // 4. 第二次黑屏，切回玩家视角（第一阶段推近）
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    if (P3_CoreAscendCamera) P3_CoreAscendCamera.Priority = 0;
                    if (P3_WalkCamera_1) P3_WalkCamera_1.Priority = 100;

                    if (P3_PlayerStartPos != null && _player != null)
                    {
                        CharacterController cc = _player.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;

                        _player.transform.position = P3_PlayerStartPos.position;
                        _player.transform.rotation = P3_PlayerStartPos.rotation;

                        if (cc != null) cc.enabled = true;
                    }

                    // ==========================================
                    // 【新增】：黑屏瞬间把第二阶段的柱子全部隐藏，清理战场！
                    if (P2_Pillars != null)
                    {
                        foreach (GameObject pillar in P2_Pillars)
                        {
                            if (pillar != null) pillar.SetActive(false);
                        }
                    }
                    // ==========================================
                });
                yield return new WaitForSeconds(1.0f);
            }

            // 5. 提醒玩家按 A 或 D 挣扎前进
            _currentPhase = BossPhase.Phase3_Walk1;
            ShowQTEPrompt("[A] / [D] Alternate steps to move forward!");
        }


        // 通用的“玩家走一段路 -> 切下一个机位”的协程
        // 【重构】：2秒定频脉冲 + E键只防守不前进
        private IEnumerator CinematicWalkRoutine(Transform targetPos, CinemachineCamera nextCamera, BossPhase nextPhase, bool firstStepWasLeft)
        {
            _currentPhase = BossPhase.Phase3_Cinematic;
            HideQTEPrompt();

            if (_player != null && targetPos != null)
            {
                CharacterController cc = _player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                if (_playerAnimator != null)
                {
                    _playerAnimator.enabled = true;
                    _playerAnimator.speed = 1f;
                    _playerAnimator.Play("StruggleWalk", 0, 0f);
                }

                Vector3 startPos = _player.transform.position;
                Vector3 endPos = targetPos.position;
                Quaternion targetRot = Quaternion.LookRotation(endPos - startPos);
                _player.transform.rotation = targetRot;

                int totalSteps = 16;
                float targetProgress = 1f;
                float currentProgress = 0f;

                bool expectLeft = !firstStepWasLeft;

                // 【修改 1】：脉冲计时器设为绝对的 2.0 秒！
                float pulseTimer = 2.0f;
                ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");

                yield return null;

                while (targetProgress < totalSteps || currentProgress < totalSteps - 0.1f)
                {
                    // === 1. 发射电磁波倒计时 ===
                    pulseTimer -= Time.deltaTime;
                    if (pulseTimer <= 0f && _currentPulseRadius < 0f)
                    {
                        // 【修改 2】：一波结束后，严格等待 2.0 秒再发下一波！
                        pulseTimer = 2.0f;
                        _pulseHandledThisRound = false;
                        StartCoroutine(SpawnPulseVisualEffect());
                    }

                    // === 2. 【核心】：雷达测距！计算波浪边缘离玩家还有多远 ===
                    bool isPulseIncoming = false;

                    if (_currentPulseRadius > 0f && !_pulseHandledThisRound)
                    {
                        Vector2 bossPos2D = new Vector2(BossCore.position.x, BossCore.position.z);
                        Vector2 playerPos2D = new Vector2(_player.transform.position.x, _player.transform.position.z);
                        float distToPlayer = Vector2.Distance(bossPos2D, playerPos2D);

                        float distanceToWave = distToPlayer - _currentPulseRadius;

                        // 判定 A：波浪逼近，进入格挡窗口期！（距离玩家 8 米内）
                        if (distanceToWave <= 8.0f && distanceToWave >= 0f)
                        {
                            isPulseIncoming = true; // 锁定迈步

                            if (Mathf.FloorToInt(Time.time * 15) % 2 == 0)
                                ShowQTEPrompt("<color=red> Pulse is coming! Press [E] !</color>");
                            else
                                ShowQTEPrompt("<color=white> Pulse is coming! Press [E] !</color>");

                            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                            {
                                _pulseHandledThisRound = true; // 完美格挡！
                                Debug.Log("<color=cyan>成功驱散脉冲！格挡成功，留在原地！</color>");
                                RawCameraShake.Shake(0.3f, 0.2f);

                                // 【修改 3】：删除了奖励前进的代码，现在只格挡，不前进！
                                // targetProgress = Mathf.Min(totalSteps, targetProgress + 1.5f); 

                                ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");
                            }
                        }
                        // 判定 B：波浪越过了玩家（距离 < 0），且玩家没格挡 -> 挨打被击退！
                        else if (distanceToWave < 0f)
                        {
                            _pulseHandledThisRound = true;
                            Debug.Log("<color=red>Struck by electromagnetic waves! Defused!</color>");
                            RawCameraShake.Shake(1.0f, 0.5f);
                            targetProgress = Mathf.Max(0, targetProgress - 1.5f);
                            ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");
                        }
                    }

                    // === 3. 走路输入逻辑 (电磁波警告期间必须专注按E，不能按AD) ===
                    if (Keyboard.current != null && !isPulseIncoming)
                    {
                        bool pressedA = Keyboard.current.aKey.wasPressedThisFrame;
                        bool pressedD = Keyboard.current.dKey.wasPressedThisFrame;

                        if (pressedA || pressedD)
                        {
                            if ((expectLeft && pressedA) || (!expectLeft && pressedD))
                            {
                                expectLeft = !expectLeft;
                                targetProgress = Mathf.Min(totalSteps, targetProgress + 1f);
                                RawCameraShake.Shake(0.08f, 0.1f);
                                ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");
                            }
                            else
                            {
                                RawCameraShake.Shake(0.2f, 0.2f);
                                targetProgress = Mathf.Max(0, targetProgress - 0.6f);
                                ShowQTEPrompt("<color=yellow>Stagger! Be careful to alternate between your left and right feet.</color>");
                            }
                        }
                    }

                    // === 4. 物理平滑移动与倒放动画 ===
                    if (currentProgress != targetProgress)
                    {
                        float moveDir = targetProgress > currentProgress ? 1f : -1f;
                        float speed = moveDir > 0 ? 3.0f : 4.5f;

                        currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.deltaTime * speed);
                        _player.transform.position = Vector3.Lerp(startPos, endPos, currentProgress / totalSteps);

                        if (_playerAnimator != null)
                        {
                            _playerAnimator.enabled = true;
                            _playerAnimator.speed = moveDir > 0 ? 1f : -1f;
                        }
                    }
                    else if (_playerAnimator != null) _playerAnimator.speed = 0f;

                    _player.transform.rotation = targetRot;
                    yield return null;
                }

                if (_playerAnimator != null)
                {
                    _playerAnimator.speed = 0f;
                    _playerAnimator.enabled = false;
                }
            }

            if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = default;

            if (P3_WalkCamera_1) P3_WalkCamera_1.Priority = 0;
            if (P3_WalkCamera_2) P3_WalkCamera_2.Priority = 0;
            if (P3_WalkCamera_3) P3_WalkCamera_3.Priority = 0;
            if (nextCamera != null) nextCamera.Priority = 100;

            yield return new WaitForEndOfFrame();
            _currentPhase = nextPhase;

            if (nextPhase == BossPhase.Phase3_Execution)
                ShowQTEPrompt("[HOLD E] Do It!");
            else
                ShowQTEPrompt("[A] / [D] Keeping On!");
        }
        #endregion
        #region Visual Effects (第三阶段视觉特效)
        // 动态生成一个贴地扩散的扁平扫描盘
        // 动态生成一个贴地扩散的扁平扫描盘
        private IEnumerator SpawnPulseVisualEffect()
        {
            if (BossCore == null || _player == null) yield break;

            GameObject pulseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pulseDisc.GetComponent<Collider>());

            // 【修改 1】：高度既然变了，位置也要稍微往上抬一点，让光环贴着地
            // 假设光环 1.5 米高，中心点就在 y + 0.75f 的位置
            pulseDisc.transform.position = new Vector3(BossCore.position.x, _player.transform.position.y + 0.75f, BossCore.position.z);

            Renderer r = pulseDisc.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (PulseWaveMat != null) r.material = PulseWaveMat;

            float duration = 2.5f;
            float t = 0;

            // 【修改 2】：把高度(Y)从 0.05f 改成 1.5f ！！
            // 这样侧面就会有一堵 1.5 米高的透明光墙扫过来，心电图波浪绝对清晰可见！
            Vector3 startScale = new Vector3(1f, 1.5f, 1f);
            Vector3 endScale = new Vector3(100f, 1.5f, 100f);

            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;

                pulseDisc.transform.localScale = Vector3.Lerp(startScale, endScale, progress);
                _currentPulseRadius = pulseDisc.transform.localScale.x / 2f;

                if (r.material.HasProperty("_Fade"))
                    r.material.SetFloat("_Fade", Mathf.Lerp(1f, 0f, progress));

                yield return null;
            }

            _currentPulseRadius = -1f;
            Destroy(pulseDisc);
        }
        #endregion
        #region Tools & UI (保持不变)
        private IEnumerator CinematicCameraPan(Transform target, float transitionTime, float endSlowMoScale)
        {
            DisablePlayerAbilities(true);
            if (QTE_CloseUpCamera == null || target == null) yield break;
            CinemachineBlendDefinition oldBlend = default;
            if (_mainCameraBrain != null)
            {
                oldBlend = _mainCameraBrain.DefaultBlend;
                _mainCameraBrain.DefaultBlend = default;
            }
            if (!QTE_CloseUpCamera.gameObject.activeSelf) QTE_CloseUpCamera.gameObject.SetActive(true);
            QTE_CloseUpCamera.transform.position = Camera.main.transform.position;
            QTE_CloseUpCamera.transform.rotation = Camera.main.transform.rotation;
            QTE_CloseUpCamera.Priority = 200;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            float t = 0f;
            Vector3 startPos = QTE_CloseUpCamera.transform.position;
            Quaternion startRot = QTE_CloseUpCamera.transform.rotation;
            while (t < transitionTime)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0, 1, t / transitionTime);
                QTE_CloseUpCamera.transform.position = Vector3.Lerp(startPos, target.position, progress);
                QTE_CloseUpCamera.transform.rotation = Quaternion.Lerp(startRot, target.rotation, progress);
                yield return null;
            }
            if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = oldBlend;
            Time.timeScale = endSlowMoScale;
        }

        private void ResetQTECamera() { if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 0; }

        private void ExitQTEState(bool keepPlayerDisabled = false)
        {
            Time.timeScale = 1f;
            HideQTEPrompt();
            if (QTE_ProgressBar)
            {
                QTE_ProgressBar.gameObject.SetActive(false);
                QTE_ProgressBar.transform.localScale = _qteUIBarOriginalScale;
            }
            if (QTE_PromptTextUI) QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;
            ResetQTECamera();
            if (!keepPlayerDisabled) DisablePlayerAbilities(false);
        }

        private void DisablePlayerAbilities(bool disable)
        {
            if (_player == null) return;
            if (_thirdPerson != null) _thirdPerson.enabled = !disable;
            if (_starterInputs != null)
            {
                if (disable)
                {
                    _starterInputs.move = Vector2.zero;
                    _starterInputs.look = Vector2.zero;
                    _starterInputs.jump = false;
                    _starterInputs.sprint = false;
                }
                _starterInputs.enabled = !disable;
            }
            if (disable && _playerAnimator != null && _currentPhase != BossPhase.P1_Dodging)
            {
                _playerAnimator.SetFloat("Speed", 0f);
                _playerAnimator.SetFloat("MotionSpeed", 0f);
            }
        }

        private void ShowQTEPrompt(string text)
        {
            if (QTE_PromptTextUI)
            {
                QTE_PromptTextUI.SetActive(true);
                QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;
            }
            if (QTE_TextComponent) QTE_TextComponent.text = text;
        }

        private void HideQTEPrompt() => QTE_PromptTextUI?.SetActive(false);
        #endregion
        #region Ending Cinematic (大结局演出)
        // 动态生成一个无敌全屏白光，随后切黑
        private IEnumerator WhiteoutEndingRoutine()
        {
            // 1. 代码动态创建一个优先级最高的 Canvas，保证挡住全屏所有东西
            GameObject canvasObj = new GameObject("EndingWhiteoutCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // 最高层级

            GameObject imageObj = new GameObject("WhiteImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            Image img = imageObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // 初始全透明

            RectTransform rect = img.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 2. 接触瞬间：极速致盲的白光爆炸 (0.1秒)
            // 这里只给一个瞬间的微弱碰撞震动，代表核心撞到了玩家
            RawCameraShake.Shake(0.4f, 0.2f);

            float t = 0;
            float flashDuration = 0.1f;
            while (t < flashDuration)
            {
                t += Time.unscaledDeltaTime;
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t / flashDuration));
                yield return null;
            }
            img.color = Color.white;

            // 3. 保持刺眼的白屏 2.5 秒钟 (让玩家在无BGM的静音中感受震撼)
            yield return new WaitForSecondsRealtime(2.5f);

            // 4. 瞬间切成黑屏！
            img.color = Color.black;

            Debug.Log("<color=green>【结局】屏幕已黑！等待后续结局制作...</color>");

            // 在这里你可以随意添加加载制作人员名单（Credits）或者返回主菜单的代码
        }
        #endregion
        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if (BombLocations != null)
            {
                foreach (Transform t in BombLocations) if (t != null) Gizmos.DrawWireSphere(t.position, BombRadius);
            }
            Gizmos.color = Color.yellow;
            if (ProjectileSourceType == DanmakuSourceType.RandomInArea && ProjectileSpawnArea != null)
            {
                Gizmos.DrawWireCube(ProjectileSpawnArea.bounds.center, ProjectileSpawnArea.bounds.size);
            }
        }
        #endregion
    }
}
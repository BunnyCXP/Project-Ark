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
        private enum BossPhase
        {
            Waiting, Intro, P1_Dodging, P1_QTE, Transitioning,
            Phase2_Idle, Phase2_LaserAiming, Phase2_LaserFiring, Phase2_HackingMinigame, Phase2_Stunned,
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

        [Header("第一阶段：系统清洗 (追踪矩阵)")]
        public GameObject AoEWarningPrefab;
        public Transform[] BombLocations;
        public float WarningTime = 4.0f;
        [Range(1f, 15f)] public float AoETrackingSpeed = 4.0f;

        public int BombRounds = 3;
        public int BombsPerRound = 4;
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

        [Header("P1 骇客小游戏：数据吞噬 (Pac-Man)")]
        public float P1_QTESlowMo = 0.08f;
        public float P1_HackTimeLimit = 12.0f;
        public int P1_FoodRequired = 5;
        public float P1_SnakeMoveInterval = 0.15f;

        private Vector2Int _p1SnakePos;
        private Vector2Int _p1FoodPos;
        private WheelDir _p1SnakeDir;
        private int _p1FoodCollected;
        private float _p1HackTimer;
        private float _p1SnakeMoveTimer;
        private int _p1GridWidth = 14;
        private int _p1GridHeight = 5;
        private System.Collections.Generic.List<Vector2Int> _p1SnakeHistory = new System.Collections.Generic.List<Vector2Int>();
        private System.Collections.Generic.List<Vector2Int> _p1EatenPositions = new System.Collections.Generic.List<Vector2Int>();

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
        public ParticleSystem LaserChargeFX;
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

        [Header("--- 骇客输入控制 ---")]
        public float HackDeadZone = 35f;
        public float HackMaxRadius = 120f;

        [Header("--- 骇客轮盘 UI 引用 ---")]
        public HackWheelUI BossHackWheel;

        [Header("P2 序列记忆破解 (全息投影)")]
        public int P2_SequenceLength = 4;
        public float P2_HackTimeLimit = 5.0f;
        private System.Collections.Generic.List<WheelDir> _p2Sequence = new System.Collections.Generic.List<WheelDir>();
        private int _p2CurrentSeqIndex = 0;
        private int _p2TargetPillarIndex = -1;
        private WheelDir _p2LastInput = WheelDir.None;
        private float _p2HackTimer = 0f;
        private float _p2InputCooldownTimer = 0f;
        private GameObject _holoBoardObj;
        private TextMeshProUGUI _holoBoardText;
        private RectTransform _scanlineRT;

        [Header("第三阶段：终极处决演出")]
        public Transform P3_PlayerStartPos;
        public ParticleSystem GlobalWindParticles;
        public ParticleSystem CoreEnergyParticles;

        public CinemachineCamera P3_CoreAscendCamera;
        public Transform P3_CoreAscendPos;

        [Space(10)]
        public CinemachineCamera P3_WalkCamera_1;
        public Transform P3_WalkTarget_1;

        public CinemachineCamera P3_WalkCamera_2;
        public Transform P3_WalkTarget_2;

        public CinemachineCamera P3_WalkCamera_3;
        public Transform P3_WalkTarget_3;

        [Space(10)]
        public CinemachineCamera P3_ExecutionCamera;
        public float P3_ExecutionTimeRequired = 3.0f;

        [Header("UI 设置")]
        public GameObject WarningTextUI;
        public GameObject QTE_PromptTextUI;
        public TextMeshProUGUI QTE_TextComponent;
        public Slider QTE_ProgressBar;
        public ParticleSystem SiphonParticles;

        public Material PulseWaveMat;
        private float _currentPulseRadius = -1f;
        private bool _pulseHandledThisRound = false;

        private Material[] _wpOriginalMats;
        private int _currentWeakPointIndex = 0;

        private Vector3 _qteUIPromptOriginalScale;
        private Vector3 _qteUIBarOriginalScale;
        private Vector3 _currentUIPromptScale;
        private Vector3 _currentUIBarScale;

        private Transform _dynamicCamAnchor;
        private Transform _uiTargetAnchor;

        private float _p2StateTimer = 0f;
        private Vector3 _lockedLaserTargetPos;
        private bool _hasLaserDamagedPlayer = false;

        private Vector3 _p1ArenaCenter;
        private Quaternion _p1ArenaRot;

        private Vector3 _doorStartPos;
        private Vector3 _coreStartPos;
        private Vector3[] _pillarStartPos;
        private Rigidbody[] _armorRbs;
        private Vector3[] _armorStartPos;
        private Quaternion[] _armorStartRot;

        private CinemachineBlendDefinition _initialCameraBlend;

        private void Awake()
        {
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

            if (_mainCameraBrain != null) _initialCameraBlend = _mainCameraBrain.DefaultBlend;

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

            _uiTargetAnchor = new GameObject("UITargetAnchor").transform;
            _uiTargetAnchor.SetParent(this.transform);

            if (P2_LaserRenderer != null) P2_LaserRenderer.gameObject.SetActive(false);

            Time.timeScale = 1f;

            if (BossDoor) _doorStartPos = BossDoor.transform.position;
            if (BossCore) _coreStartPos = BossCore.position;

            if (P2_Pillars != null)
            {
                _pillarStartPos = new Vector3[P2_Pillars.Length];
                for (int i = 0; i < P2_Pillars.Length; i++)
                {
                    if (P2_Pillars[i]) _pillarStartPos[i] = P2_Pillars[i].transform.position;
                }
            }

            if (BossArmorParent)
            {
                _armorRbs = BossArmorParent.GetComponentsInChildren<Rigidbody>();
                _armorStartPos = new Vector3[_armorRbs.Length];
                _armorStartRot = new Quaternion[_armorRbs.Length];
                for (int i = 0; i < _armorRbs.Length; i++)
                {
                    _armorStartPos[i] = _armorRbs[i].transform.localPosition;
                    _armorStartRot[i] = _armorRbs[i].transform.localRotation;
                    _armorRbs[i].isKinematic = true;
                }
            }
        }

        private void OnDisable() => Time.timeScale = 1f;

        private IEnumerator RestoreBlendNextFrame(CinemachineBlendDefinition blend)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = blend;
        }

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

            _p1ArenaCenter = _player.transform.position + _player.transform.forward * 8f;
            _p1ArenaCenter.y = Phase2Settings != null && Phase2Settings.PlayerStage != null ? Phase2Settings.PlayerStage.position.y : _player.transform.position.y;
            _p1ArenaRot = _player.transform.rotation;

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
        #endregion

        private IEnumerator PlayCRTWarning(string top, string mid, string bot, float duration)
        {
            if (QTE_PromptTextUI == null || QTE_TextComponent == null) yield break;

            Vector2 originalPromptPos = QTE_PromptTextUI.GetComponent<RectTransform>().anchoredPosition;

            QTE_PromptTextUI.GetComponent<RectTransform>().anchoredPosition = originalPromptPos + new Vector2(0, 300);

            QTE_PromptTextUI.SetActive(true);
            QTE_TextComponent.text = $"<color=red><size=30>{top}</size>\n<size=65><b>{mid}</b></size>\n<size=25>{bot}</size></color>";

            float crtTime = 0f;
            while (crtTime < 0.15f)
            {
                crtTime += Time.unscaledDeltaTime;
                float y = Mathf.Lerp(0.02f, 1f, crtTime / 0.15f);
                QTE_PromptTextUI.transform.localScale = new Vector3(_qteUIPromptOriginalScale.x, y * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                yield return null;
            }
            QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;

            yield return new WaitForSeconds(duration);

            crtTime = 0f;
            while (crtTime < 0.1f)
            {
                crtTime += Time.unscaledDeltaTime;
                float y = Mathf.Lerp(1f, 0.02f, crtTime / 0.1f);
                QTE_PromptTextUI.transform.localScale = new Vector3(_qteUIPromptOriginalScale.x, y * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                yield return null;
            }

            crtTime = 0f;
            while (crtTime < 0.1f)
            {
                crtTime += Time.unscaledDeltaTime;
                float x = Mathf.Lerp(1f, 0f, crtTime / 0.1f);
                QTE_PromptTextUI.transform.localScale = new Vector3(x * _qteUIPromptOriginalScale.x, 0.02f * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                yield return null;
            }

            QTE_PromptTextUI.SetActive(false);

            QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;
            QTE_PromptTextUI.GetComponent<RectTransform>().anchoredPosition = originalPromptPos;
        }

        #region Intro + Phase 1 
        private IEnumerator IntroRoutine()
        {
            if (BossDoor != null && DoorClosedPos != null)
            {
                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.DoFadeAndAction(() =>
                    {
                        PlayBGM(Phase1_BGM);
                        if (_mainCameraBrain != null)
                        {
                            _mainCameraBrain.DefaultBlend = default;
                            StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                        }
                        if (IntroDoorCamera != null) IntroDoorCamera.Priority = 200;
                        if (P1_TopCamera != null) P1_TopCamera.Priority = 0;
                    });
                    yield return new WaitForSeconds(1.0f);
                }
                else
                {
                    PlayBGM(Phase1_BGM);
                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                    if (IntroDoorCamera != null) IntroDoorCamera.Priority = 200;
                    if (P1_TopCamera != null) P1_TopCamera.Priority = 0;
                    yield return new WaitForSeconds(0.5f);
                }

                yield return StartCoroutine(AnimateDoorClosing());
                yield return new WaitForSeconds(1.0f);

                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.DoFadeAndAction(() =>
                    {
                        if (_mainCameraBrain != null)
                        {
                            _mainCameraBrain.DefaultBlend = default;
                            StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                        }
                        if (IntroDoorCamera != null) IntroDoorCamera.Priority = 0;
                        if (P1_TopCamera != null) P1_TopCamera.Priority = 100;
                        if (_playerLife != null) _playerLife.ShowPlayerHealthBar();
                    });
                    yield return new WaitForSeconds(1.0f);
                }
            }
            else
            {
                PlayBGM(Phase1_BGM);
                if (_mainCameraBrain != null)
                {
                    _mainCameraBrain.DefaultBlend = default;
                    StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                }
                if (P1_TopCamera) P1_TopCamera.Priority = 100;
                if (_playerLife != null) _playerLife.ShowPlayerHealthBar();
                yield return new WaitForSeconds(0.5f);
            }

            yield return StartCoroutine(PlayCRTWarning("/// SECURITY BREACH DETECTED ///", "[ INTRUDER ALERT ]", "INITIATING PURGE PROTOCOL...", 2.0f));

            DisablePlayerAbilities(false);
            StartCoroutine(P1_DodgingRoutine());
        }

        public void SoftResetBoss(GameObject playerObj)
        {
            StopAllCoroutines();

            _currentPhase = BossPhase.Intro;

            _currentWeakPointIndex = 0;
            _p1HackTimer = 0f;
            _p1FoodCollected = 0;
            _p2HackGauge = 0f;
            _p2StateTimer = 0f;
            _hasLaserDamagedPlayer = false;

            if (_mainCameraBrain != null) _mainCameraBrain.DefaultBlend = _initialCameraBlend;

            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerObj.transform.position = _p1ArenaCenter + Vector3.up * 0.1f;
            playerObj.transform.rotation = _p1ArenaRot;
            if (cc != null) cc.enabled = true;

            if (BossDoor && DoorClosedPos) BossDoor.transform.position = DoorClosedPos.position;
            if (BossCore) BossCore.position = _coreStartPos;

            if (BossArmorParent)
            {
                BossArmorParent.SetActive(true);
                for (int i = 0; i < _armorRbs.Length; i++)
                {
                    if (_armorRbs[i])
                    {
                        _armorRbs[i].isKinematic = true;
                        _armorRbs[i].useGravity = false;
                        _armorRbs[i].transform.localPosition = _armorStartPos[i];
                        _armorRbs[i].transform.localRotation = _armorStartRot[i];
                    }
                }
            }

            if (P1_WeakPoints != null)
            {
                for (int i = 0; i < P1_WeakPoints.Length; i++)
                {
                    if (P1_WeakPoints[i]) P1_WeakPoints[i].SetActive(true);
                    Renderer r = GetWeakPointRenderer(i);
                    if (r != null && _wpOriginalMats != null && _wpOriginalMats[i] != null)
                        r.material = _wpOriginalMats[i];
                }
            }

            if (P2_Pillars != null && _pillarStates != null)
            {
                for (int i = 0; i < P2_Pillars.Length; i++)
                {
                    _pillarStates[i] = PillarState.Normal;
                    ChangePillarMaterial(i, PillarNormalMat);
                    if (P2_Pillars[i]) P2_Pillars[i].transform.position = _pillarStartPos[i];
                }
            }

            var projectiles = Object.FindObjectsByType<BossProjectile>(FindObjectsSortMode.None);
            foreach (var p in projectiles) Destroy(p.gameObject);

            if (AoEWarningPrefab)
            {
                var allObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var obj in allObjs)
                {
                    if (obj.name.Contains(AoEWarningPrefab.name)) Destroy(obj);
                }
            }

            if (P2_LaserRenderer) P2_LaserRenderer.gameObject.SetActive(false);
            if (LaserChargeFX) LaserChargeFX.Stop();
            if (GlobalWindParticles) GlobalWindParticles.Stop();
            if (CoreEnergyParticles) CoreEnergyParticles.Stop();
            if (SiphonParticles) SiphonParticles.Stop();

            InitUI();
            HideQTEPrompt();
            if (_holoBoardObj) _holoBoardObj.SetActive(false);
            if (BossHackWheel) BossHackWheel.Close();

            StartCoroutine(RetryIntroRoutine());
        }

        private IEnumerator RetryIntroRoutine()
        {
            _currentPhase = BossPhase.Intro;

            if (IntroDoorCamera) IntroDoorCamera.Priority = 0;
            if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 0;
            if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 0;
            if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 0;
            if (P2_DynamicLockCamera) P2_DynamicLockCamera.Priority = 0;
            if (P3_CoreAscendCamera) P3_CoreAscendCamera.Priority = 0;
            if (P3_WalkCamera_1) P3_WalkCamera_1.Priority = 0;
            if (P3_WalkCamera_2) P3_WalkCamera_2.Priority = 0;
            if (P3_WalkCamera_3) P3_WalkCamera_3.Priority = 0;
            if (P3_ExecutionCamera) P3_ExecutionCamera.Priority = 0;

            if (P1_TopCamera) P1_TopCamera.Priority = 100;

            if (_mainCameraBrain != null)
            {
                _mainCameraBrain.DefaultBlend = default;
                StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
            }

            PlayBGM(Phase1_BGM);

            yield return new WaitForSeconds(1.5f);

            if (_playerLife != null) _playerLife.ShowPlayerHealthBar();

            yield return StartCoroutine(PlayCRTWarning("/// TIMELINE FRACTURE DETECTED ///", "[ REVERSE INITIATED ]", "RESTORING PREVIOUS STATE...", 1.5f));

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
                    System.Collections.Generic.List<Transform> chosenSpots = new System.Collections.Generic.List<Transform>();

                    for (int i = 0; i < bombsThisRound; i++)
                    {
                        int randomIndex = Random.Range(0, availableSpots.Count);
                        chosenSpots.Add(availableSpots[randomIndex]);
                        availableSpots.RemoveAt(randomIndex);
                    }

                    int half = bombsThisRound / 2;
                    for (int i = 0; i < bombsThisRound; i++)
                    {
                        float dormantTime = (i < half) ? 0f : (WarningTime + 0.65f);
                        StartCoroutine(SpawnAoE(chosenSpots[i].position, dormantTime));
                    }

                    float totalRoundTime = 0.3f + (WarningTime + 0.65f) * 2f + 1.0f;
                    yield return new WaitForSeconds(totalRoundTime);
                }
                else
                {
                    yield return new WaitForSeconds(WarningTime + 0.7f);
                }
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

            if (bullet.GetComponent<DataStreamProjectile>() == null)
            {
                bullet.AddComponent<DataStreamProjectile>();
            }

            BossProjectile projScript = bullet.GetComponent<BossProjectile>();
            if (projScript != null) projScript.Speed = ProjectileSpeed;
        }

        private IEnumerator SpawnAoE(Vector3 position, float dormantTime)
        {
            GameObject warning = null;
            Renderer warningRenderer = null;
            float targetDiameter = BombRadius * 2f;

            // ==========================================
            // 【新增】：雷达扫描线
            // ==========================================
            GameObject scannerLine = null;

            if (AoEWarningPrefab != null)
            {
                warning = Instantiate(AoEWarningPrefab, position, Quaternion.identity);
                warning.transform.localScale = new Vector3(0, 0.1f, 0);
                warningRenderer = warning.GetComponent<Renderer>();

                if (warningRenderer != null) warningRenderer.material.color = new Color(0f, 1f, 1f, 0.1f);

                scannerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(scannerLine.GetComponent<Collider>());
                scannerLine.transform.SetParent(warning.transform);
                scannerLine.transform.localPosition = new Vector3(0, 0.5f, 0);
                scannerLine.transform.localScale = new Vector3(0.02f, 0.1f, 1f);
                Renderer scanR = scannerLine.GetComponent<Renderer>();
                scanR.material = new Material(Shader.Find("Sprites/Default"));
                scanR.material.color = new Color(0f, 1f, 1f, 0.6f);
                scannerLine.SetActive(false);
            }

            float expandTime = 0.3f;
            float et = 0f;
            while (et < expandTime)
            {
                et += Time.deltaTime;
                float currentSize = Mathf.Lerp(0f, targetDiameter, et / expandTime);
                if (warning != null) warning.transform.localScale = new Vector3(currentSize, 0.1f, currentSize);
                yield return null;
            }

            if (dormantTime > 0f)
            {
                yield return new WaitForSeconds(dormantTime);
            }

            if (scannerLine != null) scannerLine.SetActive(true);

            float t = 0f;
            Vector3 currentPos = position;

            while (t < WarningTime)
            {
                t += Time.deltaTime;
                if (warning != null && _player != null)
                {
                    Vector3 targetPos = _player.transform.position;
                    targetPos.y = position.y;
                    currentPos = Vector3.MoveTowards(currentPos, targetPos, Time.deltaTime * AoETrackingSpeed);
                    warning.transform.position = currentPos;

                    if (scannerLine != null) scannerLine.transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.Self);

                    if (warningRenderer != null)
                    {
                        float alphaPulse = 0.15f + 0.1f * Mathf.Sin(Time.time * 20f);
                        warningRenderer.material.color = new Color(0f, 1f, 1f, alphaPulse);
                    }
                }
                yield return null;
            }

            if (scannerLine != null) Destroy(scannerLine);

            if (warning != null)
            {
                warning.transform.localScale = new Vector3(targetDiameter, 0.1f, targetDiameter);
                if (warningRenderer != null) warningRenderer.material.color = new Color(1f, 0f, 0f, 0.8f);
            }

            RawCameraShake.Shake(0.15f, 0.1f);

            yield return new WaitForSeconds(0.5f);

            if (warning != null)
            {
                if (warningRenderer != null) warningRenderer.material.color = new Color(1f, 0f, 0f, 1f);
                ApplyBombDamage(warning.transform.position);
                StartCoroutine(SpawnOrbitalLaserFX(warning.transform.position, targetDiameter));
            }

            RawCameraShake.Shake(0.6f, 0.4f);

            yield return new WaitForSeconds(0.15f);
            if (warning != null) Destroy(warning);
        }

        private IEnumerator SpawnOrbitalLaserFX(Vector3 pos, float size)
        {
            GameObject laser = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(laser.GetComponent<Collider>());

            laser.transform.position = pos + Vector3.up * 20f;
            laser.transform.localScale = new Vector3(size * 0.8f, 20f, size * 0.8f);

            Renderer laserR = laser.GetComponent<Renderer>();
            laserR.material = new Material(Shader.Find("Sprites/Default"));
            laserR.material.color = new Color(1f, 1f, 1f, 1f);

            GameObject boom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(boom.GetComponent<Collider>());
            boom.transform.position = pos + Vector3.up * 0.2f;
            boom.transform.localScale = new Vector3(size, 0.01f, size);

            Renderer boomR = boom.GetComponent<Renderer>();
            boomR.material = new Material(Shader.Find("Sprites/Default"));
            boomR.material.color = new Color(1f, 0.1f, 0.1f, 1f);

            float t = 0;
            float duration = 0.5f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                float easeOut = 1f - (1f - progress) * (1f - progress);

                Color laserColor = Color.Lerp(Color.white, new Color(1f, 0f, 0f, 0f), easeOut);
                laserR.material.color = laserColor;
                float laserWidth = Mathf.Lerp(size * 0.8f, 0f, easeOut);
                laser.transform.localScale = new Vector3(laserWidth, 20f, laserWidth);

                boom.transform.localScale = new Vector3(size * (1f + easeOut * 1.5f), 0.01f, size * (1f + easeOut * 1.5f));
                Color boomColor = boomR.material.color;
                boomColor.a = Mathf.Lerp(1f, 0f, easeOut);
                boomR.material.color = boomColor;

                yield return null;
            }

            Destroy(laser);
            Destroy(boom);
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

            Vector3 centerPos = r != null ? r.bounds.center : targetWP.transform.position;

            Transform camTarget = targetWP.transform;
            if (P1_WeakPointCamAnchors != null && _currentWeakPointIndex < P1_WeakPointCamAnchors.Length && P1_WeakPointCamAnchors[_currentWeakPointIndex] != null)
            {
                camTarget = P1_WeakPointCamAnchors[_currentWeakPointIndex];
            }
            else
            {
                _dynamicCamAnchor.position = centerPos + targetWP.transform.forward * 2.5f + Vector3.up * 0.5f;
                _dynamicCamAnchor.LookAt(centerPos);
                camTarget = _dynamicCamAnchor;
            }

            yield return StartCoroutine(CinematicCameraPan(camTarget, 2.2f, P1_QTESlowMo));
            yield return new WaitForSecondsRealtime(0.15f);

            StartP1HackingMinigame();
        }

        private void StartP1HackingMinigame()
        {
            _currentPhase = BossPhase.P1_QTE;
            _p1HackTimer = P1_HackTimeLimit;
            _p1FoodCollected = 0;
            _p1SnakePos = new Vector2Int(2, 2);
            _p1SnakeDir = WheelDir.Right;
            _p1SnakeMoveTimer = P1_SnakeMoveInterval;

            _p1SnakeHistory.Clear();
            _p1EatenPositions.Clear();

            SpawnP1Food();

            DisablePlayerAbilities(true);

            CreateHoloBoard();
            StartCoroutine(CrtOnHoloBoardRoutine());
            StartCoroutine(P1SnakeHologramRoutine());

            if (BossHackWheel != null)
            {
                BossHackWheel.Open(
                    new QuickHackOption { Name = "UP" },
                    new QuickHackOption { Name = "RIGHT" },
                    new QuickHackOption { Name = "DOWN" },
                    new QuickHackOption { Name = "LEFT" }
                );

                if (P1_WeakPoints[_currentWeakPointIndex] != null)
                {
                    Renderer r = GetWeakPointRenderer(_currentWeakPointIndex);
                    if (r != null)
                    {
                        _uiTargetAnchor.position = r.bounds.center;
                        BossHackWheel.SetFollow(_uiTargetAnchor, Camera.main);
                    }
                    else
                    {
                        BossHackWheel.SetFollow(P1_WeakPoints[_currentWeakPointIndex].transform, Camera.main);
                    }
                }
            }
        }

        private void SpawnP1Food()
        {
            int x, y;
            do
            {
                x = Random.Range(0, _p1GridWidth);
                y = Random.Range(0, _p1GridHeight);
            } while (x == _p1SnakePos.x && y == _p1SnakePos.y);
            _p1FoodPos = new Vector2Int(x, y);
        }

        private void UpdateP1QTE()
        {
            _p1HackTimer -= Time.unscaledDeltaTime;
            if (_p1HackTimer <= 0f)
            {
                FailP1Hacking("TIMEOUT!");
                return;
            }

            if (BossHackWheel != null && Mouse.current != null)
            {
                BossHackWheel.FeedMouseDelta(Mouse.current.delta.ReadValue());
            }

            WheelDir currentInput = BossHackWheel != null ? BossHackWheel.CurrentDir : WheelDir.None;
            if (currentInput != WheelDir.None)
            {
                _p1SnakeDir = currentInput;
            }

            _p1SnakeMoveTimer -= Time.unscaledDeltaTime;
            if (_p1SnakeMoveTimer <= 0f)
            {
                _p1SnakeMoveTimer = P1_SnakeMoveInterval;

                _p1SnakeHistory.Insert(0, _p1SnakePos);
                if (_p1SnakeHistory.Count > 3) _p1SnakeHistory.RemoveAt(_p1SnakeHistory.Count - 1);

                if (_p1SnakeDir == WheelDir.Up) _p1SnakePos.y++;
                else if (_p1SnakeDir == WheelDir.Down) _p1SnakePos.y--;
                else if (_p1SnakeDir == WheelDir.Left) _p1SnakePos.x--;
                else if (_p1SnakeDir == WheelDir.Right) _p1SnakePos.x++;

                if (_p1SnakePos.x < 0) _p1SnakePos.x = _p1GridWidth - 1;
                else if (_p1SnakePos.x >= _p1GridWidth) _p1SnakePos.x = 0;

                if (_p1SnakePos.y < 0) _p1SnakePos.y = _p1GridHeight - 1;
                else if (_p1SnakePos.y >= _p1GridHeight) _p1SnakePos.y = 0;

                if (_p1SnakePos == _p1FoodPos)
                {
                    _p1FoodCollected++;

                    _p1EatenPositions.Add(_p1FoodPos);

                    if (_p1FoodCollected >= P1_FoodRequired)
                    {
                        _currentPhase = BossPhase.Transitioning;
                        StartCoroutine(ShutHoloBoardRoutineP1(true));
                    }
                    else
                    {
                        SpawnP1Food();
                    }
                }
            }
        }

        private IEnumerator P1SnakeHologramRoutine()
        {
            if (_holoBoardText == null) yield break;

            float bootTime = 0.25f;

            while (_currentPhase == BossPhase.P1_QTE)
            {
                if (_holoBoardText != null)
                {
                    string uiText = "";

                    if (bootTime > 0)
                    {
                        uiText += $"<align=center><color=red><b>[ INITIALIZING VULNERABILITY... ]</b></color>\n";
                        uiText += $"<size=20>ACCESSING NODE DATA...</size></align>\n\n";
                        bootTime -= 0.05f;
                    }
                    else
                    {
                        uiText += $"<align=center><color=#00FF55><b>/// DATA EXTRACTION ///</b></color>\n";
                        uiText += $"<size=20>TIME: {_p1HackTimer:F1}s   |   PACKETS: {_p1FoodCollected} / {P1_FoodRequired}</size></align>\n\n";
                    }

                    uiText += "<align=center><size=26><line-height=100%><mspace=40>";
                    for (int y = _p1GridHeight - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < _p1GridWidth; x++)
                        {
                            Vector2Int currentCell = new Vector2Int(x, y);

                            if (bootTime > 0)
                            {
                                uiText += "<color=#113333>░</color>";
                            }
                            else if (currentCell == _p1SnakePos)
                            {
                                string headChar = "■";
                                if (_p1SnakeDir == WheelDir.Up) headChar = "▲";
                                else if (_p1SnakeDir == WheelDir.Down) headChar = "▼";
                                else if (_p1SnakeDir == WheelDir.Left) headChar = "◄";
                                else if (_p1SnakeDir == WheelDir.Right) headChar = "►";

                                uiText += $"<color=#00FFFF>{headChar}</color>";
                            }
                            else if (currentCell == _p1FoodPos)
                            {
                                string foodColor = (Time.unscaledTime % 0.2f < 0.1f) ? "#FF0055" : "#FFFFFF";
                                uiText += $"<color={foodColor}>◈</color>";
                            }
                            else if (_p1SnakeHistory.Contains(currentCell))
                            {
                                int index = _p1SnakeHistory.IndexOf(currentCell);
                                if (index == 0) uiText += "<color=#00CCCC>▓</color>";
                                else if (index == 1) uiText += "<color=#008888>▒</color>";
                                else uiText += "<color=#005555>░</color>";
                            }
                            else if (_p1EatenPositions.Contains(currentCell))
                            {
                                uiText += "<color=#00FF55>+</color>";
                            }
                            else
                            {
                                uiText += "<color=#2A5555>+</color>";
                            }
                        }
                        uiText += "\n";
                    }
                    uiText += "</mspace></line-height></size></align>";

                    _holoBoardText.text = uiText;
                }

                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private void FailP1Hacking(string reason)
        {
            _currentPhase = BossPhase.Transitioning;

            Debug.Log($"<color=red>【一阶段破解失败】{reason}</color>");
            RawCameraShake.Shake(0.5f, 0.3f);
            StartCoroutine(ShutHoloBoardRoutineP1(false));
        }

        private IEnumerator ShutHoloBoardRoutineP1(bool success)
        {
            if (BossHackWheel != null) BossHackWheel.Close();

            if (success && _holoBoardText != null)
            {
                _holoBoardText.color = Color.green;
                _holoBoardText.text = "\n\n<align=center><size=70><b>[ NODE DESTROYED ]</b></size></align>";

                yield return new WaitForSecondsRealtime(0.8f);
            }

            if (_holoBoardObj != null && _holoBoardObj.transform.childCount > 0)
            {
                Transform bgTransform = _holoBoardObj.transform.GetChild(0);
                float crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float y = Mathf.Lerp(1f, 0.02f, crtTime / 0.1f);
                    bgTransform.localScale = new Vector3(1f, y, 1f);
                    yield return null;
                }
                crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float x = Mathf.Lerp(1f, 0f, crtTime / 0.1f);
                    bgTransform.localScale = new Vector3(x, 0.02f, 1f);
                    yield return null;
                }
                _holoBoardObj.SetActive(false);
                bgTransform.localScale = new Vector3(1f, 0.02f, 1f);
            }

            Cursor.lockState = CursorLockMode.Locked;
            DisablePlayerAbilities(false);

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
            Time.timeScale = 1f;

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    ResetQTECamera();
                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });

                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                ResetQTECamera();
                yield return new WaitForSeconds(0.5f);
            }

            DisablePlayerAbilities(false);
            yield return new WaitForSeconds(0.2f);
            StartCoroutine(P1_DodgingRoutine());
        }
        #endregion

        #region Phase 2 (运镜过渡)
        private IEnumerator TransitionToPhase2()
        {
            _currentPhase = BossPhase.Transitioning;
            Time.timeScale = 1f;

            PlayBGM(Phase2_BGM);

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    ResetQTECamera();
                    if (P1_TopCamera) P1_TopCamera.Priority = 0;
                    if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 100;
                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                ResetQTECamera();
                if (P1_TopCamera) P1_TopCamera.Priority = 0;
                if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 100;
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

                        _player.transform.position = Phase2Settings.PlayerStage.position + Vector3.up * 0.1f;
                        _player.transform.rotation = Phase2Settings.PlayerStage.rotation;
                        if (cc != null) cc.enabled = true;
                    }

                    if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 100;
                    if (PillarPanStartPos && P2_PillarPanCamera)
                    {
                        P2_PillarPanCamera.transform.position = PillarPanStartPos.position;
                        P2_PillarPanCamera.transform.rotation = PillarPanStartPos.rotation;
                    }

                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                if (BossArmorParent != null) BossArmorParent.SetActive(false);
                if (P2_ArmorDropCamera) P2_ArmorDropCamera.Priority = 0;
                if (Phase2Settings != null && Phase2Settings.PlayerStage != null && _player != null)
                {
                    CharacterController cc = _player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    _player.transform.position = Phase2Settings.PlayerStage.position + Vector3.up * 0.1f;
                    _player.transform.rotation = Phase2Settings.PlayerStage.rotation;
                    if (cc != null) cc.enabled = true;
                }
                if (P2_PillarPanCamera) P2_PillarPanCamera.Priority = 100;
                if (PillarPanStartPos && P2_PillarPanCamera)
                {
                    P2_PillarPanCamera.transform.position = PillarPanStartPos.position;
                    P2_PillarPanCamera.transform.rotation = PillarPanStartPos.rotation;
                }
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

                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });
                yield return new WaitForSeconds(1.0f);
            }
            else
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

                            P2_LaserRenderer.startWidth = 0f;
                            P2_LaserRenderer.endWidth = 0f;
                            P2_LaserRenderer.material.color = new Color(1f, 0f, 0f, 0f);
                        }

                        if (LaserChargeFX != null)
                        {
                            LaserChargeFX.Play();
                        }
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

                        float chargeProgress = 1f - (_p2StateTimer / P2_LaserAimTime);

                        if (LaserChargeFX != null)
                        {
                            LaserChargeFX.transform.LookAt(_lockedLaserTargetPos);
                            var em = LaserChargeFX.emission;
                            em.rateOverTime = Mathf.Lerp(50f, 1000f, chargeProgress);
                        }

                        float baseWidth = Mathf.Lerp(0f, 0.2f, chargeProgress);
                        float jitter = chargeProgress > 0.6f ? Random.Range(-0.06f, 0.06f) : 0f;
                        float currentWidth = Mathf.Max(0.01f, baseWidth + jitter);

                        P2_LaserRenderer.startWidth = currentWidth;
                        P2_LaserRenderer.endWidth = currentWidth;
                        P2_LaserRenderer.material.color = new Color(1f, chargeProgress * 0.8f, chargeProgress * 0.8f, chargeProgress);
                    }

                    _p2StateTimer -= Time.deltaTime;
                    if (_p2StateTimer <= 0f)
                    {
                        _p2StateTimer = P2_LaserFireTime;
                        _currentPhase = BossPhase.Phase2_LaserFiring;
                        _hasLaserDamagedPlayer = false;

                        if (LaserChargeFX != null) LaserChargeFX.Stop();

                        if (P2_LaserRenderer != null)
                        {
                            P2_LaserRenderer.startWidth = 1.2f;
                            P2_LaserRenderer.endWidth = 1.2f;
                            P2_LaserRenderer.material.color = Color.white;
                        }
                        RawCameraShake.Shake(0.5f, 0.4f);
                    }
                    break;

                case BossPhase.Phase2_LaserFiring:
                    MaintainLockOnCamera();

                    if (BossCore != null && P2_LaserRenderer != null)
                    {
                        P2_LaserRenderer.SetPosition(0, BossCore.position);
                        P2_LaserRenderer.SetPosition(1, _lockedLaserTargetPos);

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

                case BossPhase.Phase2_HackingMinigame:
                    MaintainLockOnCamera();
                    UpdateP2HackingMinigame();
                    break;

                case BossPhase.Phase2_Stunned:
                    MaintainLockOnCamera();
                    break;

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
                    if (SiphonParticles != null && BossCore != null && _player != null)
                    {
                        Vector3 chestPos = _player.transform.position + Vector3.up * 0.8f - _player.transform.right * 0f;
                        SiphonParticles.transform.position = chestPos;
                        SiphonParticles.transform.rotation = Quaternion.identity;

                        var shape = SiphonParticles.shape;
                        shape.position = BossCore.position - chestPos;
                    }

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

                        if (BossCore != null && P3_CoreAscendPos != null && _player != null)
                        {
                            Vector3 startPos = P3_CoreAscendPos.position;
                            Vector3 targetPos = _player.transform.position + Vector3.up * 0.8f + _player.transform.forward * 0.5f;
                            BossCore.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, pullProgress));
                        }

                        if (P3_ExecutionCamera != null)
                        {
                            P3_ExecutionCamera.Lens.FieldOfView = Mathf.Lerp(60f, 25f, pullProgress);
                        }

                        float heavyShake = Mathf.Lerp(0f, 0.08f, pullProgress);
                        if (heavyShake > 0) RawCameraShake.Shake(heavyShake, 0.1f);

                        if (SiphonParticles != null)
                        {
                            if (!SiphonParticles.isPlaying) SiphonParticles.Play();
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

                        if (SiphonParticles != null)
                        {
                            var em = SiphonParticles.emission;
                            em.enabled = false;
                        }
                    }

                    // 【注意】：此处仅用于第三阶段长按 E 时更新 UI 动画缩放
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
                ShowQTEPrompt("[PRESS E] HACK FIREWALL");

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    HideQTEPrompt();
                    StartP2HackingMinigame(nearestExposedIndex);
                }
            }
            else
            {
                HideQTEPrompt();
                if (QTE_ProgressBar != null) QTE_ProgressBar.gameObject.SetActive(false);
            }
            // 顺便在这里调用 RecoverUIPunch 让 QTE 提示的弹跳恢复
            RecoverUIPunch();
        }

        private void StartP2HackingMinigame(int pillarIndex)
        {
            _currentPhase = BossPhase.Phase2_HackingMinigame;
            _p2TargetPillarIndex = pillarIndex;
            _p2CurrentSeqIndex = 0;
            _p2LastInput = WheelDir.None;
            _p2HackTimer = P2_HackTimeLimit;

            DisablePlayerAbilities(true);

            CreateHoloBoard();
            StartCoroutine(CrtOnHoloBoardRoutine());
            StartCoroutine(RollingHologramRoutine());

            _p2Sequence.Clear();
            for (int i = 0; i < P2_SequenceLength; i++)
            {
                WheelDir randomDir = (WheelDir)Random.Range(1, 5);
                _p2Sequence.Add(randomDir);
            }

            if (BossHackWheel != null)
            {
                RectTransform wheelRT = BossHackWheel.GetComponent<RectTransform>();
                if (wheelRT != null)
                {
                    float offsetX = (wheelRT.pivot.x - 0.5f) * wheelRT.rect.width;
                    float offsetY = (wheelRT.pivot.y - 0.5f) * wheelRT.rect.height;
                    BossHackWheel.FollowScreenOffset = new Vector2(offsetX, offsetY);
                }

                BossHackWheel.FollowWorldOffset = Vector3.zero;

                var optUp = new QuickHackOption { Name = "UP" };
                var optRight = new QuickHackOption { Name = "RIGHT" };
                var optDown = new QuickHackOption { Name = "DOWN" };
                var optLeft = new QuickHackOption { Name = "LEFT" };
                BossHackWheel.Open(optUp, optRight, optDown, optLeft);

                if (P2_Pillars[_p2TargetPillarIndex] != null)
                {
                    Renderer r = P2_Pillars[_p2TargetPillarIndex].GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        _uiTargetAnchor.position = r.bounds.center;
                        BossHackWheel.SetFollow(_uiTargetAnchor, Camera.main);
                    }
                    else
                    {
                        BossHackWheel.SetFollow(P2_Pillars[_p2TargetPillarIndex].transform, Camera.main);
                    }
                }
            }
        }

        private void UpdateP2HackingMinigame()
        {
            _p2HackTimer -= Time.deltaTime;

            if (_p2HackTimer <= 0f)
            {
                FailP2Hacking("TIMEOUT! (骇客超时)");
                return;
            }

            if (_p2InputCooldownTimer > 0f)
            {
                _p2InputCooldownTimer -= Time.deltaTime;
                if (_p2InputCooldownTimer <= 0f)
                {
                    if (BossHackWheel != null)
                    {
                        RectTransform wheelRT = BossHackWheel.GetComponent<RectTransform>();
                        if (wheelRT != null)
                        {
                            float offsetX = (wheelRT.pivot.x - 0.5f) * wheelRT.rect.width;
                            float offsetY = (wheelRT.pivot.y - 0.5f) * wheelRT.rect.height;
                            BossHackWheel.FollowScreenOffset = new Vector2(offsetX, offsetY);
                        }

                        var optUp = new QuickHackOption { Name = "UP" };
                        var optRight = new QuickHackOption { Name = "RIGHT" };
                        var optDown = new QuickHackOption { Name = "DOWN" };
                        var optLeft = new QuickHackOption { Name = "LEFT" };
                        BossHackWheel.Open(optUp, optRight, optDown, optLeft);
                    }
                    _p2LastInput = WheelDir.None;
                }
                return;
            }

            if (BossHackWheel != null && Mouse.current != null)
            {
                BossHackWheel.FeedMouseDelta(Mouse.current.delta.ReadValue());
            }

            WheelDir currentInput = BossHackWheel != null ? BossHackWheel.CurrentDir : WheelDir.None;

            if (currentInput != WheelDir.None && currentInput != _p2LastInput)
            {
                if (currentInput == _p2Sequence[_p2CurrentSeqIndex])
                {
                    _p2CurrentSeqIndex++;
                    RawCameraShake.Shake(0.05f, 0.1f);

                    if (_p2CurrentSeqIndex >= P2_SequenceLength)
                    {
                        _currentPhase = BossPhase.Transitioning;

                        if (BossHackWheel != null) BossHackWheel.Close();
                        Cursor.lockState = CursorLockMode.Locked;
                        DisablePlayerAbilities(false);
                        ExecuteHack(_p2TargetPillarIndex);
                    }
                    else
                    {
                        _p2InputCooldownTimer = 0.25f;
                    }
                }
                else
                {
                    FailP2Hacking("WRONG SEQUENCE! (输入错误)");
                }
            }
            else if (currentInput == WheelDir.None)
            {
                _p2LastInput = WheelDir.None;
            }
            else
            {
                _p2LastInput = currentInput;
            }
        }

        private void ExecuteHack(int pillarIndex)
        {
            _pillarStates[pillarIndex] = PillarState.Hacked;
            ChangePillarMaterial(pillarIndex, PillarHackedMat);

            _p2HackGauge = 0f;
            HideQTEPrompt();
            if (QTE_ProgressBar != null) QTE_ProgressBar.gameObject.SetActive(false);

            StartCoroutine(ShutHoloBoardRoutine(true));
        }

        private void FailP2Hacking(string reason)
        {
            _currentPhase = BossPhase.Transitioning;

            Debug.Log($"<color=red>【破解失败】{reason} 防火墙重置！</color>");
            RawCameraShake.Shake(0.5f, 0.3f);
            StartCoroutine(ShutHoloBoardRoutine(false));
        }

        private IEnumerator ShutHoloBoardRoutine(bool success)
        {
            if (BossHackWheel != null) BossHackWheel.Close();

            if (success && _holoBoardText != null)
            {
                _holoBoardText.color = Color.cyan;
                _holoBoardText.text = "\n\n<align=center><size=70><b>[ REBOOT SUCCESS ]</b></size></align>";

                yield return new WaitForSecondsRealtime(0.8f);
            }

            if (_holoBoardObj != null && _holoBoardObj.transform.childCount > 0)
            {
                Transform bgTransform = _holoBoardObj.transform.GetChild(0);

                float crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float y = Mathf.Lerp(1f, 0.02f, crtTime / 0.1f);
                    bgTransform.localScale = new Vector3(1f, y, 1f);
                    yield return null;
                }

                crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float x = Mathf.Lerp(1f, 0f, crtTime / 0.1f);
                    bgTransform.localScale = new Vector3(x, 0.02f, 1f);
                    yield return null;
                }

                _holoBoardObj.SetActive(false);
                bgTransform.localScale = new Vector3(1f, 0.02f, 1f);
            }

            Cursor.lockState = CursorLockMode.Locked;
            DisablePlayerAbilities(false);

            if (success)
            {
                StartCoroutine(BossStunRoutine());
            }
            else
            {
                _pillarStates[_p2TargetPillarIndex] = PillarState.Normal;
                ChangePillarMaterial(_p2TargetPillarIndex, PillarNormalMat);
                _currentPhase = BossPhase.Phase2_Idle;
            }
        }

        private void CreateHoloBoard()
        {
            if (_holoBoardObj != null) return;

            _holoBoardObj = new GameObject("BossHoloBoardUI");
            Canvas c = _holoBoardObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999;

            GameObject bgObj = new GameObject("DarkBG");
            bgObj.transform.SetParent(_holoBoardObj.transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.04f, 0.06f, 0.85f);

            bgObj.AddComponent<RectMask2D>();

            RectTransform bgRT = bg.rectTransform;
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(1000, 320);
            bgRT.anchoredPosition = new Vector2(0, -280);

            int hexOffset = 40;

            GameObject topLineObj = new GameObject("TopLine");
            topLineObj.transform.SetParent(bgObj.transform, false);
            Image topLine = topLineObj.AddComponent<Image>();
            topLine.color = new Color(0f, 1f, 1f, 0.8f);
            RectTransform topRT = topLine.rectTransform;
            topRT.anchorMin = new Vector2(0, 1);
            topRT.anchorMax = new Vector2(1, 1);
            topRT.offsetMin = new Vector2(0, -2);
            topRT.offsetMax = new Vector2(0, 0);

            GameObject botLineObj = new GameObject("BotLine");
            botLineObj.transform.SetParent(bgObj.transform, false);
            Image botLine = botLineObj.AddComponent<Image>();
            botLine.color = new Color(1f, 0f, 0.3f, 0.8f);
            RectTransform botRT = botLine.rectTransform;
            botRT.anchorMin = new Vector2(0, 0);
            botRT.anchorMax = new Vector2(1, 0);
            botRT.offsetMin = new Vector2(0, 0);
            botRT.offsetMax = new Vector2(0, 2);

            GameObject bgTextObj = new GameObject("Watermark");
            bgTextObj.transform.SetParent(bgObj.transform, false);
            TextMeshProUGUI bgText = bgTextObj.AddComponent<TextMeshProUGUI>();
            bgText.alignment = TextAlignmentOptions.Center;
            bgText.fontSize = 110;
            bgText.color = new Color(0f, 1f, 1f, 0.04f);
            bgText.enableWordWrapping = false;
            bgText.text = "01010110 01100010\n10100111 11010100";
            RectTransform bgTextRT = bgText.rectTransform;
            bgTextRT.anchorMin = Vector2.zero;
            bgTextRT.anchorMax = Vector2.one;
            bgTextRT.offsetMin = Vector2.zero;
            bgTextRT.offsetMax = Vector2.zero;

            GameObject scanlineObj = new GameObject("Scanline");
            scanlineObj.transform.SetParent(bgObj.transform, false);
            Image scanline = scanlineObj.AddComponent<Image>();
            scanline.color = new Color(0f, 1f, 1f, 0.15f);
            _scanlineRT = scanline.rectTransform;
            _scanlineRT.anchorMin = new Vector2(0, 1);
            _scanlineRT.anchorMax = new Vector2(1, 1);
            _scanlineRT.sizeDelta = new Vector2(0, 5);
            _scanlineRT.anchoredPosition = new Vector2(0, 0);

            GameObject txtObj = new GameObject("HoloText");
            txtObj.transform.SetParent(bgObj.transform, false);
            _holoBoardText = txtObj.AddComponent<TextMeshProUGUI>();
            _holoBoardText.alignment = TextAlignmentOptions.TopLeft;
            _holoBoardText.fontSize = 32;
            _holoBoardText.enableWordWrapping = false;

            RectTransform txtRT = _holoBoardText.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(20, -20);
            txtRT.offsetMax = new Vector2(-20, -20);

            _holoBoardObj.SetActive(false);
            Transform bgTransform = _holoBoardObj.transform.GetChild(0);
            bgTransform.localScale = new Vector3(1f, 0.02f, 1f);
        }

        private IEnumerator CrtOnHoloBoardRoutine()
        {
            if (_holoBoardObj == null) yield break;

            _holoBoardObj.SetActive(true);

            Transform bgTransform = _holoBoardObj.transform.GetChild(0);

            float crtTime = 0f;
            Vector3 targetScale = Vector3.one;

            while (crtTime < 0.15f)
            {
                crtTime += Time.unscaledDeltaTime;
                float y = Mathf.Lerp(0.02f, 1f, crtTime / 0.15f);

                bgTransform.localScale = new Vector3(1f, y, 1f);
                yield return null;
            }

            bgTransform.localScale = targetScale;
            Debug.Log("<color=cyan>【协议启动】CRT On 特效完成！防火墙协议正式切入！</color>");
        }

        private IEnumerator AnimateScanline()
        {
            if (_scanlineRT == null) yield break;
            float height = 320f;
            float speed = 180f;
            float currentY = 0f;

            while (_currentPhase == BossPhase.Phase2_HackingMinigame)
            {
                currentY -= speed * Time.unscaledDeltaTime;
                if (currentY < -height) currentY = 0f;

                _scanlineRT.anchoredPosition = new Vector2(0, currentY);
                yield return null;
            }
        }

        private string GetArrowString(WheelDir dir)
        {
            switch (dir)
            {
                case WheelDir.Up: return "↑";
                case WheelDir.Down: return "↓";
                case WheelDir.Left: return "←";
                case WheelDir.Right: return "→";
            }
            return "■";
        }

        private IEnumerator RollingHologramRoutine()
        {
            if (_holoBoardText == null) yield break;

            string[] hexChars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F" };
            float bootTime = 0.25f;

            while (_currentPhase == BossPhase.Phase2_HackingMinigame)
            {
                if (_holoBoardText != null)
                {
                    string seqLine = "";
                    string hexLine = "";

                    for (int i = 0; i < P2_SequenceLength; i++)
                    {
                        string randomHex = hexChars[Random.Range(0, 16)] + hexChars[Random.Range(0, 16)];
                        int basePos = 130 + (i * 200);
                        int hexOffset = 45;

                        if (bootTime > 0)
                        {
                            WheelDir randomDir = (WheelDir)Random.Range(1, 5);
                            seqLine += $"<pos={basePos}><color=#889999>[ {GetArrowString(randomDir)} ]</color>";
                            hexLine += $"<pos={basePos + hexOffset}><color=#889999>{randomHex}</color>";
                        }
                        else if (i < _p2CurrentSeqIndex)
                        {
                            seqLine += $"<pos={basePos}><color=#00FF00>[ OK ]</color>";
                            hexLine += $"<pos={basePos + hexOffset}><color=#00FF00>{randomHex}</color>";
                        }
                        else if (i == _p2CurrentSeqIndex)
                        {
                            seqLine += $"<pos={basePos}><color=#00FFFF>[ {GetArrowString(_p2Sequence[i])} ]</color>";
                            hexLine += $"<pos={basePos + hexOffset}><color=#00FFFF>{randomHex}</color>";
                        }
                        else
                        {
                            WheelDir randomDir = (WheelDir)Random.Range(1, 5);
                            seqLine += $"<pos={basePos}><color=#889999>[ {GetArrowString(randomDir)} ]</color>";
                            hexLine += $"<pos={basePos + hexOffset}><color=#889999>{randomHex}</color>";
                        }
                    }

                    string uiText = "";

                    if (bootTime > 0)
                    {
                        uiText += $"<align=center><color=red><b>[ INITIALIZING OVERRIDE... ]</b></color>\n";
                        uiText += $"<size=20>ESTABLISHING CONNECTION...</size></align>\n\n";
                        bootTime -= 0.05f;
                    }
                    else
                    {
                        uiText += $"<align=center><color=#FF0055><b>/// FIREWALL BYPASS ///</b></color>\n";
                        uiText += $"<size=20>UPLINK TIME: {_p2HackTimer:F1}s</size></align>\n\n";
                    }

                    uiText += $"<align=left><size=55>{seqLine}</size>\n";
                    uiText += $"<size=22>{hexLine}</size></align>";

                    _holoBoardText.text = uiText;
                }

                yield return new WaitForSecondsRealtime(0.05f);
            }
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
                RawCameraShake.Shake(1.2f, 0.8f);
                StartCoroutine(TransitionToPhase3());
                yield break;
            }

            DisablePlayerAbilities(true);
            Time.timeScale = 0.2f;

            CinemachineBlendDefinition oldBlend = default;
            if (_mainCameraBrain != null)
            {
                oldBlend = _mainCameraBrain.DefaultBlend;
                _mainCameraBrain.DefaultBlend = default;
                StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
            }

            if (QTE_CloseUpCamera != null && BossCore != null)
            {
                QTE_CloseUpCamera.gameObject.SetActive(true);
                QTE_CloseUpCamera.transform.position = BossCore.position + new Vector3(2f, 3f, -4f);
                QTE_CloseUpCamera.transform.LookAt(BossCore);
                QTE_CloseUpCamera.Lens.Dutch = Random.Range(-15f, 15f);
                QTE_CloseUpCamera.Priority = 200;
            }

            Vector2 originalPromptPos = Vector2.zero;
            if (QTE_PromptTextUI != null)
            {
                RectTransform rt = QTE_PromptTextUI.GetComponent<RectTransform>();
                if (rt != null)
                {
                    originalPromptPos = rt.anchoredPosition;
                    rt.anchoredPosition = originalPromptPos + new Vector2(0, 300);
                }
            }

            if (QTE_PromptTextUI != null && QTE_TextComponent != null)
            {
                QTE_PromptTextUI.SetActive(true);
                QTE_TextComponent.text = "<color=#FF0033><size=30>/// SYSTEM OVERRIDE ///</size>\n<size=65><b>[ CRITICAL ERROR ]</b></size>\n<size=25>INITIATING REBOOT SEQUENCE...</size></color>";

                float crtTime = 0f;
                while (crtTime < 0.15f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float y = Mathf.Lerp(0.02f, 1f, crtTime / 0.15f);
                    QTE_PromptTextUI.transform.localScale = new Vector3(_qteUIPromptOriginalScale.x, y * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                    yield return null;
                }
                QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;
            }

            Vector3 originalCorePos = BossCore.position;
            float spasmTime = 2.5f;
            float ghostTimer = 0f;
            float uiFlickerTimer = 0f;

            while (spasmTime > 0)
            {
                spasmTime -= Time.unscaledDeltaTime;
                ghostTimer -= Time.unscaledDeltaTime;
                uiFlickerTimer -= Time.unscaledDeltaTime;

                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.15f;
                    StartCoroutine(SpawnAfterimageRoutine(BossCore));
                    RawCameraShake.Shake(0.4f, 0.05f);
                }

                if (uiFlickerTimer <= 0f)
                {
                    uiFlickerTimer = Random.Range(0.05f, 0.15f);
                    float rand = Random.value;

                    if (rand < 0.6f)
                    {
                        ShowQTEPrompt("<color=#FF0033><size=30>/// SYSTEM OVERRIDE ///</size>\n<size=65><b>[ CRITICAL ERROR ]</b></size>\n<size=25>INITIATING REBOOT SEQUENCE...</size></color>");
                    }
                    else if (rand < 0.85f)
                    {
                        ShowQTEPrompt("<color=#550000><size=30>/// SY*T#M OV%RRI&E ///</size>\n<size=65><b>[ CR!T!CAL E#R0R ]</b></size>\n<size=25>IN!TI@T!NG R*B00T...</size></color>");
                    }
                    else if (rand < 0.95f)
                    {
                        string[] hexGlitch = { "0xDEADBEEF", "FATAL_EXCEPTION", "ERR_MEM_CORRUPT", "0xFFFFFFFF" };
                        string randomHex = hexGlitch[Random.Range(0, hexGlitch.Length)];
                        string color = Random.value > 0.5f ? "#00FFFF" : "#FFFFFF";
                        ShowQTEPrompt($"<color={color}><size=30>/// KERNEL PANIC ///</size>\n<size=65><b>[ {randomHex} ]</b></size>\n<size=25>DUMPING PHYSICAL MEMORY...</size></color>");
                    }
                    else
                    {
                        HideQTEPrompt();
                    }
                }

                BossCore.position = originalCorePos + (Random.insideUnitSphere * 0.05f);
                yield return null;
            }

            BossCore.position = originalCorePos;

            if (QTE_PromptTextUI != null && QTE_TextComponent != null)
            {
                QTE_PromptTextUI.SetActive(true);
                QTE_TextComponent.text = "<color=#00FFFF><size=30>/// SYSTEM OVERRIDE ///</size>\n<size=65><b>[ REBOOT SUCCESS ]</b></size>\n<size=25>FIREWALL RESTORED...</size></color>";

                yield return new WaitForSecondsRealtime(1.0f);

                float crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float y = Mathf.Lerp(1f, 0.02f, crtTime / 0.1f);
                    QTE_PromptTextUI.transform.localScale = new Vector3(_qteUIPromptOriginalScale.x, y * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                    yield return null;
                }

                crtTime = 0f;
                while (crtTime < 0.1f)
                {
                    crtTime += Time.unscaledDeltaTime;
                    float x = Mathf.Lerp(1f, 0f, crtTime / 0.1f);
                    QTE_PromptTextUI.transform.localScale = new Vector3(x * _qteUIPromptOriginalScale.x, 0.02f * _qteUIPromptOriginalScale.y, _qteUIPromptOriginalScale.z);
                    yield return null;
                }

                QTE_PromptTextUI.SetActive(false);
                QTE_PromptTextUI.transform.localScale = _qteUIPromptOriginalScale;
            }

            Time.timeScale = 1f;

            if (QTE_PromptTextUI != null)
            {
                RectTransform rt = QTE_PromptTextUI.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = originalPromptPos;
            }

            HideQTEPrompt();

            if (QTE_CloseUpCamera != null)
            {
                QTE_CloseUpCamera.Priority = 0;
                QTE_CloseUpCamera.Lens.Dutch = 0f;
            }

            if (_mainCameraBrain != null)
                _mainCameraBrain.DefaultBlend = oldBlend;

            DisablePlayerAbilities(false);

            _p2StateTimer = 1.0f;
            _currentPhase = BossPhase.Phase2_Idle;
            Debug.Log("【重启】Boss 杀毒完毕，开始下一轮扫描！");
        }

        private IEnumerator SpawnAfterimageRoutine(Transform target)
        {
            if (target == null) yield break;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            System.Collections.Generic.List<GameObject> ghosts = new System.Collections.Generic.List<GameObject>();
            System.Collections.Generic.List<Mesh> bakedMeshes = new System.Collections.Generic.List<Mesh>();

            System.Collections.Generic.List<Vector3> startPositions = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector3> pullTargetPositions = new System.Collections.Generic.List<Vector3>();

            Shader defaultShader = Shader.Find("Sprites/Default");
            if (defaultShader == null) defaultShader = Shader.Find("Unlit/Transparent");

            Vector3 pullOffset = Random.onUnitSphere * Random.Range(0.3f, 0.8f);

            foreach (Renderer r in renderers)
            {
                if (r.gameObject.activeInHierarchy && r.enabled)
                {
                    Mesh meshToDraw = null;
                    if (r is MeshRenderer && r.GetComponent<MeshFilter>() != null)
                        meshToDraw = r.GetComponent<MeshFilter>().mesh;
                    else if (r is SkinnedMeshRenderer)
                    {
                        meshToDraw = new Mesh();
                        ((SkinnedMeshRenderer)r).BakeMesh(meshToDraw);
                        bakedMeshes.Add(meshToDraw);
                    }

                    if (meshToDraw != null)
                    {
                        GameObject ghost = new GameObject("GlitchGhost");
                        ghost.transform.position = r.transform.position;
                        ghost.transform.rotation = r.transform.rotation;
                        ghost.transform.localScale = r.transform.lossyScale * 1.02f;

                        MeshFilter mf = ghost.AddComponent<MeshFilter>();
                        mf.mesh = meshToDraw;
                        MeshRenderer mr = ghost.AddComponent<MeshRenderer>();

                        Material mat = new Material(defaultShader);
                        Color glitchColor = Random.value > 0.5f ? new Color(0f, 1f, 1f, 0.6f) : new Color(1f, 0f, 0.5f, 0.6f);
                        mat.color = glitchColor;
                        mr.material = mat;

                        ghosts.Add(ghost);
                        startPositions.Add(r.transform.position);
                        pullTargetPositions.Add(r.transform.position + pullOffset);
                    }
                }
            }

            float lifeTime = 0.3f;
            float t = 0;
            while (t < lifeTime)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / lifeTime;

                float elasticPull = Mathf.Sin(progress * Mathf.PI);

                for (int i = 0; i < ghosts.Count; i++)
                {
                    if (ghosts[i] != null)
                    {
                        ghosts[i].transform.position = Vector3.Lerp(startPositions[i], pullTargetPositions[i], elasticPull);

                        Color c = ghosts[i].GetComponent<Renderer>().material.color;
                        c.a = Mathf.Lerp(0.6f, 0f, progress * progress);
                        ghosts[i].GetComponent<Renderer>().material.color = c;
                    }
                }
                yield return null;
            }

            foreach (GameObject g in ghosts) if (g != null) Destroy(g);
            foreach (Mesh m in bakedMeshes) if (m != null) Destroy(m);
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

            Vector3 originalCorePos = BossCore.position;
            float spasmTime = 1.5f;
            while (spasmTime > 0)
            {
                spasmTime -= Time.deltaTime;
                BossCore.position = originalCorePos + (Random.insideUnitSphere * 0.4f);
                yield return null;
            }
            BossCore.position = originalCorePos;

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    if (P2_DynamicLockCamera) P2_DynamicLockCamera.Priority = 0;
                    if (P3_CoreAscendCamera) P3_CoreAscendCamera.Priority = 100;
                    DisablePlayerAbilities(true);
                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });
                yield return new WaitForSeconds(1.0f);
            }

            if (GlobalWindParticles != null) GlobalWindParticles.Play();
            if (CoreEnergyParticles != null) CoreEnergyParticles.Play();

            Vector3 ascendTarget = P3_CoreAscendPos != null ? P3_CoreAscendPos.position : originalCorePos + Vector3.up * 8f;
            float ascendDuration = 4f;
            float t = 0;
            while (t < ascendDuration)
            {
                t += Time.deltaTime;
                BossCore.position = Vector3.Lerp(originalCorePos, ascendTarget, Mathf.SmoothStep(0, 1, t / ascendDuration));

                RawCameraShake.Shake(0.08f, 0.15f);
                yield return null;
            }

            yield return new WaitForSeconds(1f);

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

                        _player.transform.position = P3_PlayerStartPos.position + Vector3.up * 0.1f;
                        _player.transform.rotation = P3_PlayerStartPos.rotation;

                        if (cc != null) cc.enabled = true;
                    }

                    if (_playerAnimator != null)
                    {
                        _playerAnimator.enabled = true;
                        _playerAnimator.Play("StruggleWalk", 0, 0f);
                        _playerAnimator.speed = 0f;
                    }

                    if (P2_Pillars != null)
                    {
                        foreach (GameObject pillar in P2_Pillars)
                        {
                            if (pillar != null) pillar.SetActive(false);
                        }
                    }

                    if (_mainCameraBrain != null)
                    {
                        _mainCameraBrain.DefaultBlend = default;
                        StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
                    }
                });
                yield return new WaitForSeconds(1.0f);
            }

            _currentPhase = BossPhase.Phase3_Walk1;
            ShowQTEPrompt("[A] / [D] Alternate steps to move forward!");
        }

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

                float pulseTimer = 2.0f;
                ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");

                yield return null;

                while (targetProgress < totalSteps || currentProgress < totalSteps - 0.1f)
                {
                    pulseTimer -= Time.deltaTime;
                    if (pulseTimer <= 0f && _currentPulseRadius < 0f)
                    {
                        pulseTimer = 2.0f;
                        _pulseHandledThisRound = false;
                        StartCoroutine(SpawnPulseVisualEffect());
                    }

                    bool isPulseIncoming = false;

                    if (_currentPulseRadius > 0f && !_pulseHandledThisRound)
                    {
                        Vector2 bossPos2D = new Vector2(BossCore.position.x, BossCore.position.z);
                        Vector2 playerPos2D = new Vector2(_player.transform.position.x, _player.transform.position.z);
                        float distToPlayer = Vector2.Distance(bossPos2D, playerPos2D);

                        float distanceToWave = distToPlayer - _currentPulseRadius;

                        if (distanceToWave <= 8.0f && distanceToWave >= 0f)
                        {
                            isPulseIncoming = true;

                            if (Mathf.FloorToInt(Time.time * 15) % 2 == 0)
                                ShowQTEPrompt("<color=red> Pulse is coming! Press [E] !</color>");
                            else
                                ShowQTEPrompt("<color=white> Pulse is coming! Press [E] !</color>");

                            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                            {
                                _pulseHandledThisRound = true;
                                Debug.Log("<color=cyan>成功驱散脉冲！格挡成功，留在原地！</color>");
                                RawCameraShake.Shake(0.3f, 0.2f);
                                ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");
                            }
                        }
                        else if (distanceToWave < 0f)
                        {
                            _pulseHandledThisRound = true;
                            Debug.Log("<color=red>Struck by electromagnetic waves! Defused!</color>");
                            RawCameraShake.Shake(1.0f, 0.5f);
                            targetProgress = Mathf.Max(0, targetProgress - 1.5f);
                            ShowQTEPrompt(expectLeft ? "[A] Move Your Left" : "[D] Move Your Right");
                        }
                    }

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
        private IEnumerator SpawnPulseVisualEffect()
        {
            if (BossCore == null || _player == null) yield break;

            GameObject pulseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pulseDisc.GetComponent<Collider>());

            pulseDisc.transform.position = new Vector3(BossCore.position.x, _player.transform.position.y + 0.75f, BossCore.position.z);

            Renderer r = pulseDisc.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (PulseWaveMat != null) r.material = PulseWaveMat;

            float duration = 2.5f;
            float t = 0;

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
        #region Tools & UI
        private IEnumerator CinematicCameraPan(Transform target, float transitionTime, float endSlowMoScale)
        {
            DisablePlayerAbilities(true);
            if (QTE_CloseUpCamera == null || target == null) yield break;

            if (_mainCameraBrain != null)
            {
                _mainCameraBrain.DefaultBlend = default;
                StartCoroutine(RestoreBlendNextFrame(_initialCameraBlend));
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

        // ==========================================
        // 【UI 缩放动画】：仅保留唯一的一份！
        // ==========================================
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
        #endregion

        #region Ending Cinematic (大结局演出)
        private IEnumerator WhiteoutEndingRoutine()
        {
            GameObject canvasObj = new GameObject("EndingWhiteoutCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            GameObject imageObj = new GameObject("WhiteImage");
            imageObj.transform.SetParent(canvasObj.transform, false);
            Image img = imageObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);

            RectTransform rect = img.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

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

            yield return new WaitForSecondsRealtime(2.5f);

            img.color = Color.black;

            Debug.Log("<color=green>【结局】屏幕已黑！等待后续结局制作...</color>");
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

    // ==========================================
    // 【全新黑客机制】：数据流闪现弹幕病毒脚本
    // ==========================================
    public class DataStreamProjectile : MonoBehaviour
    {
        private TextMeshPro[] _chars;
        private Vector3[] _history;
        private int _length = 12;
        private float _lifeTime = 0f;
        private bool _hasTeleported = false;

        private void Start()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;

            _chars = new TextMeshPro[_length];
            _history = new Vector3[_length];

            Color streamColor = Random.value > 0.5f ? new Color(0.1f, 1f, 0.2f, 0.9f) : new Color(0f, 0.7f, 0.1f, 0.9f);

            for (int i = 0; i < _length; i++)
            {
                GameObject textObj = new GameObject("DataChar");
                textObj.transform.position = transform.position;

                TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
                tmp.text = Random.value > 0.5f ? "0" : "1";

                tmp.fontSize = Mathf.Lerp(10f, 4f, (float)i / _length);
                tmp.alignment = TextAlignmentOptions.Center;

                Color c = (i == 0) ? Color.white : streamColor;
                c.a = Mathf.Lerp(1f, 0f, (float)i / _length);
                tmp.color = c;

                _chars[i] = tmp;
                _history[i] = transform.position;
            }
        }

        private void Update()
        {
            _lifeTime += Time.deltaTime;

            if (!_hasTeleported && _lifeTime > 0.3f && Random.value < 0.05f)
            {
                _hasTeleported = true;
                transform.position += transform.forward * 4.0f;

                for (int i = 0; i < _length; i++)
                {
                    _history[i] = transform.position - transform.forward * (i * 0.3f) + Random.insideUnitSphere * 0.8f;
                }
            }
        }

        private void LateUpdate()
        {
            if (Vector3.Distance(_history[0], transform.position) > 0.2f)
            {
                for (int i = _length - 1; i > 0; i--)
                {
                    _history[i] = _history[i - 1];
                }
                _history[0] = transform.position;
            }

            for (int i = 0; i < _length; i++)
            {
                if (_chars[i] != null)
                {
                    Vector3 targetPos = _history[i] + Random.insideUnitSphere * 0.05f;
                    _chars[i].transform.position = Vector3.Lerp(_chars[i].transform.position, targetPos, Time.deltaTime * 25f);

                    if (Camera.main != null) _chars[i].transform.rotation = Camera.main.transform.rotation;

                    if (Random.value < 0.1f) _chars[i].text = Random.value > 0.5f ? "0" : "1";
                }
            }
        }

        private void OnDestroy()
        {
            if (_chars != null)
            {
                foreach (var tmp in _chars)
                {
                    if (tmp != null)
                    {
                        tmp.gameObject.AddComponent<DataParticleFade>();
                    }
                }
            }
        }
    }

    public class DataParticleFade : MonoBehaviour
    {
        private TextMeshPro _tmp;
        private float _timer = 0f;
        private float _duration = 0.3f;

        private void Start()
        {
            _tmp = GetComponent<TextMeshPro>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                Destroy(gameObject);
                return;
            }
            if (_tmp != null)
            {
                Color c = _tmp.color;
                c.a = Mathf.Lerp(c.a, 0f, _timer / _duration);
                _tmp.color = c;
            }
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace TheGlitch
{
    public class MainframeBossManager : MonoBehaviour
    {
        private enum BossPhase { Waiting, P1_Dodging, P1_QTE, Transitioning, P2_Idle, P2_Charging, P2_BlockQTE, P2_PunishQTE, Dead }
        private BossPhase _currentPhase = BossPhase.Waiting;

        [Header("基础与场景设置")]
        public GameObject BossDoor;
        public Transform DoorClosedPos;
        public CinemachineCamera P1_TopCamera;
        public CinemachineCamera QTE_CloseUpCamera;
        private GameObject _player;

        [Header("第一阶段：系统清洗")]
        public GameObject AoEWarningPrefab;
        public Transform[] BombLocations;
        public float WarningTime = 1.5f;
        public GameObject[] P1_WeakPoints;
        public Material WeakPointExposedMat;
        private Material[] _wpOriginalMats;
        private int _currentWeakPointIndex = 0;

        [Header("第二阶段：核心直连")]
        public GameObject BossArmorPlates;
        public GameObject BossCore;
        public GameObject BossLaserVFX;
        public GameObject PlayerShieldVFX;
        public float P2_ChargeTime = 2.0f;
        public float P2_BlockWindow = 1.5f;
        private int _p2RoundsCompleted = 0;

        [Header("UI 设置")]
        public GameObject WarningTextUI;
        public GameObject QTE_PromptTextUI;
        public TMPro.TextMeshProUGUI QTE_TextComponent;
        public Slider QTE_ProgressBar;
        public Slider BossHealthBar;

        [Header("QTE 数值")]
        public int P1_HacksRequired = 15;
        public int P2_PunishRequired = 20;
        private int _currentMashes = 0;
        private float _qteTimer = 0f;

        private void Start()
        {
            if (WarningTextUI) WarningTextUI.SetActive(false);
            if (QTE_PromptTextUI) QTE_PromptTextUI.SetActive(false);
            if (QTE_ProgressBar) QTE_ProgressBar.gameObject.SetActive(false);
            if (BossHealthBar) BossHealthBar.gameObject.SetActive(false);
            if (BossLaserVFX) BossLaserVFX.SetActive(false);
            if (PlayerShieldVFX) PlayerShieldVFX.SetActive(false);

            _wpOriginalMats = new Material[P1_WeakPoints.Length];
            for (int i = 0; i < P1_WeakPoints.Length; i++)
            {
                if (P1_WeakPoints[i] != null)
                {
                    _wpOriginalMats[i] = P1_WeakPoints[i].GetComponent<Renderer>().material;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && _currentPhase == BossPhase.Waiting)
            {
                _player = other.gameObject;
                StartCoroutine(IntroRoutine());
            }
        }

        #region 第一阶段逻辑
        private IEnumerator IntroRoutine()
        {
            _currentPhase = BossPhase.P1_Dodging;
            if (BossDoor && DoorClosedPos) BossDoor.transform.position = DoorClosedPos.position;
            DisablePlayerAbilities(true);

            if (WarningTextUI) WarningTextUI.SetActive(true);
            if (P1_TopCamera) P1_TopCamera.Priority = 100;

            yield return new WaitForSeconds(3f);
            if (WarningTextUI) WarningTextUI.SetActive(false);

            StartCoroutine(P1_BombingRoutine());
        }

        private IEnumerator P1_BombingRoutine()
        {
            _currentPhase = BossPhase.P1_Dodging;

            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Transform spawnPoint = BombLocations[Random.Range(0, BombLocations.Length)];
                    StartCoroutine(SpawnAoE(spawnPoint.position));
                }
                yield return new WaitForSeconds(WarningTime + 1.0f);
            }

            if (_currentWeakPointIndex < P1_WeakPoints.Length)
            {
                _currentPhase = BossPhase.P1_QTE;
                GameObject targetWP = P1_WeakPoints[_currentWeakPointIndex];
                if (targetWP && WeakPointExposedMat) targetWP.GetComponent<Renderer>().material = WeakPointExposedMat;

                Time.timeScale = 0f;

                if (QTE_CloseUpCamera)
                {
                    QTE_CloseUpCamera.Follow = targetWP.transform;
                    QTE_CloseUpCamera.LookAt = targetWP.transform;
                    QTE_CloseUpCamera.Priority = 200;
                }

                ShowQTEPrompt("[E] INITIATE HACK");
            }
        }

        private IEnumerator SpawnAoE(Vector3 position)
        {
            GameObject warning = Instantiate(AoEWarningPrefab, position, Quaternion.identity);
            yield return new WaitForSeconds(WarningTime);
            if (warning) warning.GetComponent<Renderer>().material.color = Color.white;
            yield return new WaitForSeconds(0.2f);
            if (warning) Destroy(warning);
        }
        #endregion

        #region 第二阶段逻辑
        private IEnumerator TransitionToPhase2()
        {
            _currentPhase = BossPhase.Transitioning;

            if (P1_TopCamera) P1_TopCamera.Priority = 10;
            if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;
            Time.timeScale = 1.0f;

            if (BossArmorPlates) BossArmorPlates.SetActive(false);
            yield return new WaitForSeconds(1.5f);

            if (BossHealthBar)
            {
                BossHealthBar.gameObject.SetActive(true);
                BossHealthBar.maxValue = 3;
                BossHealthBar.value = 3;
            }

            yield return new WaitForSeconds(1.0f);
            StartCoroutine(P2_AttackRoutine());
        }

        private IEnumerator P2_AttackRoutine()
        {
            _currentPhase = BossPhase.P2_Charging;
            if (BossCore) BossCore.GetComponent<Renderer>().material.color = Color.red;
            yield return new WaitForSeconds(P2_ChargeTime);

            _currentPhase = BossPhase.P2_BlockQTE;
            _qteTimer = P2_BlockWindow;
            Time.timeScale = 0f;

            if (QTE_CloseUpCamera && BossCore)
            {
                QTE_CloseUpCamera.Follow = BossCore.transform;
                QTE_CloseUpCamera.LookAt = BossCore.transform;
                QTE_CloseUpCamera.Priority = 200;
            }

            ShowQTEPrompt("[Q] BLOCK!");
        }

        private IEnumerator P2_PunishRoutine()
        {
            _currentPhase = BossPhase.P2_PunishQTE;

            // ==========================================
            // 【核心追踪】：动态计算护盾出现的位置！
            // ==========================================
            if (PlayerShieldVFX && _player != null && BossCore != null)
            {
                PlayerShieldVFX.SetActive(true);

                // 1. 算出从玩家指向 Boss 的方向
                Vector3 dirToBoss = (BossCore.transform.position - _player.transform.position).normalized;

                // 2. 把护盾放在玩家胸口偏前 1.5 米的地方
                Vector3 shieldPos = _player.transform.position + (Vector3.up * 1.5f) + (dirToBoss * 1.5f);
                PlayerShieldVFX.transform.position = shieldPos;

                // 3. 让护盾永远正面朝着 Boss
                PlayerShieldVFX.transform.LookAt(BossCore.transform.position);
            }

            if (BossCore) BossCore.GetComponent<Renderer>().material.color = Color.gray;

            _currentMashes = 0;
            _qteTimer = 5.0f;
            Time.timeScale = 0.2f;

            ShowQTEPrompt("[E] OVERRIDE");
            if (QTE_ProgressBar)
            {
                QTE_ProgressBar.gameObject.SetActive(true);
                QTE_ProgressBar.maxValue = P2_PunishRequired;
                QTE_ProgressBar.value = 0;
            }

            yield return null;
        }
        #endregion

        #region 输入监听
        private void Update()
        {
            if (Keyboard.current == null) return;

            if (_currentPhase == BossPhase.P1_QTE && !QTE_ProgressBar.gameObject.activeSelf)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    HideQTEPrompt();
                    Time.timeScale = 0.2f;

                    _currentMashes = 0;
                    _qteTimer = 4.0f;
                    QTE_ProgressBar.gameObject.SetActive(true);
                    QTE_ProgressBar.maxValue = P1_HacksRequired;
                    QTE_ProgressBar.value = 0;
                }
            }
            else if (_currentPhase == BossPhase.P1_QTE && QTE_ProgressBar.gameObject.activeSelf)
            {
                _qteTimer -= Time.unscaledDeltaTime;
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    _currentMashes++;
                    QTE_ProgressBar.value = _currentMashes;
                }

                if (_currentMashes >= P1_HacksRequired)
                {
                    Time.timeScale = 1.0f;
                    QTE_ProgressBar.gameObject.SetActive(false);
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;

                    if (P1_WeakPoints[_currentWeakPointIndex]) P1_WeakPoints[_currentWeakPointIndex].SetActive(false);
                    _currentWeakPointIndex++;

                    if (_currentWeakPointIndex >= P1_WeakPoints.Length) StartCoroutine(TransitionToPhase2());
                    else StartCoroutine(P1_BombingRoutine());
                }
                else if (_qteTimer <= 0f)
                {
                    Time.timeScale = 1.0f;
                    QTE_ProgressBar.gameObject.SetActive(false);
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;
                    if (P1_WeakPoints[_currentWeakPointIndex]) P1_WeakPoints[_currentWeakPointIndex].GetComponent<Renderer>().material = _wpOriginalMats[_currentWeakPointIndex];
                    StartCoroutine(P1_BombingRoutine());
                }
            }
            else if (_currentPhase == BossPhase.P2_BlockQTE)
            {
                _qteTimer -= Time.unscaledDeltaTime;

                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    HideQTEPrompt();
                    StartCoroutine(P2_PunishRoutine());
                }
                else if (_qteTimer <= 0f)
                {
                    HideQTEPrompt();
                    Time.timeScale = 1.0f;
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;
                    Debug.Log("防反失败！玩家扣血！");

                    // ==========================================
                    // 【核心追踪】：动态计算死光发射的位置与角度！
                    // ==========================================
                    if (BossLaserVFX && BossCore != null && _player != null)
                    {
                        BossLaserVFX.SetActive(true);

                        // 1. 让激光的起点吸附在 Boss 核心上
                        BossLaserVFX.transform.position = BossCore.transform.position;

                        // 2. 让激光像狙击枪一样死死盯住玩家的胸口！
                        Vector3 playerChestPos = _player.transform.position + Vector3.up * 1.5f;
                        BossLaserVFX.transform.LookAt(playerChestPos);
                    }

                    Invoke(nameof(HideLaser), 0.5f);
                    StartCoroutine(P2_AttackRoutine());
                }
            }
            else if (_currentPhase == BossPhase.P2_PunishQTE)
            {
                _qteTimer -= Time.unscaledDeltaTime;
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    _currentMashes++;
                    QTE_ProgressBar.value = _currentMashes;
                }

                if (_currentMashes >= P2_PunishRequired)
                {
                    Time.timeScale = 1.0f;
                    HideQTEPrompt();
                    QTE_ProgressBar.gameObject.SetActive(false);
                    if (PlayerShieldVFX) PlayerShieldVFX.SetActive(false);
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;

                    _p2RoundsCompleted++;
                    if (BossHealthBar) BossHealthBar.value = 3 - _p2RoundsCompleted;

                    if (_p2RoundsCompleted >= 3)
                    {
                        _currentPhase = BossPhase.Dead;
                        Debug.Log("BOSS 已摧毁！");
                        DisablePlayerAbilities(false);
                        if (BossHealthBar) BossHealthBar.gameObject.SetActive(false);
                    }
                    else
                    {
                        StartCoroutine(P2_AttackRoutine());
                    }
                }
                else if (_qteTimer <= 0f)
                {
                    Time.timeScale = 1.0f;
                    HideQTEPrompt();
                    QTE_ProgressBar.gameObject.SetActive(false);
                    if (PlayerShieldVFX) PlayerShieldVFX.SetActive(false);
                    if (QTE_CloseUpCamera) QTE_CloseUpCamera.Priority = 10;
                    StartCoroutine(P2_AttackRoutine());
                }
            }
        }
        #endregion

        #region 工具方法
        private void DisablePlayerAbilities(bool disable)
        {
            if (_player == null) return;
            var recorder = _player.GetComponent<PlayerEchoRecorder>();
            if (recorder != null) recorder.enabled = !disable;

            var scanner = Object.FindFirstObjectByType<ScannerWireInteractor>();
            if (scanner != null) scanner.enabled = !disable;
        }

        private void ShowQTEPrompt(string text)
        {
            if (QTE_PromptTextUI) QTE_PromptTextUI.SetActive(true);
            if (QTE_TextComponent) QTE_TextComponent.text = text;
        }

        private void HideQTEPrompt()
        {
            if (QTE_PromptTextUI) QTE_PromptTextUI.SetActive(false);
        }

        private void HideLaser()
        {
            if (BossLaserVFX) BossLaserVFX.SetActive(false);
        }
        #endregion
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace TheGlitch
{
    public class AsyncSceneTeleporter : MonoBehaviour
    {
        [Header("异步场景传送设置")]
        public string SceneToLoad = "Level_Boss";
        public string SpawnPointName = "BossSpawnPoint";

        [Header("交互 UI")]
        public GameObject PromptUI;

        [Header("🎬 沉浸式加载界面 (Loading Screen)")]
        public GameObject LoadingScreenRoot;
        public TextMeshProUGUI LoadingText;
        public float FadeInDuration = 0.5f;
        public float FadeOutDuration = 1.0f;
        public float CameraSettleTime = 1.0f;

        [Header("⚙️ Hackwheel 专属设置")]
        public GameObject HackwheelSpinner;
        public string CursorSymbol = "█";

        private CharacterController _playerCC;
        private bool _canTeleport = false;
        private bool _isLoading = false;

        private void Start()
        {
            if (PromptUI != null) PromptUI.SetActive(false);
            if (LoadingScreenRoot != null) LoadingScreenRoot.SetActive(false);
            if (HackwheelSpinner != null) HackwheelSpinner.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerCC = other.GetComponent<CharacterController>();
                _canTeleport = true;
                if (PromptUI != null && !_isLoading) PromptUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_playerCC != null && other.gameObject == _playerCC.gameObject) _playerCC = null;
                _canTeleport = false;
                if (PromptUI != null) PromptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (_canTeleport && !_isLoading && _playerCC != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                StartCoroutine(ExecuteAsyncTeleport());
            }
        }

        private IEnumerator ExecuteAsyncTeleport()
        {
            _isLoading = true;
            _canTeleport = false;

            // 👑 关键修复：把玩家引用存到局部变量里保护起来！然后再清空全局引用！
            CharacterController playerToTeleport = _playerCC;
            _playerCC = null;

            if (PromptUI != null) PromptUI.SetActive(false);

            // ==========================================
            // 1. 丝滑淡入黑屏
            // ==========================================
            if (LoadingScreenRoot != null)
            {
                LoadingScreenRoot.SetActive(true);
                CanvasGroup cg = LoadingScreenRoot.GetComponent<CanvasGroup>();
                if (cg == null) cg = LoadingScreenRoot.AddComponent<CanvasGroup>();

                if (LoadingText != null) LoadingText.text = "";
                cg.alpha = 0f;

                float fadeTimer = 0f;
                while (fadeTimer < FadeInDuration)
                {
                    fadeTimer += Time.deltaTime;
                    cg.alpha = Mathf.Lerp(0f, 1f, fadeTimer / FadeInDuration);
                    yield return null;
                }
                cg.alpha = 1f;

                yield return null;
                yield return null;
            }

            if (HackwheelSpinner != null) HackwheelSpinner.SetActive(true);

            // ==========================================
            // 2. 纯黑屏掩护下加载场景
            // ==========================================
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(SceneToLoad, LoadSceneMode.Additive);
            while (!asyncLoad.isDone) yield return null;

            // ==========================================
            // 3. 极客级打字特效
            // ==========================================
            if (LoadingText != null)
            {
                LoadingText.text = "";
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(HackerTypewriterRoutine("PURGE THE ANOMALY"));
            }

            // ==========================================
            // 4. 彻底掩护完毕，把保护好的玩家变量传进去执行传送！
            // ==========================================
            PerformTeleportCore(playerToTeleport);

            // ==========================================
            // 5. 相机落地缓冲期
            // ==========================================
            yield return new WaitForSeconds(CameraSettleTime);

            if (HackwheelSpinner != null) HackwheelSpinner.SetActive(false);

            // ==========================================
            // 6. 缓冲完毕，黑屏丝滑淡出
            // ==========================================
            if (LoadingScreenRoot != null)
            {
                CanvasGroup cg = LoadingScreenRoot.GetComponent<CanvasGroup>();
                float fadeTimer = 0f;
                while (fadeTimer < FadeOutDuration)
                {
                    fadeTimer += Time.deltaTime;
                    cg.alpha = Mathf.Lerp(1f, 0f, fadeTimer / FadeOutDuration);
                    yield return null;
                }
                LoadingScreenRoot.SetActive(false);
                if (LoadingText != null) LoadingText.text = "";
            }

            _isLoading = false;
        }

        private IEnumerator HackerTypewriterRoutine(string targetText)
        {
            if (LoadingText == null) yield break;

            targetText = targetText.ToUpper();
            string cursorHtml = CursorSymbol;

            LoadingText.maxVisibleCharacters = 99999;
            LoadingText.text = "";

            for (int i = 0; i <= targetText.Length; i++)
            {
                LoadingText.text = targetText.Substring(0, i) + cursorHtml;

                if (i < targetText.Length)
                {
                    float pauseTime = Random.Range(0.02f, 0.08f);
                    if (Random.value < 0.15f) pauseTime = Random.Range(0.2f, 0.5f);
                    yield return new WaitForSeconds(pauseTime);
                }
            }

            for (int blink = 0; blink < 3; blink++)
            {
                LoadingText.text = targetText;
                yield return new WaitForSeconds(0.35f);

                LoadingText.text = targetText + cursorHtml;
                yield return new WaitForSeconds(0.25f);
            }

            yield return new WaitForSeconds(0.5f);
        }

        // 注意这里接收了刚刚保护起来的 player 变量
        private void PerformTeleportCore(CharacterController player)
        {
            if (player == null) return;

            // 🚨 【2D 吃豆人防护机制】
            BillboardAvatarController2D avatar2D = player.GetComponent<BillboardAvatarController2D>();
            if (avatar2D != null)
            {
                BillboardPortalEnter enterSys = Object.FindFirstObjectByType<BillboardPortalEnter>();
                if (enterSys != null && enterSys.PlayerRoot != null)
                {
                    player = enterSys.PlayerRoot.GetComponent<CharacterController>();
                    foreach (var r in enterSys.PlayerRoot.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                    if (enterSys.VCam2D != null) enterSys.VCam2D.gameObject.SetActive(false);
                    Destroy(avatar2D.gameObject);
                }
            }

            GameObject spawnObj = GameObject.Find(SpawnPointName);
            if (spawnObj == null)
            {
                Debug.LogError($"[传送失败] 无法在场景中找到名为 '{SpawnPointName}' 的出生点！");
                return;
            }

            Transform targetTransform = spawnObj.transform;

            player.enabled = false;
            player.transform.position = targetTransform.position;
            player.transform.rotation = targetTransform.rotation;

            var playerScript = player.GetComponent<MonoBehaviour>();
            foreach (var comp in player.GetComponents<MonoBehaviour>())
            {
                if (comp.GetType().Name.Contains("Controller"))
                {
                    playerScript = comp;
                    break;
                }
            }

            if (playerScript != null)
            {
                var type = playerScript.GetType();
                var yawField = type.GetField("_cinemachineTargetYaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (yawField != null) yawField.SetValue(playerScript, targetTransform.eulerAngles.y);

                var pitchField = type.GetField("_cinemachineTargetPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pitchField != null) pitchField.SetValue(playerScript, 0f);
            }

            player.enabled = true;

            var allCams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam.gameObject.activeInHierarchy)
                {
                    cam.PreviousStateIsValid = false;
                }
            }
        }
    }
}
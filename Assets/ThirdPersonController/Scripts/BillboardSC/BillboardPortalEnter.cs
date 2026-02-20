using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheGlitch
{
    public class BillboardPortalEnter : MonoBehaviour
    {
        [Header("核心引用")]
        public Billboard2DRail SceneRail;
        public Transform PlayerRoot;

        [Header("2D 设置")]
        public GameObject Avatar2DPrefab;
        public CinemachineCamera VCam2D;
        public int VCamActivePriority = 20;

        [Header("遮挡处理")]
        public GameObject AdSurfaceRoot;

        [Header("UI")]
        public GameObject PromptUI;

        public CameraTargetSmoother SmoothTarget;
        // --- 内部缓存 ---
        private List<MonoBehaviour> _cachedScripts = new List<MonoBehaviour>();
        private List<Renderer> _cachedRenderers = new List<Renderer>();

        private bool _canEnter;
        private GameObject _spawnedAvatar;
        private float _enterCooldown = 0f;

        private void Start()
        {
            if (PlayerRoot == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) PlayerRoot = p.transform;
            }

            if (PlayerRoot != null)
            {
                _cachedRenderers.AddRange(PlayerRoot.GetComponentsInChildren<Renderer>(true));
                var allScripts = PlayerRoot.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var s in allScripts)
                {
                    string sName = s.GetType().Name;
                    if (sName.Contains("Controller") || sName.Contains("Input") || sName.Contains("Character") || sName.Contains("Starter"))
                    {
                        if (s != this) _cachedScripts.Add(s);
                    }
                }
            }

            if (PromptUI) PromptUI.SetActive(false);
            if (VCam2D != null) VCam2D.gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _canEnter = true;
                if (PromptUI) PromptUI.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _canEnter = false;
                if (PromptUI) PromptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (_enterCooldown > 0f)
            {
                _enterCooldown -= Time.deltaTime;
                return;
            }

            if (_canEnter && _spawnedAvatar == null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Enter2DMode();
            }
        }

        private void Enter2DMode()
        {
            Debug.Log("进入 2D 模式...");
            if (PromptUI) PromptUI.SetActive(false);

            // 1. 隐藏外部遮挡板
            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(false);
            else Debug.LogWarning("AdSurfaceRenderer 没拖！记得去 Inspector 里把 Quad 拖进去！");

            // 2. 禁用 3D 玩家
            foreach (var s in _cachedScripts) if (s) s.enabled = false;
            foreach (var r in _cachedRenderers) if (r) r.enabled = false;

            // 3. 计算位置
            float startS = 0f;
            if (SceneRail != null && PlayerRoot != null)
            {
                SceneRail.ClosestPoint(PlayerRoot.position, out startS);
            }

            // 4. 生成 2D 替身
            _spawnedAvatar = Instantiate(Avatar2DPrefab, transform.position, Quaternion.identity);

            var ctrl = _spawnedAvatar.GetComponent<BillboardAvatarController2D>();
            if (ctrl != null)
            {
                ctrl.InitOnRail(SceneRail, startS);
            }

            // 5. 设置相机 (关键修改)
            // 2. 修改 Enter2DMode 的最后部分
            if (VCam2D != null)
            {
                VCam2D.Priority = VCamActivePriority;

                // 如果有平滑替身，就用替身；否则还用原来的
                if (SmoothTarget != null)
                {
                    // 告诉替身：“你的新主人是这个新生成的 2D 小人”
                    SmoothTarget.PlayerRoot = _spawnedAvatar.transform;

                    // 相机盯着替身
                    VCam2D.Follow = SmoothTarget.transform;
                    VCam2D.LookAt = SmoothTarget.transform;
                }
                else
                {
                    VCam2D.Follow = _spawnedAvatar.transform;
                    VCam2D.LookAt = _spawnedAvatar.transform;
                }

                VCam2D.gameObject.SetActive(true);
            }
        }

        public void Restore3DPlayer()
        {
            Debug.Log("恢复 3D 模式...");
            _enterCooldown = 1.5f;

            // 1. 恢复遮挡板
            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(true);

            // 2. 销毁替身
            if (_spawnedAvatar != null) Destroy(_spawnedAvatar);
            var leftovers = FindObjectsOfType<BillboardAvatarController2D>();
            foreach (var avatar in leftovers)
            {
                if (avatar && avatar.gameObject) Destroy(avatar.gameObject);
            }
            _spawnedAvatar = null;

            // 3. 关闭相机
            if (VCam2D != null)
            {
                VCam2D.Priority = -1;
                VCam2D.gameObject.SetActive(false);
            }

            // 4. 恢复 3D 玩家
            foreach (var r in _cachedRenderers) if (r) r.enabled = true;
            foreach (var s in _cachedScripts) if (s) s.enabled = true;

            if (PromptUI) PromptUI.SetActive(false);
        }
    }
}
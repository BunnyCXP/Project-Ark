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
        // 【新增】缓存碰撞体和动画器，彻底解决卡死和脚步声Bug
        private List<Collider> _cachedColliders = new List<Collider>();
        private List<Animator> _cachedAnimators = new List<Animator>();

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
                // 【新增】自动抓取所有碰撞体和动画器
                _cachedColliders.AddRange(PlayerRoot.GetComponentsInChildren<Collider>(true));
                _cachedAnimators.AddRange(PlayerRoot.GetComponentsInChildren<Animator>(true));

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

            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(false);

            // 【核心修复 1】强制重置动画状态，防止脚步声循环
            foreach (var anim in _cachedAnimators)
            {
                if (anim)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                    anim.enabled = false;
                }
            }

            // 【核心修复 2】关闭所有 3D 碰撞体，防止空气墙卡住 2D 玩家
            foreach (var c in _cachedColliders) if (c) c.enabled = false;

            foreach (var s in _cachedScripts) if (s) s.enabled = false;
            foreach (var r in _cachedRenderers) if (r) r.enabled = false;

            float startS = 0f;
            if (SceneRail != null && PlayerRoot != null)
            {
                SceneRail.ClosestPoint(PlayerRoot.position, out startS);
            }

            _spawnedAvatar = Instantiate(Avatar2DPrefab, transform.position, Quaternion.identity);

            var ctrl = _spawnedAvatar.GetComponent<BillboardAvatarController2D>();
            if (ctrl != null)
            {
                ctrl.InitOnRail(SceneRail, startS);
            }

            if (VCam2D != null)
            {
                VCam2D.Priority = VCamActivePriority;

                if (SmoothTarget != null)
                {
                    SmoothTarget.PlayerRoot = _spawnedAvatar.transform;
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

            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(true);

            if (_spawnedAvatar != null) Destroy(_spawnedAvatar);
            var leftovers = FindObjectsOfType<BillboardAvatarController2D>();
            foreach (var avatar in leftovers)
            {
                if (avatar && avatar.gameObject) Destroy(avatar.gameObject);
            }
            _spawnedAvatar = null;

            if (VCam2D != null)
            {
                VCam2D.Priority = -1;
                VCam2D.gameObject.SetActive(false);
            }

            // 【恢复修复】恢复 3D 玩家的动画器和碰撞体
            foreach (var r in _cachedRenderers) if (r) r.enabled = true;
            foreach (var s in _cachedScripts) if (s) s.enabled = true;
            foreach (var c in _cachedColliders) if (c) c.enabled = true;
            foreach (var anim in _cachedAnimators) if (anim) anim.enabled = true;

            if (PromptUI) PromptUI.SetActive(false);
        }
    }
}
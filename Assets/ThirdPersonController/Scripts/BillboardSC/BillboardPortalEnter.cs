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

        [Header("UI")]
        public GameObject PromptUI;

        // --- 内部缓存 ---
        private List<MonoBehaviour> _cachedScripts = new List<MonoBehaviour>();
        private List<Renderer> _cachedRenderers = new List<Renderer>();

        private bool _canEnter;
        private GameObject _spawnedAvatar;

        // 【新增】冷却时间，防止退出瞬间立刻又进去了
        private float _enterCooldown = 0f;

        private void Start()
        {
            // 1. 自动找 Player
            if (PlayerRoot == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) PlayerRoot = p.transform;
            }

            // 2. 自动收集组件
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
            // 【新增】倒计时逻辑
            if (_enterCooldown > 0f)
            {
                _enterCooldown -= Time.deltaTime;
                return; // 还在冷却中，禁止任何操作，直接返回
            }

            // 只有当 冷却结束 && 替身不存在 && 在触发区 && 按下E 时才执行
            if (_canEnter && _spawnedAvatar == null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                Enter2DMode();
            }
        }

        private void Enter2DMode()
        {
            Debug.Log("进入 2D 模式...");
            if (PromptUI) PromptUI.SetActive(false);

            // 禁用 3D
            foreach (var s in _cachedScripts) if (s) s.enabled = false;
            foreach (var r in _cachedRenderers) if (r) r.enabled = false;

            // 计算位置
            float startS = 0f;
            if (SceneRail != null && PlayerRoot != null)
            {
                SceneRail.ClosestPoint(PlayerRoot.position, out startS);
            }

            // 生成 2D 替身
            _spawnedAvatar = Instantiate(Avatar2DPrefab, transform.position, Quaternion.identity);

            // 初始化替身
            var ctrl = _spawnedAvatar.GetComponent<BillboardAvatarController2D>();
            if (ctrl != null)
            {
                ctrl.InitOnRail(SceneRail, startS);
            }

            // 开启 2D 相机
            if (VCam2D != null)
            {
                VCam2D.Priority = VCamActivePriority;
                VCam2D.Follow = _spawnedAvatar.transform;
                VCam2D.gameObject.SetActive(true);
            }
        }

        // === 供 Exit 脚本调用 ===
        public void Restore3DPlayer()
        {
            Debug.Log("恢复 3D 模式...");

            // 【重点】设置 1.5 秒冷却时间
            // 这样就算按键判定还在，或者人还在触发器里，Update 也会被拦截
            _enterCooldown = 1.5f;

            // 1. 销毁替身
            if (_spawnedAvatar != null) Destroy(_spawnedAvatar);
            var leftovers = FindObjectsOfType<BillboardAvatarController2D>();
            foreach (var avatar in leftovers)
            {
                if (avatar && avatar.gameObject) Destroy(avatar.gameObject);
            }
            _spawnedAvatar = null;

            // 2. 关闭 2D 相机
            if (VCam2D != null)
            {
                VCam2D.Priority = -1;
                VCam2D.gameObject.SetActive(false);
            }

            // 3. 复活 3D 玩家
            foreach (var r in _cachedRenderers) if (r) r.enabled = true;
            foreach (var s in _cachedScripts) if (s) s.enabled = true;

            // 4. 防止 UI 残留
            if (PromptUI) PromptUI.SetActive(false);
        }
    }
}
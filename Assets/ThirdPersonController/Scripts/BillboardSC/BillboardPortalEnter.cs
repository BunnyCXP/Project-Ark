using System.Collections;
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

        [Header("【新增】溶解特效设置 (3D模型)")]
        public Material[] HackerMaterials;
        public string ShaderVariableName = "_Dissolve";
        public float DissolveDuration = 1.2f;
        public GameObject ReassemblyVFX; // 场景里那个粒子特效
        public AudioSource MaterializeSound;

        // --- 内部缓存 ---
        private List<MonoBehaviour> _cachedScripts = new List<MonoBehaviour>();
        private List<Renderer> _cachedRenderers = new List<Renderer>();
        private List<Collider> _cachedColliders = new List<Collider>();
        private List<Animator> _cachedAnimators = new List<Animator>();

        private bool _canEnter;
        private GameObject _spawnedAvatar;
        private float _enterCooldown = 0f;
        private bool _isTransitioning = false; // 防止演出期间玩家狂按 E 卡Bug

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

            if (ReassemblyVFX != null) ReassemblyVFX.SetActive(false);
            SetAllMaterialsFloat(0f); // 确保一开始 3D 玩家是实体的
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

            // 按下 E 键，且不在演出过渡中
            if (_canEnter && _spawnedAvatar == null && !_isTransitioning && Keyboard.current.eKey.wasPressedThisFrame)
            {
                StartCoroutine(Enter2DRoutine());
            }
        }

        // ==========================================
        // 进入 2D：3D角色溶解消失 -> 切换出 2D 纸片人
        // ==========================================
        private IEnumerator Enter2DRoutine()
        {
            _isTransitioning = true;
            if (PromptUI) PromptUI.SetActive(false);

            // 1. 冻结3D玩家，防止他在溶解时乱跑乱按
            foreach (var s in _cachedScripts) if (s) s.enabled = false;
            foreach (var anim in _cachedAnimators)
            {
                if (anim)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                }
            }

            // 2. 将粒子特效移动到玩家脚下，并喷射！
            if (ReassemblyVFX != null)
            {
                ReassemblyVFX.transform.position = PlayerRoot.position;
                ReassemblyVFX.SetActive(true);
                ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play(true);
            }
            if (MaterializeSound != null) MaterializeSound.Play();

            // 3. 核心演出：3D 玩家逐渐溶解透明！
            if (HackerMaterials != null && HackerMaterials.Length > 0)
            {
                float t = 0f;
                while (t < DissolveDuration)
                {
                    t += Time.deltaTime;
                    // 从 0 变成 1 (越来越透明)
                    float amount = Mathf.Clamp01(t / DissolveDuration);
                    SetAllMaterialsFloat(amount);
                    yield return null;
                }
                SetAllMaterialsFloat(1f); // 确保完全透明
            }

            // 停止喷射粒子，让剩下的自然消散
            if (ReassemblyVFX != null)
            {
                ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop();
                else ReassemblyVFX.SetActive(false);
            }

            // 4. 正式切换为 2D 模式
            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(false);

            foreach (var anim in _cachedAnimators) if (anim) anim.enabled = false;
            foreach (var c in _cachedColliders) if (c) c.enabled = false;
            foreach (var r in _cachedRenderers) if (r) r.enabled = false;

            float startS = 0f;
            if (SceneRail != null && PlayerRoot != null)
                SceneRail.ClosestPoint(PlayerRoot.position, out startS);

            _spawnedAvatar = Instantiate(Avatar2DPrefab, transform.position, Quaternion.identity);
            var ctrl = _spawnedAvatar.GetComponent<BillboardAvatarController2D>();
            if (ctrl != null) ctrl.InitOnRail(SceneRail, startS);

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

            _isTransitioning = false;
        }

        // 外部出口调用的接口
        public void Restore3DPlayer(Transform exitPoint)
        {
            if (!_isTransitioning)
                StartCoroutine(Restore3DRoutine(exitPoint));
        }

        // ==========================================
        // 退出 2D：瞬移 -> 3D角色从数据流中重组现身！
        // ==========================================
        private IEnumerator Restore3DRoutine(Transform exitPoint)
        {
            _isTransitioning = true;
            _enterCooldown = 1.5f;
            if (PromptUI) PromptUI.SetActive(false);

            // 1. 瞬移 3D 玩家到出口点 (此时碰撞体依然是关闭的安全状态)
            if (PlayerRoot != null && exitPoint != null)
            {
                PlayerRoot.position = exitPoint.position;
                PlayerRoot.rotation = exitPoint.rotation;
            }

            if (AdSurfaceRoot != null) AdSurfaceRoot.SetActive(true);

            // 销毁 2D 纸片人
            if (_spawnedAvatar != null) Destroy(_spawnedAvatar);
            var leftovers = FindObjectsOfType<BillboardAvatarController2D>();
            foreach (var avatar in leftovers) if (avatar && avatar.gameObject) Destroy(avatar.gameObject);
            _spawnedAvatar = null;

            if (VCam2D != null)
            {
                VCam2D.Priority = -1;
                VCam2D.gameObject.SetActive(false);
            }

            // 2. 开启 3D 渲染器，但强制让材质保持“完全透明 (1f)”
            foreach (var r in _cachedRenderers) if (r) r.enabled = true;
            SetAllMaterialsFloat(1f);

            // 3. 把粒子特效移动到出口位置并喷发！
            if (ReassemblyVFX != null)
            {
                ReassemblyVFX.transform.position = exitPoint != null ? exitPoint.position : PlayerRoot.position;
                ReassemblyVFX.SetActive(true);
                ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play(true);
            }
            if (MaterializeSound != null) MaterializeSound.Play();

            // 4. 核心演出：3D 玩家重组实体化！
            if (HackerMaterials != null && HackerMaterials.Length > 0)
            {
                float t = 0f;
                while (t < DissolveDuration)
                {
                    t += Time.deltaTime;
                    // 从 1 变成 0 (慢慢变为实体)
                    float amount = 1f - Mathf.Clamp01(t / DissolveDuration);
                    SetAllMaterialsFloat(amount);
                    yield return null;
                }
                SetAllMaterialsFloat(0f); // 确保最后是绝对的实体
            }

            if (ReassemblyVFX != null)
            {
                ParticleSystem ps = ReassemblyVFX.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop();
                else ReassemblyVFX.SetActive(false);
            }

            // 5. 演出结束，全面恢复物理和移动能力
            foreach (var s in _cachedScripts) if (s) s.enabled = true;
            foreach (var c in _cachedColliders) if (c) c.enabled = true;
            foreach (var anim in _cachedAnimators) if (anim) anim.enabled = true;

            _isTransitioning = false;
        }

        private void SetAllMaterialsFloat(float value)
        {
            if (HackerMaterials == null) return;
            foreach (Material mat in HackerMaterials)
            {
                if (mat != null) mat.SetFloat(ShaderVariableName, value);
            }
        }
    }
}
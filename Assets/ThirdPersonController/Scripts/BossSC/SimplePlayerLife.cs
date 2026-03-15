using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TheGlitch
{
    public class SimplePlayerLife : MonoBehaviour
    {
        [Header("生命设置")]
        public int MaxHP = 3;
        public float ReloadDelay = 1.0f;
        public float InvincibleTimeAfterHit = 0.15f;

        [Header("受击反馈 (红屏)")]
        public Image DamageFlashUI;

        [Header("UI 设置")]
        public Slider PlayerHealthBar;

        // 不再自动弹血条，只是纯粹的逻辑标志
        [HideInInspector]
        public bool IsInBossRoom = false;

        private int _currentHP;
        private bool _isDead = false;
        private float _lastHitTime = -999f;

        private ThirdPersonController _thirdPerson;
        private StarterAssetsInputs _starterInputs;

        private void Awake()
        {
            _currentHP = MaxHP;
            _thirdPerson = GetComponent<ThirdPersonController>();
            _starterInputs = GetComponent<StarterAssetsInputs>();

            if (DamageFlashUI != null)
            {
                Color c = DamageFlashUI.color;
                c.a = 0f;
                DamageFlashUI.color = c;
            }

            InitHPUI();
        }

        public void TakeDamage(int amount)
        {
            if (!IsInBossRoom) return;
            if (_isDead) return;
            if (Time.time - _lastHitTime < InvincibleTimeAfterHit) return;

            _lastHitTime = Time.time;
            _currentHP -= amount;

            UpdateHPUI();

            RawCameraShake.Shake(0.3f, 0.4f);

            if (DamageFlashUI != null) StartCoroutine(FlashRedEffect());

            if (_currentHP <= 0)
            {
                StartCoroutine(DeathRoutine());
            }
        }

        private void InitHPUI()
        {
            if (PlayerHealthBar != null)
            {
                PlayerHealthBar.maxValue = MaxHP;
                PlayerHealthBar.value = _currentHP;
                // 游戏刚开始探索时，强制隐藏玩家血条！
                PlayerHealthBar.gameObject.SetActive(false);
            }
        }

        // ==========================================
        // 【新增】：专门给 Boss 导演调用的亮血条方法
        // ==========================================
        public void ShowPlayerHealthBar()
        {
            if (PlayerHealthBar != null)
            {
                PlayerHealthBar.gameObject.SetActive(true);
            }
        }

        private void UpdateHPUI()
        {
            if (PlayerHealthBar != null)
            {
                PlayerHealthBar.value = _currentHP;
            }
        }

        private IEnumerator FlashRedEffect()
        {
            Color c = DamageFlashUI.color;
            c.a = 0.45f;
            DamageFlashUI.color = c;

            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.45f, 0f, elapsed / duration);
                DamageFlashUI.color = c;
                yield return null;
            }
            c.a = 0f;
            DamageFlashUI.color = c;
        }

        private IEnumerator DeathRoutine()
        {
            _isDead = true;
            Time.timeScale = 1f;

            if (_thirdPerson != null) _thirdPerson.enabled = false;
            if (_starterInputs != null)
            {
                _starterInputs.move = Vector2.zero;
                _starterInputs.look = Vector2.zero;
                _starterInputs.jump = false;
                _starterInputs.sprint = false;
                _starterInputs.enabled = false;
            }

            yield return new WaitForSeconds(ReloadDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    [DefaultExecutionOrder(1000)]
    public class RawCameraShake : MonoBehaviour
    {
        private float _duration;
        private float _magnitude;

        public static void Shake(float duration, float magnitude)
        {
            if (Camera.main == null) return;
            var shaker = Camera.main.GetComponent<RawCameraShake>();
            if (shaker == null) shaker = Camera.main.gameObject.AddComponent<RawCameraShake>();
            shaker._duration = Mathf.Max(shaker._duration, duration);
            shaker._magnitude = Mathf.Max(shaker._magnitude, magnitude);
        }

        private void Update()
        {
            _duration -= Time.unscaledDeltaTime;
            if (_duration <= 0) Destroy(this);
        }

        private void LateUpdate()
        {
            if (_duration > 0)
            {
                float x = Random.Range(-1f, 1f) * _magnitude;
                float y = Random.Range(-1f, 1f) * _magnitude;
                transform.position += transform.right * x + transform.up * y;
            }
        }
    }
}
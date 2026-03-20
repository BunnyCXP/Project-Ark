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
        public float ReloadDelay = 1.5f;
        public float InvincibleTimeAfterHit = 0.15f;

        [Header("受击反馈 (红屏)")]
        public Image DamageFlashUI;

        [Header("UI 设置")]
        public Slider PlayerHealthBar;

        [HideInInspector]
        public bool IsInBossRoom = false;

        public static Vector3? BossRespawnPosition = null;
        public static Quaternion? BossRespawnRotation = null;

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

            if (_currentHP <= 0)
            {
                StartCoroutine(DeathRoutine());
            }
            else
            {
                if (DamageFlashUI != null) StartCoroutine(FlashRedEffect());
            }
        }

        private void InitHPUI()
        {
            if (PlayerHealthBar != null)
            {
                PlayerHealthBar.maxValue = MaxHP;
                PlayerHealthBar.value = _currentHP;
                PlayerHealthBar.gameObject.SetActive(false);
            }
        }

        public void ShowPlayerHealthBar()
        {
            if (PlayerHealthBar != null) PlayerHealthBar.gameObject.SetActive(true);
        }

        private void UpdateHPUI()
        {
            if (PlayerHealthBar != null) PlayerHealthBar.value = _currentHP;
        }

        private IEnumerator FlashRedEffect()
        {
            // 【神级修复 1】：强制重新赋予纯正的红色！
            // 彻底解决复活后因为底层颜色残留导致“红屏变成黑屏”的隐形 Bug！
            DamageFlashUI.color = new Color(1f, 0f, 0f, 0.45f);

            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                DamageFlashUI.color = new Color(1f, 0f, 0f, Mathf.Lerp(0.45f, 0f, elapsed / duration));
                yield return null;
            }
            DamageFlashUI.color = new Color(1f, 0f, 0f, 0f);
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

            if (DamageFlashUI != null)
            {
                float elapsed = 0f;
                float fadeTime = 1.0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    DamageFlashUI.color = Color.Lerp(new Color(1f, 0f, 0f, 0.45f), Color.black, elapsed / fadeTime);
                    yield return null;
                }
                DamageFlashUI.color = Color.black;
            }

            yield return new WaitForSecondsRealtime(ReloadDelay);

            if (BossRespawnPosition.HasValue && BossRespawnRotation.HasValue)
            {
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = BossRespawnPosition.Value;
                transform.rotation = BossRespawnRotation.Value;
                if (cc != null) cc.enabled = true;
            }

            _currentHP = MaxHP;
            UpdateHPUI();
            if (PlayerHealthBar != null) PlayerHealthBar.gameObject.SetActive(false);

            MainframeBossManager boss = Object.FindFirstObjectByType<MainframeBossManager>();
            if (boss != null) boss.SoftResetBoss(this.gameObject);

            if (DamageFlashUI != null)
            {
                float elapsed = 0f;
                float fadeTime = 1.0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    DamageFlashUI.color = Color.Lerp(Color.black, new Color(0, 0, 0, 0), elapsed / fadeTime);
                    yield return null;
                }
                DamageFlashUI.color = new Color(0, 0, 0, 0);
            }

            if (_thirdPerson != null) _thirdPerson.enabled = true;
            if (_starterInputs != null) _starterInputs.enabled = true;

            _isDead = false;
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
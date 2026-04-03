using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public enum FlickerStyle
        {
            SmoothPulse,   // 平滑呼吸
            ErraticFlicker,// 电压不稳 (柏林噪声)
            BrokenNeon     // 坏灯管 (硬切抽搐)
        }

        [Header("💡 闪烁风格")]
        [Tooltip("选择你想要的忽明忽暗效果")]
        public FlickerStyle Style = FlickerStyle.ErraticFlicker;

        [Header("⚙️ 亮度控制")]
        [Tooltip("灯光暗下去时的最低亮度")]
        public float MinIntensity = 0.5f;
        [Tooltip("灯光亮起时的最高亮度")]
        public float MaxIntensity = 5.0f;

        [Header("⏱️ 速度与频率")]
        [Tooltip("数值越大，闪得越快/呼吸越快")]
        [Range(0.1f, 30f)] public float Speed = 5.0f;

        // 坏灯管专属参数
        [Header("🔧 坏灯管专属设置 (Broken Neon)")]
        [Tooltip("灯光完全熄灭的概率 (0~1)")]
        [Range(0f, 1f)] public float BlackoutChance = 0.2f;

        private Light _targetLight;
        private float _randomSeed;
        private float _neonTimer;

        private void Start()
        {
            // 自动获取挂在同一个物体上的 Light 组件
            _targetLight = GetComponent<Light>();

            // 给每个灯泡一个随机种子，防止多个灯泡闪烁频率完全同步
            _randomSeed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (_targetLight == null) return;

            switch (Style)
            {
                case FlickerStyle.SmoothPulse:
                    // 【平滑呼吸】：利用正弦波，在 -1 到 1 之间平滑震荡，然后映射到 0 到 1
                    float wave = (Mathf.Sin(Time.time * Speed) + 1f) / 2f;
                    _targetLight.intensity = Mathf.Lerp(MinIntensity, MaxIntensity, wave);
                    break;

                case FlickerStyle.ErraticFlicker:
                    // 【电压不稳】：利用柏林噪声生成极其自然的随机平滑起伏
                    float noise = Mathf.PerlinNoise(Time.time * Speed, _randomSeed);
                    _targetLight.intensity = Mathf.Lerp(MinIntensity, MaxIntensity, noise);
                    break;

                case FlickerStyle.BrokenNeon:
                    // 【坏灯管抽搐】：高频随机硬切
                    _neonTimer += Time.deltaTime;
                    // 使用 1/Speed 作为切换间隔，Speed 越大切换越快
                    if (_neonTimer > (1f / Speed))
                    {
                        _neonTimer = 0f;
                        if (Random.value < BlackoutChance)
                        {
                            // 触发断电
                            _targetLight.intensity = MinIntensity;
                        }
                        else
                        {
                            // 随机在较高亮度间跳动
                            _targetLight.intensity = Random.Range(MaxIntensity * 0.5f, MaxIntensity);
                        }
                    }
                    break;
            }
        }
    }
}
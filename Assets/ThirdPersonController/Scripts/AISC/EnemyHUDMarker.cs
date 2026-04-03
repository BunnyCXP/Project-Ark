using UnityEngine;
using TMPro; // 使用 TextMeshPro 显示清晰的文字图标

namespace TheGlitch
{
    public class EnemyHUDMarker : MonoBehaviour
    {
        [Header("References")]
        public EnemyAI AI;
        public TextMeshProUGUI MarkerText;

        [Header("State Colors (状态颜色)")]
        [ColorUsage(true, true)] public Color PatrolColor = new Color(0.2f, 0.8f, 1f, 0.6f);  // 平时的幽蓝色
        [ColorUsage(true, true)] public Color AlertColor = new Color(1f, 0.1f, 0.1f, 1f);     // 发现时的刺眼红色
        [ColorUsage(true, true)] public Color DisabledColor = new Color(1f, 0.8f, 0.2f, 0.8f);// 被黑客控制时的橘黄色

        [Header("State Icons (状态图标)")]
        public string PatrolIcon = "▼";
        public string AlertIcon = "!";
        public string DisabledIcon = "X";

        [Header("Settings")]
        public float HeightOffset = 2.2f;    // 悬浮在头顶的高度
        public float LerpSpeed = 10f;        // 颜色渐变速度

        [Header("Floating Animation (呼吸悬浮)")]
        public float FloatSpeed = 4f;        // 上下浮动的速度
        public float FloatAmplitude = 0.08f; // 上下浮动的幅度

        private Camera _mainCam;
        private Color _currentColor;

        private void Start()
        {
            _mainCam = Camera.main;

            // 自动寻找挂在父物体上的 EnemyAI 脚本
            if (AI == null) AI = GetComponentInParent<EnemyAI>();

            _currentColor = PatrolColor;
        }

        private void LateUpdate()
        {
            if (AI == null || MarkerText == null || _mainCam == null) return;

            // 1. 状态判断
            Color targetColor = PatrolColor;
            string targetIcon = PatrolIcon;
            bool isVisible = true;

            switch (AI.CurrentState)
            {
                case EnemyAI.State.Patrol:
                    targetColor = PatrolColor;
                    targetIcon = PatrolIcon;
                    break;

                case EnemyAI.State.Chase:
                case EnemyAI.State.Catching:
                    targetColor = AlertColor;
                    targetIcon = AlertIcon;
                    break;

                case EnemyAI.State.Stunned:
                case EnemyAI.State.Frozen:
                case EnemyAI.State.Rebel:
                    targetColor = DisabledColor;
                    targetIcon = DisabledIcon;
                    break;

                case EnemyAI.State.Dead:
                    isVisible = false; // 死了就隐藏标记
                    break;
            }

            if (!isVisible)
            {
                MarkerText.enabled = false;
                return;
            }

            MarkerText.enabled = true;

            // 2. 平滑过渡颜色和切换文本
            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * LerpSpeed);
            MarkerText.color = _currentColor;
            MarkerText.text = targetIcon;

            // 3. 看板娘效果 (Billboard)：让文字永远正对着玩家摄像机
            transform.LookAt(transform.position + _mainCam.transform.rotation * Vector3.forward,
                             _mainCam.transform.rotation * Vector3.up);

            // 4. 定位在敌人头顶 + 呼吸悬浮动画
            float bounceOffset = Mathf.Sin(Time.time * FloatSpeed) * FloatAmplitude;
            transform.position = AI.transform.position + Vector3.up * (HeightOffset + bounceOffset);
        }
    }
} 
using UnityEngine;

namespace TheGlitch
{
    public class GlitchMaterialAnimator : MonoBehaviour
    {
        [Tooltip("把你的红色暴露材质、绿色黑入材质拖进来")]
        public Material[] GlitchMaterials;
        public float GlitchSpeed = 0.05f; // 闪烁速度

        private float _timer = 0f;

        void Update()
        {
            if (GlitchMaterials == null || GlitchMaterials.Length == 0) return;

            _timer += Time.deltaTime;
            if (_timer >= GlitchSpeed)
            {
                _timer = 0f;
                foreach (var mat in GlitchMaterials)
                {
                    if (mat == null) continue;
                    // 随机让材质的亮度在 0.5 到 5 之间疯狂跳动！
                    float flicker = Random.Range(0.5f, 5.0f);
                    mat.SetColor("_EmissionColor", Color.red * flicker);

                    // 如果你的材质上有贴图，还可以让贴图位置疯狂乱跳
                    mat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));
                }
            }
        }
    }
}
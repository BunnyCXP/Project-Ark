using UnityEngine;

namespace TheGlitch
{
    public class LineRadarController : MonoBehaviour
    {
        [Header("把你的主角模型拖到这里")]
        public Transform PlayerTarget;

        private Material _lineMat;

        void Start()
        {
            // 获取这根线身上的专属材质球
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                _lineMat = lr.material;
            }
        }

        void Update()
        {
            if (_lineMat != null && PlayerTarget != null)
            {
                // 1对1精准强行把玩家的坐标喂给材质球！绝对不会丢失！
                _lineMat.SetVector("_PlayerPosition", PlayerTarget.position);
            }
        }
    }
}
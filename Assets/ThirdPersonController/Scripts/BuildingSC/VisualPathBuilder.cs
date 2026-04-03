using UnityEngine;

namespace TheGlitch
{
    // 👑 [ExecuteAlways] 是黑魔法！它让这段代码在“没按 Play 键”的编辑模式下也能运行！
    [ExecuteAlways]
    public class VisualPathBuilder : MonoBehaviour
    {
        [Header("拖入你的 Line Renderer")]
        public LineRenderer Line;

        [Header("把路标按顺序拖到这里")]
        public Transform[] PathNodes;

        void Update()
        {
            if (Line == null || PathNodes == null || PathNodes.Length == 0) return;

            // 自动根据路标的数量，设置线的节点数
            Line.positionCount = PathNodes.Length;

            // 把每个路标的真实世界坐标，塞给线段
            for (int i = 0; i < PathNodes.Length; i++)
            {
                if (PathNodes[i] != null)
                {
                    Line.SetPosition(i, PathNodes[i].position);
                }
            }
        }
    }
}
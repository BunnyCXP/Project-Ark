using UnityEngine;
using System.Collections;

namespace TheGlitch
{
    public class DynamicCollapse : MonoBehaviour
    {
        [System.Serializable]
        public class CollapsePiece
        {
            [Tooltip("要移动的模型（地板或天花板）")]
            public Transform Piece;

            [Tooltip("塌陷的位移量 (比如向下塌2米填 0, -2, 0)")]
            public Vector3 PositionOffset;

            [Tooltip("塌陷时的倾斜角度 (比如稍微倾斜 15度)")]
            public Vector3 RotationOffset;

            [Tooltip("踩到触发器后，延迟几秒开始动？(用来做多块石头接连塌陷的链式反应)")]
            public float Delay = 0f;

            [Tooltip("塌陷的速度 (越大砸得越快)")]
            public float MoveSpeed = 5f;
        }

        [Header("触发设置")]
        public bool TriggerOnce = true;
        private bool _hasTriggered = false;

        [Header("动态塌陷序列")]
        [Tooltip("在这里添加所有参与这次塌陷的石块和地板")]
        public CollapsePiece[] CollapseSequence;

        [Header("音效与特效 (可选)")]
        public AudioSource CollapseSound;
        public ParticleSystem DustEffect;

        private void OnTriggerEnter(Collider other)
        {
            if (!_hasTriggered && other.CompareTag("Player"))
            {
                if (TriggerOnce) _hasTriggered = true;

                if (CollapseSound) CollapseSound.Play();
                if (DustEffect) DustEffect.Play();

                // 启动所有石块的塌陷动画
                foreach (var piece in CollapseSequence)
                {
                    if (piece.Piece != null)
                    {
                        StartCoroutine(AnimatePiece(piece));
                    }
                }
            }
        }

        private IEnumerator AnimatePiece(CollapsePiece data)
        {
            // 1. 等待延迟时间 (做出层次感)
            if (data.Delay > 0f)
            {
                yield return new WaitForSeconds(data.Delay);
            }

            // 2. 记录起点和终点
            Vector3 startPos = data.Piece.localPosition;
            Vector3 targetPos = startPos + data.PositionOffset;

            Quaternion startRot = data.Piece.localRotation;
            Quaternion targetRot = startRot * Quaternion.Euler(data.RotationOffset);

            float t = 0f;

            // 3. 平滑且沉重地移动到目标位置
            while (t < 1f)
            {
                t += Time.deltaTime * (data.MoveSpeed * 0.5f);

                // 使用 SmoothStep 让移动有一种“开始快，砸到底部时变慢吃力”的厚重感
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                data.Piece.localPosition = Vector3.Lerp(startPos, targetPos, smoothT);
                data.Piece.localRotation = Quaternion.Lerp(startRot, targetRot, smoothT);

                yield return null;
            }

            // 确保精确到达终点
            data.Piece.localPosition = targetPos;
            data.Piece.localRotation = targetRot;
        }
    }
}

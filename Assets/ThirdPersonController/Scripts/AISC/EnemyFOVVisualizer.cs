using UnityEngine;

namespace TheGlitch
{
    [RequireComponent(typeof(EnemyAI))]
    public class EnemyFOVVisualizer : MonoBehaviour
    {
        [Header("Refs")]
        public EnemyAI AI;
        public LineRenderer ArcLine;     // 扇形轮廓
        public LineRenderer LockLine;    // 红色锁定线（可选）

        [Header("Show Only In Scan/Hack")]
        public bool OnlyShowInScanOrHack = true;

        [Header("Shape")]
        [Range(8, 120)] public int Segments = 48;
        public float HeightOffset = 0.06f;          // 离地高度，避免闪
        public bool ClampByWalls = true;            // 弧线被墙裁剪（更真实）
        public LayerMask ObstacleMask = ~0;         // 墙体层（建议排除 Player/Enemy 层）

        [Header("Lock Line")]
        public bool ShowLockLine = true;
        public float LockLineHeightOffset = 1.2f;   // 锁定线从敌人胸口位置出来（按你模型调）
        public bool RequireUnobstructedToLock = true;

        private void Reset()
        {
            AI = GetComponent<EnemyAI>();
            var lrs = GetComponentsInChildren<LineRenderer>(true);
            if (lrs != null && lrs.Length > 0) ArcLine = lrs[0];
            if (lrs != null && lrs.Length > 1) LockLine = lrs[1];
        }

        private void Awake()
        {
            if (AI == null) AI = GetComponent<EnemyAI>();

            if (ArcLine == null) ArcLine = GetComponentInChildren<LineRenderer>(true);

            if (ArcLine != null)
            {
                ArcLine.useWorldSpace = true;
                ArcLine.loop = false; // 我们手动回到起点闭合
            }

            if (LockLine != null)
            {
                LockLine.useWorldSpace = true;
                LockLine.loop = false;
                LockLine.positionCount = 2;
            }
        }

        private void LateUpdate()
        {
            bool show = true;

            if (OnlyShowInScanOrHack)
            {
                // 你之前用的是 ScannerController.IsScanOrHackActive
                // 如果你项目里确实有这个 static，就保留这一行；
                // 没有的话：你就在 ScannerController 里加一个 static bool（下面我也写了怎么加）。
                show = ScannerController.IsScanOrHackActive;
            }

            if (ArcLine == null) return;

            ArcLine.enabled = show;

            if (LockLine != null)
                LockLine.enabled = show && ShowLockLine && IsPlayerLocked();

            if (!show) return;

            DrawSectorOutline();

            if (LockLine != null && LockLine.enabled)
                DrawLockLine();
        }

        // ====== 扇形完整轮廓：origin -> leftEdge -> arc... -> rightEdge -> origin ======
        private void DrawSectorOutline()
        {
            float radius = Mathf.Max(0.01f, AI.ViewRadius);
            float halfAngle = Mathf.Max(0.01f, AI.ViewAngle * 0.5f);

            Vector3 origin = transform.position + Vector3.up * HeightOffset;
            Vector3 forward = transform.forward;

            Vector3 leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            Vector3 rightDir = Quaternion.Euler(0f, +halfAngle, 0f) * forward;

            float leftDist = radius;
            float rightDist = radius;

            if (ClampByWalls)
            {
                if (Physics.Raycast(origin, leftDir, out var hitL, radius, ObstacleMask, QueryTriggerInteraction.Ignore))
                    leftDist = hitL.distance;

                if (Physics.Raycast(origin, rightDir, out var hitR, radius, ObstacleMask, QueryTriggerInteraction.Ignore))
                    rightDist = hitR.distance;
            }

            // 点数：起点(1) + 左边界点(1) + 弧线点(Segments+1) + 右边界点(其实弧线最后一个就是右边界) + 回到起点(1)
            // 为了简单：positionCount = (1 + 1 + (Segments + 1) + 1)
            // 其中弧线包含左右端点
            int count = 1 + 1 + (Segments + 1) + 1;
            ArcLine.positionCount = count;

            int idx = 0;

            // 0) 起点
            ArcLine.SetPosition(idx++, origin);

            // 1) 左边界
            ArcLine.SetPosition(idx++, origin + leftDir * leftDist);

            // 2) 弧线（从左到右）
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;              // 0~1
                float ang = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * forward;

                float dist = radius;
                if (ClampByWalls)
                {
                    if (Physics.Raycast(origin, dir, out RaycastHit hit, radius, ObstacleMask, QueryTriggerInteraction.Ignore))
                        dist = hit.distance;
                }

                ArcLine.SetPosition(idx++, origin + dir * dist);
            }

            // 3) 回到起点（闭合轮廓）
            ArcLine.SetPosition(idx++, origin);

            // idx 应该等于 count
        }

        // ====== 是否锁定玩家（真的看到） ======
        private bool IsPlayerLocked()
        {
            if (AI == null || AI.Player == null) return false;

            Vector3 eye = transform.position + Vector3.up * LockLineHeightOffset;
            Vector3 toP = AI.Player.position - eye;

            float dist = toP.magnitude;
            if (dist > AI.ViewRadius) return false;

            Vector3 dir = toP.normalized;
            float ang = Vector3.Angle(transform.forward, dir);
            if (ang > AI.ViewAngle * 0.5f) return false;

            if (!RequireUnobstructedToLock) return true;

            // 中间有墙就不算锁定
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, ObstacleMask, QueryTriggerInteraction.Ignore))
                return false;

            return true;
        }

        private void DrawLockLine()
        {
            if (LockLine == null || AI == null || AI.Player == null) return;

            Vector3 a = transform.position + Vector3.up * LockLineHeightOffset;
            Vector3 b = AI.Player.position + Vector3.up * 1.0f; // 玩家腰/胸位置，按你模型调

            LockLine.positionCount = 2;
            LockLine.SetPosition(0, a);
            LockLine.SetPosition(1, b);
        }
    }
}

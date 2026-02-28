using System.Collections.Generic;
using UnityEngine;

namespace TheGlitch
{
    public class WirePuzzleManager : MonoBehaviour
    {
        [Header("基础设置")]
        public MirrorRaceManager RaceManager;
        public bool IsGhostPuzzle = false;
        public bool IsHacked = false;

        public List<WireNode> Nodes;

        public void EvaluatePower()
        {
            // 1. 初始化逻辑状态 (不立刻改变颜色，先存旧数据)
            foreach (var n in Nodes)
            {
                n.PreviousIncomingCount = n.IncomingPowerCount; // 记住上一次有几根线连着我
                n.IncomingPowerCount = 0; // 清零，准备重新算
                n.IsPowered = false;
            }

            WireNode startNode = Nodes.Find(n => n.Type == WireNode.NodeType.Start);
            WireNode endNode = Nodes.Find(n => n.Type == WireNode.NodeType.End);

            if (startNode == null || endNode == null) return;

            HashSet<(WireNode, WireNode)> visitedEdges = new HashSet<(WireNode, WireNode)>();
            Queue<WireNode> queue = new Queue<WireNode>();

            startNode.IsPowered = true;
            queue.Enqueue(startNode);

            // 2. 核心传导算法
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();

                for (int d = 0; d < 4; d++)
                {
                    if (curr.HasPort(d))
                    {
                        WireNode neighbor = GetNeighbor(curr, d);
                        if (neighbor != null)
                        {
                            int oppositePort = (d + 2) % 4;

                            if (neighbor.HasPort(oppositePort))
                            {
                                if (visitedEdges.Add((curr, neighbor)))
                                {
                                    neighbor.IncomingPowerCount++;

                                    bool canPower = false;
                                    if (neighbor.Type == WireNode.NodeType.Cross)
                                    {
                                        // 十字路口必须 3 路汇合才能变蓝通电！
                                        if (neighbor.IncomingPowerCount >= 3) canPower = true;
                                    }
                                    else
                                    {
                                        if (neighbor.IncomingPowerCount >= 1) canPower = true;
                                    }

                                    if (canPower && !neighbor.IsPowered)
                                    {
                                        neighbor.IsPowered = true;
                                        queue.Enqueue(neighbor);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 3. 全部算完后，统一通知所有节点应用视觉效果！
            foreach (var n in Nodes)
            {
                n.ApplyVisualState();
            }

            // 4. 检查终点是否通电
            if (endNode.IsPowered && !IsHacked)
            {
                IsHacked = true;
                Debug.Log($"谜题 {(IsGhostPuzzle ? "Ghost" : "Player")} 解锁！");
                if (RaceManager) RaceManager.CheckWinCondition();
            }
            else if (!endNode.IsPowered && IsHacked)
            {
                IsHacked = false;
            }
        }

        private WireNode GetNeighbor(WireNode curr, int dir)
        {
            int dx = 0, dy = 0;
            if (dir == 0) dy = 1;
            else if (dir == 1) dx = 1;
            else if (dir == 2) dy = -1;
            else if (dir == 3) dx = -1;

            return Nodes.Find(n => n.GridX == curr.GridX + dx && n.GridY == curr.GridY + dy);
        }

        public void ScrambleOneNode()
        {
            var rotatables = Nodes.FindAll(n => n.IsRotatable && n.Type != WireNode.NodeType.Start && n.Type != WireNode.NodeType.End);
            if (rotatables.Count > 0)
            {
                var target = rotatables[Random.Range(0, rotatables.Count)];
                target.CurrentRotation = (target.CurrentRotation + Random.Range(1, 4)) % 4;
                target.UpdateVisuals();
            }
            EvaluatePower();
        }

        public void ResetBoard()
        {
            IsHacked = false;

            // 【核心修改】：不再随机打乱，而是让所有管线时光倒流，回到你设计的初始状态！
            foreach (var n in Nodes)
            {
                n.ResetToInitial();
            }

            EvaluatePower();
        }
    }
}
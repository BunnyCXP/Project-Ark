using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace TheGlitch
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class MirrorGhostAI : MonoBehaviour
    {
        private MirrorRaceManager _manager;
        private NavMeshAgent _agent;

        [Header("AI 能力")]
        public float NodeRotateDelay = 0.8f;
        public float SabotageCooldown = 15.0f;

        private float _sabTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public void StartHacking(MirrorRaceManager mgr)
        {
            _manager = mgr;
            if (_agent != null) _agent.enabled = true;
            _sabTimer = SabotageCooldown;

            StopAllCoroutines();
            StartCoroutine(HackingRoutine());
        }

        public void StopHacking()
        {
            StopAllCoroutines();
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void Update()
        {
            if (_manager != null && _manager.IsRaceActive)
            {
                _sabTimer -= Time.deltaTime;
                if (_sabTimer <= 0f)
                {
                    Debug.Log("⚠️ Ghost 发动了干扰！打乱玩家电路！");
                    if (_manager.PlayerPuzzle != null) _manager.PlayerPuzzle.ScrambleOneNode();

                    _sabTimer = SabotageCooldown + Random.Range(-2f, 2f);
                }
            }
        }

        private IEnumerator HackingRoutine()
        {
            while (true)
            {
                // 1. 找一个还没转对的可旋转节点
                WireNode targetNode = null;
                foreach (var n in _manager.GhostPuzzle.Nodes)
                {
                    if (n.IsRotatable && n.CurrentRotation != n.CorrectRotation && n.Type != WireNode.NodeType.Start && n.Type != WireNode.NodeType.End)
                    {
                        targetNode = n;
                        break;
                    }
                }

                // 2. 走向节点
                if (targetNode != null)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(targetNode.transform.position);

                    float timeout = 8f; // 防卡死保险

                    // 忽略高度的平面测距
                    Vector3 flatGhost = new Vector3(transform.position.x, 0, transform.position.z);
                    Vector3 flatNode = new Vector3(targetNode.transform.position.x, 0, targetNode.transform.position.z);

                    // 靠近到 2.5f 以内，或者超时，就停止等待
                    while (Vector3.Distance(flatGhost, flatNode) > 2.5f && timeout > 0)
                    {
                        timeout -= Time.deltaTime;
                        flatGhost = new Vector3(transform.position.x, 0, transform.position.z);
                        yield return null;
                    }

                    // 到达目的地，开始破译
                    _agent.isStopped = true;
                    yield return new WaitForSeconds(NodeRotateDelay);

                    // 咔咔咔旋转直到对齐
                    while (targetNode.CurrentRotation != targetNode.CorrectRotation)
                    {
                        targetNode.RotateNode();
                        _manager.GhostPuzzle.EvaluatePower();
                        yield return new WaitForSeconds(0.3f);
                    }
                }
                else
                {
                    // 没有可解密的了，原地待命
                    yield return new WaitForSeconds(1f);
                }
            }
        }
    }
}
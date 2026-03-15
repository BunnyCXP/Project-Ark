using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TheGlitch
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour, IHackable, IQuickHackable
    {
        public enum State
        {
            Patrol,
            Chase,
            Stunned,
            Frozen,
            Rebel,
            Dead,
            Catching // 【新增】：正在处决/抓捕玩家中
        }
        [Header("Animation")]
        public Animator anim; // 动画控制器
        [Header("AI Vision")]
        public Transform Player;
        public Transform[] PatrolPoints;
        public float PatrolSpeed = 2f;
        public float ChaseSpeed = 4f;
        public float ViewRadius = 10f;
        [Range(0, 180)] public float ViewAngle = 60f;
        public float LoseSightTime = 2f;
        public bool EnablePatrol = false;

        // 【新增】视线遮挡遮罩，设置哪些层级算作墙壁（务必在编辑器里配置，不要勾选 Player）
        public LayerMask ObstacleMask = ~0;
        // 【新增】眼睛的高度，防止射线贴着地板发射导致误判
        public float EyeHeight = 1.5f;

        [Header("Catch Player (抓捕机制)")]
        // 【新增】抓捕距离，小于这个距离算抓到
        public float CatchDistance = 1.5f;
        // 【新增】玩家被抓后重生的出生点
        public Transform PlayerSpawnPoint;

        [Header("Hack Timers")]
        public float StunDuration = 2.0f;
        public float FreezeDuration = 5.0f;
        public float RebelDuration = 6.0f;

        [Header("Overload (爆炸)")]
        public float OverloadChargeTime = 1.0f;
        public float ExplosionRadius = 3.0f;

        // ===== IHackable =====
        public string DisplayName => "Guard";
        public Transform WorldTransform => transform;
        private bool _scanTriggered;

        private NavMeshAgent _agent;
        private int _patrolIndex;
        private State _state = State.Patrol;
        private float _stateTimer;
        private float _lastSeePlayerTime;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = true;
            _agent.updateUpAxis = true;

            // 【新增】：自动去子物体身上找 Animator 组件
            anim = GetComponentInChildren<Animator>();

            ApplyMoveTuning(PatrolSpeed);
        }

        private void Start()
        {
            if (Player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) Player = p.transform;
            }
        }

        private void Update()
        {
            if (_state == State.Dead)
            {
                if (anim != null) anim.speed = 0f; // 如果死了没死亡动画，也可以直接定格
                return;
            }

            // 【新增】：根据状态控制动画的播放速度（定格功能）
            if (anim != null)
            {
                if (_state == State.Stunned || _state == State.Frozen)
                {
                    anim.speed = 0f; // 速度为0，瞬间定格当前动作！
                }
                else
                {
                    anim.speed = 1f; // 恢复正常播放
                }
            }

            // 之前加的传递移动速度的代码
            if (anim != null && _agent != null)
            {
                float targetSpeed = _agent.desiredVelocity.magnitude;
                float currentAnimSpeed = anim.GetFloat("Speed");
                float finalSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * 10f);
                anim.SetFloat("Speed", finalSpeed);
            }
            switch (_state)
            {
                case State.Patrol:
                    TickPatrol();
                    LookForPlayer();
                    break;

                case State.Chase:
                    TickChase();
                    LookForPlayer();
                    break;

                case State.Stunned:
                    TickStunned();
                    break;

                case State.Frozen:
                    TickFrozen();
                    break;

                case State.Rebel:
                    TickRebel();
                    break;
            }
        }

        // ================== 基础 AI ==================

        private void TickPatrol()
        {
            if (!EnablePatrol) { StopAgent(); return; }
            if (PatrolPoints == null || PatrolPoints.Length == 0) { StopAgent(); return; }

            ApplyMoveTuning(PatrolSpeed);

            Transform target = PatrolPoints[_patrolIndex];
            SetDestinationSafe(target.position);

            if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, 0.25f))
            {
                _patrolIndex = (_patrolIndex + 1) % PatrolPoints.Length;
            }
        }

        private void TickChase()
        {
            if (Player == null)
            {
                _state = State.Patrol;
                return;
            }

            // 【新增】抓捕距离检测
            float distToPlayer = Vector3.Distance(transform.position, Player.position);
            if (distToPlayer <= CatchDistance)
            {
                CatchPlayer();
                return;
            }

            ApplyMoveTuning(ChaseSpeed);
            SetDestinationSafe(Player.position);

            if (Time.time - _lastSeePlayerTime > LoseSightTime)
            {
                _state = State.Patrol;
            }
        }

        // 【新增】抓到玩家后的处理逻辑
        // 【修改后】电影级抓捕逻辑
        private void CatchPlayer()
        {
            // 防止连续触发
            if (_state == State.Catching) return;

            _state = State.Catching;
            StopAgent(); // AI 原地站住，不追了

            Debug.Log("AI 抓到了玩家！开始黑屏过渡...");

            // 呼叫全局黑屏管理器
            if (ScreenFader.Instance != null)
            {
                // 在屏幕完全变黑的那一瞬间，执行传送
                ScreenFader.Instance.DoFadeAndAction(() =>
                {
                    TeleportPlayer();

                    // 传完之后，AI 继续回去巡逻
                    _state = State.Patrol;
                });
            }
            else
            {
                // 防呆设计：如果没配置黑屏，就直接硬传
                TeleportPlayer();
                _state = State.Patrol;
            }
        }

        // 把实际的传送代码单独提出来
        private void TeleportPlayer()
        {
            if (PlayerSpawnPoint != null && Player != null)
            {
                CharacterController cc = Player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                Player.position = PlayerSpawnPoint.position;
                Player.rotation = PlayerSpawnPoint.rotation;

                if (cc != null) cc.enabled = true;
            }
        }

        private void TickStunned()
        {
            StopAgent();
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
                _state = State.Patrol;
        }

        private void TickFrozen()
        {
            StopAgent();
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
                _state = State.Patrol;
        }

        private void TickRebel()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                _state = State.Patrol;
                return;
            }

            EnemyAI target = FindNearestEnemy();
            if (target != null)
            {
                ApplyMoveTuning(ChaseSpeed * 0.8f);
                SetDestinationSafe(target.transform.position);
            }
            else
            {
                StopAgent();
            }
        }

        private void LookForPlayer()
        {
            if (Player == null) return;

            Vector3 toPlayer = Player.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > ViewRadius) return;

            Vector3 fwd = transform.forward;
            toPlayer.Normalize();

            float angle = Vector3.Angle(fwd, toPlayer);
            if (angle <= ViewAngle * 0.5f)
            {
                // 【核心修复】：增加射线检测，防止透视墙壁
                // 抬高发射点到胸部/眼睛位置，避免贴地射线撞到小石块
                Vector3 eyePosition = transform.position + Vector3.up * EyeHeight;
                Vector3 targetPosition = Player.position + Vector3.up * EyeHeight;
                Vector3 dirToTarget = (targetPosition - eyePosition).normalized;
                float distToTarget = Vector3.Distance(eyePosition, targetPosition);

                // 发射射线检测墙壁。如果没有撞到 ObstacleMask 设定的墙壁层，才算真正看到
                if (!Physics.Raycast(eyePosition, dirToTarget, distToTarget, ObstacleMask))
                {
                    _state = State.Chase;
                    _lastSeePlayerTime = Time.time;
                }
            }
        }

        // ... [这下方保留你原来的代码：ApplyMoveTuning, StopAgent, SetDestinationSafe, FindNearestEnemy, 以及 IHackable 相关实现] ...

        private void ApplyMoveTuning(float speed)
        {
            if (_agent == null) return;
            _agent.speed = speed;
        }

        private void StopAgent()
        {
            if (_agent == null) return;
            if (!_agent.enabled) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        private void SetDestinationSafe(Vector3 worldPos)
        {
            if (_agent == null) return;
            if (!_agent.enabled) return;

            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
            else
            {
                StopAgent();
            }
        }

        private EnemyAI FindNearestEnemy()
        {
            EnemyAI[] all = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

            EnemyAI best = null;
            float bestDist = Mathf.Infinity;

            foreach (var e in all)
            {
                if (e == this) continue;
                if (e._state == State.Dead) continue;

                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        // ================== IHackable（扫描用） ==================

        public void ResetScanFlag() => _scanTriggered = false;

        public void OnScannedOnce()
        {
            if (_scanTriggered) return;
            _scanTriggered = true;
            StartCoroutine(ScanGlitchFX());
        }

        private IEnumerator ScanGlitchFX()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            Material mat = rend.material;

            Color baseColor =
                mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                Color.white;

            for (int i = 0; i < 3; i++)
            {
                SetMatColor(mat, new Color(1f, 0.2f, 0.2f));
                transform.localScale *= 1.05f;
                yield return new WaitForSecondsRealtime(0.04f);

                SetMatColor(mat, baseColor);
                transform.localScale /= 1.05f;
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public List<HackField> GetFields()
        {
            return new List<HackField>
            {
                new HackField("state","Enemy.State",_state.ToString(), new[] { "Patrol", "Chase", "Stunned", "Frozen", "Rebel", "Dead" }),
                new HackField("alert","Enemy.Alert", _state == State.Chase ? "HIGH" : "LOW", new[] { "LOW", "HIGH" })
            };
        }

        public void Apply(List<HackField> fields) { }

        // ================== IQuickHackable（轮盘） ==================

        public void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left)
        {
            up = new QuickHackOption
            {
                Id = "Enemy_Stun",
                Name = "Stun",
                RequiresCharge = false,
                Execute = () =>
                {
                    StopAllCoroutines();
                    _state = State.Stunned;
                    _stateTimer = StunDuration;
                    StopAgent();
                }
            };

            right = new QuickHackOption
            {
                Id = "Enemy_Freeze",
                Name = "Freeze",
                RequiresCharge = false,
                Execute = () =>
                {
                    StopAllCoroutines();
                    _state = State.Frozen;
                    _stateTimer = FreezeDuration;
                    StopAgent();
                }
            };

            down = new QuickHackOption
            {
                Id = "Enemy_Rebel",
                Name = "Rebel",
                RequiresCharge = false,
                Execute = () =>
                {
                    StopAllCoroutines();
                    _state = State.Rebel;
                    _stateTimer = RebelDuration;
                }
            };

            left = new QuickHackOption
            {
                Id = "Enemy_Overload",
                Name = "Overload",
                RequiresCharge = true,
                ChargeTime = OverloadChargeTime,
                Execute = () =>
                {
                    StartCoroutine(DoOverload());
                }
            };
        }

        private IEnumerator DoOverload()
        {
            StartCoroutine(ScanGlitchFX());
            yield return new WaitForSecondsRealtime(0.2f);

            Collider[] hits = Physics.OverlapSphere(transform.position, ExplosionRadius);
            foreach (var c in hits)
            {
                var e = c.GetComponentInParent<EnemyAI>();
                if (e != null && e != this && e._state != State.Dead)
                {
                    e._state = State.Stunned;
                    e._stateTimer = StunDuration;
                    e.StopAgent();
                }
            }

            _state = State.Dead;
            StopAgent();
            gameObject.SetActive(false);

            Debug.Log("Enemy Overloaded and exploded");
        }
    }
}
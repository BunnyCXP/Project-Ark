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
            Dead
        }

        [Header("AI")]
        public Transform Player;
        public Transform[] PatrolPoints;
        public float PatrolSpeed = 2f;
        public float ChaseSpeed = 4f;
        public float ViewRadius = 10f;
        [Range(0, 180)] public float ViewAngle = 60f;
        public float LoseSightTime = 2f;
        public bool EnablePatrol = false;

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
            _agent.updateRotation = true;   // 让 Agent 自己转向
            _agent.updateUpAxis = true;

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
            if (_state == State.Dead) return;

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

            // 用 remainingDistance 判断是否到达
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

            ApplyMoveTuning(ChaseSpeed);
            SetDestinationSafe(Player.position);

            if (Time.time - _lastSeePlayerTime > LoseSightTime)
            {
                _state = State.Patrol;
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
                _state = State.Chase;
                _lastSeePlayerTime = Time.time;
            }
        }

        private void ApplyMoveTuning(float speed)
        {
            if (_agent == null) return;
            _agent.speed = speed;
            // 你也可以按需调加速度/转向速度
            // _agent.acceleration = 16f;
            // _agent.angularSpeed = 540f;
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

            // 从目标点附近找一个最近的 NavMesh 点，避免 SetDestination 失败
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
            else
            {
                // 找不到就先停住，避免疯狂抽搐
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

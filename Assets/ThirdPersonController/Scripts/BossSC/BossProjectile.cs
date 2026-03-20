using UnityEngine;

namespace TheGlitch
{
    public class BossProjectile : MonoBehaviour
    {
        [HideInInspector]
        public float Speed = 10f;

        [Header("射程设置")]
        [Tooltip("子弹最多能飞多远（单位：米），超过这个距离自动销毁")]
        public float MaxDistance = 30f;

        // 记录子弹生成时的初始位置
        private Vector3 _startPos;

        private void Start()
        {
            // 刚生出来的时候，记住自己的老家在哪
            _startPos = transform.position;
        }

        private void Update()
        {
            // 每一帧直接让它朝自己的正前方移动
            transform.Translate(Vector3.forward * Speed * Time.deltaTime, Space.Self);

            // 算一下离老家有多远了，如果超过了最大射程，直接自我毁灭
            if (Vector3.Distance(_startPos, transform.position) >= MaxDistance)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 如果碰到了玩家，造成伤害并销毁子弹
            if (other.CompareTag("Player"))
            {
                SimplePlayerLife life = other.GetComponentInParent<SimplePlayerLife>();
                if (life != null)
                {
                    life.TakeDamage(1); // 扣 1 点血
                }
                Destroy(gameObject); // 击中后销毁子弹
            }
        }

    }
}
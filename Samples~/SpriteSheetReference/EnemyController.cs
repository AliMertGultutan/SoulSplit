using UnityEngine;

namespace SoulSplit.SpriteSheetReference
{
    /// <summary>
    /// REFERANS SCRIPT — bkz. PlayerMovementAnimator.cs baslik yorumu. Projenin
    /// canli dusman sistemi (SoulSplit.Enemies.EnemyBase ve alt siniflari) bundan
    /// bagimsizdir ve Animator kullanmaz.
    ///
    /// Basit devriye -> kovalama -> saldiri durum makinesi; Animator parametreleri:
    ///   - Speed      (Float, 0-1)
    ///   - IsAttacking (Bool)
    ///   - Hurt        (Trigger)
    ///   - Death       (Trigger)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        private enum State { Patrol, Chase, Attack, Dead }

        [Header("Devriye")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float patrolDistance = 3f;

        [Header("Algi")]
        [SerializeField] private Transform player;
        [SerializeField] private float detectionRange = 5f;
        [SerializeField] private float attackRange = 0.8f;

        [Header("Saldiri")]
        [SerializeField] private float attackChaseSpeed = 3f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackHitDelay = 0.25f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackHitboxRadius = 0.7f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private int attackDamage = 1;

        [Header("Can")]
        [SerializeField] private int maxHealth = 3;

        private Rigidbody2D _rb;
        private Animator _animator;
        private State _state = State.Patrol;
        private Vector2 _patrolOrigin;
        private int _facing = 1;
        private int _currentHealth;
        private float _nextAttackTime;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");
        private static readonly int HurtParam = Animator.StringToHash("Hurt");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _patrolOrigin = transform.position;
            _currentHealth = maxHealth;
        }

        private void Update()
        {
            if (_state == State.Dead) return;

            float distanceToPlayer = player != null
                ? Vector2.Distance(transform.position, player.position)
                : Mathf.Infinity;

            switch (_state)
            {
                case State.Patrol:
                    Patrol();
                    if (distanceToPlayer <= detectionRange) _state = State.Chase;
                    break;

                case State.Chase:
                    if (distanceToPlayer <= attackRange && Time.time >= _nextAttackTime)
                    {
                        _state = State.Attack;
                        BeginAttack();
                    }
                    else if (distanceToPlayer > detectionRange * 1.5f)
                    {
                        _state = State.Patrol;
                    }
                    else
                    {
                        ChasePlayer();
                    }
                    break;

                case State.Attack:
                    // Saldiri sirasinda hareket durur; BeginAttack/EndAttack yonetiyor.
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    break;
            }

            _animator.SetFloat(SpeedParam, Mathf.Abs(_rb.linearVelocity.x) / Mathf.Max(attackChaseSpeed, 0.01f));
        }

        private void Patrol()
        {
            float offset = transform.position.x - _patrolOrigin.x;
            if (offset > patrolDistance) _facing = -1;
            else if (offset < -patrolDistance) _facing = 1;

            _rb.linearVelocity = new Vector2(_facing * patrolSpeed, _rb.linearVelocity.y);
            FaceDirection(_facing);
        }

        private void ChasePlayer()
        {
            int direction = player.position.x >= transform.position.x ? 1 : -1;
            _rb.linearVelocity = new Vector2(direction * attackChaseSpeed, _rb.linearVelocity.y);
            FaceDirection(direction);
        }

        private void FaceDirection(int direction)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            transform.localScale = scale;
        }

        private void BeginAttack()
        {
            _nextAttackTime = Time.time + attackCooldown;
            _animator.SetBool(IsAttackingParam, true);
            Invoke(nameof(OpenHitbox), attackHitDelay);
            Invoke(nameof(EndAttack), attackHitDelay + 0.2f);
        }

        private void OpenHitbox()
        {
            if (attackPoint == null) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxRadius, playerLayer);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(attackDamage);
                }
            }
        }

        private void EndAttack()
        {
            _animator.SetBool(IsAttackingParam, false);
            _state = State.Chase;
        }

        public void TakeDamage(int amount)
        {
            if (_state == State.Dead) return;

            _currentHealth -= amount;
            if (_currentHealth <= 0)
            {
                Die();
            }
            else
            {
                _animator.SetTrigger(HurtParam);
            }
        }

        private void Die()
        {
            _state = State.Dead;
            _rb.linearVelocity = Vector2.zero;
            _animator.SetTrigger(DeathParam);
            // Olum animasyonu bitince obje kaldirilabilir — Animation Event ile
            // bu objede Destroy(gameObject) cagiran bir metod baglayin, ya da
            // basitce bir sure sonra devre disi birakin:
            Destroy(gameObject, 1.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (attackPoint != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                Gizmos.DrawWireSphere(attackPoint.position, attackHitboxRadius);
            }
        }
    }
}

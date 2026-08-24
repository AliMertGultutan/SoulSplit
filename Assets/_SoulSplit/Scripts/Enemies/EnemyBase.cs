using System.Collections;
using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Enemies
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    /// <summary>
    /// Iki dusman tipinin ortak iskeleti: durum makinesi, hedef takibi,
    /// saldiri temposu ve olum. Hedefin KIM oldugunu ve NASIL hareket
    /// edildigini alt siniflar belirler — fiziksel dusman zeminde yurur,
    /// hayalet duvarlardan gecer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Hareket")]
        [SerializeField] protected float patrolSpeed = 2f;
        [SerializeField] protected float chaseSpeed = 4f;

        [Header("Algi")]
        [Tooltip("Hedefi bu mesafede fark eder.")]
        [SerializeField] protected float detectionRadius = 7f;
        [Tooltip("Hedef bu mesafeden uzaklasirsa takibi birakir. Detection'dan buyuk olmali.")]
        [SerializeField] protected float loseTargetRadius = 11f;

        [Header("Saldiri")]
        [SerializeField] protected float attackRange = 1.4f;
        [SerializeField] protected int attackDamage = 1;
        [Tooltip("Vurustan once bekleme. Oyuncuya kacma firsati verir; 0 yapma.")]
        [SerializeField] protected float attackWindup = 0.35f;
        [SerializeField] protected float attackCooldown = 1.2f;
        [SerializeField] protected Vector2 attackHitboxSize = new Vector2(1.6f, 1.4f);
        [Tooltip("Bu dusmanin vurabilecegi katmanlar (bedene mi ruha mi vuruyor).")]
        [SerializeField] protected LayerMask attackTargetLayers;
        [Tooltip("Bu dusman hangi boyuttan hasar verir.")]
        [SerializeField] protected DamageType dealtDamageType = DamageType.Physical;

        [Header("Hasar Tepkisi")]
        [Tooltip("Hasar alinca kac saniye kontrolu kaybeder.")]
        [SerializeField] protected float hurtDuration = 0.25f;
        [SerializeField] protected float knockbackForce = 4f;

        [Header("Olum")]
        [Tooltip("Can bitince obje kac saniye sonra sahneden kalkacak.")]
        [SerializeField] protected float deathFadeDuration = 0.45f;

        [Header("Hata Ayiklama")]
        [SerializeField] protected bool drawGizmos = true;

        protected Rigidbody2D _rb;
        protected Health _health;
        protected EnemyState _state = EnemyState.Patrol;
        protected Transform _target;
        protected int _facing = 1;

        private float _stateTimer;
        private float _nextAttackTime;
        private bool _attackLanded;
        private readonly Collider2D[] _hitResults = new Collider2D[8];
        private ContactFilter2D _attackFilter;

        public EnemyState State => _state;
        public int Facing => _facing;
        /// <summary>Anlik hiz. Yurume animasyonu bunu okuyor.</summary>
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        /// <summary>Vurus oncesi hazirlik suresi. Saldiri animasyonu buna gore zamanlanir.</summary>
        public float AttackWindupDuration => attackWindup;

        /// <summary>Saldiri baslarken tetiklenir. Animasyon scriptleri bunu dinler.</summary>
        public event System.Action OnAttackTriggered;

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _rb.freezeRotation = true;

            _attackFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = attackTargetLayers,
                useTriggers = true
            };
        }

        protected virtual void OnEnable()
        {
            _health.OnHit += HandleHit;
            _health.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            _health.OnHit -= HandleHit;
            _health.OnDeath -= HandleDeath;
        }

        protected virtual void Update()
        {
            if (_state == EnemyState.Dead) return;

            _stateTimer -= Time.deltaTime;

            switch (_state)
            {
                case EnemyState.Hurt:
                    if (_stateTimer <= 0f) _state = EnemyState.Patrol;
                    return;

                case EnemyState.Attack:
                    TickAttack();
                    return;
            }

            _target = FindTarget();
            UpdateSensing();
        }

        protected virtual void FixedUpdate()
        {
            switch (_state)
            {
                case EnemyState.Patrol:
                    Patrol();
                    break;
                case EnemyState.Chase:
                    if (_target != null) MoveTowards(_target.position, chaseSpeed);
                    break;
                case EnemyState.Attack:
                case EnemyState.Hurt:
                case EnemyState.Dead:
                    Brake();
                    break;
            }
        }

        /// <summary>Hedefi gorup gormedigimize gore Patrol / Chase / Attack arasinda gecis.</summary>
        private void UpdateSensing()
        {
            if (_target == null)
            {
                _state = EnemyState.Patrol;
                return;
            }

            float distance = Vector2.Distance(transform.position, _target.position);

            if (distance <= attackRange && Time.time >= _nextAttackTime)
            {
                BeginAttack();
            }
            else if (_state == EnemyState.Chase)
            {
                if (distance > loseTargetRadius) _state = EnemyState.Patrol;
            }
            else if (distance <= GetDetectionRadius())
            {
                _state = EnemyState.Chase;
            }
        }

        /// <summary>
        /// Algi yaricapi. Alt siniflar buna mudahale edebilir —
        /// ornegin fiziksel dusman, savunmasiz birakilmis bedeni daha uzaktan sezer.
        /// </summary>
        protected virtual float GetDetectionRadius() => detectionRadius;

        private void BeginAttack()
        {
            _state = EnemyState.Attack;
            _stateTimer = attackWindup;
            _attackLanded = false;
            _nextAttackTime = Time.time + attackCooldown;

            if (_target != null)
            {
                _facing = _target.position.x >= transform.position.x ? 1 : -1;
            }
            OnAttackStarted();
            OnAttackTriggered?.Invoke();
        }

        private void TickAttack()
        {
            // Windup bittigi anda tek kare hitbox ac.
            if (!_attackLanded && _stateTimer <= 0f)
            {
                _attackLanded = true;
                ApplyAttackDamage();
                _stateTimer = 0.2f;   // toparlanma
            }
            else if (_attackLanded && _stateTimer <= 0f)
            {
                _state = EnemyState.Patrol;
            }
        }

        private void ApplyAttackDamage()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(attackRange * 0.6f * _facing, 0f);
            int count = Physics2D.OverlapBox(center, attackHitboxSize, 0f, _attackFilter, _hitResults);

            Vector2 hitDirection = new Vector2(_facing, 0.15f).normalized;
            for (int i = 0; i < count; i++)
            {
                Health victim = Health.FindOn(_hitResults[i]);
                if (victim != null) victim.TryTakeDamage(attackDamage, dealtDamageType, hitDirection);
            }
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 hitDirection)
        {
            if (result != HitResult.Damaged) return;

            _state = EnemyState.Hurt;
            _stateTimer = hurtDuration;

            // hitDirection zaten saldirgandan hedefe (bu dusmana) dogru, yani
            // dusmanin savrulmasi gereken yonle ayni. Bilinmiyorsa (sifir
            // vektor) bakis yonunun tersini kullan.
            Vector2 away = hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection
                : new Vector2(-_facing, 0f);
            _rb.linearVelocity = new Vector2(away.x * knockbackForce, _rb.linearVelocity.y);
        }

        private void HandleDeath()
        {
            _state = EnemyState.Dead;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;

            // Artik kimseyi engellemesin ve vurulamasin.
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            {
                col.enabled = false;
            }

            OnDied();
            StartCoroutine(FadeOutAndDisable());
        }

        /// <summary>Olum sonrasi soluklasarak sahneden kalkar.</summary>
        private IEnumerator FadeOutAndDisable()
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            float elapsed = 0f;
            Color start = sprite != null ? sprite.color : Color.white;

            while (elapsed < deathFadeDuration)
            {
                elapsed += Time.deltaTime;
                if (sprite != null)
                {
                    Color c = start;
                    c.a = Mathf.Lerp(start.a, 0f, elapsed / deathFadeDuration);
                    sprite.color = c;
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }

        protected void Brake()
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }

        // --- Alt siniflarin doldurdugu kisimlar ---

        /// <summary>Bu dusman kimi hedef aliyor? Yoksa null dondur.</summary>
        protected abstract Transform FindTarget();

        /// <summary>Hedefe dogru hareket. Yurume ve ucma farki burada.</summary>
        protected abstract void MoveTowards(Vector3 targetPosition, float speed);

        /// <summary>Hedef yokken ne yapiyor?</summary>
        protected abstract void Patrol();

        protected virtual void OnAttackStarted() { }
        protected virtual void OnDied() { }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, loseTargetRadius);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}

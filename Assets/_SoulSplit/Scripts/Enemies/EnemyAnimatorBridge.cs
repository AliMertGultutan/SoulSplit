using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Enemies
{
    /// <summary>
    /// Dusmanlar icin sprite-sheet animasyon koprusu; oyuncudaki
    /// PlayerAnimatorBridge'in karsiligi. EnemyBase'in public arayuzunu
    /// (State / Facing / Velocity / OnAttackTriggered) ve Health olaylarini
    /// Animator parametrelerine baglar.
    ///
    /// Bu script transform'a DOKUNMAZ; sadece parametre yazar ve sprite'i
    /// bakis yonune gore cevirir. Ayni obje uzerinde EnemyProceduralAnimator
    /// ile birlikte kullanilmamali — ikisi ayni gorseli cekistirir.
    ///
    /// ONEMLI ZAMANLAMA NOTU: EnemyBase, hasari attackWindup suresinin
    /// SONUNDA uygular (bkz. EnemyBase.TickAttack). Attack klibindeki vurus
    /// karesi de bu ana denk getirilmeli; aksi halde hasar, silah havadayken
    /// iner. Klip zamanlamasi bu yuzden elle ayarlanmistir.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimatorBridge : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private EnemyBase enemy;
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Hiz")]
        [Tooltip("Speed parametresini normalize etmek icin referans hiz. " +
                 "Genelde bu dusmanin chaseSpeed degeriyle esitlenmeli.")]
        [SerializeField] private float referenceSpeed = 5.5f;
        [Tooltip("Bu oranin altindaki hiz 'duruyor' sayilir.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float idleThreshold = 0.1f;
        [SerializeField] private float speedSmoothing = 12f;
        [Tooltip("UCAN dusmanlar icin isaretle. Zeminde yuruyenlerde sadece yatay hiz " +
                 "anlamlidir (dikey hiz ziplama/dusme demektir), ama suzulen bir dusman " +
                 "dikeyde de hareket eder; orada yatay hiz tek basina yaniltir.")]
        [SerializeField] private bool useTotalVelocity;

        [Header("Gorsel")]
        [Tooltip("Sprite kaynak gorselde SOLA bakiyorsa isaretle. Knight seti saga bakiyor -> kapali birak.")]
        [SerializeField] private bool spriteFacesLeft;

        private Animator _animator;
        private float _smoothedSpeed;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HeavyAttackParam = Animator.StringToHash("HeavyAttack");
        private static readonly int HurtParam = Animator.StringToHash("Hurt");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (enemy == null) enemy = GetComponentInParent<EnemyBase>();
            if (health == null) health = GetComponentInParent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.OnAttackTriggered += HandleAttack;
            if (health != null)
            {
                health.OnHit += HandleHit;
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.OnAttackTriggered -= HandleAttack;
            if (health != null)
            {
                health.OnHit -= HandleHit;
                health.OnDeath -= HandleDeath;
            }
        }

        private void LateUpdate()
        {
            if (enemy == null) return;

            float speed = useTotalVelocity ? enemy.Velocity.magnitude : Mathf.Abs(enemy.Velocity.x);
            float raw = speed / Mathf.Max(0.01f, referenceSpeed);
            if (raw < idleThreshold) raw = 0f;

            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, raw, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));
            _animator.SetFloat(SpeedParam, _smoothedSpeed);

            if (spriteRenderer != null)
            {
                bool faceRight = enemy.Facing > 0;
                spriteRenderer.flipX = spriteFacesLeft ? faceRight : !faceRight;
            }
        }

        /// <summary>
        /// Agir saldiri icin AYRI bir tetikleyici kullanilir. Controller'da
        /// HeavyAttack state'i yoksa parametre de yoktur; o durumda Animator
        /// uyari basar. Bu yuzden once parametrenin varligi kontrol ediliyor
        /// ve yoksa hafif klibe dusuluyor — boylece HeavyAttack klibi
        /// eklenmemis dusmanlar da calismaya devam eder.
        /// </summary>
        private void HandleAttack(AttackTier tier)
        {
            if (tier == AttackTier.Heavy && HasParameter(HeavyAttackParam))
            {
                _animator.SetTrigger(HeavyAttackParam);
                return;
            }
            _animator.SetTrigger(AttackParam);
        }

        private bool HasParameter(int nameHash)
        {
            foreach (var p in _animator.parameters)
                if (p.nameHash == nameHash) return true;
            return false;
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            // Sekme (yanlis form) ve dokunulmazlik animasyon tetiklemesin.
            if (result == HitResult.Damaged) _animator.SetTrigger(HurtParam);
        }

        private void HandleDeath()
        {
            _animator.SetTrigger(DeathParam);
        }
    }
}

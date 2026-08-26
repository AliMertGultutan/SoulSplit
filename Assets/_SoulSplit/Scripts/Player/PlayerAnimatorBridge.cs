using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Player
{
    /// <summary>
    /// Sprite-sheet animasyonuna gecis koprusu: mevcut oyun sistemlerini
    /// (PlayerController / MeleeAttack / Health) Animator parametrelerine baglar.
    ///
    /// Bu script HICBIR transform'a dokunmaz — sadece parametre yazar. Gorsel
    /// hareketin tamami artik Animator'daki kare animasyonlarindan geliyor.
    /// Bu yuzden ayni obje uzerinde PlayerProceduralAnimator ile BIRLIKTE
    /// kullanilmamali; ikisi ayni SpriteRenderer/transform'u cekistirir.
    ///
    /// Beklenen Animator parametreleri (SpriteAnimationBuilder bunlari uretir):
    ///   Speed (Float 0-1), IsGrounded (Bool), Attack/Hurt/Death (Trigger)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private MeleeAttack meleeAttack;
        [SerializeField] private Health health;

        [Header("Esikler")]
        [Tooltip("Bu oranin altindaki hiz 'duruyor' sayilir. Animator'daki " +
                 "Idle<->Walk gecis esigiyle ayni olmali (varsayilan 0.1).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float idleThreshold = 0.1f;
        [Tooltip("Hiz degerinin yumusatilmasi. 0 = ani, yuksek = daha gecisli. " +
                 "Cok yumusak olursa Walk/Run gecisi gec tepki verir.")]
        [SerializeField] private float speedSmoothing = 14f;

        private Animator _animator;
        private float _smoothedSpeed;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int CrouchingParam = Animator.StringToHash("IsCrouching");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HurtParam = Animator.StringToHash("Hurt");
        private static readonly int DeathParam = Animator.StringToHash("Death");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (meleeAttack == null) meleeAttack = GetComponentInParent<MeleeAttack>();
            if (health == null) health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered += HandleAttack;
            if (health != null)
            {
                health.OnHit += HandleHit;
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered -= HandleAttack;
            if (health != null)
            {
                health.OnHit -= HandleHit;
                health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (controller == null) return;

            float raw = Mathf.Abs(controller.Velocity.x) / Mathf.Max(0.01f, controller.MaxSpeed);
            if (raw < idleThreshold) raw = 0f;

            // Ani hiz sicramalarinda Walk/Run arasinda titremeyi (flicker) onler.
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, raw, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));

            _animator.SetFloat(SpeedParam, _smoothedSpeed);
            _animator.SetBool(GroundedParam, controller.IsGrounded);
            _animator.SetBool(CrouchingParam, controller.IsCrouching);
        }

        /// <summary>Hem hafif hem agir saldiri ayni klibi oynatir; tur ayrimi su an gorselde yok.</summary>
        private void HandleAttack(AttackTier tier)
        {
            _animator.SetTrigger(AttackParam);
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            // Sadece gercekten hasar gectiginde; sekme/dokunulmazlik animasyon tetiklemesin.
            if (result == HitResult.Damaged) _animator.SetTrigger(HurtParam);
        }

        private void HandleDeath()
        {
            _animator.SetTrigger(DeathParam);
        }
    }
}

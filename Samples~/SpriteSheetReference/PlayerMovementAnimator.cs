using UnityEngine;

namespace SoulSplit.SpriteSheetReference
{
    /// <summary>
    /// REFERANS SCRIPT — sprite-sheet + Animator Controller mimarisine gecis icin.
    /// Projenin canli oyuncu kontrolcusu (SoulSplit.Player.PlayerController +
    /// PlayerProceduralAnimator) bununla ILGILI DEGIL ve bu dosyadan etkilenmez —
    /// o sistem kod-tabanli (Animator YOK) calismaya devam ediyor.
    ///
    /// Bu script klasik "Animator + sprite sheet" yaklasimini gosterir: hiz/yer
    /// durumunu Animator parametrelerine yazar, saldiri penceresinde hitbox acar.
    /// Animator Controller'da beklenen parametreler:
    ///   - Speed      (Float, 0-1 normalize)
    ///   - IsGrounded (Bool)
    ///   - Attack     (Trigger)
    /// State/transition kurulumu icin proje sohbetindeki semaya bakin.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerMovementAnimator : MonoBehaviour
    {
        [Header("Hareket")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("Bu deger bu script'te kullanilmaz — Animator Controller'daki Walk->Run gecis kosulunun (Speed > X) referans degeri. Ikisini ayni tutun.")]
        [SerializeField] private float runSpeedThreshold = 0.6f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Zemin Kontrolu")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Saldiri / Hitbox")]
        [Tooltip("Saldiri tetiklendikten kac saniye sonra hitbox acilsin (animasyonun 'vurus' karesiyle eslesmeli).")]
        [SerializeField] private float attackHitDelay = 0.15f;
        [SerializeField] private float attackHitboxDuration = 0.08f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRange = 0.6f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private int attackDamage = 1;
        [Tooltip("Saldiri sonrasi tekrar saldirabilmek icin bekleme.")]
        [SerializeField] private float attackCooldown = 0.4f;

        private Rigidbody2D _rb;
        private Animator _animator;
        private float _horizontalInput;
        private bool _isGrounded;
        private bool _isAttacking;
        private float _nextAttackTime;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int AttackParam = Animator.StringToHash("Attack");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            ReadInput();
            UpdateAnimatorParams();

            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            }

            if (Input.GetMouseButtonDown(0) && Time.time >= _nextAttackTime)
            {
                BeginAttack();
            }
        }

        private void FixedUpdate()
        {
            _isGrounded = groundCheck != null &&
                          Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            // Saldiri sirasinda yatay hareketi kilitle — klasik "saldirirken durur" hissi.
            float speed = _isAttacking ? 0f : _horizontalInput * moveSpeed;
            _rb.linearVelocity = new Vector2(speed, _rb.linearVelocity.y);

            if (Mathf.Abs(_horizontalInput) > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(_horizontalInput);
                transform.localScale = scale;
            }
        }

        private void ReadInput()
        {
            _horizontalInput = Input.GetAxisRaw("Horizontal");
        }

        private void UpdateAnimatorParams()
        {
            float normalizedSpeed = Mathf.Abs(_horizontalInput);
            _animator.SetFloat(SpeedParam, normalizedSpeed);
            _animator.SetBool(GroundedParam, _isGrounded);
        }

        private void BeginAttack()
        {
            _isAttacking = true;
            _nextAttackTime = Time.time + attackCooldown;
            _animator.SetTrigger(AttackParam);
            Invoke(nameof(OpenHitbox), attackHitDelay);
        }

        /// <summary>
        /// Zamanlayici ile cagrilir (bkz. BeginAttack). Alternatif: bu metodu
        /// Attack animasyon klibine bir Animation Event olarak baglayip
        /// attackHitDelay'i tamamen kaldirabilirsiniz — o zaman zamanlama
        /// klibin kendisinden gelir, elle ayarlanan bir sureden degil.
        /// </summary>
        private void OpenHitbox()
        {
            if (attackPoint == null) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(attackDamage);
                }
            }

            Invoke(nameof(EndAttack), attackHitboxDuration);
        }

        private void EndAttack()
        {
            _isAttacking = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (attackPoint == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }

    /// <summary>Basit hasar arayuzu — hem oyuncu hem dusman hedefleri bunu uygulayabilir.</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}

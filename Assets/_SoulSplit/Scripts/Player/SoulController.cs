using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Player
{
    /// <summary>
    /// Ruh formunun hareketi. Yercekimsiz, 8 yone serbest suzulme.
    /// Duvarlardan gecis collider ile degil, Physics2D Layer matrisiyle cozuluyor
    /// (Soul katmani ile Ground katmani carpismiyor) — boylece burada
    /// tek bir "duvardan gec" kodu yok, is fizige birakiliyor.
    ///
    /// Bu script SADECE hareket eder. Ne zaman aktif olacagina
    /// SoulSwitchManager karar verir.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SoulController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Vurus animasyonunu tetikleyen kaynak. Bos birakilirsa ayni objede aranir.")]
        [SerializeField] private MeleeAttack meleeAttack;

        [Header("Suzulme")]
        [Tooltip("Ruh formunun en yuksek hizi. Bedenden daha hizli olmali ki gecis odullendirici hissetsin.")]
        [SerializeField] private float maxSpeed = 11f;
        [Tooltip("Hedef hiza ulasma ivmesi. Dusuk deger = daha 'agir', suzuluyor hissi.")]
        [SerializeField] private float acceleration = 45f;
        [Tooltip("Girdi birakilinca yavaslama. Dusuk deger = kayarak durur.")]
        [SerializeField] private float deceleration = 30f;

        [Header("Salinim")]
        [Tooltip("Dururken hafif yukari-asagi suzulme miktari (dunya birimi).")]
        [SerializeField] private float driftAmount = 0.12f;
        [SerializeField] private float driftFrequency = 1.6f;

        [Header("Gorsel Mod")]
        [Tooltip("Vurus animasyonunu bu script'in kendi donme/lunge matematigiyle oynatir. " +
                 "Sprite-sheet (Animator) kullaniliyorsa KAPAT — aksi halde iki efekt ust uste biner. " +
                 "Drift salinimi her iki durumda da calismaya devam eder.")]
        [SerializeField] private bool useAttackRotationOverlay = true;

        [Header("Dovus Animasyonu (hazirlik/vurus/TUTMA/toparlanma)")]
        [SerializeField] private float attackAnticipationDuration = 0.08f;
        [SerializeField] private float attackStrikeDuration = 0.045f;
        [Tooltip("Vurus tepe pozunun ekranda asili kalma suresi (impact frame hold).")]
        [SerializeField] private float attackHoldDuration = 0.09f;
        [SerializeField] private float attackRecoveryDuration = 0.15f;
        [SerializeField] private float attackWindupAngle = 14f;
        [SerializeField] private float attackSwingAngle = 32f;
        [SerializeField] private float attackLungeDistance = 0.22f;

        [Header("Dovus Animasyonu — Agir Saldiri")]
        [SerializeField] private float heavyAttackAnticipationDuration = 0.16f;
        [SerializeField] private float heavyAttackStrikeDuration = 0.05f;
        [SerializeField] private float heavyAttackHoldDuration = 0.14f;
        [SerializeField] private float heavyAttackRecoveryDuration = 0.28f;
        [SerializeField] private float heavyAttackWindupAngle = 24f;
        [SerializeField] private float heavyAttackSwingAngle = 46f;
        [SerializeField] private float heavyAttackLungeDistance = 0.34f;

        private Rigidbody2D _rb;
        private int _facingDirection = 1;
        private float _driftPhase;
        private Vector3 _visualBasePosition;

        private float _attackTimer;
        private AttackOverlayTimings _lightAttackTimings;
        private AttackOverlayTimings _heavyAttackTimings;
        private AttackTier _attackTier = AttackTier.Light;

        /// <summary>Bakis yonu; gorsel cevirme icin.</summary>
        public int FacingDirection => _facingDirection;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            // Ruh formu yercekimsiz ve donmez.
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            if (spriteRenderer != null) _visualBasePosition = spriteRenderer.transform.localPosition;
            if (meleeAttack == null) meleeAttack = GetComponent<MeleeAttack>();

            _lightAttackTimings = new AttackOverlayTimings(attackAnticipationDuration, attackStrikeDuration,
                attackHoldDuration, attackRecoveryDuration, attackWindupAngle, attackSwingAngle, attackLungeDistance);
            _heavyAttackTimings = new AttackOverlayTimings(heavyAttackAnticipationDuration, heavyAttackStrikeDuration,
                heavyAttackHoldDuration, heavyAttackRecoveryDuration, heavyAttackWindupAngle, heavyAttackSwingAngle, heavyAttackLungeDistance);
        }

        private void OnEnable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered += HandleAttackTriggered;
        }

        private void OnDisable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered -= HandleAttackTriggered;
        }

        private void HandleAttackTriggered(AttackTier tier)
        {
            _attackTier = tier;
            _attackTimer = tier == AttackTier.Heavy ? _heavyAttackTimings.TotalDuration : _lightAttackTimings.TotalDuration;
        }

        /// <summary>SoulSwitchManager ruhu bedenden cikarirken cagirir.</summary>
        public void Spawn(Vector2 position, int facing)
        {
            transform.position = position;
            _facingDirection = facing;
            _rb.linearVelocity = Vector2.zero;
            _driftPhase = 0f;
            _attackTimer = 0f;
        }

        private void Update()
        {
            if (spriteRenderer == null) return;

            spriteRenderer.flipX = _facingDirection < 0;

            // Hicbir sey yapmasan bile ruh hafifce suzulsun; cansiz durmasin.
            // Bu sadece gorsel — fizige dokunmuyor.
            _driftPhase += Time.deltaTime * driftFrequency * Mathf.PI * 2f;
            Vector3 offset = new Vector3(0f, Mathf.Sin(_driftPhase) * driftAmount, 0f);

            Quaternion attackRotation = Quaternion.identity;
            if (_attackTimer > 0f)
            {
                if (useAttackRotationOverlay)
                {
                    attackRotation = ComputeAttackRotation(ref offset);
                }
                else
                {
                    // Gorseli Animator suruyor; ama sayac yine de islemeli ki
                    // saldiri durumu normal suresinde sona ersin.
                    _attackTimer -= Time.deltaTime;
                }
            }

            spriteRenderer.transform.localPosition = _visualBasePosition + offset;
            spriteRenderer.transform.localRotation = attackRotation;
        }

        /// <summary>
        /// Bedenin vurus animasyonuyla ayni 4 faz yapisi: hazirlik (yavas) ->
        /// vurus (cok hizli) -> TUTMA (impact frame, pozu asili tutar) ->
        /// toparlanma (orta). Ruh formu icin ayri tutuldu cunku ruhun kendi
        /// gorsel guncelleme dongusu (drift) zaten burada; ikinci bir
        /// bilesenle ayni transform'a yazip yaristirmak yerine tek yerden yonetiliyor.
        /// </summary>
        private Quaternion ComputeAttackRotation(ref Vector3 positionOffset)
        {
            bool isHeavy = _attackTier == AttackTier.Heavy;
            AttackOverlayTimings timings = isHeavy ? _heavyAttackTimings : _lightAttackTimings;

            _attackTimer -= Time.deltaTime;
            float elapsed = timings.TotalDuration - Mathf.Max(_attackTimer, 0f);

            AttackOverlayAnimator.Evaluate(timings, elapsed, out float angle, out float lunge);

            positionOffset += new Vector3(lunge * _facingDirection, 0f, 0f);
            return Quaternion.Euler(0f, 0f, angle * _facingDirection);
        }

        private void FixedUpdate()
        {
            Vector2 moveInput = input.MoveInput;
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            Vector2 targetVelocity = moveInput * maxSpeed;
            bool isAccelerating = moveInput.sqrMagnitude > 0.01f;
            float rate = isAccelerating ? acceleration : deceleration;

            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                _facingDirection = moveInput.x > 0f ? 1 : -1;
            }
        }
    }
}

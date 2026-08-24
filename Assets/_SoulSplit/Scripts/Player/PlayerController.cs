using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>Karakterin icinde bulundugu hareket durumu (basit state machine).</summary>
    public enum PlayerState
    {
        Idle,
        Running,
        Jumping,
        Falling,
        WallSliding
    }

    /// <summary>
    /// Maddi bedenin hareket kontrolcusu.
    /// Sorumlulugu SADECE hareket: kosma, ziplama, cift ziplama, duvar kaymasi,
    /// duvardan sekme, coyote time ve jump buffer.
    /// Girdi PlayerInputHandler'dan gelir; savas ve ruh mekanigi baska scriptlerde.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler input;

        [Header("Yatay Hareket")]
        [Tooltip("Ulasilabilecek en yuksek kosma hizi (birim/saniye).")]
        [SerializeField] private float maxSpeed = 9f;
        [Tooltip("Yerde hedef hiza ulasma ivmesi. Yuksek deger = daha keskin baslangic.")]
        [SerializeField] private float groundAcceleration = 90f;
        [Tooltip("Yerde durma yavaslamasi. Yuksek deger = daha keskin durus.")]
        [SerializeField] private float groundDeceleration = 110f;
        [Tooltip("Havada ivmelenme. Yerdekinden dusuk olmali ki hava kontrolu daha agir hissettirsin.")]
        [SerializeField] private float airAcceleration = 55f;
        [Tooltip("Havada yavaslama. Cok yuksek olursa karakter havada anlik duruyormus gibi hissettirir.")]
        [SerializeField] private float airDeceleration = 25f;

        [Header("Ziplama")]
        [Tooltip("Ziplama aninda dikey hiza dogrudan yazilan deger.")]
        [SerializeField] private float jumpVelocity = 17f;
        [Tooltip("Yere degdikten sonra kullanilabilecek EK ziplama sayisi. 1 = cift ziplama.")]
        [SerializeField] private int maxAirJumps = 1;
        [Tooltip("Yerden ayrildiktan sonra hala ziplayabilme suresi (saniye).")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("Yere inmeden once basilan ziplama tusunun hatirlanma suresi (saniye).")]
        [SerializeField] private float jumpBufferTime = 0.15f;
        [Tooltip("Ziplama tusu erken birakilinca yukari hizin kalan orani. Dusuk deger = daha kisa zip.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float jumpCutMultiplier = 0.45f;

        [Header("Yercekimi Hissiyati")]
        [Tooltip("Rigidbody2D'nin temel gravity scale degeri.")]
        [SerializeField] private float baseGravityScale = 5f;
        [Tooltip("Duserken yercekimi carpani. 1'den buyuk = daha agir, daha az havada asili kalma.")]
        [SerializeField] private float fallGravityMultiplier = 1.8f;
        [Tooltip("Ziplama tusu birakilmisken yukari giderken uygulanan carpan (degisken ziplama).")]
        [SerializeField] private float lowJumpMultiplier = 3.2f;
        [Tooltip("Maksimum dusme hizi (mutlak deger).")]
        [SerializeField] private float maxFallSpeed = 26f;

        [Header("Duvar Etkilesimi")]
        [Tooltip("Duvara yapisikken sabit dusme hizi.")]
        [SerializeField] private float wallSlideSpeed = 3.5f;
        [Tooltip("Duvardan sekme kuvveti. X = duvardan itilme, Y = yukseklik.")]
        [SerializeField] private Vector2 wallJumpVelocity = new Vector2(12f, 16f);
        [Tooltip("Duvardan sektikten sonra yatay girdinin kilitli kalma suresi. Duvara geri yapismayi engeller.")]
        [SerializeField] private float wallJumpInputLockTime = 0.14f;
        [Tooltip("Duvardan ayrildiktan/tam degemedikten sonra hala duvar ziplamasi yapilabilecek sure (saniye). Zemin coyote time'inin duvar karsiligi — dar araliklarda 'kor bolge'de basilan zip tusunun bosa gitmesini engeller.")]
        [SerializeField] private float wallCoyoteTime = 0.09f;

        [Header("Egilme (Crouch)")]
        [Tooltip("Yerdeyken asagi yon (S / Down) basiliyken egilme aktif olur.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float crouchSpeedMultiplier = 0.4f;
        [Tooltip("Egilirken collider yuksekliginin kalan orani. Ayaklar yerde sabit kalir, tepe alcalir.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float crouchColliderHeightMultiplier = 0.55f;
        [Tooltip("Asagi girdisinin bu esigi asmasi egilmeyi tetikler (olu bolgeden sonra).")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float crouchInputThreshold = 0.5f;

        [Header("Zemin / Duvar Tespiti")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.55f);
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.12f);
        [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.45f, 0f);
        [SerializeField] private Vector2 wallCheckSize = new Vector2(0.12f, 0.85f);

        [Header("Hata Ayiklama")]
        [SerializeField] private bool drawGizmos = true;

        private Rigidbody2D _rb;
        private CapsuleCollider2D _capsule;

        // --- Durum ---
        private PlayerState _state = PlayerState.Idle;
        private bool _isGrounded;
        private int _wallDirection;          // -1 sol duvar, +1 sag duvar, 0 duvar yok
        private int _lastWallDirection;       // wall coyote sirasinda hangi yone itilecegini hatirlar
        private bool _isWallSliding;
        private bool _isCrouching;
        private int _facingDirection = 1;
        private int _airJumpsLeft;

        private Vector2 _standingColliderSize;
        private Vector2 _standingColliderOffset;

        // --- Zamanlayicilar ---
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private float _wallJumpLockCounter;
        private float _wallCoyoteCounter;
        private float _knockbackLockCounter;

        // --- Update'te toplanip FixedUpdate'te tuketilen bayrak ---
        private bool _jumpCutRequested;

        /// <summary>Animasyon ve efekt sistemlerinin okuyabilecegi mevcut durum.</summary>
        public PlayerState State => _state;
        public bool IsGrounded => _isGrounded;
        public int FacingDirection => _facingDirection;
        /// <summary>Her gercek ziplamada (yer/coyote/hava) tetiklenir. Duvar ziplamasi ayri.</summary>
        public event System.Action OnJumped;
        /// <summary>Her duvar ziplamasinda tetiklenir.</summary>
        public event System.Action OnWallJumped;
        /// <summary>Anlik hiz. Prosedurel animasyon bunu okuyor.</summary>
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        /// <summary>Ayarlanan en yuksek kosma hizi; animasyonun normalize etmesi icin.</summary>
        public float MaxSpeed => maxSpeed;
        /// <summary>Ziplama hizi; squash/stretch'in referans aldigi deger.</summary>
        public float JumpVelocity => jumpVelocity;
        /// <summary>Duvar yonu: -1 sol, +1 sag, 0 yok.</summary>
        public int WallDirection => _wallDirection;
        /// <summary>Su an egiliyor mu? Gorsel/hitbox sistemleri okuyabilir.</summary>
        public bool IsCrouching => _isCrouching;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _capsule = GetComponent<CapsuleCollider2D>();
            if (input == null) input = GetComponent<PlayerInputHandler>();

            if (_capsule != null)
            {
                _standingColliderSize = _capsule.size;
                _standingColliderOffset = _capsule.offset;
            }

            // Platformer icin savrulma istemiyoruz: hizi dogrudan kontrol ediyoruz.
            _rb.freezeRotation = true;
            _rb.gravityScale = baseGravityScale;
            _airJumpsLeft = maxAirJumps;
        }

        private void Update()
        {
            // Girdi okuma ve kare bazli zamanlayicilar burada.
            if (input.JumpPressedThisFrame) _jumpBufferCounter = jumpBufferTime;
            else _jumpBufferCounter -= Time.deltaTime;

            if (input.JumpReleasedThisFrame) _jumpCutRequested = true;

            _coyoteCounter -= Time.deltaTime;
            _wallJumpLockCounter -= Time.deltaTime;
            _wallCoyoteCounter -= Time.deltaTime;
            _knockbackLockCounter -= Time.deltaTime;
        }

        /// <summary>
        /// Hasar aninda disaridan (ornegin PlayerHitReaction) cagrilir.
        /// Verilen hizi dogrudan uygular ve belirtilen sure boyunca normal
        /// yatay hareketi (wall-jump kilidiyle ayni desende) devre disi birakir.
        /// </summary>
        public void ApplyKnockback(Vector2 velocity, float lockDuration)
        {
            _rb.linearVelocity = velocity;
            _knockbackLockCounter = lockDuration;
        }

        private void FixedUpdate()
        {
            CheckCollisions();
            HandleCrouch();
            HandleWallSlide();
            HandleHorizontalMovement();
            HandleJump();
            ApplyGravityFeel();
            UpdateState();
        }

        /// <summary>
        /// Yerdeyken asagi basilirsa egilir: hiz duser, collider kisalir.
        /// Havadayken veya duvar kayarken egilme uygulanmaz.
        /// </summary>
        private void HandleCrouch()
        {
            bool wantsCrouch = _isGrounded && !_isWallSliding && input.MoveInput.y < -crouchInputThreshold;

            if (wantsCrouch == _isCrouching) return;
            _isCrouching = wantsCrouch;

            if (_capsule == null) return;

            if (_isCrouching)
            {
                float newHeight = _standingColliderSize.y * crouchColliderHeightMultiplier;
                float heightDelta = _standingColliderSize.y - newHeight;
                _capsule.size = new Vector2(_standingColliderSize.x, newHeight);
                _capsule.offset = _standingColliderOffset - new Vector2(0f, heightDelta * 0.5f);
            }
            else
            {
                _capsule.size = _standingColliderSize;
                _capsule.offset = _standingColliderOffset;
            }
        }

        /// <summary>Zemin ve duvar temasini OverlapBox ile kontrol eder.</summary>
        private void CheckCollisions()
        {
            Vector2 position = _rb.position;

            _isGrounded = Physics2D.OverlapBox(position + groundCheckOffset, groundCheckSize, 0f, groundLayer);

            if (_isGrounded)
            {
                _coyoteCounter = coyoteTime;
                _airJumpsLeft = maxAirJumps;
            }

            bool wallRight = Physics2D.OverlapBox(position + wallCheckOffset, wallCheckSize, 0f, groundLayer);
            bool wallLeft = Physics2D.OverlapBox(position + new Vector2(-wallCheckOffset.x, wallCheckOffset.y), wallCheckSize, 0f, groundLayer);

            if (wallRight && !wallLeft) _wallDirection = 1;
            else if (wallLeft && !wallRight) _wallDirection = -1;
            else _wallDirection = 0;

            // Duvar coyote: tam degme anini kacirsa bile (dar bosluklarda hizli
            // gecislerde fizik karesi araliginda oldugu gibi) kisa bir sure
            // daha duvar ziplamasi hakki tanir. Zemin coyote time'inin ayni mantigi.
            if (_wallDirection != 0)
            {
                _wallCoyoteCounter = wallCoyoteTime;
                _lastWallDirection = _wallDirection;
            }

            // Duvara degmek de hava ziplamasini geri verir; parkurun akiciligi icin.
            if (_wallDirection != 0 && !_isGrounded) _airJumpsLeft = maxAirJumps;
        }

        /// <summary>Duvara yapisip yavas kayma durumunu belirler.</summary>
        private void HandleWallSlide()
        {
            float moveX = input.HorizontalInput;

            _isWallSliding =
                !_isGrounded &&
                _wallDirection != 0 &&
                _rb.linearVelocity.y < 0.01f &&
                Mathf.Abs(moveX) > 0.1f &&
                Mathf.Sign(moveX) == _wallDirection;

            if (_isWallSliding)
            {
                Vector2 v = _rb.linearVelocity;
                v.y = Mathf.Max(v.y, -wallSlideSpeed);
                _rb.linearVelocity = v;
            }
        }

        /// <summary>Ayri ivmelenme/yavaslama degerleriyle yatay hareket.</summary>
        private void HandleHorizontalMovement()
        {
            // Duvardan sekmeden hemen sonra girdiyi kilitle; oyuncu duvara geri yapismasin.
            if (_wallJumpLockCounter > 0f) return;
            // Knockback sirasinda da ayni sekilde: oyuncu itilme hizini aninda iptal etmesin.
            if (_knockbackLockCounter > 0f) return;

            float moveX = input.HorizontalInput;
            float speedCap = _isCrouching ? maxSpeed * crouchSpeedMultiplier : maxSpeed;
            float targetSpeed = moveX * speedCap;
            float currentSpeed = _rb.linearVelocity.x;

            bool isAccelerating = Mathf.Abs(targetSpeed) > 0.01f;
            float rate = _isGrounded
                ? (isAccelerating ? groundAcceleration : groundDeceleration)
                : (isAccelerating ? airAcceleration : airDeceleration);

            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newSpeed, _rb.linearVelocity.y);

            // Bakis yonu guncelle (duvar kayarken donme yapma).
            if (!_isWallSliding && Mathf.Abs(moveX) > 0.1f)
            {
                _facingDirection = moveX > 0f ? 1 : -1;
            }
        }

        /// <summary>Ziplama onceligi: duvar ziplamasi > yer/coyote ziplamasi > hava ziplamasi.</summary>
        private void HandleJump()
        {
            if (_jumpBufferCounter > 0f)
            {
                // Anlik temas (_wallDirection) VEYA yakin zamanda temas etmis
                // olma (wall coyote) — ikisi de duvar ziplamasini tetikler.
                if ((_wallDirection != 0 || _wallCoyoteCounter > 0f) && !_isGrounded)
                {
                    PerformWallJump();
                }
                else if (_coyoteCounter > 0f)
                {
                    PerformJump();
                    _coyoteCounter = 0f;
                }
                else if (_airJumpsLeft > 0)
                {
                    PerformJump();
                    _airJumpsLeft--;
                }
            }

            // Degisken ziplama yuksekligi: tus erken birakilinca yukari hizi kirp.
            if (_jumpCutRequested)
            {
                _jumpCutRequested = false;
                if (_rb.linearVelocity.y > 0f)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * jumpCutMultiplier);
                }
            }
        }

        private void PerformJump()
        {
            _jumpBufferCounter = 0f;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
            OnJumped?.Invoke();
        }

        private void PerformWallJump()
        {
            _jumpBufferCounter = 0f;
            _wallCoyoteCounter = 0f;
            _wallJumpLockCounter = wallJumpInputLockTime;
            _isWallSliding = false;

            // Anlik temas yoksa (coyote penceresindeyiz) son bilinen duvar yonunu kullan.
            int direction = _wallDirection != 0 ? _wallDirection : _lastWallDirection;

            // Duvarin tersine dogru it.
            _rb.linearVelocity = new Vector2(-direction * wallJumpVelocity.x, wallJumpVelocity.y);
            _facingDirection = -direction;
            OnWallJumped?.Invoke();
        }

        /// <summary>Yercekimini duruma gore olcekler; game feel'in buyuk kismi burada.</summary>
        private void ApplyGravityFeel()
        {
            if (_isWallSliding)
            {
                _rb.gravityScale = baseGravityScale;
                return;
            }

            if (_rb.linearVelocity.y < 0f)
            {
                _rb.gravityScale = baseGravityScale * fallGravityMultiplier;
            }
            else if (_rb.linearVelocity.y > 0f && !input.JumpHeld)
            {
                _rb.gravityScale = baseGravityScale * lowJumpMultiplier;
            }
            else
            {
                _rb.gravityScale = baseGravityScale;
            }

            // Dusme hizini sinirla; yuksek dususlerde kontrol kaybolmasin.
            if (_rb.linearVelocity.y < -maxFallSpeed)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -maxFallSpeed);
            }
        }

        /// <summary>Basit durum makinesi. Raporlama amacli; hareketi kendisi yonetmiyor.</summary>
        private void UpdateState()
        {
            if (_isWallSliding) _state = PlayerState.WallSliding;
            else if (!_isGrounded && _rb.linearVelocity.y > 0.01f) _state = PlayerState.Jumping;
            else if (!_isGrounded) _state = PlayerState.Falling;
            else if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f) _state = PlayerState.Running;
            else _state = PlayerState.Idle;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Vector2 p = (Application.isPlaying && _rb != null) ? _rb.position : (Vector2)transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(p + groundCheckOffset, groundCheckSize);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(p + wallCheckOffset, wallCheckSize);
            Gizmos.DrawWireCube(p + new Vector2(-wallCheckOffset.x, wallCheckOffset.y), wallCheckSize);
        }
    }
}

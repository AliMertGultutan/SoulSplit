using SoulSplit.Combat;
using SoulSplit.Core;
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
        WallSliding,
        Dashing
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
        [SerializeField] private float maxSpeed = 11.5f;
        [Tooltip("Yerde hedef hiza ulasma ivmesi. Yuksek deger = daha keskin baslangic.")]
        [SerializeField] private float groundAcceleration = 135f;
        [Tooltip("Yerde durma yavaslamasi. Yuksek deger = daha keskin durus.")]
        [SerializeField] private float groundDeceleration = 155f;
        [Tooltip("Havada ivmelenme. Yerdekinden dusuk olmali ki hava kontrolu daha agir hissettirsin.")]
        [SerializeField] private float airAcceleration = 78f;
        [Tooltip("Havada yavaslama. Cok yuksek olursa karakter havada anlik duruyormus gibi hissettirir.")]
        [SerializeField] private float airDeceleration = 42f;

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

        [Header("Apex Hang")]
        [Tooltip("Dikey hiz bu degerin altindayken ziplamanin tepe noktasi yumusatilir.")]
        [SerializeField] private float apexHangVelocityThreshold = 1.35f;
        [Tooltip("Tepe noktasindaki yercekimi orani. Dusuk deger = daha okunabilir yon degistirme penceresi.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float apexGravityMultiplier = 0.42f;
        [Tooltip("Apex sirasinda yatay ivmeye verilen ek carpan.")]
        [SerializeField] private float apexHorizontalAccelerationMultiplier = 1.18f;

        [Header("Corner Correction")]
        [Tooltip("Koseye kafa carpinca karakterin yana duzeltilebilecegi en fazla mesafe.")]
        [SerializeField] private float cornerCorrectionDistance = 0.18f;
        [Tooltip("Tavan kose kontrolunun yukari tarama mesafesi.")]
        [SerializeField] private float cornerCheckDistance = 0.12f;

        [Header("Duvar Etkilesimi")]
        [Tooltip("Duvara yapisikken sabit dusme hizi.")]
        [SerializeField] private float wallSlideSpeed = 3.5f;
        [Tooltip("Duvardan sekme kuvveti. X = duvardan itilme, Y = yukseklik.")]
        [SerializeField] private Vector2 wallJumpVelocity = new Vector2(12f, 16f);
        [Tooltip("Duvardan sektikten sonra yatay girdinin kilitli kalma suresi. Duvara geri yapismayi engeller.")]
        [SerializeField] private float wallJumpInputLockTime = 0.14f;
        [Tooltip("Duvardan ayrildiktan/tam degemedikten sonra hala duvar ziplamasi yapilabilecek sure (saniye). Zemin coyote time'inin duvar karsiligi — dar araliklarda 'kor bolge'de basilan zip tusunun bosa gitmesini engeller.")]
        [SerializeField] private float wallCoyoteTime = 0.09f;

        [Header("Ruh Adimi (Kacinma)")]
        [Tooltip("Kacinma boyunca uygulanan sabit yatay hiz.")]
        [SerializeField] private float dodgeSpeed = 18f;
        [Tooltip("Kacinma hareketinin surdugu sure (saniye).")]
        [SerializeField] private float dodgeDuration = 0.16f;
        [Tooltip("Iki kacinma arasindaki en kisa sure. Havada ayrica tek kullanim vardir.")]
        [SerializeField] private float dodgeCooldown = 0.75f;
        [Tooltip("Kacinma girdisinin fizik karesine kadar hatirlanma suresi.")]
        [SerializeField] private float dodgeBufferTime = 0.12f;
        [Tooltip("Ruh Adimi basladiginda verilen dokunulmazlik suresi.")]
        [SerializeField] private float dodgeInvincibilityDuration = 0.20f;
        [Tooltip("Ruh Adimi baslangicinda uretilen parcaciklarin rengi.")]
        [SerializeField] private Color dodgeFxColor = new Color(0.42f, 0.9f, 1f, 0.9f);

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
        private Health _health;

        // --- Durum ---
        private PlayerState _state = PlayerState.Idle;
        private bool _isGrounded;
        private int _wallDirection;          // -1 sol duvar, +1 sag duvar, 0 duvar yok
        private int _lastWallDirection;       // wall coyote sirasinda hangi yone itilecegini hatirlar
        private bool _isWallSliding;
        private bool _isCrouching;
        private bool _isDodging;
        private bool _airDodgeAvailable = true;
        private int _facingDirection = 1;
        private int _dodgeDirection = 1;
        private int _airJumpsLeft;

        private Vector2 _standingColliderSize;
        private Vector2 _standingColliderOffset;

        // --- Zamanlayicilar ---
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private float _wallJumpLockCounter;
        private float _wallCoyoteCounter;
        private float _knockbackLockCounter;
        private float _dodgeBufferCounter;
        private float _dodgeTimer;
        private float _dodgeCooldownCounter;
        private bool _bodyEnemyCollisionIgnored;
        private bool _bodyGhostCollisionIgnored;
        private bool _dodgeCollisionIgnoreActive;

        // --- Update'te toplanip FixedUpdate'te tuketilen bayrak ---
        private bool _jumpCutRequested;
        private Vector2 _previousPhysicsVelocity;

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
        /// <summary>Ruh Adimi hareketi su an etkin mi?</summary>
        public bool IsDodging => _isDodging;
        /// <summary>Taklanin 0-1 arasi tamamlanma orani; gorsel tam tur donusu bununla oynatir.</summary>
        public float DodgeProgressNormalized => !_isDodging || dodgeDuration <= 0f
            ? 0f
            : 1f - Mathf.Clamp01(_dodgeTimer / dodgeDuration);
        /// <summary>Taklanin yatay yonu: -1 sol, +1 sag.</summary>
        public int DodgeDirection => _dodgeDirection;
        /// <summary>Cooldown ve hava kullanim kurallarina gore yeni kacinma hazir mi?</summary>
        public bool IsDodgeReady => !_isDodging && _dodgeCooldownCounter <= 0f && (_isGrounded || _airDodgeAvailable);
        /// <summary>Gecici gucler icin temel denge degerini bozmayan harici hiz carpani.</summary>
        public float MovementSpeedMultiplier { get; set; } = 1f;
        /// <summary>Basarili her Ruh Adimi baslangicinda tetiklenir.</summary>
        public event System.Action OnDodged;

        /// <summary>
        /// Form degisimi/isinlanma gibi harici sistemlerin oyuncunun baktigi yonu
        /// hareket girdisi beklemeden esitleyebilmesi icin.
        /// </summary>
        public void SetFacingDirection(int direction)
        {
            if (direction != 0) _facingDirection = direction > 0 ? 1 : -1;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _capsule = GetComponent<CapsuleCollider2D>();
            _health = GetComponent<Health>();
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

        private void OnDisable()
        {
            if (_rb != null && _isDodging) _rb.gravityScale = baseGravityScale;
            _isDodging = false;
            _dodgeBufferCounter = 0f;
            EndDodgeCollisionIgnore();
            SetCrouching(false, force: true);
        }

        private void Update()
        {
            // Girdi okuma ve kare bazli zamanlayicilar burada.
            if (input.JumpPressedThisFrame) _jumpBufferCounter = jumpBufferTime;
            else _jumpBufferCounter -= Time.deltaTime;

            if (input.JumpReleasedThisFrame) _jumpCutRequested = true;
            if (input.DodgePressedThisFrame) RequestDodge();

            _coyoteCounter -= Time.deltaTime;
            _wallJumpLockCounter -= Time.deltaTime;
            _wallCoyoteCounter -= Time.deltaTime;
            _knockbackLockCounter -= Time.deltaTime;
            _dodgeBufferCounter -= Time.deltaTime;
            _dodgeCooldownCounter -= Time.deltaTime;
        }

        /// <summary>
        /// Bir Ruh Adimi istegini kisa sureligine tamponlar. Girdi sistemi ve
        /// testler ayni kapidan gecer; gercek fizik hareketi FixedUpdate'te baslar.
        /// </summary>
        public bool RequestDodge()
        {
            if (!isActiveAndEnabled || _health == null || _health.IsDead || TimeScaleController.IsPaused)
                return false;

            _dodgeBufferCounter = Mathf.Max(_dodgeBufferCounter, dodgeBufferTime);
            return true;
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
            if (HandleDodge())
            {
                UpdateState();
                _previousPhysicsVelocity = _rb.linearVelocity;
                return;
            }
            HandleCornerCorrection();
            HandleCrouch();
            HandleWallSlide();
            HandleHorizontalMovement();
            HandleJump();
            ApplyGravityFeel();
            UpdateState();
            _previousPhysicsVelocity = _rb.linearVelocity;
        }

        /// <summary>
        /// Yerdeyken asagi basilirsa egilir: hiz duser, collider kisalir.
        /// Havadayken veya duvar kayarken egilme uygulanmaz.
        /// </summary>
        private void HandleCrouch()
        {
            bool wantsCrouch = _isGrounded && !_isWallSliding && input.MoveInput.y < -crouchInputThreshold;

            // Takla bittiginde oyuncu hâlâ alcak tavan altindaysa collider'i zorla
            // buyutme; bosluk bulunana kadar egik profil korunur.
            if (!wantsCrouch && _isCrouching && !CanUseStandingCollider())
                wantsCrouch = true;

            SetCrouching(wantsCrouch);
        }

        private void SetCrouching(bool crouching, bool force = false)
        {
            if (!force && crouching == _isCrouching) return;
            _isCrouching = crouching;

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

        private bool CanUseStandingCollider()
        {
            if (_capsule == null || !_capsule.enabled) return true;

            float currentTop = _capsule.offset.y + _capsule.size.y * 0.5f;
            float standingTop = _standingColliderOffset.y + _standingColliderSize.y * 0.5f;
            float localClearanceHeight = standingTop - currentTop;
            if (localClearanceHeight <= 0.01f) return true;

            Vector3 scale = transform.lossyScale;
            float clearanceHeight = localClearanceHeight * Mathf.Abs(scale.y);
            Vector2 center = _rb.position + new Vector2(
                _standingColliderOffset.x * scale.x,
                (currentTop + localClearanceHeight * 0.5f) * scale.y);
            Vector2 size = new Vector2(
                _standingColliderSize.x * Mathf.Abs(scale.x) * 0.9f,
                Mathf.Max(0.02f, clearanceHeight * 0.92f));

            Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                center, size, transform.eulerAngles.z, groundLayer);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap != null && overlap != _capsule && !overlap.isTrigger)
                    return false;
            }
            return true;
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
                _airDodgeAvailable = true;
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
            float boostedMaxSpeed = maxSpeed * Mathf.Max(0.01f, MovementSpeedMultiplier);
            float speedCap = _isCrouching ? boostedMaxSpeed * crouchSpeedMultiplier : boostedMaxSpeed;
            float targetSpeed = moveX * speedCap;
            float currentSpeed = _rb.linearVelocity.x;

            bool isAccelerating = Mathf.Abs(targetSpeed) > 0.01f;
            float rate = _isGrounded
                ? (isAccelerating ? groundAcceleration : groundDeceleration)
                : (isAccelerating ? airAcceleration : airDeceleration);

            if (!_isGrounded && Mathf.Abs(_rb.linearVelocity.y) <= apexHangVelocityThreshold)
                rate *= apexHorizontalAccelerationMultiplier;

            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newSpeed, _rb.linearVelocity.y);

            // Bakis yonu guncelle (duvar kayarken donme yapma).
            if (!_isWallSliding && Mathf.Abs(moveX) > 0.1f)
            {
                _facingDirection = moveX > 0f ? 1 : -1;
            }
        }

        /// <summary>
        /// Kisa, yatay ve okunabilir bir kacinma uygular. Hareket sirasinda
        /// normal ivme/yercekimi devre disidir; duvar carpismasini Rigidbody2D cozer.
        /// </summary>
        private bool HandleDodge()
        {
            if (_isDodging)
            {
                _dodgeTimer -= Time.fixedDeltaTime;
                if (_dodgeTimer <= 0f)
                {
                    _isDodging = false;
                    _rb.gravityScale = baseGravityScale;
                    EndDodgeCollisionIgnore();
                    return false;
                }

                _rb.gravityScale = 0f;
                _rb.linearVelocity = new Vector2(_dodgeDirection * dodgeSpeed, 0f);
                return true;
            }

            if (_dodgeBufferCounter <= 0f || !IsDodgeReady || _knockbackLockCounter > 0f)
                return false;

            _dodgeBufferCounter = 0f;
            _dodgeCooldownCounter = dodgeCooldown;
            _dodgeTimer = dodgeDuration;
            _dodgeDirection = Mathf.Abs(input.HorizontalInput) > 0.1f
                ? (input.HorizontalInput > 0f ? 1 : -1)
                : _facingDirection;
            _facingDirection = _dodgeDirection;
            _isDodging = true;
            BeginDodgeCollisionIgnore();
            // Takla boyunca fiziksel profil de kuculur; sadece sprite donmez,
            // oyuncu gercekten alcak platformlarin altindan gecebilir.
            SetCrouching(true);
            if (!_isGrounded) _airDodgeAvailable = false;

            _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(_dodgeDirection * dodgeSpeed, 0f);
            _health.GrantInvincibility(dodgeInvincibilityDuration);
            ParticleFX.Burst(transform.position, dodgeFxColor, 8, 3.2f, 0.11f, 0.24f,
                65f, new Vector2(-_dodgeDirection, 0.25f), 0f);
            OnDodged?.Invoke();
            return true;
        }

        private void BeginDodgeCollisionIgnore()
        {
            if (_dodgeCollisionIgnoreActive) return;
            _dodgeCollisionIgnoreActive = true;
            int bodyLayer = gameObject.layer;
            int physicalEnemyLayer = LayerMask.NameToLayer("PhysicalEnemy");
            int ghostEnemyLayer = LayerMask.NameToLayer("GhostEnemy");

            if (physicalEnemyLayer >= 0)
            {
                _bodyEnemyCollisionIgnored = Physics2D.GetIgnoreLayerCollision(bodyLayer, physicalEnemyLayer);
                Physics2D.IgnoreLayerCollision(bodyLayer, physicalEnemyLayer, true);
            }
            if (ghostEnemyLayer >= 0)
            {
                _bodyGhostCollisionIgnored = Physics2D.GetIgnoreLayerCollision(bodyLayer, ghostEnemyLayer);
                Physics2D.IgnoreLayerCollision(bodyLayer, ghostEnemyLayer, true);
            }
        }

        private void EndDodgeCollisionIgnore()
        {
            if (!_dodgeCollisionIgnoreActive) return;
            _dodgeCollisionIgnoreActive = false;
            int bodyLayer = gameObject.layer;
            int physicalEnemyLayer = LayerMask.NameToLayer("PhysicalEnemy");
            int ghostEnemyLayer = LayerMask.NameToLayer("GhostEnemy");

            if (physicalEnemyLayer >= 0)
                Physics2D.IgnoreLayerCollision(bodyLayer, physicalEnemyLayer, _bodyEnemyCollisionIgnored);
            if (ghostEnemyLayer >= 0)
                Physics2D.IgnoreLayerCollision(bodyLayer, ghostEnemyLayer, _bodyGhostCollisionIgnored);
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

            if (!_isGrounded && input.JumpHeld && Mathf.Abs(_rb.linearVelocity.y) <= apexHangVelocityThreshold)
            {
                _rb.gravityScale = baseGravityScale * apexGravityMultiplier;
            }
            else if (_rb.linearVelocity.y < 0f)
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

        /// <summary>
        /// Ziplamanin tepesinde bir tavan kosesine tek omuzla takilinca oyuncuyu
        /// acik tarafa hafifce kaydirir ve yukari momentumunu korur.
        /// </summary>
        private void HandleCornerCorrection()
        {
            if (_capsule == null || _isGrounded || _previousPhysicsVelocity.y <= 0.1f ||
                _rb.linearVelocity.y > 0.1f || cornerCorrectionDistance <= 0f)
                return;

            Bounds bounds = _capsule.bounds;
            float inset = Mathf.Min(bounds.extents.x * 0.35f, 0.12f);
            Vector2 leftOrigin = new Vector2(bounds.min.x + inset, bounds.max.y - 0.02f);
            Vector2 rightOrigin = new Vector2(bounds.max.x - inset, bounds.max.y - 0.02f);
            bool leftBlocked = Physics2D.Raycast(leftOrigin, Vector2.up, cornerCheckDistance, groundLayer);
            bool rightBlocked = Physics2D.Raycast(rightOrigin, Vector2.up, cornerCheckDistance, groundLayer);

            if (leftBlocked == rightBlocked) return;

            float correction = leftBlocked ? cornerCorrectionDistance : -cornerCorrectionDistance;
            Vector2 target = _rb.position + Vector2.right * correction;
            _rb.position = target;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _previousPhysicsVelocity.y);
        }

        /// <summary>Basit durum makinesi. Raporlama amacli; hareketi kendisi yonetmiyor.</summary>
        private void UpdateState()
        {
            if (_isDodging) _state = PlayerState.Dashing;
            else if (_isWallSliding) _state = PlayerState.WallSliding;
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

        private void OnValidate()
        {
            maxAirJumps = Mathf.Max(0, maxAirJumps);
            maxSpeed = Mathf.Max(0.1f, maxSpeed);
            apexHangVelocityThreshold = Mathf.Max(0f, apexHangVelocityThreshold);
            apexHorizontalAccelerationMultiplier = Mathf.Max(1f, apexHorizontalAccelerationMultiplier);
            cornerCorrectionDistance = Mathf.Max(0f, cornerCorrectionDistance);
            cornerCheckDistance = Mathf.Max(0.01f, cornerCheckDistance);
            dodgeSpeed = Mathf.Max(0f, dodgeSpeed);
            dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
            dodgeCooldown = Mathf.Max(dodgeDuration, dodgeCooldown);
            dodgeBufferTime = Mathf.Max(0f, dodgeBufferTime);
            dodgeInvincibilityDuration = Mathf.Max(0f, dodgeInvincibilityDuration);
        }
    }
}

using UnityEngine;
using SoulSplit.Combat;
using SoulSplit.Core;

namespace SoulSplit.Player
{
    /// <summary>
    /// Sprite kare animasyonu OLMADAN hareket hissi veren prosedurel animasyon.
    /// Sadece gorsel objenin localScale / localRotation / localPosition
    /// degerlerini oynatir; fizige veya kontrolcuye hic dokunmaz.
    ///
    /// YURUME ve KOSMA ayrimi adim temposu (stride) uzerinden yapiliyor:
    /// yavas giderken uzun, agir ve az sekmeli adimlar; hizlanınca adimlar
    /// sikilasir, sekme buyur, govde one yatar.
    ///
    /// ZIPLAMA "launch pop" ile satirlarina anticipation kazandirir: gercek
    /// ziplama aninda (input degil, PlayerController.OnJumped olayi) once
    /// hizli bir CÖKME, ardindan UZAMA oynar — klasik "coil-release" hissi,
    /// oyunun kendi tepki suresini hic geciktirmeden.
    /// </summary>
    public class PlayerProceduralAnimator : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerController controller;
        [Tooltip("Vurus animasyonunu tetikleyen kaynak. Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private MeleeAttack meleeAttack;

        [Header("Yurume (dusuk hiz)")]
        [Tooltip("Yurume sayilan en yuksek hiz orani (0-1). Ustu kosma kabul edilir.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float walkThreshold = 0.45f;
        [Tooltip("Yururken saniyedeki adim sayisi.")]
        [SerializeField] private float walkStepRate = 2.6f;
        [Tooltip("Yururken her adimda govdenin yukselme miktari.")]
        [SerializeField] private float walkBounce = 0.05f;
        [Tooltip("Yururken govdenin one yatma acisi (derece).")]
        [SerializeField] private float walkLean = 6f;
        [Tooltip("Yururken saga sola salinma acisi (derece).")]
        [SerializeField] private float walkSway = 4f;

        [Header("Kosma (yuksek hiz)")]
        [SerializeField] private float runStepRate = 4.6f;
        [SerializeField] private float runBounce = 0.13f;
        [SerializeField] private float runLean = 15f;
        [SerializeField] private float runSway = 8f;

        [Header("Adim Basisi")]
        [Tooltip("Ayagin yere bastigi anda uygulanan ezilme. Adima agirlik verir.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float footPlantSquash = 0.09f;
        [Tooltip("Adim basisinin ne kadar keskin hissedilecegi. Yuksek = daha vurgulu.")]
        [SerializeField] private float footPlantSharpness = 3f;

        [Header("Havada Uzama / Ezilme (surekli, hiza bagli)")]
        [Range(0f, 0.6f)]
        [SerializeField] private float airStretch = 0.26f;
        [SerializeField] private float stretchResponse = 16f;

        [Header("Inis Darbesi")]
        [Range(0f, 0.7f)]
        [SerializeField] private float landSquash = 0.46f;
        [SerializeField] private float landSquashFullSpeed = 16f;
        [SerializeField] private float landRecoverySpeed = 9f;

        [Header("Ziplama Anticipation'i (launch pop)")]
        [Tooltip("Cokme+uzama animasyonunun toplam suresi. Kisa tut; oyun tepkisini geciktirmez, sadece gorseldir.")]
        [SerializeField] private float launchPopDuration = 0.11f;
        [Tooltip("Suren ne kadarinin 'cokme' fazina ayrilacagi (0-1). Geri kalani uzamaya gider.")]
        [Range(0.1f, 0.6f)]
        [SerializeField] private float launchPopSquashPortion = 0.35f;
        [Tooltip("Cokme fazinin derinligi (negatif = ezilme).")]
        [Range(-0.4f, 0f)]
        [SerializeField] private float launchPopSquashAmount = -0.18f;
        [Tooltip("Uzama fazinin tepe degeri.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float launchPopStretchAmount = 0.36f;
        [Tooltip("Duvar ziplamasinda pop siddetinin normal ziplamaya orani. Daha kucuk — duvar sekmesi daha ani.")]
        [Range(0f, 1f)]
        [SerializeField] private float wallJumpPopMultiplier = 0.65f;

        [Header("Duvar Kaymasi")]
        [SerializeField] private float wallSlideTilt = 10f;

        [Header("Egilme (Crouch) Gorseli")]
        [Tooltip("Egilirken sprite'in ne kadar basilacagi. Collider zaten kisaliyor (PlayerController) — bu sadece GORSEL geri bildirim, oyuncu tusun isledigini gorsun diye.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float crouchSquashAmount = 0.32f;
        [SerializeField] private float crouchTransitionSpeed = 14f;

        [Header("Bekleme Nefesi")]
        [Range(0f, 0.12f)]
        [SerializeField] private float idleBreathAmount = 0.045f;
        [SerializeField] private float idleBreathFrequency = 1.9f;

        [Header("Genel")]
        [Tooltip("Egilme acisinin yay sertligi. Yuksek = daha hizli tepki.")]
        [SerializeField] private float tiltSpringStiffness = 260f;
        [Tooltip("Yay sonumlemesi. Dusuk = daha fazla asiri-salinim (overshoot/wobble), yuksek = daha sert durma.")]
        [SerializeField] private float tiltSpringDamping = 16f;

        [Header("Toz Efektleri")]
        [SerializeField] private Color dustColor = new Color(0.55f, 0.5f, 0.42f, 1f);
        [Tooltip("Kosarken iki adim arasi en kisa toz araligi (saniye). Hizli tekrari engeller.")]
        [SerializeField] private float runDustCooldown = 0.16f;
        [SerializeField] private float wallSlideDustInterval = 0.12f;

        // ARASTIRMA: dovus oyunlarinda vurus 4 fazlidir — hazirlik/aksiyon/TUTMA/toparlanma.
        // "4 kareli guclu pozlu vurus, 12 kareli mushy olandan iyidir" (Sprite-AI, GDC juice konusmalari).
        // Zamanlama KONTRASTLI olmali: yavas hazirlik, COK hizli vurus, vurus anini UZUN tutma, orta toparlanma.
        [Header("Dovus Animasyonu (2D dovus oyunu zamanlamasi)")]
        [Tooltip("Hazirlik (geri cekilme) suresi. YAVAS — oyuncuya 'geliyor' hissi verir.")]
        [SerializeField] private float attackAnticipationDuration = 0.08f;
        [Tooltip("Vurus darbesinin kendisi. COK KISA — 'snap' hissi, darbe zaten bu anda uygulaniyor.")]
        [SerializeField] private float attackStrikeDuration = 0.045f;
        [Tooltip("Vurus tepe pozunun EKRANDA ASILI KALMA suresi (impact frame hold). " +
                 "Klasik teknik: digger kareler ~80ms iken bu kare 150-200ms tutulur — goz vurusu 'okur'.")]
        [SerializeField] private float attackHoldDuration = 0.09f;
        [Tooltip("Notre donme suresi. ORTA hizda — ne cok ani ne cok yavas.")]
        [SerializeField] private float attackRecoveryDuration = 0.15f;
        [Tooltip("Geri cekilme acisi (derece). Bakis yonunun tersine.")]
        [SerializeField] private float attackWindupAngle = 18f;
        [Tooltip("Savurma acisi (derece). Bakis yonune dogru asiri donus — vurus hissi verir.")]
        [SerializeField] private float attackSwingAngle = 42f;
        [Tooltip("Savurma aninda one kayma mesafesi (dunya birimi).")]
        [SerializeField] private float attackLungeDistance = 0.27f;

        [Header("Dovus Animasyonu — Agir Saldiri")]
        [Tooltip("Hazirlik suresi. Hafiften UZUN — buyuk vurusun 'geliyor' hissi daha belirgin olmali.")]
        [SerializeField] private float heavyAttackAnticipationDuration = 0.16f;
        [Tooltip("Vurus darbesi. Hazirliktan cok daha kisa kalmali — 'yavas hazirlik, ani darbe' kontrasti agirda da gecerli.")]
        [SerializeField] private float heavyAttackStrikeDuration = 0.05f;
        [SerializeField] private float heavyAttackHoldDuration = 0.14f;
        [Tooltip("Toparlanma. Hafiften UZUN — agir saldirinin bedeli budur, oyuncu daha uzun sure acik kalir.")]
        [SerializeField] private float heavyAttackRecoveryDuration = 0.28f;
        [SerializeField] private float heavyAttackWindupAngle = 30f;
        [SerializeField] private float heavyAttackSwingAngle = 60f;
        [SerializeField] private float heavyAttackLungeDistance = 0.42f;

        private float _spriteHalfHeight = 0.9f;
        private Vector3 _basePosition;

        private float _currentStretch;
        private float _landSquashAmount;
        private float _currentTilt;
        private float _stridePhase;
        private float _breathPhase;
        private bool _wasGrounded;

        private float _launchPopTimer;
        private float _launchPopMagnitude = 1f;

        private float _tiltAngularVelocity;
        private float _dustCooldownTimer;
        private float _wallSlideDustTimer;
        private float _crouchAmount;
        private bool _wasDodging;

        private float _attackTimer;
        private AttackOverlayTimings _lightAttackTimings;
        private AttackOverlayTimings _heavyAttackTimings;
        private AttackTier _attackTier = AttackTier.Light;
        private int _attackFacing = 1;

        /// <summary>Su an kosma temposunda mi? Ses ve toz efektleri bunu okuyabilir.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>Adim dongusundeki konum (0-1). Ayak sesi tetiklemek icin.</summary>
        public float StrideProgress => Mathf.Repeat(_stridePhase / (Mathf.PI * 2f), 1f);

        /// <summary>
        /// Hasar aninda disaridan (PlayerHitReaction) cagrilir. Inis ezilmesiyle
        /// AYNI decay mekanizmasini (_landSquashAmount, landRecoverySpeed) kullanir;
        /// yeni bir zamanlayici/state eklemeye gerek yok.
        /// </summary>
        public void TriggerHitSquash(float amount)
        {
            _landSquashAmount = amount;
            _currentStretch = 0f;
            _launchPopTimer = 0f;
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (meleeAttack == null) meleeAttack = GetComponentInParent<MeleeAttack>();
            _basePosition = transform.localPosition;

            if (TryGetComponent(out SpriteRenderer sr) && sr.sprite != null)
            {
                _spriteHalfHeight = sr.sprite.bounds.extents.y;
            }

            _lightAttackTimings = new AttackOverlayTimings(attackAnticipationDuration, attackStrikeDuration,
                attackHoldDuration, attackRecoveryDuration, attackWindupAngle, attackSwingAngle, attackLungeDistance);
            _heavyAttackTimings = new AttackOverlayTimings(heavyAttackAnticipationDuration, heavyAttackStrikeDuration,
                heavyAttackHoldDuration, heavyAttackRecoveryDuration, heavyAttackWindupAngle, heavyAttackSwingAngle, heavyAttackLungeDistance);
        }

        private void OnEnable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered += HandleAttackTriggered;
            if (controller != null)
            {
                controller.OnJumped += HandleJumped;
                controller.OnWallJumped += HandleWallJumped;
            }
        }

        private void OnDisable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered -= HandleAttackTriggered;
            if (controller != null)
            {
                controller.OnJumped -= HandleJumped;
                controller.OnWallJumped -= HandleWallJumped;
            }
        }

        private void HandleAttackTriggered(AttackTier tier)
        {
            _attackTier = tier;
            _attackTimer = tier == AttackTier.Heavy ? _heavyAttackTimings.TotalDuration : _lightAttackTimings.TotalDuration;
            _attackFacing = controller != null ? controller.FacingDirection : 1;
        }

        private void HandleJumped()
        {
            _launchPopTimer = launchPopDuration;
            _launchPopMagnitude = 1f;
            _landSquashAmount = 0f;
            ParticleFX.Dust(FeetWorldPosition(), 0.5f, dustColor);
        }

        private void HandleWallJumped()
        {
            _launchPopTimer = launchPopDuration;
            _launchPopMagnitude = wallJumpPopMultiplier;
            _landSquashAmount = 0f;
            ParticleFX.Impact(transform.position, dustColor,
                new Vector2(controller.WallDirection, 0.3f), 0.7f);
        }

        /// <summary>Ayaklarin dunya konumu; toz efektleri buradan cikar.</summary>
        private Vector3 FeetWorldPosition()
        {
            return transform.position + new Vector3(0f, -_spriteHalfHeight * 0.9f, 0f);
        }

        private void LateUpdate()
        {
            if (controller == null) return;

            float dt = Time.deltaTime;
            Vector2 velocity = controller.Velocity;
            bool grounded = controller.IsGrounded;
            PlayerState state = controller.State;

            DetectLanding(grounded, velocity.y);
            _wasGrounded = grounded;

            if (_launchPopTimer > 0f)
            {
                ApplyLaunchPop(dt);
            }
            else
            {
                float targetStretch = grounded
                    ? 0f
                    : Mathf.Clamp(velocity.y / Mathf.Max(1f, controller.JumpVelocity), -1f, 1f) * airStretch;
                _currentStretch = Mathf.Lerp(_currentStretch, targetStretch, 1f - Mathf.Exp(-stretchResponse * dt));
            }

            _landSquashAmount = Mathf.Lerp(_landSquashAmount, 0f, 1f - Mathf.Exp(-landRecoverySpeed * dt));

            float targetCrouch = controller.IsCrouching ? crouchSquashAmount : 0f;
            _crouchAmount = Mathf.Lerp(_crouchAmount, targetCrouch, 1f - Mathf.Exp(-crouchTransitionSpeed * dt));

            ApplyStride(velocity, grounded, state, dt);
            ApplyTilt(state, velocity, grounded, dt);
            ApplyAttackOverlay(dt);
        }

        /// <summary>
        /// Cokme -> uzama sirasini oynatir. Exponential smoothing KULLANMAZ —
        /// cartoon animasyonun keskinligi icin dogrudan deger atar.
        /// </summary>
        private void ApplyLaunchPop(float dt)
        {
            _launchPopTimer -= dt;
            float elapsed = launchPopDuration - Mathf.Max(_launchPopTimer, 0f);
            float t = launchPopDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / launchPopDuration);

            float squashTarget = launchPopSquashAmount * _launchPopMagnitude;
            float stretchTarget = launchPopStretchAmount * _launchPopMagnitude;

            if (t < launchPopSquashPortion)
            {
                float squashT = t / launchPopSquashPortion;
                _currentStretch = Mathf.Lerp(0f, squashTarget, AttackOverlayAnimator.EaseOutQuad(squashT));
            }
            else
            {
                float stretchT = (t - launchPopSquashPortion) / (1f - launchPopSquashPortion);
                _currentStretch = Mathf.Lerp(squashTarget, stretchTarget, AttackOverlayAnimator.EaseOutQuad(stretchT));
            }
        }

        /// <summary>
        /// Vurus animasyonunu hareket animasyonunun UZERINE bindirir; kosarken
        /// bile vurabildigi icin ikisini birbirini iptal etmeden toplayan bir
        /// katman olarak calisir. Hasar zaten aninda uygulaniyor — buradaki
        /// gecikme sadece gorsel.
        ///
        /// 4 FAZ (2D dovus oyunu zamanlamasi): hazirlik (yavas) -> vurus (COK
        /// hizli, "snap") -> TUTMA (vurus pozu ekranda asili kalir, goz okur)
        /// -> toparlanma (orta). Kontrastli zamanlama, tek-duz-hizli
        /// interpolasyondan cok daha "punchy" okunuyor.
        /// </summary>
        private void ApplyAttackOverlay(float dt)
        {
            if (_attackTimer <= 0f) return;

            bool isHeavy = _attackTier == AttackTier.Heavy;
            AttackOverlayTimings timings = isHeavy ? _heavyAttackTimings : _lightAttackTimings;

            _attackTimer -= dt;
            float elapsed = timings.TotalDuration - Mathf.Max(_attackTimer, 0f);

            AttackOverlayAnimator.Evaluate(timings, elapsed, out float angleOffset, out float lungeOffset);

            transform.localRotation *= Quaternion.Euler(0f, 0f, angleOffset * _attackFacing);
            transform.localPosition += new Vector3(lungeOffset * _attackFacing, 0f, 0f);
        }

        /// <summary>
        /// Adim dongusu: hiza gore yurume ya da kosma parametreleri secilir,
        /// aradaki gecis yumusak olsun diye iki set arasinda harmanlanir.
        /// </summary>
        private void ApplyStride(Vector2 velocity, bool grounded, PlayerState state, float dt)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(velocity.x) / Mathf.Max(1f, controller.MaxSpeed));
            bool isMoving = grounded && speedRatio > 0.08f;

            // 0 = tam yurume, 1 = tam kosma. Esik civarinda yumusak gecis.
            float runBlend = Mathf.InverseLerp(walkThreshold, 1f, speedRatio);
            IsRunning = isMoving && runBlend > 0.5f;

            float bounce = 0f;
            float squash = 0f;

            if (isMoving)
            {
                float stepRate = Mathf.Lerp(walkStepRate, runStepRate, runBlend);
                float bounceAmount = Mathf.Lerp(walkBounce, runBounce, runBlend);

                // Adim temposu hiza da bagli: yavaslarken adimlar seyrelir.
                _stridePhase += dt * stepRate * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1f, speedRatio);

                // Iki ayak = bir adim dongusunde iki sekme. abs(sin) tam bunu verir.
                bounce = Mathf.Abs(Mathf.Sin(_stridePhase)) * bounceAmount;

                // Ayak yere bastigi an (sin sifira yaklastigi an) govde ezilir.
                float plant = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(_stridePhase)), footPlantSharpness);
                squash = plant * footPlantSquash * Mathf.Lerp(0.7f, 1.4f, runBlend);

                // Adim basisinda toz: sadece kosarken (yururken degil), ayni
                // basisa iki kere tozlanmasin diye kisa bir bekleme ile sinirlanir.
                _dustCooldownTimer -= dt;
                if (runBlend > 0.4f && plant > 0.9f && _dustCooldownTimer <= 0f)
                {
                    ParticleFX.Dust(FeetWorldPosition(), runBlend * 0.6f, dustColor);
                    _dustCooldownTimer = runDustCooldown;
                }
            }
            else if (grounded && state == PlayerState.Idle)
            {
                _breathPhase += dt * idleBreathFrequency;
                bounce = Mathf.Sin(_breathPhase) * idleBreathAmount;
                _stridePhase = 0f;
            }

            float stretch = _currentStretch - _landSquashAmount - squash - _crouchAmount;
            float scaleY = 1f + stretch;
            // Hacim korunur gibi: uzarken incelir, ezilirken genisler.
            float scaleX = 1f / Mathf.Max(0.2f, scaleY);

            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            // Olcek merkezden buyudugu icin ayaklar zeminden kopmasin diye telafi,
            // uzerine de adim sekmesini ekliyoruz.
            float heightDelta = (scaleY - 1f) * _spriteHalfHeight + bounce;
            transform.localPosition = _basePosition + new Vector3(0f, heightDelta, 0f);
        }

        private void ApplyTilt(PlayerState state, Vector2 velocity, bool grounded, float dt)
        {
            if (state == PlayerState.Dashing)
            {
                // Shift hareketi okunabilir bir tam takladir. Fizik rotasyonu
                // kilitli kalir; yalnizca gorsel cocuk kendi ekseninde doner.
                _currentTilt = -controller.DodgeDirection * 360f * controller.DodgeProgressNormalized;
                _tiltAngularVelocity = 0f;
                transform.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);
                _wasDodging = true;
                return;
            }

            if (_wasDodging)
            {
                // -360 ve 360 gorsel olarak sifirdir; yay hesabinin ters yone
                // fazladan bir tur atmamasi icin aciyi en yakin esdegerine sar.
                _currentTilt = Mathf.DeltaAngle(0f, _currentTilt);
                _tiltAngularVelocity = 0f;
                _wasDodging = false;
            }

            float targetTilt;

            if (state == PlayerState.WallSliding)
            {
                targetTilt = -controller.WallDirection * wallSlideTilt;

                _wallSlideDustTimer -= dt;
                if (_wallSlideDustTimer <= 0f)
                {
                    ParticleFX.Dust(transform.position, 0.3f, dustColor);
                    _wallSlideDustTimer = wallSlideDustInterval;
                }
            }
            else
            {
                float speedRatio = Mathf.Clamp(velocity.x / Mathf.Max(1f, controller.MaxSpeed), -1f, 1f);
                float runBlend = Mathf.InverseLerp(walkThreshold, 1f, Mathf.Abs(speedRatio));
                float lean = Mathf.Lerp(walkLean, runLean, runBlend);

                // Gidilen yone dogru yatma.
                targetTilt = -speedRatio * lean;

                // Uzerine adim salinimi: her adimda govde hafifce saga sola dusuyor.
                if (grounded && Mathf.Abs(speedRatio) > 0.08f)
                {
                    float sway = Mathf.Lerp(walkSway, runSway, runBlend);
                    targetTilt += Mathf.Sin(_stridePhase * 0.5f) * sway * Mathf.Sign(speedRatio);
                }
            }

            // Yay-sonumlu hareket: pure exponential lerp'in aksine HEDEFI
            // ASIP geri gelir (overshoot) — Disney'in "follow-through" ilkesi.
            // Ani yon degisimlerinde (duvar sekmesi, sert inis) govde bir tik
            // fazla yatip sonra yerine oturuyor; bu "canlilik" hissi verir.
            float error = targetTilt - _currentTilt;
            float springForce = error * tiltSpringStiffness - _tiltAngularVelocity * tiltSpringDamping;
            _tiltAngularVelocity += springForce * dt;
            _currentTilt += _tiltAngularVelocity * dt;

            transform.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);
        }

        private void DetectLanding(bool grounded, float verticalVelocity)
        {
            if (grounded && !_wasGrounded)
            {
                float impact = Mathf.InverseLerp(0f, landSquashFullSpeed, Mathf.Abs(verticalVelocity));
                _landSquashAmount = landSquash * impact;
                _currentStretch = 0f;
                _launchPopTimer = 0f;
                _stridePhase = 0f;

                if (impact > 0.15f) ParticleFX.Dust(FeetWorldPosition(), impact, dustColor);
            }
        }
    }
}

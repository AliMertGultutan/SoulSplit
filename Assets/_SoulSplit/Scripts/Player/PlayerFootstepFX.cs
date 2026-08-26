using UnityEngine;
using SoulSplit.Core;

namespace SoulSplit.Player
{
    /// <summary>
    /// Oyuncunun toz/parcacik efektleri — HANGI animasyon sistemi kullanilirsa
    /// kullanilsin calisir.
    ///
    /// NEDEN AYRI BIR BILESEN: Bu efektler eskiden PlayerProceduralAnimator'in
    /// icinde yasiyordu. Sprite-sheet animasyonuna gecince o bilesen devre disi
    /// birakildi ve TUM toz efektleri sessizce kayboldu. Buraya cikarilarak
    /// animasyon sisteminden bagimsiz hale getirildi; prosedurel animator geri
    /// acilsa bile cift toz olmaz (oradaki cagrilar artik kullanilmiyor).
    ///
    /// Ayak sesi tozu, Animator varsa GERCEK yurume/kosma karelerine gore
    /// tetiklenir (dongude 0.0 ve 0.5 noktalari = iki ayak basisi) — bu,
    /// prosedurel stride fazina gore daha dogru bir senkronizasyon.
    /// </summary>
    public class PlayerFootstepFX : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private PlayerController controller;
        [Tooltip("Ayak basisini kare animasyonuna senkronlamak icin. Bos ise sureye dayali yedek yontem kullanilir.")]
        [SerializeField] private Animator animator;

        [Header("Toz")]
        [SerializeField] private Color dustColor = new Color(0.55f, 0.5f, 0.42f, 1f);
        [Tooltip("Ayaklarin obje merkezine gore dikey konumu. Bos birakilirsa collider'dan hesaplanir.")]
        [SerializeField] private float feetOffsetY = 0f;

        [Header("Kosma Tozu")]
        [Tooltip("Bu hiz oraninin uzerinde ayak basisi tozu cikar (yururken cikmaz).")]
        [Range(0f, 1f)]
        [SerializeField] private float runDustSpeedThreshold = 0.45f;
        [Tooltip("Iki toz arasi en kisa sure; ayni basista iki kere tozlanmayi engeller.")]
        [SerializeField] private float runDustCooldown = 0.16f;

        [Header("Inis")]
        [Tooltip("Bu dikey hizda inis tam siddetli sayilir.")]
        [SerializeField] private float landFullSpeed = 16f;
        [Tooltip("Bu siddetin altindaki inisler toz cikarmaz.")]
        [Range(0f, 1f)]
        [SerializeField] private float landMinIntensity = 0.15f;

        [Header("Duvar Kaymasi")]
        [SerializeField] private float wallSlideDustInterval = 0.12f;

        private float _feetOffset;
        private bool _wasGrounded = true;
        private float _lastAirborneVelocityY;
        private float _dustCooldownTimer;
        private float _wallSlideDustTimer;
        private float _prevCyclePhase;
        private bool _hasPrevPhase;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (animator == null) animator = GetComponent<Animator>();

            _feetOffset = feetOffsetY;
            if (Mathf.Approximately(_feetOffset, 0f) && controller != null)
            {
                // Ayak seviyesi collider'in alt kenari; sprite sinirlarindan
                // hesaplamak yanlis olurdu cunku kare animasyonunda sprite
                // cercevesi karakterden cok daha buyuk (128x128 icinde 43x64).
                var capsule = controller.GetComponent<CapsuleCollider2D>();
                if (capsule != null) _feetOffset = capsule.offset.y - capsule.size.y * 0.5f;
            }
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnJumped += HandleJumped;
            controller.OnWallJumped += HandleWallJumped;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnJumped -= HandleJumped;
            controller.OnWallJumped -= HandleWallJumped;
        }

        private void Update()
        {
            if (controller == null) return;

            float dt = Time.deltaTime;
            bool grounded = controller.IsGrounded;

            DetectLanding(grounded);
            _wasGrounded = grounded;

            // Inis siddeti icin dususteki EN YUKSEK hizi sakla.
            //
            // Neden anlik hiz degil: carpisma cozuldugu kare ile IsGrounded'in
            // true olmasi arasinda BIR KARE fark var. O ara karede fizik hizi
            // zaten sifirlamis ama karakter hala "havada" sayiliyor; anlik hiz
            // yazilsaydi iyi deger sifirla ezilir ve inis tozu hic cikmazdi.
            if (!grounded)
                _lastAirborneVelocityY = Mathf.Min(_lastAirborneVelocityY, controller.Velocity.y);
            else
                _lastAirborneVelocityY = 0f;   // DetectLanding okuduktan SONRA sifirlanir

            if (controller.State == PlayerState.WallSliding) TickWallSlideDust(dt);
            else _wallSlideDustTimer = 0f;

            TickFootsteps(grounded, dt);
        }

        /// <summary>Havadan yere inis anini yakalar; toz siddeti dusus hizina gore olceklenir.</summary>
        private void DetectLanding(bool grounded)
        {
            if (!grounded || _wasGrounded) return;

            // Anlik hiz degil, DUSERKEN kaydedilen son hiz kullanilir.
            float impact = Mathf.InverseLerp(0f, landFullSpeed, Mathf.Abs(_lastAirborneVelocityY));
            if (impact > landMinIntensity)
                ParticleFX.Dust(FeetWorldPosition(), impact, dustColor);
        }

        private void TickWallSlideDust(float dt)
        {
            _wallSlideDustTimer -= dt;
            if (_wallSlideDustTimer > 0f) return;

            ParticleFX.Dust(transform.position, 0.3f, dustColor);
            _wallSlideDustTimer = wallSlideDustInterval;
        }

        /// <summary>
        /// Kosarken ayak basisi tozu. Animator varsa dongunun 0.0 ve 0.5
        /// noktalari (iki ayak) kullanilir; yoksa sabit araliga duser.
        /// </summary>
        private void TickFootsteps(bool grounded, float dt)
        {
            _dustCooldownTimer -= dt;

            float speedRatio = Mathf.Clamp01(Mathf.Abs(controller.Velocity.x) / Mathf.Max(1f, controller.MaxSpeed));
            bool fastEnough = grounded && speedRatio > runDustSpeedThreshold;

            if (!fastEnough)
            {
                _hasPrevPhase = false;
                return;
            }

            bool planted;
            if (animator != null)
            {
                float phase = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
                // Dongude 0.0 ve 0.5'i gecerken ayak yere basiyor kabul edilir.
                planted = _hasPrevPhase &&
                          (CrossedThreshold(_prevCyclePhase, phase, 0f) ||
                           CrossedThreshold(_prevCyclePhase, phase, 0.5f));
                _prevCyclePhase = phase;
                _hasPrevPhase = true;
            }
            else
            {
                planted = _dustCooldownTimer <= 0f;
            }

            if (!planted || _dustCooldownTimer > 0f) return;

            ParticleFX.Dust(FeetWorldPosition(), speedRatio * 0.6f, dustColor);
            _dustCooldownTimer = runDustCooldown;
        }

        /// <summary>Dongusel fazda (0-1, basa saran) bir esigin gecilip gecilmedigi.</summary>
        private static bool CrossedThreshold(float prev, float now, float threshold)
        {
            if (now >= prev) return prev < threshold && now >= threshold;   // normal ilerleme
            return prev < threshold || now >= threshold;                    // dongu basa sardi
        }

        private void HandleJumped()
        {
            ParticleFX.Dust(FeetWorldPosition(), 0.5f, dustColor);
        }

        private void HandleWallJumped()
        {
            ParticleFX.Impact(transform.position, dustColor,
                new Vector2(controller.WallDirection, 0.3f), 0.7f);
        }

        private Vector3 FeetWorldPosition()
        {
            Vector3 origin = controller != null ? controller.transform.position : transform.position;
            return origin + new Vector3(0f, _feetOffset, 0f);
        }
    }
}

using UnityEngine;
using SoulSplit.Player;
using SoulSplit.Core;

namespace SoulSplit.Combat
{
    /// <summary>Hangi saldiri turu tetiklendi. Animasyon ve hasar tarafi bunu okur.</summary>
    public enum AttackTier
    {
        Light,
        Heavy
    }

    /// <summary>
    /// Yakin dovus vurusu. Hem beden hem ruh ayni scripti kullanir;
    /// aralarindaki tek fark Inspector'daki damageType degeri.
    ///
    /// Hedef bulma Layer + ContactFilter2D ile yapilir, if-else zinciri yok.
    /// Filtre HER IKI dusman katmanini da kapsar; boylece yanlis formla
    /// vurulan dusman hasar almaz ama "sekme" geri bildirimi alabilir.
    ///
    /// AGIR SALDIRI: ayni hitscan mantigini paylasir, sadece daha yuksek
    /// hasar/daha genis kutu/daha uzun sure ile. heavyDamage kasitli olarak
    /// Health/PlayerHitReaction/EnemyBase'teki "siddet kademesi" esigiyle
    /// (varsayilan 2) eslesecek sekilde secildi — agir saldiri gercekten
    /// agir vurus tepkisini (buyuk knockback, uzun sersemleme) tetikler.
    /// </summary>
    public class MeleeAttack : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler input;
        [Tooltip("Vurusun yonunu belirleyen kaynak. Bos birakilirsa bu obje kullanilir.")]
        [SerializeField] private Transform originTransform;

        [Header("Hasar")]
        [Tooltip("Bu saldirinin boyutu. Beden = Physical, ruh = Spiritual.")]
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private int damage = 1;

        [Header("Vurus Alani — Hafif")]
        [Tooltip("Vurus kutusunun bakis yonundeki kaymasi.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.9f, 0f);
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.4f, 1.3f);
        [Tooltip("Her iki dusman katmanini da isaretle; yanlis form geri bildirimi buna bagli.")]
        [SerializeField] private LayerMask targetLayers;

        [Header("Tempo — Hafif")]
        [SerializeField] private float cooldown = 0.35f;
        [Tooltip("Tusa basistan gercek hasar anina kadar gecen sure. Gorsel hazirlik+vurus " +
                 "fazlarinin toplamiyla (PlayerProceduralAnimator/SoulController'daki " +
                 "attackAnticipationDuration+attackStrikeDuration) eslesecek sekilde ayarlanmali; " +
                 "aksi halde hasar, kilic daha havadayken iner.")]
        [SerializeField] private float impactDelay = 0.125f;

        [Header("Carpisma Efekti — Hafif")]
        [Tooltip("Isabet aninda cikan parcaciklarin rengi. Fiziksel = sicak, ruhani = soguk secilmeli.")]
        [SerializeField] private Color impactColor = new Color(1f, 0.6f, 0.25f);
        [Tooltip("Silahin yorungesini gosteren yay efekti. Isabetten BAGIMSIZ olarak her savurmada cikar.")]
        [SerializeField] private bool showSlashTrail = true;
        [Tooltip("Vurus izinin rengi. Genelde impactColor'dan daha acik/parlak secilir.")]
        [SerializeField] private Color slashColor = new Color(1f, 0.92f, 0.75f, 0.85f);

        [Header("Agir Saldiri — Hasar & Vurus Alani")]
        [Tooltip("Health/PlayerHitReaction/EnemyBase'teki 'agir vurus' esigiyle eslesmeli (varsayilan 2) — " +
                 "aksi halde agir saldiri gercek hayatta agir tepki tetiklemez.")]
        [SerializeField] private int heavyDamage = 2;
        [SerializeField] private Vector2 heavyHitboxSize = new Vector2(1.7f, 1.5f);

        [Header("Agir Saldiri — Tempo")]
        [Tooltip("Agir saldiridan sonra bir sonraki saldiriya kadar kilit suresi. Hafiften uzun — " +
                 "buyuk vurusun bedeli, daha uzun sure acik kalmaktir.")]
        [SerializeField] private float heavyCooldown = 0.75f;
        [Tooltip("Agir saldirinin hazirlik+vurus suresi toplami (PlayerProceduralAnimator/" +
                 "SoulController'daki heavyAttackAnticipationDuration+heavyAttackStrikeDuration ile eslesmeli).")]
        [SerializeField] private float heavyImpactDelay = 0.21f;

        [Header("Agir Saldiri — Carpisma Efekti")]
        [SerializeField] private Color heavyImpactColor = new Color(1f, 0.32f, 0.08f);
        [Tooltip("Agir savurmanin yay rengi. Hafiften daha sicak/yogun olmali.")]
        [SerializeField] private Color heavySlashColor = new Color(1f, 0.75f, 0.42f, 0.95f);

        [Header("Hata Ayiklama")]
        [SerializeField] private bool drawGizmos = true;

        private float _nextAttackTime;
        private readonly Collider2D[] _results = new Collider2D[8];
        private ContactFilter2D _filter;
        private int _facing = 1;

        /// <summary>Bekleyen bir vurusun hasar anina kalan suresi. Negatifse bekleyen vurus yok.</summary>
        private float _pendingImpactTimer = -1f;
        /// <summary>Bekleyen (veya en son tetiklenen) vurusun turu.</summary>
        private AttackTier _pendingTier = AttackTier.Light;

        /// <summary>Son vurusta en az bir dogru hedefe isabet edildi mi? Efektler icin.</summary>
        public bool LastAttackConnected { get; private set; }
        /// <summary>Son vurusta yanlis formda bir dusmana carpildi mi? Sekme efekti icin.</summary>
        public bool LastAttackDeflected { get; private set; }
        /// <summary>Vurus tetiklenince (isabetten bagimsiz) calisir; hangi tur oldugunu tasir. Animasyon bunu dinler.</summary>
        public event System.Action<AttackTier> OnAttackTriggered;

        /// <summary>Aktif formun bu saldiri bileseninden girdi kabul edip etmedigi.</summary>
        public bool AcceptsInput { get; private set; } = true;

        private void Awake()
        {
            if (originTransform == null) originTransform = transform;

            if (input == null) input = GetComponentInParent<PlayerInputHandler>();
            if (input == null)
            {
                Debug.LogError("[MeleeAttack] PlayerInputHandler bulunamadi; saldiri devre disi.", this);
                enabled = false;
                return;
            }

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true
            };
        }

        private void Update()
        {
            if (!AcceptsInput) return;

            UpdateFacing();

            // Bekleyen bir vurus varsa geri sayimini isle. Hasar, gorsel hazirlik+vurus
            // fazi bitip "impact frame"e ulasilana kadar UYGULANMAZ — boylece kilic
            // havadayken degil, gercekten indigi anda hasar veriyor.
            if (_pendingImpactTimer >= 0f)
            {
                _pendingImpactTimer -= Time.deltaTime;
                if (_pendingImpactTimer <= 0f)
                {
                    _pendingImpactTimer = -1f;
                    PerformAttack(_pendingTier);
                }
            }

            if (Time.time < _nextAttackTime) return;

            // Agir saldiri hafife oncelikli: ayni karede ikisi de basilirsa
            // oyuncunun ACIKCA daha guclu tusu sectigi kabul edilir.
            if (input.HeavyAttackPressedThisFrame)
            {
                BeginAttack(AttackTier.Heavy, heavyCooldown, heavyImpactDelay);
            }
            else if (input.AttackPressedThisFrame)
            {
                BeginAttack(AttackTier.Light, cooldown, impactDelay);
            }
        }

        /// <summary>
        /// Form degisiminde saldiri girdisini el degistirir. Kapatilan formun
        /// impact frame'i daha sonra calismasin diye bekleyen vurus da iptal edilir.
        /// </summary>
        public void SetInputEnabled(bool value)
        {
            AcceptsInput = value;
            if (!value) CancelPendingAttack();
        }

        public void CancelPendingAttack()
        {
            _pendingImpactTimer = -1f;
            LastAttackConnected = false;
            LastAttackDeflected = false;
        }

        private void OnDisable() => CancelPendingAttack();

        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            heavyDamage = Mathf.Max(1, heavyDamage);
            cooldown = Mathf.Max(0f, cooldown);
            heavyCooldown = Mathf.Max(0f, heavyCooldown);
            impactDelay = Mathf.Clamp(impactDelay, 0f, cooldown);
            heavyImpactDelay = Mathf.Clamp(heavyImpactDelay, 0f, heavyCooldown);
            hitboxSize = new Vector2(Mathf.Max(0.01f, hitboxSize.x), Mathf.Max(0.01f, hitboxSize.y));
            heavyHitboxSize = new Vector2(Mathf.Max(0.01f, heavyHitboxSize.x), Mathf.Max(0.01f, heavyHitboxSize.y));
        }

        private void BeginAttack(AttackTier tier, float attackCooldown, float attackImpactDelay)
        {
            _nextAttackTime = Time.time + attackCooldown;
            _pendingTier = tier;
            _pendingImpactTimer = attackImpactDelay;
            // Animasyon (hazirlik pozu) hemen, tusa basildigi anda baslamali —
            // bu yuzden OnAttackTriggered burada, gecikmeden once tetiklenir.
            OnAttackTriggered?.Invoke(tier);
        }

        /// <summary>Bakis yonunu aktif kontrolcuden okur.</summary>
        private void UpdateFacing()
        {
            if (originTransform.TryGetComponent(out PlayerController body))
            {
                _facing = body.FacingDirection;
            }
            else if (originTransform.TryGetComponent(out SoulController soul))
            {
                _facing = soul.FacingDirection;
            }
        }

        private void PerformAttack(AttackTier tier)
        {
            LastAttackConnected = false;
            LastAttackDeflected = false;

            int appliedDamage = tier == AttackTier.Heavy ? heavyDamage : damage;
            Vector2 appliedHitboxSize = tier == AttackTier.Heavy ? heavyHitboxSize : hitboxSize;
            Color appliedImpactColor = tier == AttackTier.Heavy ? heavyImpactColor : impactColor;

            // Vurus izi ISABETTEN BAGIMSIZ: havaya savurdugunda da gorunmeli,
            // yoksa oyuncu saldirinin ciktigini goremez.
            if (showSlashTrail)
            {
                Vector3 arcCenter = originTransform.position;
                if (tier == AttackTier.Heavy) SlashFX.Heavy(arcCenter, _facing, heavySlashColor);
                else SlashFX.Light(arcCenter, _facing, slashColor);
            }

            Vector2 center = (Vector2)originTransform.position
                             + new Vector2(hitboxOffset.x * _facing, hitboxOffset.y);

            int count = Physics2D.OverlapBox(center, appliedHitboxSize, 0f, _filter, _results);
            for (int i = 0; i < count; i++)
            {
                Health target = Health.FindOn(_results[i]);
                if (target == null) continue;

                Vector2 hitDirection = new Vector2(_facing, 0.15f).normalized;
                HitResult result = target.TryTakeDamage(appliedDamage, damageType, hitDirection);
                if (result == HitResult.Damaged || result == HitResult.Killed)
                {
                    LastAttackConnected = true;
                    Vector2 hitPoint = _results[i].bounds.center;
                    ParticleFX.Impact(hitPoint, appliedImpactColor, new Vector2(_facing, 0.2f),
                        tier == AttackTier.Heavy ? 1.6f : 1f);
                }
                else if (result == HitResult.Deflected)
                {
                    LastAttackDeflected = true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Transform origin = originTransform != null ? originTransform : transform;
            Vector2 center = (Vector2)origin.position
                             + new Vector2(hitboxOffset.x * _facing, hitboxOffset.y);

            Color lightColor = damageType == DamageType.Physical
                ? new Color(1f, 0.55f, 0.2f, 0.8f)
                : new Color(0.4f, 0.85f, 1f, 0.8f);

            Gizmos.color = lightColor;
            Gizmos.DrawWireCube(center, hitboxSize);

            // Agir kutu genelde hafiften buyuk oldugu icin saydam farkli renkle ustune cizilir.
            Gizmos.color = new Color(1f, 0.2f, 0.05f, 0.5f);
            Gizmos.DrawWireCube(center, heavyHitboxSize);
        }
    }
}

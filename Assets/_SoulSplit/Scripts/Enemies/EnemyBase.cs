using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulSplit.Combat;
using SoulSplit.Core;

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
        [Tooltip("TASMA SINIRI: kovalarken baslangic noktasindan en fazla bu kadar uzaklasir; " +
                 "asarsa hedefi birakip postuna doner. 0 = sinirsiz (eski davranis).\n\n" +
                 "Neden gerekli: loseTargetRadius sadece hedef UZAKLASINCA devreye giriyor. " +
                 "Oyuncu yakinda ama ULASILAMAZ bir yere saklanirsa (ornegin dusmanin " +
                 "sigmadigi egilme tuneli) dusman sonsuza kadar agizda kamp ediyordu.")]
        [SerializeField] protected float maxChaseDistanceFromPost = 0f;

        [Header("Saldiri")]
        [SerializeField] protected float attackRange = 1.4f;
        [SerializeField] protected int attackDamage = 1;
        [Tooltip("Vurustan once bekleme. Oyuncuya kacma firsati verir; 0 yapma.")]
        [SerializeField] protected float attackWindup = 0.35f;
        [SerializeField] protected float attackCooldown = 1.2f;
        [Tooltip("Hasardan tam bu kadar once dusmanin ustunde ! gorunur.")]
        [SerializeField, Min(0.1f)] protected float attackWarningLeadTime = 0.5f;
        [SerializeField] protected Vector2 attackHitboxSize = new Vector2(1.6f, 1.4f);
        [Tooltip("Bu dusmanin vurabilecegi katmanlar (bedene mi ruha mi vuruyor).")]
        [SerializeField] protected LayerMask attackTargetLayers;
        [Tooltip("Bu dusman hangi boyuttan hasar verir.")]
        [SerializeField] protected DamageType dealtDamageType = DamageType.Physical;

        [Header("Agir Saldiri (dusman)")]
        [Tooltip("Acikken dusman arada bir agir saldiri yapar. Kapaliyken davranis eskisi gibi kalir.")]
        [SerializeField] protected bool enableHeavyAttack = false;
        [Tooltip("Her saldiri denemesinde agir olma olasiligi.")]
        [Range(0f, 1f)]
        [SerializeField] protected float heavyAttackChance = 0.3f;
        [Tooltip("Agir saldirinin hasari. Oyuncunun PlayerHitReaction/HitFlash tarafindaki " +
                 "heavyDamageThreshold ile (varsayilan 2) ESLESMELI — yoksa agir vurus tepkisi " +
                 "(buyuk knockback, guclu hit-stop, siddetli kamera sarsintisi) tetiklenmez.")]
        [SerializeField] protected int heavyAttackDamage = 2;
        [Tooltip("Agir saldirinin hazirlik suresi. Hafiften belirgin UZUN olmali — " +
                 "oyuncunun kacabilmesi icin adil telgraf budur.")]
        [SerializeField] protected float heavyAttackWindup = 0.7f;
        [Tooltip("Agir saldiridan sonraki bekleme. Hafiften uzun; buyuk vurusun bedeli.")]
        [SerializeField] protected float heavyAttackCooldown = 2.4f;
        [SerializeField] protected Vector2 heavyAttackHitboxSize = new Vector2(2.2f, 1.8f);

        [Header("Hasar Tepkisi")]
        [Tooltip("Hasar alinca kac saniye kontrolu kaybeder.")]
        [SerializeField] protected float hurtDuration = 0.25f;
        [SerializeField] protected float knockbackForce = 4f;

        [Header("Hasar Tepkisi — Siddet Kademesi (Agir Vurus)")]
        [Tooltip("Bu esigi veya ustunu gecen hasar 'agir vurus' sayilir; knockback ve sersemleme suresi buyur.")]
        [SerializeField] protected int heavyDamageThreshold = 2;
        [Tooltip("Agir vurusta knockbackForce bu katla carpilir.")]
        [SerializeField] protected float heavyKnockbackMultiplier = 1.6f;
        [Tooltip("Agir vurusta hurtDuration bu katla carpilir; sersemleme daha uzun surer.")]
        [SerializeField] protected float heavyHurtDurationMultiplier = 1.5f;

        [Header("Vurus Izi")]
        [Tooltip("Saldiri savurmasinda silah/pence yorungesini gosteren yay. " +
                 "Dusman saldirilari zaten attackWindup ile telegraf edildigi icin " +
                 "bu iz OYUNCUNUNKINDEN DAHA SONUK tutulmali; amac geri bildirim, gorsel gurultu degil.")]
        [SerializeField] protected bool showSlashTrail = true;
        [SerializeField] protected Color slashColor = new Color(1f, 0.88f, 0.7f, 0.4f);
        [Tooltip("Yay yaricapi. 0 birakilirsa attackRange'ten turetilir.")]
        [SerializeField] protected float slashRadius = 0f;
        [SerializeField] protected float slashThickness = 0.22f;
        [Tooltip("Tarama acisi (derece). Negatif = saat yonu (yukaridan asagi savurma).")]
        [SerializeField] protected float slashSweep = -115f;

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

        private Vector2 _spawnPosition;
        private AttackTier _currentTier = AttackTier.Light;
        /// <summary>Tasma sinirini asip postuna donuyor mu? Sinirda titremeyi onleyen histerezis bayragi.</summary>
        private bool _returningToPost;
        private float _stateTimer;
        private float _nextAttackTime;
        private bool _attackLanded;
        private float _activeAttackWindup;
        private GameObject _attackWarning;
        private TextMesh _attackWarningText;
        private readonly Collider2D[] _hitResults = new Collider2D[8];
        private readonly HashSet<Health> _damagedThisAttack = new HashSet<Health>();
        private ContactFilter2D _attackFilter;

        public EnemyState State => _state;
        public int Facing => _facing;
        /// <summary>Anlik hiz. Yurume animasyonu bunu okuyor.</summary>
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        /// <summary>Vurus oncesi hazirlik suresi. Saldiri animasyonu buna gore zamanlanir.</summary>
        public float AttackWindupDuration => _activeAttackWindup > 0f ? _activeAttackWindup : attackWindup;
        public bool IsAttackWarningVisible => _attackWarning != null && _attackWarning.activeSelf;

        /// <summary>
        /// Saldiri baslarken tetiklenir; hangi kademe oldugunu tasir.
        /// Animasyon scriptleri bunu dinler. (Oyuncudaki MeleeAttack.OnAttackTriggered
        /// ile ayni desen — ayni AttackTier enum'u paylasiliyor.)
        /// </summary>
        public event System.Action<AttackTier> OnAttackTriggered;

        /// <summary>Su an hazirlanan/uygulanan saldirinin kademesi.</summary>
        public AttackTier CurrentAttackTier => _currentTier;

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _rb.freezeRotation = true;
            // Tasma sinirinin olculdugu nokta. Alt siniflarin kendi devriye/dolanma
            // merkezleri var ama onlar protected degil; bu yuzden EnemyBase kendi
            // dogum noktasini ayrica tutuyor.
            _spawnPosition = transform.position;

            RefreshAttackFilter();
            BuildAttackWarning();
            if (GetComponent<EnemyAudioFeedback>() == null) gameObject.AddComponent<EnemyAudioFeedback>();
        }

        protected virtual void OnEnable()
        {
            _health.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            _health.OnDeath -= HandleDeath;
            SetAttackWarning(false);
        }

        protected virtual void Update()
        {
            if (_state == EnemyState.Dead) return;

            if (IsAttackWarningVisible)
            {
                bool imminent = _state == EnemyState.Attack && !_attackLanded && _stateTimer <= 0.25f;
                if (_attackWarningText != null)
                    _attackWarningText.color = imminent
                        ? new Color(1f, 0.08f, 0.04f, 1f)
                        : new Color(1f, 0.78f, 0.08f, 1f);

                float scale = imminent
                    ? 1.55f + Mathf.Sin(Time.unscaledTime * 28f) * 0.22f
                    : 1.35f + Mathf.Sin(Time.unscaledTime * 7f) * 0.04f;
                _attackWarning.transform.localScale = Vector3.one * scale;
            }

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

            // TASMA: postundan cok uzaklastiysa hedefi birak ve geri don.
            // Bu kontrol digerlerinden ONCE gelmeli — aksi halde tasmis dusman
            // menzile giren oyuncuya saldirmaya devam ederdi.
            //
            // HISTEREZIS sart: tek esik kullanilinca dusman sinirda takilip
            // Chase<->Patrol arasinda saniyede birkac kez titriyordu (sinir disi
            // -> Patrol -> Patrol onu iceri itiyor -> yeniden Chase -> ...).
            // Bu yuzden birakma esigi ile yeniden hedeflenme esigi AYRI.
            //
            // Yeniden hedeflenme esigi DAR tutuldu (tasmanin 1/4'u): daha genis
            // olunca dusman posta yarim yolda donup sakli oyuncuyu tekrar algiliyor
            // ve 10-13 arasi sonsuz gidip gelme dongusune giriyordu. Dar esik,
            // dusmanin sakli oyuncunun algi yaricapindan CIKACAK kadar geri
            // donmesini garantiler.
            if (maxChaseDistanceFromPost > 0.01f)
            {
                float fromPost = Vector2.Distance(transform.position, _spawnPosition);
                if (!_returningToPost && fromPost >= maxChaseDistanceFromPost) _returningToPost = true;
                else if (_returningToPost && fromPost <= maxChaseDistanceFromPost * 0.25f) _returningToPost = false;

                if (_returningToPost)
                {
                    _state = EnemyState.Patrol;
                    return;
                }
            }

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
            // Kademe saldirinin BASINDA seciliyor; hazirlik suresi ve hasar
            // buna gore degisiyor. Boylece uzun hazirlik = agir vurus geliyor
            // demek oluyor ve oyuncu telgrafi okuyabiliyor.
            _currentTier = (enableHeavyAttack && Random.value < heavyAttackChance)
                ? AttackTier.Heavy
                : AttackTier.Light;
            bool heavy = _currentTier == AttackTier.Heavy;

            _state = EnemyState.Attack;
            _activeAttackWindup = Mathf.Max(attackWarningLeadTime,
                heavy ? heavyAttackWindup : attackWindup);
            _stateTimer = _activeAttackWindup;
            _attackLanded = false;
            _nextAttackTime = Time.time + (heavy ? heavyAttackCooldown : attackCooldown);

            if (_target != null)
            {
                _facing = _target.position.x >= transform.position.x ? 1 : -1;
            }
            OnAttackStarted();
            OnAttackTriggered?.Invoke(_currentTier);
            // Dusman saldiriya karar verir vermez buyuk sari unlem gorunur.
            // Son 0.25 saniyede Update'te kirmizi ve titreşimli hale gelir.
            SetAttackWarning(true);
        }

        private void TickAttack()
        {
            // Windup bittigi anda tek kare hitbox ac.
            if (!_attackLanded && _stateTimer <= 0f)
            {
                _attackLanded = true;
                SetAttackWarning(false);
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
            // Iz ISABETTEN BAGIMSIZ cikar (oyuncudaki MeleeAttack ile ayni kural):
            // oyuncu kacmayi basardiginda da savurmanin nereden gectigini gormeli.
            bool heavy = _currentTier == AttackTier.Heavy;

            if (showSlashTrail)
            {
                float radius = slashRadius > 0.01f ? slashRadius : attackRange * 0.95f;
                // Agir vurus gorsel olarak da agir okunmali: daha genis yay,
                // daha kalin serit, daha parlak/uzun sonme.
                var col = slashColor;
                if (heavy) col.a = Mathf.Min(1f, col.a * 1.6f);
                SlashFX.Arc(
                    transform.position + new Vector3(_facing * 0.25f, 0.1f, 0f),
                    _facing, col,
                    radius: heavy ? radius * 1.35f : radius,
                    thickness: heavy ? slashThickness * 1.5f : slashThickness,
                    startAngleDeg: heavy ? 115f : 95f,
                    sweepDeg: heavy ? slashSweep * 1.3f : slashSweep,
                    duration: heavy ? 0.22f : 0.15f);
            }

            Vector2 hitboxSize = heavy ? heavyAttackHitboxSize : attackHitboxSize;
            int appliedDamage = heavy ? heavyAttackDamage : attackDamage;

            Vector2 center = (Vector2)transform.position + new Vector2(attackRange * 0.6f * _facing, 0f);
            int count = Physics2D.OverlapBox(center, hitboxSize, 0f, _attackFilter, _hitResults);

            Vector2 hitDirection = new Vector2(_facing, 0.15f).normalized;
            _damagedThisAttack.Clear();
            for (int i = 0; i < count; i++)
            {
                Health victim = Health.FindOn(_hitResults[i]);
                if (victim == null || !_damagedThisAttack.Add(victim)) continue;

                victim.TryTakeDamage(appliedDamage, ResolveDamageTypeFor(victim), hitDirection);
            }
        }

        /// <summary>
        /// Alt dusman tiplerinin calisma aninda ek hedef katmani acabilmesi icin.
        /// Katman maskesi degisince ContactFilter da ayni karede yenilenir.
        /// </summary>
        protected void IncludeAttackTargetLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;

            attackTargetLayers |= 1 << layer;
            RefreshAttackFilter();
        }

        /// <summary>
        /// Varsayilan olarak prefabda ayarlanan hasar boyutunu kullanir.
        /// Ozel dusmanlar hedef tipine gore bunu degistirebilir.
        /// </summary>
        protected virtual DamageType ResolveDamageTypeFor(Health victim) => dealtDamageType;

        private void RefreshAttackFilter()
        {
            _attackFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = attackTargetLayers,
                useTriggers = true
            };
        }

        private void BuildAttackWarning()
        {
            _attackWarning = new GameObject("AttackWarning_Exclamation");
            _attackWarning.transform.SetParent(transform, false);

            float top = 1.35f;
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sprite in sprites)
            {
                float localTop = transform.InverseTransformPoint(sprite.bounds.max).y;
                top = Mathf.Max(top, localTop + 0.35f);
            }
            _attackWarning.transform.localPosition = new Vector3(0f, top + 0.18f, 0f);

            _attackWarningText = _attackWarning.AddComponent<TextMesh>();
            _attackWarningText.text = "!";
            _attackWarningText.anchor = TextAnchor.MiddleCenter;
            _attackWarningText.alignment = TextAlignment.Center;
            _attackWarningText.fontSize = 150;
            _attackWarningText.characterSize = 0.085f;
            _attackWarningText.fontStyle = FontStyle.Bold;
            _attackWarningText.color = new Color(1f, 0.78f, 0.08f, 1f);

            MeshRenderer renderer = _attackWarningText.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 90;
            SetAttackWarning(false);
        }

        private void SetAttackWarning(bool visible)
        {
            if (_attackWarning == null) return;
            _attackWarning.SetActive(visible);
            if (visible) _attackWarning.transform.localScale = Vector3.one * 1.35f;
        }

        private void HandleDeath()
        {
            _state = EnemyState.Dead;
            SetAttackWarning(false);
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

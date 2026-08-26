using UnityEngine;
using SoulSplit.Player;

namespace SoulSplit.Enemies
{
    /// <summary>
    /// Hayalet dusman. Duvarlardan gecer (Physics2D katman matrisi),
    /// yercekimsizdir; hem fiziksel bedeni hem de ayrilmis ruhu hedef alabilir.
    /// Ruh disaridayken iki formdan kendisine daha yakin olani secer. Boylece
    /// hayaletler bedenin yaninda guvenle beklenebilen pasif engeller olmaz.
    /// </summary>
    public class GhostEnemy : EnemyBase
    {
        [Header("Suzulme")]
        [Tooltip("Hedefe dogru hizlanma. Dusuk = daha tembel, kacilabilir bir takip.")]
        [SerializeField] private float acceleration = 12f;
        [Tooltip("Hedef yokken baslangic noktasi cevresinde dolanma yaricapi.")]
        [SerializeField] private float wanderRadius = 1.6f;
        [SerializeField] private float wanderSpeed = 0.7f;

        [Header("Uyku Hali")]
        [Tooltip("Oyuncu bedendeyken sprite'in saydamligi. Uyudugunu belli eder.")]
        [Range(0f, 1f)]
        [SerializeField] private float dormantAlpha = 0.35f;
        [SerializeField] private float alphaFadeSpeed = 4f;

        [Header("Dusmanlar Arasi Hasar")]
        [Tooltip("Ruhani saldiri alanina giren fiziksel dusmanlarin da hasar almasini saglar.")]
        [SerializeField] private bool damagePhysicalEnemies = true;

        [Header("Referanslar")]
        [SerializeField] private SoulSwitchManager switchManager;
        [Tooltip("Oyuncunun fiziksel bedeni.")]
        [SerializeField] private Transform bodyTransform;
        [Tooltip("Oyuncunun ruh objesi.")]
        [SerializeField] private Transform soulTransform;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector2 _wanderOrigin;
        private float _wanderPhase;

        protected override void Awake()
        {
            base.Awake();
            ResolvePlayerReferences();
            _rb.gravityScale = 0f;
            _wanderOrigin = transform.position;
            _wanderPhase = Random.value * Mathf.PI * 2f;

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (damagePhysicalEnemies) IncludeAttackTargetLayer("PhysicalEnemy");
            IncludeAttackTargetLayer("Body");
        }

        /// <summary>
        /// Oyuncunun ruhuna karsi saldiri ruhani kalir. Fiziksel dusman zirhi ise
        /// kendi boyutundaki hasari kabul ettigi icin, sadece bu hedef tipinde
        /// isabet fiziksel hasar olarak cozulur.
        /// </summary>
        protected override SoulSplit.Combat.DamageType ResolveDamageTypeFor(SoulSplit.Combat.Health victim)
        {
            bool physicalVictim = victim != null &&
                (victim.GetComponentInParent<PhysicalEnemy>() != null ||
                 victim.GetComponentInParent<PlayerController>() != null);
            return physicalVictim
                ? SoulSplit.Combat.DamageType.Physical
                : base.ResolveDamageTypeFor(victim);
        }

        private void ResolvePlayerReferences()
        {
            if (switchManager == null)
                switchManager = FindAnyObjectByType<SoulSwitchManager>();

            if (soulTransform == null && switchManager != null)
                soulTransform = switchManager.SoulTransform;

            if (bodyTransform == null && switchManager != null)
                bodyTransform = switchManager.BodyTransform;
        }

        /// <summary>Bedeni her zaman, ruh disaridaysa iki formdan en yakinini hedefler.</summary>
        protected override Transform FindTarget()
        {
            bool soulIsOut = switchManager != null && switchManager.IsSoulActive;
            if (!soulIsOut || soulTransform == null) return bodyTransform;
            if (bodyTransform == null) return soulTransform;

            float bodyDistance = ((Vector2)(bodyTransform.position - transform.position)).sqrMagnitude;
            float soulDistance = ((Vector2)(soulTransform.position - transform.position)).sqrMagnitude;
            return soulDistance < bodyDistance ? soulTransform : bodyTransform;
        }

        protected override void Update()
        {
            base.Update();
            UpdateDormantLook();
        }

        /// <summary>Uyanikken tam opak, uyurken saydam. Oyuncu tehdidi okuyabilsin.</summary>
        private void UpdateDormantLook()
        {
            if (spriteRenderer == null) return;

            // Artik beden de gecerli hedef oldugu icin hayalet her iki formda da aktiftir.
            float targetAlpha = 1f;

            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, 1f - Mathf.Exp(-alphaFadeSpeed * Time.deltaTime));
            spriteRenderer.color = c;
        }

        protected override void MoveTowards(Vector3 targetPosition, float speed)
        {
            Vector2 direction = ((Vector2)(targetPosition - transform.position)).normalized;
            _facing = direction.x >= 0f ? 1 : -1;

            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity, direction * speed, acceleration * Time.fixedDeltaTime);
        }

        protected override void Patrol()
        {
            // Hedefsizken baslangic noktasi cevresinde tembel tembel dolanir.
            _wanderPhase += Time.fixedDeltaTime * wanderSpeed;
            Vector2 desired = _wanderOrigin + new Vector2(
                Mathf.Cos(_wanderPhase) * wanderRadius,
                Mathf.Sin(_wanderPhase * 1.7f) * wanderRadius * 0.6f);

            Vector2 toDesired = desired - (Vector2)transform.position;
            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity, toDesired * 2f, acceleration * Time.fixedDeltaTime);
        }
    }
}

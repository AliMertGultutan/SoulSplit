using UnityEngine;
using SoulSplit.Player;

namespace SoulSplit.Enemies
{
    /// <summary>
    /// Hayalet dusman. Duvarlardan gecer (Physics2D katman matrisi),
    /// yercekimsizdir ve SADECE ruh formunu hedef alir.
    ///
    /// Oyuncu bedendeyken bu dusman uyuklar; ruh cikinca uyanir.
    /// Yani ruh formuna gecmek iki cepheyi birden acar: bedeni fiziksel
    /// dusmanlara, ruhu da hayaletlere. Bu, form degistirmenin bedelidir.
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

        [Header("Referanslar")]
        [SerializeField] private SoulSwitchManager switchManager;
        [Tooltip("Oyuncunun ruh objesi.")]
        [SerializeField] private Transform soulTransform;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector2 _wanderOrigin;
        private float _wanderPhase;

        protected override void Awake()
        {
            base.Awake();
            _rb.gravityScale = 0f;
            _wanderOrigin = transform.position;
            _wanderPhase = Random.value * Mathf.PI * 2f;

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>Ruh disarda degilse bu dusmanin hedefi yoktur.</summary>
        protected override Transform FindTarget()
        {
            bool soulIsOut = switchManager != null && switchManager.IsSoulActive;
            return soulIsOut ? soulTransform : null;
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

            bool awake = switchManager != null && switchManager.IsSoulActive;
            float targetAlpha = awake ? 1f : dormantAlpha;

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

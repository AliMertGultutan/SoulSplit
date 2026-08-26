using UnityEngine;
using SoulSplit.Player;

namespace SoulSplit.Enemies
{
    /// <summary>
    /// Fiziksel canavar. Zeminde yurur, sadece BEDENI hedef alir.
    ///
    /// USP'nin disleri burada: oyuncu ruh formuna gecince beden savunmasiz
    /// kalir ve bu dusmanin algi yaricapi buyur. Yani ruha gecmek, fiziksel
    /// dusmanlari bedene dogru cekiyor. Ceza otomatik degil, oyuncunun
    /// verdigi kararin dogal sonucu.
    /// </summary>
    public class PhysicalEnemy : EnemyBase
    {
        [Header("Devriye")]
        [Tooltip("Baslangic noktasindan saga sola kac birim gider.")]
        [SerializeField] private float patrolRange = 4f;
        [Tooltip("Onunde ucurum veya duvar var mi diye bakilan mesafe.")]
        [SerializeField] private float edgeCheckDistance = 0.8f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Savunmasiz Beden")]
        [Tooltip("Oyuncu ruh formundayken algi yaricapinin kac katina cikacagi.")]
        [SerializeField] private float abandonedBodySenseMultiplier = 1.8f;

        [Header("Referanslar")]
        [SerializeField] private SoulSwitchManager switchManager;
        [Tooltip("Oyuncunun bedeni.")]
        [SerializeField] private Transform bodyTransform;

        private Vector2 _patrolOrigin;

        protected override void Awake()
        {
            base.Awake();
            ResolvePlayerReferences();
            _patrolOrigin = transform.position;
        }

        private void ResolvePlayerReferences()
        {
            if (switchManager == null)
                switchManager = FindAnyObjectByType<SoulSwitchManager>();

            if (bodyTransform == null && switchManager != null)
                bodyTransform = switchManager.BodyTransform;
        }

        /// <summary>Bu dusman icin hedef her zaman bedendir.</summary>
        protected override Transform FindTarget() => bodyTransform;

        /// <summary>Beden sahipsiz kalinca daha genis alandan sezilir.</summary>
        protected override float GetDetectionRadius()
        {
            bool bodyAbandoned = switchManager != null && switchManager.IsSoulActive;
            return bodyAbandoned ? detectionRadius * abandonedBodySenseMultiplier : detectionRadius;
        }

        protected override void MoveTowards(Vector3 targetPosition, float speed)
        {
            float direction = Mathf.Sign(targetPosition.x - transform.position.x);
            _facing = direction >= 0f ? 1 : -1;

            // Ucurumdan yuruyup dusmesin.
            if (!HasGroundAhead()) { Brake(); return; }

            _rb.linearVelocity = new Vector2(direction * speed, _rb.linearVelocity.y);
        }

        protected override void Patrol()
        {
            float offset = transform.position.x - _patrolOrigin.x;

            if (offset > patrolRange) _facing = -1;
            else if (offset < -patrolRange) _facing = 1;
            else if (!HasGroundAhead()) _facing = -_facing;

            _rb.linearVelocity = new Vector2(_facing * patrolSpeed, _rb.linearVelocity.y);
        }

        /// <summary>Onunde basacak zemin ve gecebilecegi bosluk var mi?</summary>
        private bool HasGroundAhead()
        {
            Vector2 ahead = (Vector2)transform.position + new Vector2(_facing * edgeCheckDistance, 0f);

            bool floorAhead = Physics2D.Raycast(ahead, Vector2.down, 1.6f, groundLayer);
            bool wallAhead = Physics2D.Raycast(transform.position, Vector2.right * _facing, edgeCheckDistance, groundLayer);

            return floorAhead && !wallAhead;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!drawGizmos) return;

            Vector3 origin = Application.isPlaying ? (Vector3)_patrolOrigin : transform.position;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.7f);
            Gizmos.DrawLine(origin + Vector3.left * patrolRange, origin + Vector3.right * patrolRange);
        }
    }
}

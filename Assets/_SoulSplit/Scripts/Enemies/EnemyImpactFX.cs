using UnityEngine;
using SoulSplit.Combat;
using SoulSplit.Core;

namespace SoulSplit.Enemies
{
    /// <summary>
    /// Dusmanlarin hasar/olum parcaciklari — animasyon sisteminden bagimsiz.
    ///
    /// Bkz. PlayerFootstepFX baslik yorumu: bu efektler eskiden
    /// EnemyProceduralAnimator icinde yasiyordu ve sprite-sheet gecisinde o
    /// bilesen kapatilinca sessizce kayboldular. Buraya cikarildilar.
    ///
    /// Yer/hava ayrimi onemli: zeminde olen bir tas muhafizin enkazi dusmeli
    /// (yercekimi var, dar sacilim), havada dagilan bir hayaletin parcalari
    /// asili kalip yayilmalii (yercekimi ~0, genis sacilim).
    /// </summary>
    public class EnemyImpactFX : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private EnemyBase enemy;
        [SerializeField] private Health health;

        [Header("Hareket Tipi")]
        [Tooltip("Bu dusman zeminde mi yuruyor, havada mi suzuluyor? Olum parcaciklarinin fizigini belirler.")]
        [SerializeField] private EnemyLocomotion locomotion = EnemyLocomotion.Grounded;

        [Header("Renkler")]
        [Tooltip("Olum parcaciklari. Fiziksel dusman icin tas grisi, hayalet icin soguk teal onerilir.")]
        [SerializeField] private Color deathParticleColor = new Color(0.6f, 0.6f, 0.65f);
        [Tooltip("Hasar aninda cikan kivilcim.")]
        [SerializeField] private Color hurtSparkColor = new Color(0.95f, 0.9f, 0.7f);

        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInParent<EnemyBase>();
            if (health == null) health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.OnHit += HandleHit;
            health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnHit -= HandleHit;
            health.OnDeath -= HandleDeath;
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 hitDirection, int amount)
        {
            // Sadece gercekten hasar gectiginde; sekme/dokunulmazlik kivilcim uretmesin.
            if (result != HitResult.Damaged) return;

            int facing = enemy != null ? enemy.Facing : 1;
            ParticleFX.Impact(transform.position, hurtSparkColor, new Vector2(-facing, 0.3f), 0.6f);
        }

        private void HandleDeath()
        {
            bool grounded = locomotion == EnemyLocomotion.Grounded;

            ParticleFX.Burst(transform.position, deathParticleColor,
                count: grounded ? 12 : 9,
                speed: grounded ? 3.5f : 1.8f,
                size: grounded ? 0.1f : 0.14f,
                lifetime: grounded ? 0.5f : 0.8f,
                spreadAngle: grounded ? 70f : 100f,
                direction: Vector2.up,
                gravityScale: grounded ? 1f : 0.05f);
        }
    }
}

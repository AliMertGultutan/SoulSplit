using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Player
{
    /// <summary>
    /// Bedenin hasar aninda "hissedilmesi". Health.OnHit'i dinler ve
    /// hasar gectiginde knockback + hit-squash uygular.
    ///
    /// Flash/hit-stop/kamera sarsintisi zaten HitFlash'in isi (dusmanlarda
    /// oldugu gibi); bu script sadece bedene ozgu HAREKET tepkisini ekler
    /// (PlayerController'da HitFlash'in erisemeyecegi bir alan).
    /// </summary>
    public class PlayerHitReaction : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Health health;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerProceduralAnimator animator;

        [Header("Knockback")]
        [Tooltip("Vurus yonunde uygulanan yatay itilme hizi.")]
        [SerializeField] private float knockbackHorizontalForce = 6f;
        [Tooltip("Knockback'e eklenen sabit yukari bileşen; oyuncu tamamen yere yapismasin.")]
        [SerializeField] private float knockbackUpwardForce = 3f;
        [Tooltip("Knockback sirasinda normal hareket kontrolunun kilitli kalma suresi.")]
        [SerializeField] private float knockbackLockDuration = 0.16f;

        [Header("Hit Squash")]
        [Tooltip("Hasar aninda govdenin ezilme miktari (PlayerProceduralAnimator'daki inis ezilmesiyle ayni olcek).")]
        [SerializeField] private float hitSquashAmount = 0.32f;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (animator == null) animator = GetComponentInChildren<PlayerProceduralAnimator>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (health != null) health.OnHit -= HandleHit;
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 hitDirection)
        {
            // Olum kendi rutinini (PlayerDeathHandler) calistiriyor; ustune
            // knockback binmesin. Sekme/dokunulmazlikta gorsel tepkiye gerek yok.
            if (result != HitResult.Damaged) return;

            if (controller != null)
            {
                Vector2 direction = hitDirection.sqrMagnitude > 0.0001f
                    ? hitDirection
                    : new Vector2(-controller.FacingDirection, 0f);

                Vector2 knockbackVelocity = new Vector2(
                    Mathf.Sign(direction.x) * knockbackHorizontalForce,
                    knockbackUpwardForce);

                controller.ApplyKnockback(knockbackVelocity, knockbackLockDuration);
            }

            if (animator != null) animator.TriggerHitSquash(hitSquashAmount);
        }
    }
}

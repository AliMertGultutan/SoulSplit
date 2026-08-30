using SoulSplit.Combat;
using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Oyuncu can kaybettikten sonra, yeni hasar almadan yaptigi ilk oldurmede
    /// maksimum caninin belirlenen oranini geri kazanir.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class NoHitKillRecovery : MonoBehaviour
    {
        [Range(0.01f, 1f)]
        [SerializeField] private float recoveryRatio = 0.20f;

        private Health _health;
        private MeleeAttack[] _attacks;
        private bool _recoveryArmed;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _attacks = GetComponentsInChildren<MeleeAttack>(true);
        }

        private void OnEnable()
        {
            _health.OnHit += HandlePlayerHit;
            foreach (MeleeAttack attack in _attacks)
                attack.OnHitConfirmed += HandleHitConfirmed;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnHit -= HandlePlayerHit;
            if (_attacks == null) return;
            foreach (MeleeAttack attack in _attacks)
                if (attack != null) attack.OnHitConfirmed -= HandleHitConfirmed;
        }

        private void HandlePlayerHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            // Her yeni hasar, "bu hasardan sonra darbe almadan oldur" penceresini yeniler.
            if (result == HitResult.Damaged) _recoveryArmed = true;
            else if (result == HitResult.Killed) _recoveryArmed = false;
        }

        private void HandleHitConfirmed(AttackTier tier, HitResult result)
        {
            if (!_recoveryArmed || result != HitResult.Killed || _health.IsDead) return;

            if (_health.HealPercent(recoveryRatio) > 0)
                _recoveryArmed = false;
        }

        private void OnValidate()
        {
            recoveryRatio = Mathf.Clamp01(recoveryRatio);
        }
    }
}

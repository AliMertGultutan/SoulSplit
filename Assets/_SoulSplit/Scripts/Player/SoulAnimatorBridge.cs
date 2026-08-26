using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Player
{
    /// <summary>
    /// Ruh formu icin sprite-sheet animasyon koprusu.
    ///
    /// TASARIM NOTU — neden yurume/kosma yok: Ruh SUZULUYOR, yurumuyor. Bir
    /// yurume dongusu, duvarlardan gecen bir hayalette yanlis okunurdu. Bu
    /// yuzden hareket halinde de Idle (asili durus) oynuyor; "hareket ediyor"
    /// hissini SoulController'in kendi drift (Mathf.Sin) salinimi veriyor.
    /// O salinim transform'a yaziyor, Animator ise sadece m_Sprite'a —
    /// dolayisiyla ikisi cakismadan ust uste biniyor.
    ///
    /// Hasar, ruhun uzerinde Health olmadigi icin DamageRelay uzerinden
    /// bedenin can havuzuna gidiyor; Hurt animasyonu da o havuzu dinliyor.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SoulAnimatorBridge : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private MeleeAttack meleeAttack;
        [Tooltip("Bos birakilirsa DamageRelay uzerinden bedenin Health'i bulunur.")]
        [SerializeField] private Health health;

        private Animator _animator;

        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HurtParam = Animator.StringToHash("Hurt");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (meleeAttack == null) meleeAttack = GetComponentInParent<MeleeAttack>();
            // Health.FindOn once dogrudan Health arar, bulamazsa DamageRelay'in
            // hedefine gider — ruhta Health olmadigi icin ikinci yol isliyor.
            if (health == null) health = Health.FindOn(this);
        }

        private void OnEnable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered += HandleAttack;
            if (health != null) health.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (meleeAttack != null) meleeAttack.OnAttackTriggered -= HandleAttack;
            if (health != null) health.OnHit -= HandleHit;
        }

        private void HandleAttack(AttackTier tier)
        {
            _animator.SetTrigger(AttackParam);
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            if (result == HitResult.Damaged) _animator.SetTrigger(HurtParam);
        }
    }
}

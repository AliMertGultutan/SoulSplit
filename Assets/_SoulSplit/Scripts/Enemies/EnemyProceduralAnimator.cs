using UnityEngine;
using SoulSplit.Combat;
using SoulSplit.Core;

namespace SoulSplit.Enemies
{
    /// <summary>Bu dusman zeminde mi yuruyor, havada mi suzuluyor?</summary>
    public enum EnemyLocomotion
    {
        Grounded,
        Floating
    }

    /// <summary>
    /// Dusmanlar icin ortak prosedurel animasyon: yurume/suzulme, vurus
    /// hazirlik-savurma-toparlanma dongusu, hasar sarsintisi ve bakis yonune
    /// gore cevirme (flipX). Oyuncununkiyle ayni mantik — sprite kare
    /// animasyonu degil, tek sprite'i transform uzerinden oynatiyor.
    ///
    /// Bu obje EnemyBase'in "Visual" alt objesinde durur; EnemyBase'e hic
    /// dokunmaz, sadece onun public State/Facing/Velocity/OnAttackTriggered
    /// arayuzunu dinler.
    /// </summary>
    public class EnemyProceduralAnimator : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private EnemyBase enemy;
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Hareket Tipi")]
        [SerializeField] private EnemyLocomotion locomotion = EnemyLocomotion.Grounded;
        [Tooltip("Sprite kaynak gorselde saga bakiyorsa false birak.")]
        [SerializeField] private bool spriteFacesLeft;

        [Header("Yurume (Grounded)")]
        [Tooltip("Bu hizda adim dongusu tam hizinda olur; genelde dusmanin chaseSpeed degeriyle esle.")]
        [SerializeField] private float speedForFullGait = 4f;
        [SerializeField] private float stepRate = 3.4f;
        [SerializeField] private float stepBounce = 0.08f;
        [SerializeField] private float walkLean = 9f;
        [Tooltip("Durup hedef ararken bile hafif nefes/sallanma. Cansiz durmasin diye.")]
        [SerializeField] private float idleBreathAmount = 0.03f;
        [SerializeField] private float idleBreathFrequency = 1.4f;

        [Header("Suzulme (Floating)")]
        [Tooltip("Havada asilı dururken bile ortaya cikan yavas nefes/sallanma.")]
        [SerializeField] private float idleDriftAmount = 0.16f;
        [SerializeField] private float idleDriftFrequency = 1.1f;
        [Tooltip("Hareket yonune dogru yatma acisi (derece).")]
        [SerializeField] private float travelTilt = 15f;
        [Tooltip("Kanat/kuyruk sallanmasi gibi sürekli hafif yalpalama.")]
        [SerializeField] private float wobbleAmount = 7f;
        [SerializeField] private float wobbleFrequency = 2.4f;

        [Header("Dovus Animasyonu (hazirlik/vurus/TUTMA/toparlanma)")]
        [Tooltip("Hazirlik fazinin, dusmanin gercek vurus hazirlik suresine (attackWindup) orani. Kalani 'vurus' fazina gider.")]
        [SerializeField] private float attackAnticipationRatio = 0.7f;
        [Tooltip("Hazirlik sirasinda geriye cekilme acisi (derece).")]
        [SerializeField] private float attackWindupAngle = 15f;
        [Tooltip("Vurus aninda one savrulma acisi (derece).")]
        [SerializeField] private float attackSwingAngle = 32f;
        [SerializeField] private float attackLungeDistance = 0.3f;
        [Tooltip("Vurus tepe pozunun (gercek hasar anindan SONRA) ekranda asili kalma suresi.")]
        [SerializeField] private float attackHoldDuration = 0.08f;
        [SerializeField] private float attackRecoveryDuration = 0.18f;

        [Header("Hasar Sarsintisi")]
        [SerializeField] private float hurtShakeAmount = 10f;
        [SerializeField] private float hurtShakeFrequency = 40f;

        [Header("Olum")]
        [Tooltip("Olumde govde orijinal Y olceginin bu KATINA cokuyor. 1 = degisiklik yok. Fiziksel dusman icin 0.2 gibi dusuk, hayalet icin 1.4 gibi buyuk (dagilma) verilebilir.")]
        [SerializeField] private float deathScaleYFactor = 0.2f;
        [SerializeField] private float deathCollapseDuration = 0.35f;
        [Tooltip("Olum parcaciklarinin rengi. Fiziksel dusman icin tas grisi, hayalet icin soguk teal onerilir.")]
        [SerializeField] private Color deathParticleColor = new Color(0.6f, 0.6f, 0.65f);
        [Tooltip("Hasar aninda cikan kivilcimin rengi.")]
        [SerializeField] private Color hurtSparkColor = new Color(0.95f, 0.9f, 0.7f);

        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private float _stridePhase;
        private float _wobblePhase;
        private float _driftPhase;
        private float _breathPhase;

        private float _attackTimer;
        private float _attackTotalDuration;
        private float _attackAnticipationDuration;
        private float _attackSwingDuration;

        private bool _isDead;
        private float _deathTimer;
        private bool _wasHurt;

        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInParent<EnemyBase>();
            if (health == null) health = GetComponentInParent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.OnAttackTriggered += HandleAttackTriggered;
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.OnAttackTriggered -= HandleAttackTriggered;
            if (health != null) health.OnDeath -= HandleDeath;
        }

        private void HandleAttackTriggered()
        {
            // Hazirlik suresi dusmanin kendi windup'iyla eslesin ki
            // vurus tam hazirlik bitince "carpsin" gibi hissettirsin.
            _attackAnticipationDuration = enemy != null
                ? Mathf.Max(0.05f, enemy.AttackWindupDuration * attackAnticipationRatio)
                : 0.2f;
            // Vurus fazi = kalan windup suresi (gercek hasar TAM bu fazin sonunda uygulaniyor).
            _attackSwingDuration = Mathf.Max(0.03f, (enemy != null ? enemy.AttackWindupDuration : 0.2f) - _attackAnticipationDuration);
            // Tutma (impact frame hold) hasar anindan SONRA eklenir; oyun mantigini geciktirmez, sadece gorseli.
            _attackTotalDuration = _attackAnticipationDuration + _attackSwingDuration + attackHoldDuration + attackRecoveryDuration;
            _attackTimer = _attackTotalDuration;
        }

        private void HandleDeath()
        {
            _isDead = true;
            _deathTimer = 0f;

            float spread = locomotion == EnemyLocomotion.Grounded ? 70f : 100f;
            ParticleFX.Burst(transform.position, deathParticleColor,
                count: locomotion == EnemyLocomotion.Grounded ? 12 : 9,
                speed: locomotion == EnemyLocomotion.Grounded ? 3.5f : 1.8f,
                size: locomotion == EnemyLocomotion.Grounded ? 0.1f : 0.14f,
                lifetime: locomotion == EnemyLocomotion.Grounded ? 0.5f : 0.8f,
                spreadAngle: spread,
                direction: Vector2.up,
                gravityScale: locomotion == EnemyLocomotion.Grounded ? 1f : 0.05f);
        }

        private void LateUpdate()
        {
            if (enemy == null) return;

            UpdateFacing();

            if (_isDead)
            {
                ApplyDeathCollapse(Time.deltaTime);
                return;
            }

            bool isHurtNow = enemy.State == EnemyState.Hurt;
            if (isHurtNow && !_wasHurt)
            {
                ParticleFX.Impact(transform.position, hurtSparkColor, new Vector2(-enemy.Facing, 0.3f), 0.6f);
            }
            _wasHurt = isHurtNow;

            if (isHurtNow)
            {
                ApplyHurtShake();
                return;
            }

            Vector3 offset = Vector3.zero;
            float tilt = 0f;

            if (locomotion == EnemyLocomotion.Grounded) ApplyGroundedLocomotion(ref offset, ref tilt);
            else ApplyFloatingLocomotion(ref offset, ref tilt);

            if (_attackTimer > 0f) ApplyAttackOverlay(ref offset, ref tilt);

            transform.localPosition = _basePosition + offset;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null || enemy == null) return;
            bool faceRight = enemy.Facing > 0;
            spriteRenderer.flipX = spriteFacesLeft ? faceRight : !faceRight;
        }

        private void ApplyGroundedLocomotion(ref Vector3 offset, ref float tilt)
        {
            float speed = Mathf.Abs(enemy.Velocity.x);
            if (speed < 0.15f)
            {
                // Dururken bile cansiz kalmasin: hafif nefes.
                _breathPhase += Time.deltaTime * idleBreathFrequency;
                offset.y += Mathf.Sin(_breathPhase) * idleBreathAmount;
                _stridePhase = 0f;
                return;
            }

            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.1f, speedForFullGait));
            _stridePhase += Time.deltaTime * stepRate * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1f, speedRatio);

            offset.y += Mathf.Abs(Mathf.Sin(_stridePhase)) * stepBounce * speedRatio;
            tilt = -enemy.Facing * walkLean * speedRatio;
        }

        private void ApplyFloatingLocomotion(ref Vector3 offset, ref float tilt)
        {
            _driftPhase += Time.deltaTime * idleDriftFrequency * Mathf.PI * 2f;
            offset.y += Mathf.Sin(_driftPhase) * idleDriftAmount;

            _wobblePhase += Time.deltaTime * wobbleFrequency * Mathf.PI * 2f;
            float wobble = Mathf.Sin(_wobblePhase) * wobbleAmount;

            float speed = enemy.Velocity.magnitude;
            float speedRatio = Mathf.Clamp01(speed / 3f);
            float lean = -enemy.Facing * travelTilt * speedRatio;

            tilt = lean + wobble * (1f - speedRatio * 0.5f);
        }

        /// <summary>
        /// Hazirlik -> vurus -> TUTMA (impact frame) -> toparlanma. Hazirlik+vurus
        /// sureleri dusmanin gercek vurus temposuyla (attackWindup) esli — hasar
        /// TAM vurus fazinin bitiminde uygulaniyor. Tutma fazi onun UZERINE
        /// eklenir, oyun mantigini geciktirmeden goze "darbe" hissi verir.
        /// </summary>
        private void ApplyAttackOverlay(ref Vector3 offset, ref float tilt)
        {
            _attackTimer -= Time.deltaTime;
            float elapsed = _attackTotalDuration - Mathf.Max(_attackTimer, 0f);
            int facing = enemy.Facing;

            float angle;
            float lunge;

            float strikeStart = _attackAnticipationDuration;
            float holdStart = strikeStart + _attackSwingDuration;
            float recoveryStart = holdStart + attackHoldDuration;

            if (elapsed < strikeStart)
            {
                float t = _attackAnticipationDuration <= 0f ? 1f : elapsed / _attackAnticipationDuration;
                angle = Mathf.Lerp(0f, -attackWindupAngle, t);
                lunge = 0f;
            }
            else if (elapsed < holdStart)
            {
                float t = _attackSwingDuration <= 0f ? 1f : (elapsed - strikeStart) / _attackSwingDuration;
                float inv = 1f - t;
                float eased = 1f - inv * inv * inv;
                angle = Mathf.Lerp(-attackWindupAngle, attackSwingAngle, eased);
                lunge = Mathf.Lerp(0f, attackLungeDistance, eased);
            }
            else if (elapsed < recoveryStart)
            {
                angle = attackSwingAngle;
                lunge = attackLungeDistance;
            }
            else
            {
                float t = (elapsed - recoveryStart) / Mathf.Max(0.001f, attackRecoveryDuration);
                float eased = 1f - (1f - t) * (1f - t);
                angle = Mathf.Lerp(attackSwingAngle, 0f, eased);
                lunge = Mathf.Lerp(attackLungeDistance, 0f, eased);
            }

            tilt += angle * facing;
            offset.x += lunge * facing;
        }

        private void ApplyHurtShake()
        {
            float shake = (Mathf.PerlinNoise(Time.time * hurtShakeFrequency, 0f) - 0.5f) * 2f * hurtShakeAmount;
            transform.localPosition = _basePosition;
            transform.localRotation = Quaternion.Euler(0f, 0f, shake);
        }

        private void ApplyDeathCollapse(float dt)
        {
            _deathTimer += dt;
            float t = deathCollapseDuration <= 0f ? 1f : Mathf.Clamp01(_deathTimer / deathCollapseDuration);
            float scaleY = Mathf.Lerp(_baseScale.y, _baseScale.y * deathScaleYFactor, t);
            transform.localScale = new Vector3(_baseScale.x, scaleY, _baseScale.z);
        }
    }
}

using System;
using UnityEngine;

namespace SoulSplit.Combat
{
    /// <summary>Hasarin hangi boyuttan geldigi.</summary>
    public enum DamageType
    {
        Physical,
        Spiritual
    }

    /// <summary>Bir hedefin hangi hasar tipine acik oldugu.</summary>
    public enum DamageRealm
    {
        Physical,
        Spiritual,
        Both
    }

    /// <summary>Bir vurusun sonucu.</summary>
    public enum HitResult
    {
        /// <summary>Yanlis form: hasar gecmedi.</summary>
        Deflected,
        /// <summary>Dokunulmazlik suresi devam ediyor.</summary>
        Ignored,
        Damaged,
        Killed
    }

    /// <summary>
    /// Can havuzu. Hem oyuncu hem dusmanlar bunu kullanir.
    /// Tek sorumlulugu can tutmak ve hasarin gecip gecmeyecegine karar vermek;
    /// olum sonrasi ne olacagini dinleyen scriptler belirler.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Can")]
        [SerializeField] private int maxHealth = 3;

        [Header("Hasar Kurali")]
        [Tooltip("Bu hedefe hangi tip hasar isler. Yanlis tip 'Deflected' doner.")]
        [SerializeField] private DamageRealm vulnerableTo = DamageRealm.Physical;

        [Header("Dokunulmazlik")]
        [Tooltip("Hasar aldiktan sonra tekrar hasar alamama suresi (i-frame).")]
        [SerializeField] private float invincibilityDuration = 0.6f;

        private int _current;
        private float _invincibleUntil;

        public int Current => _current;
        public int Max => maxHealth;
        public float Normalized => maxHealth <= 0 ? 0f : (float)_current / maxHealth;
        public bool IsDead => _current <= 0;
        public DamageRealm VulnerableTo => vulnerableTo;
        public bool IsInvincible => Time.time < _invincibleUntil;

        /// <summary>
        /// Her vurusta tetiklenir — sonuc dahil. Efektler bunu dinler.
        /// Vector2, saldirganDAN hedefe dogru normalize yon (knockback icin);
        /// cevresel olumde (Kill) sifir gelir.
        /// </summary>
        public event Action<HitResult, DamageType, Vector2> OnHit;
        /// <summary>Can sifirlandiginda bir kez tetiklenir.</summary>
        public event Action OnDeath;
        /// <summary>Can degeri her degistiginde tetiklenir. UI bunu dinler.</summary>
        public event Action OnHealthChanged;

        private void Awake()
        {
            _current = maxHealth;
        }

        /// <summary>
        /// Hasar uygulamayi dener. Donen sonuc, cagiran tarafin
        /// dogru geri bildirimi (vurus mu, sekme mi) oynatmasi icin.
        /// </summary>
        /// <param name="hitDirection">Saldirgandan hedefe dogru normalize yon; knockback icin.</param>
        public HitResult TryTakeDamage(int amount, DamageType type, Vector2 hitDirection = default)
        {
            if (IsDead) return HitResult.Ignored;

            if (!IsVulnerableTo(type))
            {
                OnHit?.Invoke(HitResult.Deflected, type, hitDirection);
                return HitResult.Deflected;
            }

            if (IsInvincible)
            {
                OnHit?.Invoke(HitResult.Ignored, type, hitDirection);
                return HitResult.Ignored;
            }

            _current = Mathf.Max(0, _current - Mathf.Max(1, amount));
            _invincibleUntil = Time.time + invincibilityDuration;
            OnHealthChanged?.Invoke();

            HitResult result = _current <= 0 ? HitResult.Killed : HitResult.Damaged;
            OnHit?.Invoke(result, type, hitDirection);

            if (result == HitResult.Killed) OnDeath?.Invoke();
            return result;
        }

        /// <summary>
        /// Cevresel olum (cukura dusme, lav vb). Savas hasari degildir;
        /// form/vulnerableTo kuralini ve dokunulmazlik suresini atlar —
        /// dusme herkesi oldurur.
        /// </summary>
        public void Kill()
        {
            if (IsDead) return;

            _current = 0;
            OnHealthChanged?.Invoke();
            OnHit?.Invoke(HitResult.Killed, DamageType.Physical, Vector2.zero);
            OnDeath?.Invoke();
        }

        private bool IsVulnerableTo(DamageType type)
        {
            if (vulnerableTo == DamageRealm.Both) return true;
            return type == DamageType.Physical
                ? vulnerableTo == DamageRealm.Physical
                : vulnerableTo == DamageRealm.Spiritual;
        }

        /// <summary>
        /// Bir collider'dan can havuzunu bulur. Ruh objesinde Health yoktur,
        /// DamageRelay uzerinden bedenin havuzuna gidilir. Saldiran scriptlerin
        /// bu ayrimi bilmemesi icin tek nokta burasi.
        /// </summary>
        public static Health FindOn(Component source)
        {
            if (source == null) return null;

            Health health = source.GetComponentInParent<Health>();
            if (health != null) return health;

            DamageRelay relay = source.GetComponentInParent<DamageRelay>();
            return relay != null ? relay.Target : null;
        }

        public void ResetHealth()
        {
            _current = maxHealth;
            _invincibleUntil = 0f;
            OnHealthChanged?.Invoke();
        }
    }
}

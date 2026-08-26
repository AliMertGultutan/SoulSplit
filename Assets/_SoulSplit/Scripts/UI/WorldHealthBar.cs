using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.UI
{
    /// <summary>
    /// Dusmanin basinin uzerinde duran can bari. SpriteRenderer ile ciziliyor,
    /// Canvas kullanilmiyor — dusman basina bir Canvas acmak pahali ve gereksiz.
    ///
    /// Bar SADECE hasar alindiktan sonra gorunur. Dolu barlarin surekli ekranda
    /// durmasi atmosferi bozar ve oyuncuya bilgi vermez; onemli olan
    /// "bu dusmanin ne kadari kaldi" bilgisi, o da ancak vurusa baslayinca anlamli.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Health health;
        [Tooltip("Dolan kisim. Pivotu SOL kenarda olmali.")]
        [SerializeField] private Transform fill;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer backgroundRenderer;

        [Header("Gorunurluk")]
        [Tooltip("Hasar aldiktan sonra bar kac saniye ekranda kalsin.")]
        [SerializeField] private float visibleDuration = 2.5f;
        [SerializeField] private float fadeSpeed = 5f;

        [Header("Renk")]
        [Tooltip("Fiziksel dusman icin kehribar, hayalet icin teal kullan.")]
        [SerializeField] private Color fillColor = new Color(0.78f, 0.42f, 0.22f);
        [Tooltip("Can azaldikca kayilacak renk.")]
        [SerializeField] private Color lowColor = new Color(0.85f, 0.22f, 0.18f);

        [Header("Davranis")]
        [Tooltip("Barin degere yetisme yumusakligi.")]
        [SerializeField] private float smoothing = 0.12f;

        private float _displayed = 1f;
        private float _visibleUntil;
        private float _alpha;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            if (health != null) health.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (health != null) health.OnHit -= HandleHit;
        }

        /// <summary>Sadece gercekten hasar gectiginde bari goster; sekmede gosterme.</summary>
        private void HandleHit(HitResult result, DamageType type, Vector2 hitDirection, int amount)
        {
            if (result == HitResult.Damaged || result == HitResult.Killed)
            {
                _visibleUntil = Time.time + visibleDuration;
            }
        }

        private void LateUpdate()
        {
            if (health == null || fill == null) return;

            // Ebeveyn donse bile bar duz kalsin.
            transform.rotation = Quaternion.identity;

            _displayed = Mathf.Lerp(_displayed, health.Normalized,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smoothing)));
            fill.localScale = new Vector3(Mathf.Clamp01(_displayed), 1f, 1f);

            if (fillRenderer != null)
            {
                fillRenderer.color = Color.Lerp(lowColor, fillColor, Mathf.Clamp01(_displayed * 1.4f));
            }

            bool shouldShow = Time.time < _visibleUntil && !health.IsDead;
            float targetAlpha = shouldShow ? 1f : 0f;
            _alpha = Mathf.Lerp(_alpha, targetAlpha, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
            SetAlpha(_alpha);
        }

        private void SetAlpha(float alpha)
        {
            if (fillRenderer != null)
            {
                Color c = fillRenderer.color; c.a = alpha; fillRenderer.color = c;
            }
            if (backgroundRenderer != null)
            {
                Color c = backgroundRenderer.color; c.a = alpha * 0.75f; backgroundRenderer.color = c;
            }
        }
    }
}

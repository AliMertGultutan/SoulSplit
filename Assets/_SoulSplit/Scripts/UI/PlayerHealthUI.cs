using UnityEngine;
using UnityEngine.UI;
using SoulSplit.Combat;

namespace SoulSplit.UI
{
    /// <summary>
    /// Oyuncunun can bari. Ruh barinin ustunde durur.
    ///
    /// Ortak can havuzu kullandigimiz icin TEK bar var — ruh formundayken
    /// aldigin hasar da buradan gider. Oyuncunun "tek canim var, iki
    /// cepheden harciyorum" hissini kurmasi buna bagli.
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Health health;
        [Tooltip("Dolan kisim. Pivotu SOL kenarda olmali.")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [Tooltip("Hasar aninda geride kalip yavasca inen kirmizi katman.")]
        [SerializeField] private RectTransform delayedFill;

        [Header("Renkler")]
        [SerializeField] private Color healthyColor = new Color(0.84f, 0.55f, 0.32f);
        [SerializeField] private Color criticalColor = new Color(0.88f, 0.22f, 0.20f);
        [Tooltip("Bu orandan az canda kritik renge gecer ve nabiz atar.")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalThreshold = 0.34f;
        [SerializeField] private float pulseSpeed = 4.5f;

        [Header("Davranis")]
        [SerializeField] private float smoothing = 0.06f;
        [Tooltip("Geciken katmanin ne kadar sonra inmeye baslayacagi.")]
        [SerializeField] private float delayedCatchUpWait = 0.45f;
        [SerializeField] private float delayedCatchUpSpeed = 1.2f;

        private float _displayed = 1f;
        private float _delayed = 1f;
        private float _delayedResumeTime;

        private void Awake()
        {
            if (health != null)
            {
                _displayed = health.Normalized;
                _delayed = _displayed;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (health != null) health.OnHit -= HandleHit;
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 hitDirection, int amount)
        {
            if (result == HitResult.Damaged || result == HitResult.Killed)
            {
                _delayedResumeTime = Time.time + delayedCatchUpWait;
            }
        }

        private void Update()
        {
            if (health == null || fill == null) return;

            float target = health.Normalized;

            // Asil bar hemen iner.
            _displayed = Mathf.Lerp(_displayed, target,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smoothing)));
            fill.localScale = new Vector3(Mathf.Clamp01(_displayed), 1f, 1f);

            // Geciken katman biraz bekleyip iner; kaybettigin cani gozle gorursun.
            if (Time.time >= _delayedResumeTime)
            {
                _delayed = Mathf.MoveTowards(_delayed, target, delayedCatchUpSpeed * Time.deltaTime);
            }
            _delayed = Mathf.Max(_delayed, _displayed);
            if (delayedFill != null)
            {
                delayedFill.localScale = new Vector3(Mathf.Clamp01(_delayed), 1f, 1f);
            }

            if (fillImage == null) return;

            if (target <= criticalThreshold)
            {
                float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                fillImage.color = Color.Lerp(criticalColor, healthyColor, pulse * 0.35f);
            }
            else
            {
                fillImage.color = healthyColor;
            }
        }
    }
}

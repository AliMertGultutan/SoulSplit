using System.Collections;
using UnityEngine;
using SoulSplit.Combat;
using SoulSplit.Core;
using SoulSplit.UI;
using UnityEngine.SceneManagement;

namespace SoulSplit.Player
{
    /// <summary>
    /// Olum ve yeniden dogus dongusu.
    ///
    /// Beden olurse oyuncu olur — ruh formunda olsan bile. Ortak can havuzu
    /// kullandigimiz icin bu kural kendiliginden isliyor: ruh geride kalan
    /// bedeni koruyamazsa oyun biter.
    ///
    /// Olumden sonra oyuncunun devam, yeni oyun veya ana menu kararini
    /// kendisinin vermesi icin fizik durdurulur ve olum ekrani acilir.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerController))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Health health;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private SoulSwitchManager switchManager;
        [SerializeField] private SpriteRenderer visual;

        [Header("Olum Ekrani")]
        [Tooltip("Olumden sonra seceneklerin gorunmesine kadar gecen toplam sure.")]
        [SerializeField] private float respawnDelay = 0.9f;
        [Header("Olum Efekti")]
        [SerializeField] private float deathFadeDuration = 0.5f;

        private Rigidbody2D _rb;
        private bool _isDying;

        /// <summary>Kac kez olundu. Test ve rapor icin faydali.</summary>
        public int DeathCount { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (health == null) health = GetComponent<Health>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (input == null) input = GetComponent<PlayerInputHandler>();
            if (health == null || controller == null || _rb == null)
            {
                Debug.LogError("[PlayerDeathHandler] Zorunlu oyuncu bilesenleri eksik; yeniden dogus devre disi.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void Start()
        {
            if (!ProgressionSave.TryConsumeResume(SceneManager.GetActiveScene().name, out ProgressionSave.CheckpointData saved))
                return;

            transform.position = saved.Position;
            _rb.position = saved.Position;
            _rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (_isDying) return;
            _isDying = true;
            DeathCount++;
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            // Ruh disaridaysa once bedene don; olum her zaman bedende yasanir.
            if (switchManager != null) switchManager.ForceReturnToBody();

            controller.enabled = false;
            if (input != null) input.enabled = false;
            if (switchManager != null) switchManager.enabled = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;

            yield return FadeVisual(1f, 0f, deathFadeDuration);
            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay - deathFadeDuration));

            DeathScreenUI.GetOrCreate().Show();
        }

        private IEnumerator FadeVisual(float from, float to, float duration)
        {
            if (visual == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Color c = visual.color;
                c.a = Mathf.Lerp(from, to, elapsed / duration);
                visual.color = c;
                yield return null;
            }

            Color final = visual.color;
            final.a = to;
            visual.color = final;
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0f, respawnDelay);
            deathFadeDuration = Mathf.Max(0f, deathFadeDuration);
        }
    }
}

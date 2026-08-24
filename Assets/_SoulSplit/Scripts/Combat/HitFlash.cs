using System.Collections;
using UnityEngine;
using SoulSplit.Core;

namespace SoulSplit.Combat
{
    /// <summary>
    /// Vurus geri bildirimi. Health'in olaylarini dinler ve sprite'i
    /// sonuca gore farkli renkte yakip sonduruR; ayrica hit-stop ve
    /// kamera sarsintisini tetikler.
    ///
    /// "Yanlis formla vurdun" bilgisi oyuncuya YAZIYLA degil renkle verilir:
    /// beyaz flash = hasar gecti, soguk mavi flash = vurus sekti.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Renkler")]
        [Tooltip("Hasar gectiginde.")]
        [SerializeField] private Color damagedColor = Color.white;
        [Tooltip("Yanlis form: vurus sekti, hasar yok.")]
        [SerializeField] private Color deflectedColor = new Color(0.45f, 0.7f, 1f);

        [Header("Sure")]
        [SerializeField] private float damagedFlashDuration = 0.09f;
        [SerializeField] private float deflectedFlashDuration = 0.05f;

        [Header("Darbe Hissi (Cila)")]
        [Tooltip("Hasar aninda ekranin donma suresi (gercek zaman).")]
        [SerializeField] private float hitStopDuration = 0.035f;
        [Tooltip("Olum aninda ekranin donma suresi. Daha uzun; agirlik hissi verir.")]
        [SerializeField] private float deathHitStopDuration = 0.09f;
        [Tooltip("Hasar aninda kamera sarsinti siddeti.")]
        [SerializeField] private float shakeMagnitude = 0.06f;
        [SerializeField] private float deathShakeMagnitude = 0.14f;
        [SerializeField] private float shakeDuration = 0.14f;

        private Color _baseColor;
        private Coroutine _routine;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;
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
            if (spriteRenderer == null) return;

            switch (result)
            {
                case HitResult.Damaged:
                    Flash(damagedColor, damagedFlashDuration);
                    HitStop.Trigger(hitStopDuration);
                    CameraFollow.ShakeCamera(shakeMagnitude, shakeDuration);
                    break;
                case HitResult.Killed:
                    Flash(damagedColor, damagedFlashDuration);
                    HitStop.Trigger(deathHitStopDuration);
                    CameraFollow.ShakeCamera(deathShakeMagnitude, shakeDuration * 1.4f);
                    CameraFollow.PunchZoom(0.4f, 0.22f);
                    break;
                case HitResult.Deflected:
                    Flash(deflectedColor, deflectedFlashDuration);
                    break;
            }
        }

        private void Flash(Color color, float duration)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            // Saydamligi koru; hayalet dusmanin uyku saydamligi bozulmasin.
            Color flash = color;
            flash.a = spriteRenderer.color.a;
            spriteRenderer.color = flash;

            yield return new WaitForSeconds(duration);

            Color restored = _baseColor;
            restored.a = spriteRenderer.color.a;
            spriteRenderer.color = restored;
            _routine = null;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Carpisma aninda oyunu birkac kare dondurur (freeze frame).
    /// Static Trigger() ile her yerden cagrilir; sahnede tek bir host
    /// obje coroutine'i calistirir. Gercek zamanli bekleme kullanir ki
    /// Time.timeScale sifira yakinken bile zamanlayici islesin.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private static HitStop _instance;

        [Tooltip("Durma sirasinda zaman bu carpanla akar (0 = tam durma).")]
        [SerializeField] private float frozenTimeScale = 0.02f;

        private Coroutine _routine;
        private float _originalTimeScale = 1f;

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>Kisa bir donma tetikler. Ust uste cagrilirsa en uzun sure kazanir.</summary>
        public static void Trigger(float duration)
        {
            if (_instance == null || duration <= 0f) return;
            _instance.TriggerInternal(duration);
        }

        private void TriggerInternal(float duration)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            else
            {
                _originalTimeScale = Time.timeScale;
            }
            _routine = StartCoroutine(Routine(duration));
        }

        private IEnumerator Routine(float duration)
        {
            Time.timeScale = frozenTimeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = _originalTimeScale;
            _routine = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) Time.timeScale = _originalTimeScale;
        }
    }
}

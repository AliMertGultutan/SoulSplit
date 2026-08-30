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
        private float _endRealtime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[HitStop] Sahnede birden fazla host var; son eklenen devre disi birakildi.", this);
                enabled = false;
                return;
            }
            _instance = this;
        }

        /// <summary>Kisa bir donma tetikler. Ust uste cagrilirsa en uzun sure kazanir.</summary>
        public static void Trigger(float duration)
        {
            // Vuruslar dusman saldirilarinin zamanini dondurmasin.
            TimeScaleController.ClearHitStop();
        }

        private void TriggerInternal(float duration)
        {
            _endRealtime = Mathf.Max(_endRealtime, Time.realtimeSinceStartup + duration);
            if (_routine == null) _routine = StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            TimeScaleController.SetHitStopScale(frozenTimeScale);
            while (Time.realtimeSinceStartup < _endRealtime) yield return null;
            TimeScaleController.ClearHitStop();
            _routine = null;
        }

        private void OnValidate() => frozenTimeScale = Mathf.Clamp01(frozenTimeScale);

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            TimeScaleController.ClearHitStop();
        }
    }
}

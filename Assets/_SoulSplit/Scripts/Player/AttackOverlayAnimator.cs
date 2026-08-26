using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Hafif/agir saldiri icin 4 fazli (hazirlik -> vurus -> tutma -> toparlanma)
    /// zamanlama seti. Beden ve ruh formu ayni egri matematigini kullanir, sadece
    /// sureler/acilar farkli — bu yuzden degerler burada degil, cagiran tarafta
    /// [SerializeField] olarak kalir (Inspector'da ayri ayri ayarlanabilsin diye).
    /// </summary>
    public readonly struct AttackOverlayTimings
    {
        public readonly float anticipationDuration;
        public readonly float strikeDuration;
        public readonly float holdDuration;
        public readonly float recoveryDuration;
        public readonly float windupAngle;
        public readonly float swingAngle;
        public readonly float lungeDistance;

        public AttackOverlayTimings(float anticipationDuration, float strikeDuration, float holdDuration,
            float recoveryDuration, float windupAngle, float swingAngle, float lungeDistance)
        {
            this.anticipationDuration = anticipationDuration;
            this.strikeDuration = strikeDuration;
            this.holdDuration = holdDuration;
            this.recoveryDuration = recoveryDuration;
            this.windupAngle = windupAngle;
            this.swingAngle = swingAngle;
            this.lungeDistance = lungeDistance;
        }

        public float TotalDuration => anticipationDuration + strikeDuration + holdDuration + recoveryDuration;
    }

    /// <summary>
    /// <see cref="PlayerProceduralAnimator"/> (beden) ve <see cref="SoulController"/> (ruh)
    /// arasinda paylasilan vurus-overlay egri matematigi. Ikisi de ayni 4 fazli
    /// (hazirlik/vurus/tutma/toparlanma) zamanlamayi kullaniyordu ama hesabi birebir
    /// kopyalamisti; egri artik tek yerde, tuning degerleri hala her bilesende ayri
    /// (body/soul formu farkli hissetmeye devam ediyor).
    /// </summary>
    public static class AttackOverlayAnimator
    {
        /// <param name="elapsed">Saldiri tetiklenmesinden bu yana gecen sure (saniye).</param>
        public static void Evaluate(in AttackOverlayTimings timings, float elapsed, out float angleOffset, out float lungeOffset)
        {
            float strikeStart = timings.anticipationDuration;
            float holdStart = strikeStart + timings.strikeDuration;
            float recoveryStart = holdStart + timings.holdDuration;

            if (elapsed < strikeStart)
            {
                // Hazirlik: yavas geri cekilme.
                float t = timings.anticipationDuration <= 0f ? 1f : elapsed / timings.anticipationDuration;
                angleOffset = Mathf.Lerp(0f, -timings.windupAngle, EaseOutQuad(t));
                lungeOffset = 0f;
            }
            else if (elapsed < holdStart)
            {
                // Vurus: COK hizli firlama, neredeyse anlik.
                float t = timings.strikeDuration <= 0f ? 1f : (elapsed - strikeStart) / timings.strikeDuration;
                float eased = EaseOutCubic(t);
                angleOffset = Mathf.Lerp(-timings.windupAngle, timings.swingAngle, eased);
                lungeOffset = Mathf.Lerp(0f, timings.lungeDistance, eased);
            }
            else if (elapsed < recoveryStart)
            {
                // Tutma: tepe pozu SABIT — "impact frame" gozun vurusu okumasi icin asili kalir.
                angleOffset = timings.swingAngle;
                lungeOffset = timings.lungeDistance;
            }
            else
            {
                float recoveryDur = Mathf.Max(0.001f, timings.recoveryDuration);
                float t = (elapsed - recoveryStart) / recoveryDur;
                angleOffset = Mathf.Lerp(timings.swingAngle, 0f, EaseOutQuad(t));
                lungeOffset = Mathf.Lerp(timings.lungeDistance, 0f, EaseOutQuad(t));
            }
        }

        /// <summary>Genel amacli ease-out egrisi; launch-pop gibi diger gorsel katmanlar da kullanir.</summary>
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        /// <summary>Quad'dan daha keskin baslar — "snap" hissi icin vurus fazinda kullanilir.</summary>
        public static float EaseOutCubic(float t) { float inv = 1f - t; return 1f - inv * inv * inv; }
    }
}

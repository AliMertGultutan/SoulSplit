using UnityEngine;
using SoulSplit.Combat;

namespace SoulSplit.Core
{
    /// <summary>
    /// Cukura veya harita disina dusmeyi anlik olum sayar. Kavga hasari
    /// degildir; Health.Kill() form/vulnerableTo kuralini atlar — dusme
    /// oyuncuyu formundan bagimsiz oldurur.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class KillZone : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Health health = Health.FindOn(other);
            if (health != null) health.Kill();
        }
    }
}

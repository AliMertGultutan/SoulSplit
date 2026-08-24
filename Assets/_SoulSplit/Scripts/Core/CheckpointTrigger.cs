using UnityEngine;
using SoulSplit.Player;

namespace SoulSplit.Core
{
    /// <summary>Oyuncu buraya girince yeniden dogus noktasi burasi olur.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointTrigger : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerDeathHandler handler = other.GetComponentInParent<PlayerDeathHandler>();
            if (handler != null) handler.SetCheckpoint(transform);
        }
    }
}

using UnityEngine;
using SoulSplit.Player;
using SoulSplit.UI;

namespace SoulSplit.Core
{
    /// <summary>Bolumun sonuna varinca kazanma ekranini acar. Tek seferlik.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class WinTrigger : MonoBehaviour
    {
        [SerializeField] private WinScreenUI winScreen;

        private bool _triggered;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            _triggered = true;
            if (winScreen != null) winScreen.Show();
        }
    }
}

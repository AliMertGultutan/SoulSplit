using UnityEngine;
using SoulSplit.Player;
using SoulSplit.UI;
using SoulSplit.Enemies;
using SoulSplit.Combat;

namespace SoulSplit.Core
{
    /// <summary>Bolumun sonuna varinca, tum dusmanlar olu ise kazanma ekranini acar.</summary>
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
            TryComplete(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Oyuncu bolum sonuna erken ulasirsa, dusmanlar oldugu anda
            // geri cikmasini beklemeden kosul bir sonraki fizik karesinde
            // dogrudan yeniden kontrol edilir.
            TryComplete(other);
        }

        private void TryComplete(Collider2D other)
        {
            if (_triggered || other.GetComponentInParent<PlayerController>() == null) return;
            if (!AllEnemiesDefeated()) return;

            _triggered = true;
            if (winScreen != null) winScreen.Show();
        }

        public static bool AllEnemiesDefeated()
        {
            EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
            foreach (EnemyBase enemy in enemies)
            {
                if (enemy == null) continue;
                Health health = enemy.GetComponent<Health>();
                if (health != null && !health.IsDead) return false;
            }
            return true;
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.UI
{
    /// <summary>Kazanma panelini gosterir, oyunu durdurur, yeniden baslatma sunar.</summary>
    public class WinScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Show()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>Yeniden baslatma butonuna baglanir.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}

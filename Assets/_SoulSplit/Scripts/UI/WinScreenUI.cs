using UnityEngine;
using UnityEngine.SceneManagement;
using SoulSplit.Core;

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
            ProgressionSave.Clear();
            if (panelRoot != null) panelRoot.SetActive(true);
            TimeScaleController.SetPaused(this, true);
        }

        /// <summary>Yeniden baslatma butonuna baglanir.</summary>
        public void Restart()
        {
            TimeScaleController.SetPaused(this, false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDestroy()
        {
            TimeScaleController.SetPaused(this, false);
        }
    }
}

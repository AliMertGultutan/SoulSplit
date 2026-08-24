using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.UI
{
    /// <summary>Ana menu. Tek gorevi: Play tusuna basilinca demo sahnesini acmak.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Build Settings'e eklenmis demo sahnesinin adi.")]
        [SerializeField] private string gameSceneName = "SampleScene";

        public void PlayGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}

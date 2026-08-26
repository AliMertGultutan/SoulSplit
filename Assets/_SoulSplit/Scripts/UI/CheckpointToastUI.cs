using System.Collections;
using SoulSplit.Core;
using SoulSplit.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Basarili checkpoint kaydini kisa ve okunakli bir bildirimle dogrular.
    /// Unscaled time kullandigi icin hit-stop veya pause gecislerinden etkilenmez.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CheckpointToastUI : MonoBehaviour
    {
        private const string GameplaySceneName = "SampleScene";

        private CanvasGroup _canvasGroup;
        private Coroutine _routine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameplaySceneName) return;
            if (FindAnyObjectByType<CheckpointToastUI>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;

            new GameObject("CheckpointToast", typeof(CanvasGroup), typeof(CheckpointToastUI));
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            BuildInterface();
        }

        private void OnEnable()
        {
            ProgressionSave.OnCheckpointSaved += HandleCheckpointSaved;
        }

        private void OnDisable()
        {
            ProgressionSave.OnCheckpointSaved -= HandleCheckpointSaved;
        }

        private void HandleCheckpointSaved(ProgressionSave.CheckpointData data)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            yield return FadeTo(1f, 0.16f);
            yield return new WaitForSecondsRealtime(1.35f);
            yield return FadeTo(0f, 0.38f);
            _routine = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = _canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                _canvasGroup.alpha = Mathf.Lerp(start, target, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            _canvasGroup.alpha = target;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.88f);
            panelRect.sizeDelta = new Vector2(430f, 72f);
            panel.GetComponent<Image>().color = new Color(0.045f, 0.075f, 0.105f, 0.96f);
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.82f, 0.91f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 8f);
            textRect.offsetMax = new Vector2(-20f, -8f);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "RUH MÜHRÜ KAYDEDİLDİ";
            text.fontSize = 21;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }
    }
}

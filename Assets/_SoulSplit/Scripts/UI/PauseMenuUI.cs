using SoulSplit.Core;
using SoulSplit.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Oyun sahnesinde kendini kuran duraklatma menusu. Sahneye kalici UI
    /// referansi eklemeden klavye, gamepad ve fare ile ayni akisi sunar.
    /// </summary>
    public sealed class PauseMenuUI : MonoBehaviour
    {
        private const string GameplaySceneName = "SampleScene";
        private const string MainMenuSceneName = "MainMenu";

        private static readonly Color BackdropColor = new Color(0.015f, 0.025f, 0.045f, 0.88f);
        private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.12f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.11f, 0.16f, 0.21f, 1f);
        private static readonly Color ButtonHighlightColor = new Color(0.18f, 0.29f, 0.36f, 1f);
        private static readonly Color AccentColor = new Color(0.42f, 0.82f, 0.91f, 1f);
        private static readonly Color WarmAccentColor = new Color(0.86f, 0.55f, 0.32f, 1f);

        private GameObject _menuRoot;
        private Button _resumeButton;
        private Button _settingsButton;
        private SettingsPanelUI _settingsPanel;
        private Font _font;
        private bool _isOpen;
        private bool _ownsEventSystem;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameplaySceneName) return;
            if (FindAnyObjectByType<PauseMenuUI>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;

            GameObject host = new GameObject("PauseMenu");
            host.AddComponent<PauseMenuUI>();
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            _settingsPanel = SettingsPanelUI.GetOrCreate();
            BuildInterface();
            SetMenuVisible(false);
        }

        private void Update()
        {
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (!keyboardPressed && !gamepadPressed) return;

            if (_settingsPanel != null && _settingsPanel.IsOpen)
            {
                _settingsPanel.Close();
                return;
            }

            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen || TimeScaleController.IsPaused) return;

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _isOpen = true;
            SetMenuVisible(true);
            TimeScaleController.SetPaused(this, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null && _resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);
            }
        }

        public void Close()
        {
            if (!_isOpen) return;

            if (_settingsPanel != null && _settingsPanel.IsOpen) _settingsPanel.Close();
            _isOpen = false;
            SetMenuVisible(false);
            TimeScaleController.SetPaused(this, false);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        public void RestartLevel()
        {
            ProgressionSave.RequestResume();
            ReleasePause();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            ReleasePause();
            SceneManager.LoadScene(MainMenuSceneName);
        }

        public void OpenSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.Open(_settingsButton != null ? _settingsButton.gameObject : null);
        }

        private void ReleasePause()
        {
            _isOpen = false;
            if (_settingsPanel != null && _settingsPanel.IsOpen) _settingsPanel.Close();
            TimeScaleController.SetPaused(this, false);
        }

        private void SetMenuVisible(bool visible)
        {
            if (_menuRoot != null) _menuRoot.SetActive(visible);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            _ownsEventSystem = true;
        }

        private void BuildInterface()
        {
            _menuRoot = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _menuRoot.transform.SetParent(transform, false);

            Canvas canvas = _menuRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = _menuRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform overlayRect = _menuRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            _menuRoot.GetComponent<Image>().color = BackdropColor;

            GameObject panel = CreateUiObject("PausePanel", _menuRoot.transform, typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 560f);
            panel.GetComponent<Image>().color = PanelColor;
            Outline panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.42f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Title", "OYUN DURAKLATILDI", 32, FontStyle.Bold,
                new Vector2(0f, 215f), new Vector2(440f, 52f), Color.white);
            CreateText(panel.transform, "Subtitle", "Yolculuk seni bekliyor", 18, FontStyle.Normal,
                new Vector2(0f, 174f), new Vector2(440f, 34f), new Color(0.68f, 0.75f, 0.80f));

            _resumeButton = CreateButton(panel.transform, "ResumeButton", "DEVAM ET",
                new Vector2(0f, 104f), AccentColor, Close);
            CreateButton(panel.transform, "RestartButton", "BÖLÜMÜ YENİDEN BAŞLAT",
                new Vector2(0f, 32f), WarmAccentColor, RestartLevel);
            _settingsButton = CreateButton(panel.transform, "SettingsButton", "AYARLAR",
                new Vector2(0f, -40f), AccentColor, OpenSettings);

            CreateButton(panel.transform, "MainMenuButton", "ANA MENÜYE DÖN",
                new Vector2(0f, -112f), new Color(0.72f, 0.76f, 0.80f), ReturnToMainMenu);
            CreateText(panel.transform, "Footer", "ESC / START  •  Kapat", 15, FontStyle.Normal,
                new Vector2(0f, -210f), new Vector2(420f, 30f), new Color(0.55f, 0.62f, 0.68f));
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Color accent, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(420f, 58f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.selectedColor = new Color(accent.r * 0.42f, accent.g * 0.42f, accent.b * 0.42f, 1f);
            colors.pressedColor = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f);
            colors.disabledColor = new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(action);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            CreateText(buttonObject.transform, "Label", label, 19, FontStyle.Bold,
                Vector2.zero, rect.sizeDelta, Color.white, stretch: true);
            return button;
        }

        private Text CreateText(Transform parent, string name, string value, int fontSize,
            FontStyle fontStyle, Vector2 position, Vector2 size, Color color, bool stretch = false)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (stretch)
            {
                Stretch(rect);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }

            Text text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            foreach (System.Type component in components) result.AddComponent(component);
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            ReleasePause();
            if (_ownsEventSystem && EventSystem.current != null)
            {
                Destroy(EventSystem.current.gameObject);
            }
        }
    }
}

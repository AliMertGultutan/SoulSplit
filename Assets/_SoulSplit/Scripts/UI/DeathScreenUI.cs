using SoulSplit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Oyuncu oldugunde dunyayi durdurur ve devam yolunu acikca sectirir.
    /// Yeni oyun kaydi sildigi icin ikinci bir onay adimi kullanir.
    /// </summary>
    public sealed class DeathScreenUI : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        private static readonly Color BackdropColor = new Color(0.01f, 0.012f, 0.025f, 0.94f);
        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.105f, 0.99f);
        private static readonly Color ButtonColor = new Color(0.115f, 0.13f, 0.17f, 1f);
        private static readonly Color HighlightColor = new Color(0.22f, 0.28f, 0.36f, 1f);
        private static readonly Color SoulAccent = new Color(0.42f, 0.82f, 0.91f, 1f);
        private static readonly Color DangerAccent = new Color(0.86f, 0.42f, 0.30f, 1f);

        private GameObject _root;
        private GameObject _confirmationRoot;
        private Button _retryButton;
        private Button _newGameButton;
        private Button _cancelButton;
        private Font _font;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool IsConfirmationOpen => _confirmationRoot != null && _confirmationRoot.activeSelf;

        public static DeathScreenUI GetOrCreate()
        {
            DeathScreenUI existing = FindAnyObjectByType<DeathScreenUI>();
            return existing != null
                ? existing
                : new GameObject("DeathScreen").AddComponent<DeathScreenUI>();
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildInterface();
            _root.SetActive(false);
        }

        private void Update()
        {
            if (!IsConfirmationOpen) return;

            bool keyboardCancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadCancel = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardCancel || gamepadCancel) CancelNewGame();
        }

        public void Show()
        {
            if (IsOpen) return;

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _confirmationRoot.SetActive(false);
            _root.SetActive(true);
            TimeScaleController.SetPaused(this, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Select(_retryButton);
        }

        public void RetryFromCheckpoint()
        {
            if (ProgressionSave.HasCheckpoint) ProgressionSave.RequestResume();
            LoadScene(SceneManager.GetActiveScene().name);
        }

        public void StartNewGame()
        {
            _confirmationRoot.SetActive(true);
            Select(_cancelButton);
        }

        public void CancelNewGame()
        {
            _confirmationRoot.SetActive(false);
            Select(_newGameButton);
        }

        public void ConfirmNewGame()
        {
            ProgressionSave.RequestNewGame();
            LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            LoadScene(MainMenuSceneName);
        }

        private void LoadScene(string sceneName)
        {
            ReleasePause();
            SceneManager.LoadScene(sceneName);
        }

        private void ReleasePause()
        {
            TimeScaleController.SetPaused(this, false);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            TimeScaleController.SetPaused(this, false);
        }

        private void BuildInterface()
        {
            _root = CreateUiObject("DeathOverlay", transform, typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(Image));
            Stretch(_root.GetComponent<RectTransform>());
            _root.GetComponent<Image>().color = BackdropColor;

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateUiObject("DeathPanel", _root.transform, typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(580f, 590f);
            panel.GetComponent<Image>().color = PanelColor;
            Outline panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(DangerAccent.r, DangerAccent.g, DangerAccent.b, 0.68f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Title", "RUHUN BAĞI KOPTU", 35, FontStyle.Bold,
                new Vector2(0f, 215f), new Vector2(500f, 58f), Color.white);
            CreateText(panel.transform, "Subtitle", "Yolculuğa nasıl devam etmek istersin?", 18,
                FontStyle.Normal, new Vector2(0f, 164f), new Vector2(500f, 40f),
                new Color(0.75f, 0.80f, 0.84f));

            _retryButton = CreateButton(panel.transform, "RetryCheckpointButton", "SON CHECKPOINT'TEN DEVAM ET",
                new Vector2(0f, 82f), SoulAccent, RetryFromCheckpoint);
            _newGameButton = CreateButton(panel.transform, "DeathNewGameButton", "YENİ OYUN",
                new Vector2(0f, 4f), DangerAccent, StartNewGame);
            Button mainMenuButton = CreateButton(panel.transform, "DeathMainMenuButton", "ANA MENÜ",
                new Vector2(0f, -74f), new Color(0.72f, 0.76f, 0.82f), ReturnToMainMenu);
            CreateText(panel.transform, "Info", "Devam etmek bölümü yeniler ve son checkpoint'i yükler.", 15,
                FontStyle.Normal, new Vector2(0f, -154f), new Vector2(500f, 38f),
                new Color(0.58f, 0.65f, 0.71f));

            LinkNavigation(_retryButton, mainMenuButton, _newGameButton);
            LinkNavigation(_newGameButton, _retryButton, mainMenuButton);
            LinkNavigation(mainMenuButton, _newGameButton, _retryButton);

            BuildConfirmation(panel.transform);
        }

        private void BuildConfirmation(Transform parent)
        {
            _confirmationRoot = CreateUiObject("DeathNewGameConfirmation", _root.transform, typeof(Image));
            Stretch(_confirmationRoot.GetComponent<RectTransform>());
            _confirmationRoot.GetComponent<Image>().color = new Color(0.005f, 0.008f, 0.015f, 0.96f);
            _confirmationRoot.transform.SetAsLastSibling();

            GameObject panel = CreateUiObject("ConfirmationPanel", _confirmationRoot.transform,
                typeof(Image), typeof(Outline));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 390f);
            panel.GetComponent<Image>().color = PanelColor;
            panel.GetComponent<Outline>().effectColor = DangerAccent;

            CreateText(panel.transform, "Title", "KAYITLI İLERLEME SİLİNECEK", 27, FontStyle.Bold,
                new Vector2(0f, 112f), new Vector2(500f, 52f), Color.white);
            CreateText(panel.transform, "Message",
                "Yeni oyuna başlamak istediğine emin misin?\nBu işlem geri alınamaz.", 18,
                FontStyle.Normal, new Vector2(0f, 40f), new Vector2(480f, 76f),
                new Color(0.78f, 0.82f, 0.86f));

            Button confirm = CreateButton(panel.transform, "ConfirmDeathNewGameButton", "EVET, YENİ OYUN",
                new Vector2(0f, -54f), DangerAccent, ConfirmNewGame);
            _cancelButton = CreateButton(panel.transform, "CancelDeathNewGameButton", "VAZGEÇ",
                new Vector2(0f, -130f), SoulAccent, CancelNewGame);
            LinkNavigation(confirm, _cancelButton, _cancelButton);
            LinkNavigation(_cancelButton, confirm, confirm);
            _confirmationRoot.SetActive(false);
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Color accent, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(470f, 60f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = HighlightColor;
            colors.selectedColor = new Color(accent.r * 0.46f, accent.g * 0.46f, accent.b * 0.46f, 1f);
            colors.pressedColor = new Color(accent.r * 0.6f, accent.g * 0.6f, accent.b * 0.6f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(action);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
            CreateText(buttonObject.transform, "Label", label, 19, FontStyle.Bold,
                Vector2.zero, rect.sizeDelta, Color.white, true);
            return button;
        }

        private Text CreateText(Transform parent, string name, string value, int fontSize,
            FontStyle style, Vector2 position, Vector2 size, Color color, bool stretch = false)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (stretch) Stretch(rect);
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
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void LinkNavigation(Selectable item, Selectable up, Selectable down)
        {
            Navigation navigation = item.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            item.navigation = navigation;
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current == null || selectable == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
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
    }
}

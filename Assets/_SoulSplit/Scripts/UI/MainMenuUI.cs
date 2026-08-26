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
    /// Ana menu akisini yonetir. Kayit varsa birincil eylem devam etmektir;
    /// yeni oyun mevcut ilerlemeyi silmeden once acik onay ister.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Build Settings'e eklenmis demo sahnesinin adi.")]
        [SerializeField] private string gameSceneName = "SampleScene";

        private Button _playButton;
        private Button _quitButton;
        private Button _newGameButton;
        private Button _settingsButton;
        private SettingsPanelUI _settingsPanel;
        private GameObject _confirmationRoot;
        private Button _cancelButton;
        private Text _playLabel;

        private void Awake()
        {
            EnsureEventSystem();

            _playButton = GameObject.Find("OYNAButton")?.GetComponent<Button>();
            _quitButton = GameObject.Find("CIKISButton")?.GetComponent<Button>();
            _playLabel = _playButton != null ? _playButton.GetComponentInChildren<Text>(true) : null;
            _settingsPanel = SettingsPanelUI.GetOrCreate();

            ConfigureMenu();
        }

        private void Update()
        {
            if (_settingsPanel == null || !_settingsPanel.IsOpen) return;

            bool keyboardPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadPressed = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardPressed || gamepadPressed) _settingsPanel.Close();
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            if (inputModule.actionsAsset == null)
                inputModule.AssignDefaultActions();
        }

        private void Start()
        {
            if (EventSystem.current != null && _playButton != null)
                EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
        }

        public void PlayGame()
        {
            if (ProgressionSave.HasCheckpoint) ProgressionSave.RequestResume();
            else ProgressionSave.RequestNewGame();
            SceneManager.LoadScene(gameSceneName);
        }

        public void StartNewGame()
        {
            if (!ProgressionSave.HasCheckpoint)
            {
                ConfirmNewGame();
                return;
            }

            if (_confirmationRoot != null) _confirmationRoot.SetActive(true);
            if (EventSystem.current != null && _cancelButton != null)
                EventSystem.current.SetSelectedGameObject(_cancelButton.gameObject);
        }

        public void CancelNewGame()
        {
            if (_confirmationRoot != null) _confirmationRoot.SetActive(false);
            if (EventSystem.current != null && _newGameButton != null)
                EventSystem.current.SetSelectedGameObject(_newGameButton.gameObject);
        }

        public void ConfirmNewGame()
        {
            ProgressionSave.RequestNewGame();
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.Open(_settingsButton != null ? _settingsButton.gameObject : null);
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void ConfigureMenu()
        {
            if (_playButton == null || _quitButton == null) return;

            bool hasCheckpoint = ProgressionSave.HasCheckpoint;
            SetLabel(_playButton, hasCheckpoint ? "DEVAM ET" : "OYNA");

            if (hasCheckpoint)
            {
                _newGameButton = Instantiate(_playButton, _playButton.transform.parent);
                _newGameButton.name = "YENİ OYUNButton";
                _newGameButton.onClick = new Button.ButtonClickedEvent();
                _newGameButton.onClick.AddListener(StartNewGame);
                SetLabel(_newGameButton, "YENİ OYUN");

                RectTransform newGameRect = _newGameButton.GetComponent<RectTransform>();
                newGameRect.anchorMin = newGameRect.anchorMax = new Vector2(0.5f, 0.30f);
                newGameRect.anchoredPosition = Vector2.zero;
                Image newGameImage = _newGameButton.GetComponent<Image>();
                if (newGameImage != null) newGameImage.color = new Color(0.58f, 0.43f, 0.31f, 0.94f);

                BuildConfirmation();
            }

            _settingsButton = Instantiate(_playButton, _playButton.transform.parent);
            _settingsButton.name = "AYARLARButton";
            _settingsButton.onClick = new Button.ButtonClickedEvent();
            _settingsButton.onClick.AddListener(OpenSettings);
            SetLabel(_settingsButton, "AYARLAR");
            RectTransform settingsRect = _settingsButton.GetComponent<RectTransform>();
            float settingsAnchor = hasCheckpoint ? 0.18f : 0.30f;
            settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(0.5f, settingsAnchor);
            settingsRect.anchoredPosition = Vector2.zero;
            Image settingsImage = _settingsButton.GetComponent<Image>();
            if (settingsImage != null) settingsImage.color = new Color(0.22f, 0.40f, 0.48f, 0.94f);

            RectTransform quitRect = _quitButton.GetComponent<RectTransform>();
            float quitAnchor = hasCheckpoint ? 0.06f : 0.18f;
            quitRect.anchorMin = quitRect.anchorMax = new Vector2(0.5f, quitAnchor);
            quitRect.anchoredPosition = Vector2.zero;

            ConfigureNavigation(hasCheckpoint);
        }

        private void ConfigureNavigation(bool hasCheckpoint)
        {
            Navigation playNavigation = _playButton.navigation;
            playNavigation.mode = Navigation.Mode.Explicit;
            playNavigation.selectOnDown = hasCheckpoint ? _newGameButton : _settingsButton;
            playNavigation.selectOnUp = _quitButton;
            _playButton.navigation = playNavigation;

            if (_newGameButton != null)
            {
                Navigation newGameNavigation = _newGameButton.navigation;
                newGameNavigation.mode = Navigation.Mode.Explicit;
                newGameNavigation.selectOnDown = _settingsButton;
                newGameNavigation.selectOnUp = _playButton;
                _newGameButton.navigation = newGameNavigation;
            }

            Navigation settingsNavigation = _settingsButton.navigation;
            settingsNavigation.mode = Navigation.Mode.Explicit;
            settingsNavigation.selectOnDown = _quitButton;
            settingsNavigation.selectOnUp = hasCheckpoint ? _newGameButton : _playButton;
            _settingsButton.navigation = settingsNavigation;

            Navigation quitNavigation = _quitButton.navigation;
            quitNavigation.mode = Navigation.Mode.Explicit;
            quitNavigation.selectOnDown = _playButton;
            quitNavigation.selectOnUp = _settingsButton;
            _quitButton.navigation = quitNavigation;
        }

        private void BuildConfirmation()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            _confirmationRoot = CreateUiObject("NewGameConfirmation", canvas.transform, typeof(Image));
            RectTransform rootRect = _confirmationRoot.GetComponent<RectTransform>();
            Stretch(rootRect);
            _confirmationRoot.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.035f, 0.88f);
            _confirmationRoot.transform.SetAsLastSibling();

            GameObject panel = CreateUiObject("Panel", _confirmationRoot.transform, typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 370f);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.085f, 0.12f, 1f);
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.55f, 0.32f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Title", "KAYITLI İLERLEME SİLİNECEK", 27, FontStyle.Bold,
                new Vector2(0f, 112f), new Vector2(440f, 48f), Color.white);
            CreateText(panel.transform, "Message", "Yeni oyuna başlamak istediğine emin misin?\nBu işlem geri alınamaz.",
                18, FontStyle.Normal, new Vector2(0f, 45f), new Vector2(430f, 70f),
                new Color(0.76f, 0.81f, 0.85f));

            Button confirmButton = CloneModalButton(panel.transform, "ConfirmButton", "EVET, YENİ OYUN",
                new Vector2(0f, -45f), new Color(0.72f, 0.28f, 0.22f, 1f), ConfirmNewGame);
            _cancelButton = CloneModalButton(panel.transform, "CancelButton", "VAZGEÇ",
                new Vector2(0f, -122f), new Color(0.20f, 0.48f, 0.56f, 1f), CancelNewGame);

            Navigation cancelNavigation = _cancelButton.navigation;
            cancelNavigation.mode = Navigation.Mode.Explicit;
            cancelNavigation.selectOnUp = confirmButton;
            cancelNavigation.selectOnDown = confirmButton;
            _cancelButton.navigation = cancelNavigation;

            Navigation confirmNavigation = confirmButton.navigation;
            confirmNavigation.mode = Navigation.Mode.Explicit;
            confirmNavigation.selectOnUp = _cancelButton;
            confirmNavigation.selectOnDown = _cancelButton;
            confirmButton.navigation = confirmNavigation;

            _confirmationRoot.SetActive(false);
        }

        private Button CloneModalButton(Transform parent, string name, string label, Vector2 position,
            Color color, UnityEngine.Events.UnityAction action)
        {
            Button button = Instantiate(_playButton, parent);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            SetLabel(button, label);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(390f, 58f);
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = color;
            return button;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Vector2 position, Vector2 dimensions, Color color)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = textObject.GetComponent<Text>();
            text.font = _playLabel != null ? _playLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void SetLabel(Button button, string value)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.text = value;
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

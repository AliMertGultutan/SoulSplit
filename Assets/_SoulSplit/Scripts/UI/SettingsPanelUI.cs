using SoulSplit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Ana menu ve duraklatma menusunun paylastigi kalici temel ayarlar ekrani.
    /// Durumlar renk yaninda acik metinle de gosterilir.
    /// </summary>
    public sealed class SettingsPanelUI : MonoBehaviour
    {
        private static readonly Color BackdropColor = new Color(0.01f, 0.02f, 0.035f, 0.985f);
        private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.12f, 1f);
        private static readonly Color ControlColor = new Color(0.11f, 0.16f, 0.21f, 1f);
        private static readonly Color HighlightColor = new Color(0.18f, 0.29f, 0.36f, 1f);
        private static readonly Color AccentColor = new Color(0.42f, 0.82f, 0.91f, 1f);
        private static readonly Color WarmColor = new Color(0.86f, 0.55f, 0.32f, 1f);

        private GameObject _root;
        private GameObject _returnFocus;
        private Font _font;
        private Slider _volumeSlider;
        private Slider _cameraEffectsSlider;
        private Toggle _hintsToggle;
        private Toggle _hitStopToggle;
        private Toggle _materializationToggle;
        private Toggle _fullscreenToggle;
        private Text _volumeLabel;
        private Text _cameraEffectsLabel;
        private Text _hintsLabel;
        private Text _hitStopLabel;
        private Text _materializationLabel;
        private Text _fullscreenLabel;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static SettingsPanelUI GetOrCreate()
        {
            SettingsPanelUI existing = FindAnyObjectByType<SettingsPanelUI>();
            if (existing != null) return existing;

            return new GameObject("SettingsMenu").AddComponent<SettingsPanelUI>();
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            _root.SetActive(false);
        }

        public void Open(GameObject returnFocus = null)
        {
            _returnFocus = returnFocus;
            RefreshControls();
            _root.SetActive(true);

            if (EventSystem.current != null && _volumeSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_volumeSlider.gameObject);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            _root.SetActive(false);

            if (EventSystem.current != null && _returnFocus != null && _returnFocus.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_returnFocus);
            }
        }

        private void BuildInterface()
        {
            _root = new GameObject("SettingsOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _root.transform.SetParent(transform, false);

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Stretch(_root.GetComponent<RectTransform>());
            _root.GetComponent<Image>().color = BackdropColor;

            GameObject panel = CreateUiObject("SettingsPanel", _root.transform, typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(580f, 870f);
            panel.GetComponent<Image>().color = PanelColor;
            Outline panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.48f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Title", "AYARLAR", 32, FontStyle.Bold,
                new Vector2(0f, 365f), new Vector2(500f, 52f), Color.white);
            CreateText(panel.transform, "Subtitle", "Ses, erişilebilirlik ve oynanış tercihleri", 17,
                FontStyle.Normal, new Vector2(0f, 325f), new Vector2(500f, 34f),
                new Color(0.68f, 0.75f, 0.80f));

            _volumeLabel = CreateText(panel.transform, "VolumeLabel", string.Empty, 17, FontStyle.Bold,
                new Vector2(0f, 270f), new Vector2(460f, 30f), Color.white);
            _volumeSlider = CreateSlider(panel.transform, "MasterVolumeSlider", new Vector2(0f, 230f));
            _volumeSlider.onValueChanged.AddListener(SetVolume);

            _cameraEffectsLabel = CreateText(panel.transform, "CameraEffectsLabel", string.Empty, 17,
                FontStyle.Bold, new Vector2(0f, 165f), new Vector2(460f, 30f), Color.white);
            _cameraEffectsSlider = CreateSlider(panel.transform, "CameraEffectsSlider", new Vector2(0f, 125f));
            _cameraEffectsSlider.onValueChanged.AddListener(SetCameraEffects);

            _hintsToggle = CreateToggle(panel.transform, "ContextualHintsToggle", new Vector2(0f, 55f), out _hintsLabel);
            _hintsToggle.onValueChanged.AddListener(SetHints);
            _hitStopToggle = CreateToggle(panel.transform, "HitStopToggle", new Vector2(0f, -15f), out _hitStopLabel);
            _hitStopToggle.onValueChanged.AddListener(SetHitStop);
            _materializationToggle = CreateToggle(panel.transform, "MaterializeAtSoulToggle",
                new Vector2(0f, -85f), out _materializationLabel);
            _materializationToggle.onValueChanged.AddListener(SetMaterialization);
            _fullscreenToggle = CreateToggle(panel.transform, "FullscreenToggle",
                new Vector2(0f, -155f), out _fullscreenLabel);
            _fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

            CreateButton(panel.transform, "ResetSettingsButton", "VARSAYILANLARA DÖN",
                new Vector2(0f, -240f), WarmColor, ResetDefaults);
            CreateButton(panel.transform, "BackButton", "GERİ",
                new Vector2(0f, -312f), AccentColor, Close);
            CreateText(panel.transform, "Footer", "Değişiklikler otomatik kaydedilir", 14, FontStyle.Normal,
                new Vector2(0f, -375f), new Vector2(470f, 28f), new Color(0.55f, 0.62f, 0.68f));
        }

        private void RefreshControls()
        {
            _volumeSlider.SetValueWithoutNotify(GameplaySettings.MasterVolume);
            _cameraEffectsSlider.SetValueWithoutNotify(GameplaySettings.CameraEffectsIntensity);
            _hintsToggle.SetIsOnWithoutNotify(GameplaySettings.ContextualHintsEnabled);
            _hitStopToggle.SetIsOnWithoutNotify(GameplaySettings.HitStopEnabled);
            _materializationToggle.SetIsOnWithoutNotify(GameplaySettings.MaterializeAtSoulPosition);
            _fullscreenToggle.SetIsOnWithoutNotify(GameplaySettings.Fullscreen);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _volumeLabel.text = $"ANA SES  •  {Mathf.RoundToInt(GameplaySettings.MasterVolume * 100f)}%";
            _cameraEffectsLabel.text =
                $"KAMERA EFEKTLERİ  •  {Mathf.RoundToInt(GameplaySettings.CameraEffectsIntensity * 100f)}%";
            _hintsLabel.text = GameplaySettings.ContextualHintsEnabled
                ? "OYUN İPUÇLARI  •  AÇIK"
                : "OYUN İPUÇLARI  •  KAPALI";
            _hitStopLabel.text = GameplaySettings.HitStopEnabled
                ? "VURUŞ DONMASI  •  AÇIK"
                : "VURUŞ DONMASI  •  KAPALI";
            _materializationLabel.text = GameplaySettings.MaterializeAtSoulPosition
                ? "RUHTAN DÖNÜŞ  •  RUHUN YANINDA"
                : "RUHTAN DÖNÜŞ  •  BEDENİN ESKİ YERİNDE";
            _fullscreenLabel.text = GameplaySettings.Fullscreen
                ? "TAM EKRAN  •  AÇIK"
                : "TAM EKRAN  •  KAPALI";
        }

        private void SetVolume(float value)
        {
            GameplaySettings.MasterVolume = value;
            RefreshLabels();
        }

        private void SetCameraEffects(float value)
        {
            GameplaySettings.CameraEffectsIntensity = value;
            RefreshLabels();
        }

        private void SetHints(bool enabled)
        {
            GameplaySettings.ContextualHintsEnabled = enabled;
            RefreshLabels();
        }

        private void SetHitStop(bool enabled)
        {
            GameplaySettings.HitStopEnabled = enabled;
            if (!enabled) TimeScaleController.ClearHitStop();
            RefreshLabels();
        }

        private void SetMaterialization(bool enabled)
        {
            GameplaySettings.MaterializeAtSoulPosition = enabled;
            RefreshLabels();
        }

        private void SetFullscreen(bool enabled)
        {
            GameplaySettings.Fullscreen = enabled;
            RefreshLabels();
        }

        private void ResetDefaults()
        {
            GameplaySettings.ResetAllToDefaults();
            RefreshControls();
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 position)
        {
            GameObject sliderObject = CreateUiObject(name, parent, typeof(Slider));
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = position;
            sliderRect.sizeDelta = new Vector2(460f, 44f);

            GameObject background = CreateUiObject("Background", sliderObject.transform, typeof(Image));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 10f);
            background.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.27f, 1f);

            GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
            Stretch(fillArea.GetComponent<RectTransform>());
            fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 17f);
            fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -17f);

            GameObject fill = CreateUiObject("Fill", fillArea.transform, typeof(Image));
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = AccentColor;

            GameObject handleArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
            Stretch(handleArea.GetComponent<RectTransform>());
            GameObject handle = CreateUiObject("Handle", handleArea.transform, typeof(Image), typeof(Outline));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28f, 28f);
            handle.GetComponent<Image>().color = Color.white;
            handle.GetComponent<Outline>().effectColor = AccentColor;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private Toggle CreateToggle(Transform parent, string name, Vector2 position, out Text label)
        {
            GameObject toggleObject = CreateUiObject(name, parent, typeof(Image), typeof(Toggle), typeof(Outline));
            RectTransform rect = toggleObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(460f, 56f);

            Image rowImage = toggleObject.GetComponent<Image>();
            rowImage.color = ControlColor;
            Outline outline = toggleObject.GetComponent<Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject box = CreateUiObject("Box", toggleObject.transform, typeof(Image), typeof(Outline));
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(30f, 0f);
            boxRect.sizeDelta = new Vector2(30f, 30f);
            box.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.12f, 1f);
            box.GetComponent<Outline>().effectColor = AccentColor;

            GameObject checkmark = CreateUiObject("Checkmark", box.transform, typeof(Image));
            RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
            Stretch(checkmarkRect);
            checkmarkRect.offsetMin = new Vector2(6f, 6f);
            checkmarkRect.offsetMax = new Vector2(-6f, -6f);
            checkmark.GetComponent<Image>().color = AccentColor;

            label = CreateText(toggleObject.transform, "Label", string.Empty, 16, FontStyle.Bold,
                new Vector2(42f, 0f), new Vector2(360f, 40f), Color.white);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = rowImage;
            toggle.graphic = checkmark.GetComponent<Image>();
            ColorBlock colors = toggle.colors;
            colors.normalColor = ControlColor;
            colors.highlightedColor = HighlightColor;
            colors.selectedColor = new Color(0.15f, 0.28f, 0.34f, 1f);
            colors.pressedColor = new Color(0.20f, 0.38f, 0.44f, 1f);
            colors.fadeDuration = 0.1f;
            toggle.colors = colors;
            return toggle;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Color accent, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(460f, 56f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = ControlColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ControlColor;
            colors.highlightedColor = HighlightColor;
            colors.selectedColor = new Color(accent.r * 0.42f, accent.g * 0.42f, accent.b * 0.42f, 1f);
            colors.pressedColor = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(action);
            buttonObject.GetComponent<Outline>().effectColor = accent;

            CreateText(buttonObject.transform, "Label", label, 18, FontStyle.Bold,
                Vector2.zero, rect.sizeDelta, Color.white, stretch: true);
            return button;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Vector2 position, Vector2 dimensions, Color color, bool stretch = false)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (stretch) Stretch(rect);
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = dimensions;
            }

            Text text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
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
    }
}

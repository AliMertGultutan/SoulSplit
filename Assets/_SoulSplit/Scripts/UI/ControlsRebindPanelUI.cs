using System.Collections.Generic;
using SoulSplit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Klavye kontrollerini oyun icinde yeniden atar. Her satir gorunur etiket,
    /// mevcut tus ve secim durumunu birlikte gosterir; renk tek basina bilgi tasimaz.
    /// </summary>
    public sealed class ControlsRebindPanelUI : MonoBehaviour
    {
        private readonly struct BindingSpec
        {
            public readonly string ActionName;
            public readonly string CompositePart;
            public readonly string Label;
            public readonly int Column;
            public readonly int Row;

            public BindingSpec(string actionName, string compositePart, string label, int column, int row)
            {
                ActionName = actionName;
                CompositePart = compositePart;
                Label = label;
                Column = column;
                Row = row;
            }
        }

        private sealed class BindingRow
        {
            public BindingSpec Spec;
            public InputAction Action;
            public int BindingIndex;
            public Button Button;
            public Text Text;
        }

        private static readonly BindingSpec[] Specs =
        {
            new BindingSpec("Move", "up", "YUKARI", 0, 0),
            new BindingSpec("Move", "down", "AŞAĞI", 0, 1),
            new BindingSpec("Move", "left", "SOL", 0, 2),
            new BindingSpec("Move", "right", "SAĞ", 0, 3),
            new BindingSpec("Dodge", null, "TAKLA", 0, 4),
            new BindingSpec("Jump", null, "ZIPLAMA (W ALTERNATİF)", 1, 0),
            new BindingSpec("Attack", null, "HAFİF SALDIRI", 1, 1),
            new BindingSpec("HeavyAttack", null, "AĞIR SALDIRI", 1, 2),
            new BindingSpec("SoulSwitch", null, "RUH DEĞİŞİMİ", 1, 3),
            new BindingSpec("Ultimate", null, "SOUL SURGE", 1, 4)
        };

        private static readonly Color BackdropColor = new Color(0.01f, 0.02f, 0.035f, 0.99f);
        private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.12f, 1f);
        private static readonly Color ButtonColor = new Color(0.11f, 0.16f, 0.21f, 1f);
        private static readonly Color HighlightColor = new Color(0.18f, 0.29f, 0.36f, 1f);
        private static readonly Color AccentColor = new Color(0.42f, 0.82f, 0.91f, 1f);
        private static readonly Color WarmColor = new Color(0.86f, 0.55f, 0.32f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.52f, 0.42f, 1f);

        private readonly List<BindingRow> _rows = new List<BindingRow>();
        private GameObject _root;
        private GameObject _returnFocus;
        private Font _font;
        private Text _statusText;
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private BindingRow _activeRow;
        private string _previousOverridePath;
        private bool _actionWasEnabled;
        private int _keyboardCancelFrame = -1;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool IsRebinding => _operation != null;
        public bool KeyboardCancelHandledThisFrame => _keyboardCancelFrame == Time.frameCount;

        public static ControlsRebindPanelUI GetOrCreate()
        {
            ControlsRebindPanelUI existing = FindAnyObjectByType<ControlsRebindPanelUI>();
            return existing != null
                ? existing
                : new GameObject("ControlsRebindMenu").AddComponent<ControlsRebindPanelUI>();
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            _root.SetActive(false);
        }

        public void Open(GameObject returnFocus)
        {
            _returnFocus = returnFocus;
            RefreshRows();
            SetStatus("Değiştirmek istediğin kontrolü seç", AccentColor);
            _root.SetActive(true);

            if (EventSystem.current != null && _rows.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_rows[0].Button.gameObject);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (_operation != null) CancelRebind("Tuş seçimi iptal edildi", false);
            _root.SetActive(false);

            if (EventSystem.current != null && _returnFocus != null && _returnFocus.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_returnFocus);
            }
        }

        private void StartRebind(BindingRow row)
        {
            if (_operation != null || row.Action == null || row.BindingIndex < 0) return;

            _activeRow = row;
            _previousOverridePath = row.Action.bindings[row.BindingIndex].overridePath;
            _actionWasEnabled = row.Action.enabled;
            if (_actionWasEnabled) row.Action.Disable();

            SetButtonsInteractable(false);
            row.Text.text = $"{row.Spec.Label}  •  BEKLENİYOR...";
            SetStatus("Yeni bir klavye tuşuna bas  •  ESC ile iptal", Color.white);

            _operation = row.Action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.08f)
                .OnCancel(_ => CancelRebind("Tuş seçimi iptal edildi", true))
                .OnComplete(_ => CompleteRebind());
            _operation.Start();
        }

        private void CompleteRebind()
        {
            BindingRow row = _activeRow;
            string conflict = InputBindingSettings.FindKeyboardConflict(row.Action, row.BindingIndex);
            if (!string.IsNullOrEmpty(conflict))
            {
                RestorePreviousBinding();
                FinishOperation();
                RefreshRows();
                SetStatus($"Bu tuş zaten {FriendlyActionName(conflict)} için kullanılıyor", ErrorColor);
                return;
            }

            FinishOperation();
            InputBindingSettings.SaveOverrides();
            RefreshRows();
            SetStatus($"{row.Spec.Label} kaydedildi", AccentColor);
        }

        private void CancelRebind(string message, bool keyboardCancel)
        {
            if (_operation == null) return;
            if (keyboardCancel) _keyboardCancelFrame = Time.frameCount;
            RestorePreviousBinding();
            FinishOperation();
            RefreshRows();
            SetStatus(message, WarmColor);
        }

        private void RestorePreviousBinding()
        {
            if (_activeRow?.Action == null || _activeRow.BindingIndex < 0) return;
            if (string.IsNullOrEmpty(_previousOverridePath))
                _activeRow.Action.RemoveBindingOverride(_activeRow.BindingIndex);
            else
                _activeRow.Action.ApplyBindingOverride(_activeRow.BindingIndex, _previousOverridePath);
        }

        private void FinishOperation()
        {
            InputAction action = _activeRow?.Action;
            _operation?.Dispose();
            _operation = null;
            if (_actionWasEnabled && action != null) action.Enable();
            _activeRow = null;
            _previousOverridePath = null;
            _actionWasEnabled = false;
            SetButtonsInteractable(true);
        }

        private void ResetBindings()
        {
            if (_operation != null) CancelRebind("Tuş seçimi iptal edildi", false);
            InputBindingSettings.ResetToDefaults();
            RefreshRows();
            SetStatus("Tüm klavye tuşları varsayılanlara döndürüldü", AccentColor);
        }

        private void RefreshRows()
        {
            foreach (BindingRow row in _rows)
            {
                if (InputBindingSettings.TryGetKeyboardBinding(row.Spec.ActionName, row.Spec.CompositePart,
                        out InputAction action, out int index))
                {
                    row.Action = action;
                    row.BindingIndex = index;
                    row.Text.text = $"{row.Spec.Label}  •  {InputBindingSettings.GetDisplayName(action, index)}";
                    row.Button.interactable = true;
                }
                else
                {
                    row.Action = null;
                    row.BindingIndex = -1;
                    row.Text.text = $"{row.Spec.Label}  •  ATANMADI";
                    row.Button.interactable = false;
                }
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (BindingRow row in _rows) row.Button.interactable = interactable;
        }

        private void SetStatus(string value, Color color)
        {
            if (_statusText == null) return;
            _statusText.text = value;
            _statusText.color = color;
        }

        private void BuildInterface()
        {
            _root = new GameObject("ControlsRebindOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _root.transform.SetParent(transform, false);

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Stretch(_root.GetComponent<RectTransform>());
            _root.GetComponent<Image>().color = BackdropColor;

            GameObject panel = CreateUiObject("ControlsPanel", _root.transform, typeof(Image), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(940f, 900f);
            panel.GetComponent<Image>().color = PanelColor;
            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Title", "KLAVYE KONTROLLERİ", 31, FontStyle.Bold,
                new Vector2(0f, 380f), new Vector2(820f, 50f), Color.white);
            CreateText(panel.transform, "Subtitle", "Bir kontrol seç, ardından kullanmak istediğin tuşa bas", 17,
                FontStyle.Normal, new Vector2(0f, 340f), new Vector2(820f, 34f),
                new Color(0.68f, 0.75f, 0.80f));

            CreateText(panel.transform, "MovementHeader", "HAREKET", 18, FontStyle.Bold,
                new Vector2(-215f, 285f), new Vector2(390f, 34f), AccentColor);
            CreateText(panel.transform, "ActionHeader", "EYLEMLER", 18, FontStyle.Bold,
                new Vector2(215f, 285f), new Vector2(390f, 34f), AccentColor);

            foreach (BindingSpec spec in Specs)
            {
                float x = spec.Column == 0 ? -215f : 215f;
                float y = 225f - spec.Row * 68f;
                BindingRow row = CreateBindingRow(panel.transform, spec, new Vector2(x, y));
                _rows.Add(row);
                BindingRow captured = row;
                row.Button.onClick.AddListener(() => StartRebind(captured));
            }

            _statusText = CreateText(panel.transform, "Status", string.Empty, 17, FontStyle.Bold,
                new Vector2(0f, -145f), new Vector2(820f, 42f), AccentColor);
            CreateButton(panel.transform, "ResetBindingsButton", "TUŞLARI VARSAYILANA DÖNDÜR",
                new Vector2(0f, -220f), WarmColor, ResetBindings);
            CreateButton(panel.transform, "BackButton", "GERİ",
                new Vector2(0f, -292f), AccentColor, Close);
            CreateText(panel.transform, "Footer", "Değişiklikler otomatik kaydedilir", 14,
                FontStyle.Normal, new Vector2(0f, -370f), new Vector2(620f, 28f),
                new Color(0.55f, 0.62f, 0.68f));
        }

        private BindingRow CreateBindingRow(Transform parent, BindingSpec spec, Vector2 position)
        {
            Button button = CreateButton(parent, $"Rebind_{spec.ActionName}_{spec.CompositePart ?? "Primary"}",
                string.Empty, position, AccentColor, null, new Vector2(390f, 56f));
            Text text = button.GetComponentInChildren<Text>(true);
            text.fontSize = 16;
            return new BindingRow { Spec = spec, Button = button, Text = text, BindingIndex = -1 };
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 position,
            Color accent, UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size ?? new Vector2(520f, 56f);
            buttonObject.GetComponent<Image>().color = ButtonColor;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = HighlightColor;
            colors.selectedColor = new Color(accent.r * 0.42f, accent.g * 0.42f, accent.b * 0.42f, 1f);
            colors.pressedColor = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f);
            colors.disabledColor = new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.42f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            buttonObject.GetComponent<Outline>().effectColor = new Color(accent.r, accent.g, accent.b, 0.72f);

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

        private static string FriendlyActionName(string actionName)
        {
            switch (actionName)
            {
                case "Move": return "HAREKET";
                case "Jump": return "ZIPLAMA";
                case "Attack": return "HAFİF SALDIRI";
                case "HeavyAttack": return "AĞIR SALDIRI";
                case "SoulSwitch": return "RUH DEĞİŞİMİ";
                case "Ultimate": return "SOUL SURGE";
                case "Dodge": return "TAKLA";
                default: return actionName.ToUpperInvariant();
            }
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
            if (_operation != null) CancelRebind("Tuş seçimi iptal edildi", false);
        }
    }
}

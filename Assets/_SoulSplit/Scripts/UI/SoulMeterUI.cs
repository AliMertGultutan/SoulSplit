using UnityEngine;
using UnityEngine.UI;
using SoulSplit.Player;
using SoulSplit.Core;

namespace SoulSplit.UI
{
    /// <summary>
    /// Ruh enerjisi bari. Tek is: SoulSwitchManager'daki degeri ekrana yansitmak.
    /// Doldurma islemi RectTransform olcegiyle yapiliyor; fill sprite'in
    /// pivotu SOLDA olmali (0, 0.5).
    /// </summary>
    public class SoulMeterUI : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private SoulSwitchManager switchManager;
        [Tooltip("Dolan kisim. Pivotu sol kenarda olmali.")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;

        [Header("Renkler")]
        [Tooltip("Ruh formundayken normal tukenme rengi.")]
        [SerializeField] private Color normalColor = new Color(0.42f, 0.80f, 0.90f, 1f);
        [Tooltip("Uzaklasip tukenme hizlandigindaki renk.")]
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.35f, 0.30f, 1f);
        [Tooltip("Bedendeyken (dolarken) renk.")]
        [SerializeField] private Color rechargeColor = new Color(0.55f, 0.60f, 0.68f, 1f);

        [Header("Davranis")]
        [Tooltip("Barin degere yetisme yumusakligi. 0 = anlik.")]
        [SerializeField] private float smoothing = 0.08f;
        [Tooltip("Ayrilmaya yetmeyen enerjide bar yanip sonsun.")]
        [SerializeField] private float lowEnergyBlinkSpeed = 6f;

        private float _displayed;
        private RectTransform _ultimateFill;
        private Image _ultimateFillImage;
        private Text _ultimateStateText;
        private float _displayedUltimate;

        private static readonly Color UltimateChargingColor = new Color(0.55f, 0.32f, 0.82f, 1f);
        private static readonly Color UltimateReadyColor = new Color(1f, 0.77f, 0.24f, 1f);
        private static readonly Color UltimateActiveColor = new Color(0.86f, 0.40f, 1f, 1f);

        private void Awake()
        {
            BuildUltimateMeter();
        }

        private void Update()
        {
            if (switchManager == null || fill == null) return;

            float target = switchManager.EnergyNormalized;
            _displayed = smoothing <= 0f
                ? target
                : Mathf.Lerp(_displayed, target, 1f - Mathf.Exp(-Time.deltaTime / smoothing));

            fill.localScale = new Vector3(Mathf.Clamp01(_displayed), 1f, 1f);

            UpdateUltimateMeter();

            if (fillImage == null) return;

            Color color;
            if (switchManager.IsSoulActive)
            {
                float danger = Mathf.InverseLerp(1f, 3f, switchManager.CurrentDrainMultiplier);
                color = Color.Lerp(normalColor, dangerColor, danger);
            }
            else
            {
                // Bedende ve henuz ayrilamayacak durumdaysak yanip sonerek belli et.
                color = switchManager.CanSeparate ? normalColor : rechargeColor;
                if (!switchManager.CanSeparate)
                {
                    float blink = (Mathf.Sin(Time.time * lowEnergyBlinkSpeed) + 1f) * 0.5f;
                    color.a = Mathf.Lerp(0.35f, 1f, blink);
                }
            }
            fillImage.color = color;
        }

        private void UpdateUltimateMeter()
        {
            if (_ultimateFill == null || _ultimateFillImage == null || _ultimateStateText == null) return;

            float target = switchManager.IsUltimateActive
                ? switchManager.UltimateTimeNormalized
                : switchManager.UltimateChargeNormalized;
            _displayedUltimate = smoothing <= 0f
                ? target
                : Mathf.Lerp(_displayedUltimate, target,
                    1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.001f, smoothing)));
            _ultimateFill.localScale = new Vector3(Mathf.Clamp01(_displayedUltimate), 1f, 1f);
            string flow = switchManager.ComboCount >= 2 ? $"  •  AKIŞ x{switchManager.ComboCount}" : string.Empty;

            if (switchManager.IsUltimateActive)
            {
                _ultimateFillImage.color = UltimateActiveColor;
                _ultimateStateText.text = $"SOUL SURGE  {switchManager.UltimateSecondsRemaining:0.0}s{flow}";
            }
            else if (switchManager.UltimateReady)
            {
                float pulse = 0.78f + (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.11f;
                _ultimateFillImage.color = UltimateReadyColor * pulse;
                _ultimateFillImage.color = new Color(
                    _ultimateFillImage.color.r, _ultimateFillImage.color.g, _ultimateFillImage.color.b, 1f);
                string ultimateKey = InputBindingSettings.GetKeyboardDisplayName("Ultimate", fallback: "Q");
                _ultimateStateText.text = $"SOUL SURGE HAZIR  [{ultimateKey}]{flow}";
            }
            else
            {
                _ultimateFillImage.color = UltimateChargingColor;
                _ultimateStateText.text =
                    $"SOUL SURGE  %{Mathf.RoundToInt(switchManager.UltimateChargeNormalized * 100f)}{flow}";
            }
        }

        private void BuildUltimateMeter()
        {
            Transform parent = transform.parent;
            if (parent == null) return;

            Transform existing = parent.Find("UltimateMeter_BG");
            if (existing != null)
            {
                _ultimateFill = existing.Find("UltimateMeter_Fill") as RectTransform;
                _ultimateFillImage = _ultimateFill != null ? _ultimateFill.GetComponent<Image>() : null;
                _ultimateStateText = existing.GetComponentInChildren<Text>(true);
                return;
            }

            GameObject background = new GameObject("UltimateMeter_BG", typeof(RectTransform), typeof(Image), typeof(Outline));
            background.transform.SetParent(parent, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = Vector2.zero;
            backgroundRect.pivot = Vector2.zero;
            backgroundRect.anchoredPosition = new Vector2(48f, 12f);
            backgroundRect.sizeDelta = new Vector2(360f, 24f);
            background.GetComponent<Image>().color = new Color(0.04f, 0.025f, 0.075f, 0.88f);
            background.GetComponent<Image>().raycastTarget = false;
            Outline outline = background.GetComponent<Outline>();
            outline.effectColor = new Color(0.68f, 0.40f, 0.92f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject fillObject = new GameObject("UltimateMeter_Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(background.transform, false);
            _ultimateFill = fillObject.GetComponent<RectTransform>();
            _ultimateFill.anchorMin = Vector2.zero;
            _ultimateFill.anchorMax = Vector2.one;
            _ultimateFill.pivot = new Vector2(0f, 0.5f);
            _ultimateFill.offsetMin = new Vector2(3f, 3f);
            _ultimateFill.offsetMax = new Vector2(-3f, -3f);
            _ultimateFillImage = fillObject.GetComponent<Image>();
            _ultimateFillImage.color = UltimateChargingColor;
            _ultimateFillImage.raycastTarget = false;
            _ultimateFill.localScale = new Vector3(0f, 1f, 1f);

            GameObject textObject = new GameObject("UltimateState", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(background.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
            _ultimateStateText = textObject.GetComponent<Text>();
            _ultimateStateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _ultimateStateText.fontSize = 18;
            _ultimateStateText.resizeTextForBestFit = true;
            _ultimateStateText.resizeTextMinSize = 13;
            _ultimateStateText.resizeTextMaxSize = 18;
            _ultimateStateText.fontStyle = FontStyle.Bold;
            _ultimateStateText.alignment = TextAnchor.MiddleCenter;
            _ultimateStateText.color = Color.white;
            _ultimateStateText.raycastTarget = false;
        }
    }
}

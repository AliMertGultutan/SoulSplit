using SoulSplit.Core;
using SoulSplit.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SoulSplit.UI
{
    /// <summary>
    /// Kontrolleri ihtiyaç duyuldukları bölgede, kısa ve aşamalı olarak öğretir.
    /// Seviye konumları kasıtlı olarak burada tutulur; prototip sahnesinin eğitim
    /// akışını tek bakışta ayarlamayı kolaylaştırır.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ContextualHintUI : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Transform player;
        [SerializeField] private SoulSwitchManager switchManager;
        [SerializeField] private Text hintText;

        [Header("Geçiş")]
        [SerializeField, Min(0f)] private float fadeSpeed = 7f;

        private CanvasGroup _canvasGroup;
        private string _currentHint;

        private const string KeyColor = "#A9ECFF";

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (hintText == null) hintText = GetComponentInChildren<Text>(true);
            if (switchManager == null) switchManager = FindAnyObjectByType<SoulSwitchManager>();
            if (player == null && switchManager != null) player = switchManager.BodyTransform;

            if (player == null || hintText == null)
            {
                Debug.LogError("[ContextualHintUI] Oyuncu veya metin referansı eksik; ipuçları devre dışı.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!GameplaySettings.ContextualHintsEnabled)
            {
                _currentHint = null;
                hintText.text = string.Empty;
                _canvasGroup.alpha = Mathf.MoveTowards(
                    _canvasGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
                return;
            }

            Transform activeForm = switchManager != null && switchManager.IsSoulActive
                ? switchManager.SoulTransform
                : player;
            string nextHint = GetHint(activeForm != null ? activeForm.position.x : player.position.x);
            if (nextHint != _currentHint)
            {
                _currentHint = nextHint;
                hintText.text = nextHint ?? string.Empty;
            }

            float targetAlpha = string.IsNullOrEmpty(nextHint) ? 0f : 1f;
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                targetAlpha,
                fadeSpeed * Time.unscaledDeltaTime);
        }

        private string GetHint(float x)
        {
            string left = Key("Move", "left", "A");
            string right = Key("Move", "right", "D");
            string down = Key("Move", "down", "S");
            string jump = Key("Jump", fallback: "SPACE");
            if (!jump.Contains("W")) jump += " / W";
            string lightAttack = Key("Attack", fallback: "J");
            string heavyAttack = Key("HeavyAttack", fallback: "K");
            string soulSwitch = Key("SoulSwitch", fallback: "E");
            string ultimate = Key("Ultimate", fallback: "Q");
            string dodge = Key("Dodge", fallback: "LEFT SHIFT");

            if (x < 3.5f)
                return $"HAREKET  {left} {right}     ZIPLA  {jump}";

            if (x < 9.5f)
                return $"ALÇAK GEÇİT     {down} BASILI TUT";

            if (x < 15f)
                return $"TAKLA     {dodge}  •  DAR GEÇİTLERDEN GEÇER";

            if (x < 22f)
                return $"SALDIRI  {lightAttack}     AĞIR SALDIRI  {heavyAttack}";

            if (x < 34f)
                return $"SOUL SURGE'U VURUŞLARLA DOLDUR     HAZIR OLUNCA {ultimate}";

            if (x >= 34f && x < 48f)
            {
                return $"RUHU AYIR / BEDENE DÖN  {soulSwitch}     BEDEN BIRAKTIĞIN YERDE KALIR";
            }

            if (x >= 56f && x < 74f)
                return $"ÇİFT ZIPLAMA     {jump} {jump}";

            if (x >= 110f && x < 145f)
            {
                return switchManager != null && switchManager.IsSoulActive
                    ? $"RUH SALDIRISI  {lightAttack}     BURADA BEDENLEŞ  {soulSwitch}"
                    : $"HAYALETLERE ULAŞMAK İÇİN RUHA GEÇ  {soulSwitch}";
            }

            if (x >= 156f && x < 178f)
                return $"RUH KAPISI     RUHA GEÇ {soulSwitch}  •  KARŞI TARAFTA BEDENLEŞ {soulSwitch}";

            if (x >= 192f && x < 208f)
                return $"KIRIK KÖPRÜ     ÇİFT ZIPLA {jump} {jump}";

            if (x >= 208f && x < 224f)
                return $"SON AVLUNDA RUHANİ DÜŞMANLARI MUHAFIZLARA KARŞI KULLAN";

            return null;
        }

        private static string Key(string actionName, string compositePart = null, string fallback = "ATANMADI")
        {
            string displayName = InputBindingSettings.GetKeyboardDisplayName(
                actionName, compositePart, fallback);
            return $"<color={KeyColor}><b>[{displayName}]</b></color>";
        }

        private void OnValidate()
        {
            fadeSpeed = Mathf.Max(0f, fadeSpeed);
        }
    }
}

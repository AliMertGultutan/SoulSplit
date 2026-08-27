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
            if (x < 3.5f)
                return $"HAREKET  <color={KeyColor}><b>[A] [D]</b></color>     ZIPLA  <color={KeyColor}><b>[SPACE]</b></color>";

            if (x < 9.5f)
                return $"ALÇAK GEÇİT     <color={KeyColor}><b>[S]</b></color> BASILI TUT";

            if (x < 22f)
                return $"SALDIRI  <color={KeyColor}><b>[J]</b></color>     AĞIR SALDIRI  <color={KeyColor}><b>[K]</b></color>";

            if (x < 34f)
                return $"SOUL SURGE'U VURUŞLARLA DOLDUR     HAZIR OLUNCA <color={KeyColor}><b>[Q]</b></color>";

            if (x >= 34f && x < 48f)
            {
                string returnRule = GameplaySettings.MaterializeAtSoulPosition
                    ? "BEDEN RUHUN BULUNDUĞU YERDE OLUŞUR"
                    : "BEDEN BIRAKTIĞIN YERDE KALIR";
                return $"RUHU AYIR / BEDENLEŞ  <color={KeyColor}><b>[E]</b></color>     {returnRule}";
            }

            if (x >= 56f && x < 74f)
                return $"ÇİFT ZIPLAMA     <color={KeyColor}><b>[SPACE] [SPACE]</b></color>";

            if (x >= 110f && x < 145f)
            {
                return switchManager != null && switchManager.IsSoulActive
                    ? $"RUH SALDIRISI  <color={KeyColor}><b>[J]</b></color>     BURADA BEDENLEŞ  <color={KeyColor}><b>[E]</b></color>"
                    : $"HAYALETLERE ULAŞMAK İÇİN RUHA GEÇ  <color={KeyColor}><b>[E]</b></color>";
            }

            if (x >= 156f && x < 178f)
                return $"RUH KAPISI     RUHA GEÇ <color={KeyColor}><b>[E]</b></color>  •  KARŞI TARAFTA BEDENLEŞ <color={KeyColor}><b>[E]</b></color>";

            if (x >= 192f && x < 208f)
                return $"KIRIK KÖPRÜ     ÇİFT ZIPLA <color={KeyColor}><b>[SPACE] [SPACE]</b></color>";

            if (x >= 208f && x < 224f)
                return $"SON AVLUNDA RUHANİ DÜŞMANLARI MUHAFIZLARA KARŞI KULLAN";

            return null;
        }

        private void OnValidate()
        {
            fadeSpeed = Mathf.Max(0f, fadeSpeed);
        }
    }
}

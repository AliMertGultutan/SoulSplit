using UnityEngine;
using UnityEngine.UI;
using SoulSplit.Player;

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

        private void Update()
        {
            if (switchManager == null || fill == null) return;

            float target = switchManager.EnergyNormalized;
            _displayed = smoothing <= 0f
                ? target
                : Mathf.Lerp(_displayed, target, 1f - Mathf.Exp(-Time.deltaTime / smoothing));

            fill.localScale = new Vector3(Mathf.Clamp01(_displayed), 1f, 1f);

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
    }
}

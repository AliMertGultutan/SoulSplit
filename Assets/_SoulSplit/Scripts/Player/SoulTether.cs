using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Beden ile ruh arasindaki gorsel bag.
    /// Sadece cizim yapar; hicbir kural veya sinir uygulamaz.
    /// Rengi tukenme hizina gore degistigi icin oyuncu "cok uzaklastim"
    /// bilgisini bara bakmadan, cevresel olarak da alir.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SoulTether : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private SoulSwitchManager switchManager;
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private Transform soulTransform;

        [Header("Sekil")]
        [Tooltip("Cizgideki nokta sayisi. Fazlasi daha yumusak dalga demek.")]
        [SerializeField] private int pointCount = 16;
        [Tooltip("Cizginin yana salinim genligi (dunya birimi).")]
        [SerializeField] private float waveAmplitude = 0.25f;
        [Tooltip("Salinimin hizi.")]
        [SerializeField] private float waveSpeed = 3f;
        [Tooltip("Mesafe boyunca kac dalga olusacagi.")]
        [SerializeField] private float waveCount = 1.5f;

        [Header("Renk")]
        [Tooltip("Guvenli mesafedeki renk.")]
        [SerializeField] private Color safeColor = new Color(0.42f, 0.80f, 0.90f, 0.55f);
        [Tooltip("Tukenme hizlandigindaki renk.")]
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.35f, 0.30f, 0.85f);
        [Tooltip("Bag kalinligi: beden ucu ve ruh ucu.")]
        [SerializeField] private float startWidth = 0.10f;
        [SerializeField] private float endWidth = 0.04f;

        private LineRenderer _line;
        private float _phase;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = Mathf.Max(2, pointCount);
            _line.startWidth = startWidth;
            _line.endWidth = endWidth;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.enabled = visible;
        }

        private void LateUpdate()
        {
            if (!_line.enabled || bodyTransform == null || soulTransform == null) return;

            _phase += Time.deltaTime * waveSpeed;

            Vector3 start = bodyTransform.position;
            Vector3 end = soulTransform.position;
            Vector3 direction = end - start;
            // Dalgalarin cizgiye dik yonde olmasi icin normal vektor.
            Vector3 normal = new Vector3(-direction.y, direction.x, 0f).normalized;

            int count = _line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                // Uclar sabit dursun, salinim ortada en genis olsun.
                float envelope = Mathf.Sin(t * Mathf.PI);
                float offset = Mathf.Sin(t * Mathf.PI * 2f * waveCount + _phase) * waveAmplitude * envelope;
                _line.SetPosition(i, Vector3.Lerp(start, end, t) + normal * offset);
            }

            // Tukenme carpanina gore renk: guvenliden tehlikeliye.
            float danger = switchManager != null
                ? Mathf.InverseLerp(1f, 3f, switchManager.CurrentDrainMultiplier)
                : 0f;
            Color color = Color.Lerp(safeColor, dangerColor, danger);
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, color.a * 0.4f);
        }
    }
}

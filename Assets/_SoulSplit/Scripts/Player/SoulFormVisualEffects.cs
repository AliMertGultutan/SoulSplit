using UnityEngine;
using UnityEngine.Rendering;

namespace SoulSplit.Player
{
    /// <summary>
    /// Ruh formundayken ekrana soguk bir filtre uygular (URP Global Volume).
    /// Profilin ICERIGINE (Color Adjustments, Vignette vb.) hic dokunmuyor,
    /// sadece Volume.weight degerini SoulSwitchManager.IsSoulActive'e gore
    /// 0-1 arasi yumusakca kaydiriyor. Boylece profil tasarimi Unity
    /// Editor'de serbestce degistirilebilir, kod hep ayni kalir.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class SoulFormVisualEffects : MonoBehaviour
    {
        [SerializeField] private SoulSwitchManager switchManager;
        [Tooltip("Filtrenin acilma/kapanma hizi. Yuksek = daha ani gecis.")]
        [SerializeField] private float fadeSpeed = 4f;

        private Volume _volume;

        private void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.weight = 0f;
        }

        private void Update()
        {
            if (switchManager == null) return;

            float target = switchManager.IsSoulActive ? 1f : 0f;
            _volume.weight = Mathf.Lerp(_volume.weight, target, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
        }
    }
}

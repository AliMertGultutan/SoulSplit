using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Bedenin gorsel katmani. Tek is: bakis yonune gore sprite'i cevirmek.
    /// Ruh formunun kendi objesi ve kendi gorseli var (bkz. SoulController),
    /// o yuzden burada form degistirme mantigi yok.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerController controller;

        [Header("Ayarlar")]
        [Tooltip("Sprite kaynak gorselde saga bakiyorsa false birak.")]
        [SerializeField] private bool spriteFacesLeft;

        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (controller == null) return;

            bool faceRight = controller.FacingDirection > 0;
            _renderer.flipX = spriteFacesLeft ? faceRight : !faceRight;
        }
    }
}

using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Egilirken gorseli collider yuksekligine uydurur.
    ///
    /// NEDEN GEREKLI: Knight sprite setinde gercek bir "egilme" karesi yok;
    /// en yakini Protect (siper alma) pozu ve o da neredeyse tam boy (60px,
    /// ayaktaki 64px'e karsi). Sadece sprite degistirmek yetmiyor cunku
    /// egilme tunelinin acikligi 1.25 birim, ayaktaki karakter ise 1.8 birim —
    /// gorsel tavana gomulurdu. Bu yuzden sprite degisimine EK OLARAK dikey
    /// olcek de kuculuyor.
    ///
    /// NEDEN AYRI BILESEN: PlayerAnimatorBridge bilincli olarak transform'a
    /// dokunmuyor (sozlesmesi bu). Sprite-sheet Animator'i da sadece m_Sprite
    /// suruyor, transform'a karismiyor — dolayisiyla burasi transform'un tek
    /// sahibi, cakisma yok.
    ///
    /// PIVOT MATEMATIGI: Pivot karakterin DIKEY MERKEZINDE. Duz olcekleme
    /// ayaklari yerden kaldirirdi; bu yuzden olcekle birlikte konum da
    /// telafi ediliyor:  py = feetOffset * (1 - scale)
    /// </summary>
    public class PlayerCrouchVisual : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Bos birakilirsa ust objelerde aranir.")]
        [SerializeField] private PlayerController controller;

        [Header("Egilme Olcegi")]
        [Tooltip("Egilirken gorselin dikey olcegi. PlayerController'daki " +
                 "crouchColliderHeightMultiplier ile uyumlu olmali ki gorsel " +
                 "collider'dan tasmasin.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float crouchScaleY = 0.587f;
        [Tooltip("Gecis hizi. Yuksek = daha ani.")]
        [SerializeField] private float transitionSpeed = 16f;

        [Header("Hizalama")]
        [Tooltip("Ayaklarin obje merkezine gore dikey konumu. 0 birakilirsa collider'dan hesaplanir.")]
        [SerializeField] private float feetOffsetY = 0f;

        private float _feetOffset;
        private float _currentScale = 1f;
        private Vector3 _baseLocalPosition;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            _baseLocalPosition = transform.localPosition;

            _feetOffset = feetOffsetY;
            if (Mathf.Approximately(_feetOffset, 0f) && controller != null)
            {
                var capsule = controller.GetComponent<CapsuleCollider2D>();
                if (capsule != null) _feetOffset = capsule.offset.y - capsule.size.y * 0.5f;
            }
        }

        private void LateUpdate()
        {
            if (controller == null) return;

            float target = controller.IsCrouching ? crouchScaleY : 1f;
            _currentScale = Mathf.Lerp(_currentScale, target,
                1f - Mathf.Exp(-transitionSpeed * Time.deltaTime));

            Vector3 scale = transform.localScale;
            scale.y = _currentScale;
            transform.localScale = scale;

            // Ayaklar yerde sabit kalsin diye olcek telafisi.
            Vector3 pos = _baseLocalPosition;
            pos.y += _feetOffset * (1f - _currentScale);
            transform.localPosition = pos;
        }
    }
}

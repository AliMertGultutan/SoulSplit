using UnityEngine;

namespace SoulSplit.SpriteSheetReference
{
    /// <summary>
    /// REFERANS SCRIPT — sprite sheet indirmek YERINE tek kare statik PNG'yi
    /// kod ile hareketlendirmek icin genel amacli, bagimsiz bir ornek.
    ///
    /// NOT: SoulSplit'in kendi oyuncusu icin bu ISTE ZATEN VAR ve cok daha
    /// gelismis: SoulSplit.Player.PlayerProceduralAnimator (adim temposu,
    /// ayak basisi, hava uzamasi, inis darbesi, 4 fazli saldiri overlay'i,
    /// yay-sonumlu govde egimi). Bu dosya onun YERINE gecmez; herhangi bir
    /// statik-sprite objeye (yeni bir dusman tipi, bir NPC, vb.) hizlica
    /// "canlilik" katmak isteyen, DIStan bagimsiz, kucuk bir referans.
    ///
    /// DOTween GEREKMEZ — sadece Mathf.Sin/Lerp ile.
    /// </summary>
    public class SimpleSquashStretchAnimator : MonoBehaviour
    {
        [Header("Bekleme (Idle) Nefesi")]
        [SerializeField] private float idleBreathAmount = 0.04f;
        [SerializeField] private float idleBreathSpeed = 2f;

        [Header("Hareket Egimi")]
        [SerializeField] private float moveTiltDegrees = 8f;
        [SerializeField] private float tiltSmoothing = 10f;

        [Header("Ziplama Squash & Stretch")]
        [SerializeField] private float jumpStretchAmount = 0.25f;
        [SerializeField] private float landSquashAmount = 0.3f;
        [SerializeField] private float squashRecoverySpeed = 8f;

        [Header("Saldiri Lunge")]
        [SerializeField] private float attackLungeDistance = 0.3f;
        [SerializeField] private float attackDuration = 0.18f;

        private Rigidbody2D _rb;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private float _breathPhase;
        private float _currentTilt;
        private float _currentStretch;
        private bool _wasGrounded = true;
        private float _attackTimer;
        private int _facing = 1;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool grounded = IsGrounded();
            float velocityX = _rb != null ? _rb.linearVelocity.x : 0f;
            float velocityY = _rb != null ? _rb.linearVelocity.y : 0f;

            // Inis anini yakala: havadaydik, simdi yerdeyiz -> squash.
            if (grounded && !_wasGrounded)
            {
                _currentStretch = -landSquashAmount;
            }
            _wasGrounded = grounded;

            // Havadaykenki dikey hiza gore surekli uzama/ezilme.
            if (!grounded)
            {
                _currentStretch = Mathf.Clamp(velocityY * 0.02f, -jumpStretchAmount, jumpStretchAmount);
            }
            else if (_currentStretch != 0f)
            {
                _currentStretch = Mathf.Lerp(_currentStretch, 0f, dt * squashRecoverySpeed);
            }

            // Bekleme nefesi: sadece durgunken belirgin olsun.
            float breath = 0f;
            if (grounded && Mathf.Abs(velocityX) < 0.1f)
            {
                _breathPhase += dt * idleBreathSpeed;
                breath = Mathf.Sin(_breathPhase) * idleBreathAmount;
            }

            float scaleY = 1f + _currentStretch + breath;
            float scaleX = 1f / Mathf.Max(0.4f, scaleY); // hacim korunumu hissi
            transform.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);

            // Hareket yonune hafif egim, yumusak gecisle.
            float targetTilt = Mathf.Clamp(-velocityX * 2f, -moveTiltDegrees, moveTiltDegrees);
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, dt * tiltSmoothing);
            transform.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);

            if (Mathf.Abs(velocityX) > 0.1f) _facing = velocityX > 0f ? 1 : -1;

            if (_attackTimer > 0f)
            {
                _attackTimer -= dt;
                float t = 1f - Mathf.Clamp01(_attackTimer / attackDuration);
                float lunge = Mathf.Sin(t * Mathf.PI) * attackLungeDistance; // git-gel egrisi
                transform.localPosition = _basePosition + new Vector3(lunge * _facing, 0f, 0f);
            }
            else
            {
                transform.localPosition = _basePosition;
            }
        }

        /// <summary>Saldiri tetiklendiginde disaridan cagirin (ornegin input handler'dan).</summary>
        public void TriggerAttackLunge()
        {
            _attackTimer = attackDuration;
        }

        private bool IsGrounded()
        {
            // Basit versiyon: gercek projede PlayerController.IsGrounded gibi
            // dogru bir zemin kontrolu kullanin. Burada dikey hiz sifira
            // yakinsa "yerde" sayiyoruz — kaba ama bagimsiz bir varsayilan.
            return _rb == null || Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
        }
    }
}

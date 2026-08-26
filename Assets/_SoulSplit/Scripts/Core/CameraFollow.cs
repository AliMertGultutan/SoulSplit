using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Basit yumusak kamera takibi + screen shake.
    /// Hedef, ruh/beden arasi form degisiminde SoulSwitchManager tarafindan
    /// degistirilir (SetTarget). Shake, vurus alan/veren her yerden
    /// static ShakeCamera() ile tetiklenebilir; tek kamera oldugu icin
    /// singleton referansa gerek yok.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Hedef")]
        [SerializeField] private Transform target;

        [Header("Takip Ayarlari")]
        [Tooltip("Hedefe yetisme suresi. Buyuk deger = daha gevsek, gecikmeli kamera.")]
        [SerializeField] private float smoothTime = 0.15f;
        [Tooltip("Kameranin hedefe gore kaymasi.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
        [Tooltip("Bakis yonune dogru ekstra kaydirma; oyuncu gidecegi yeri daha iyi gorur.")]
        [SerializeField] private float lookAheadDistance = 2f;

        private static CameraFollow _instance;

        // SmoothDamp SADECE bu "gercek" konum uzerinde calisir; shake asla
        // buna karismaz — yoksa yumusatma sarsintiyi soner/bozar.
        private Vector3 _smoothedPosition;
        private Vector3 _velocity;
        private float _currentLookAhead;

        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeMagnitude;
        private Vector2 _shakeOffset;

        private Camera _camera;
        private float _baseOrthoSize;
        private float _punchTimer;
        private float _punchDuration;
        private float _punchStrength;

        private void Awake()
        {
            _instance = this;
            _smoothedPosition = transform.position;
            _camera = GetComponent<Camera>();
            if (_camera != null) _baseOrthoSize = _camera.orthographicSize;
        }

        /// <summary>Ruh/beden formu degisince cagrilir.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        /// <summary>Herhangi bir yerden cagrilabilen kamera sarsintisi.</summary>
        public static void ShakeCamera(float magnitude, float duration)
        {
            if (_instance == null) return;
            // Daha guclu sarsinti devam edeni ezsin; zayifi ezmesin.
            if (_instance._shakeTimer > 0f && magnitude < _instance._shakeMagnitude) return;
            _instance._shakeMagnitude = magnitude;
            _instance._shakeDuration = duration;
            _instance._shakeTimer = duration;
        }

        /// <summary>
        /// Kisa bir yakinlastirma darbesi (zoom-punch). Hit-stop ile ayni anda
        /// tetiklenince "bu vurus onemliydi" hissini guclendirir — donma +
        /// ani yakinlastirma kombinasyonu klasik bir "juice" tekniği.
        /// </summary>
        public static void PunchZoom(float strength, float duration)
        {
            if (_instance == null || _instance._camera == null) return;
            if (_instance._punchTimer > 0f && strength < _instance._punchStrength) return;
            _instance._punchStrength = strength;
            _instance._punchDuration = duration;
            _instance._punchTimer = duration;
        }

        private void LateUpdate()
        {
            UpdateShake();
            UpdatePunchZoom();

            if (target != null)
            {
                // Hedefin yatay hareket yonune gore ileri bakis.
                float desiredLookAhead = 0f;
                if (target.TryGetComponent(out Rigidbody2D targetBody))
                {
                    if (Mathf.Abs(targetBody.linearVelocity.x) > 0.5f)
                    {
                        desiredLookAhead = Mathf.Sign(targetBody.linearVelocity.x) * lookAheadDistance;
                    }
                }
                _currentLookAhead = Mathf.Lerp(_currentLookAhead, desiredLookAhead, Time.deltaTime * 3f);

                Vector3 desired = new Vector3(
                    target.position.x + offset.x + _currentLookAhead,
                    target.position.y + offset.y,
                    _smoothedPosition.z);

                _smoothedPosition = Vector3.SmoothDamp(_smoothedPosition, desired, ref _velocity, smoothTime);
            }

            transform.position = _smoothedPosition + (Vector3)_shakeOffset;
        }

        /// <summary>
        /// Sarsinti hesaplamasi Time.unscaledDeltaTime kullanir; hit-stop
        /// oyunu dondurse bile darbe hissi kesilmesin diye.
        /// </summary>
        private void UpdateShake()
        {
            if (_shakeTimer <= 0f)
            {
                _shakeOffset = Vector2.zero;
                return;
            }

            _shakeTimer -= Time.unscaledDeltaTime;
            float falloff = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.001f, _shakeDuration));
            _shakeOffset = Random.insideUnitCircle * _shakeMagnitude * falloff *
                           GameplaySettings.CameraEffectsIntensity;
        }

        /// <summary>Gercek zamanli calisir; hit-stop sirasinda bile ani yakinlastirma hissedilsin diye.</summary>
        private void UpdatePunchZoom()
        {
            if (_camera == null) return;

            if (_punchTimer <= 0f)
            {
                _camera.orthographicSize = _baseOrthoSize;
                return;
            }

            _punchTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_punchTimer / Mathf.Max(0.001f, _punchDuration));
            // Hizli icine gir, yavas geri don (ease-out) — darbe hissi.
            float eased = t * t;
            _camera.orthographicSize = _baseOrthoSize - _punchStrength * eased *
                                       GameplaySettings.CameraEffectsIntensity;
        }
    }
}

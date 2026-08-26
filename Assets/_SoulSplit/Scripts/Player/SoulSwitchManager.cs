using UnityEngine;
using SoulSplit.Core;
using SoulSplit.Combat;

namespace SoulSplit.Player
{
    /// <summary>
    /// Oyunun merkez mekanigi. Hangi formun aktif oldugunu yonetir,
    /// ruh enerjisini isletir ve kamerayi dogru hedefe baglar.
    ///
    /// PlayerController'a ve SoulController'a hic dokunmaz; sadece
    /// birini kapatip digerini acar. Bu sayede iki hareket sistemi de
    /// birbirinden habersiz calisir.
    ///
    /// TASARIM KARARI — Mesafe sinirlamasi:
    /// Sabit bir leash (ip) yerine "uzaklastikca daha hizli tukenme" secildi.
    /// Leash gorunmez duvar hissi verir ve oyuncuyu cezalandirmadan durdurur;
    /// yani riski ortadan kaldirir. Hizlanan tukenme ise oyuncuya her an
    /// "geri donebilecek miyim?" sorusunu sordurur — projenin USP'si bu.
    /// </summary>
    public class SoulSwitchManager : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerController body;
        [SerializeField] private SoulController soul;
        [SerializeField] private GameObject soulObject;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private SoulTether tether;
        [SerializeField] private MeleeAttack bodyAttack;
        [SerializeField] private MeleeAttack soulAttack;

        [Header("Ruh Enerjisi")]
        [Tooltip("Ruh formunda kalinabilecek en uzun sure (saniye), bedene yapisikken.")]
        [SerializeField] private float maxSoulDuration = 6f;
        [Tooltip("Bedendeyken enerjinin saniyede ne kadar dolacagi. 1 = gercek zamanli dolum.")]
        [SerializeField] private float rechargeRate = 0.8f;
        [Tooltip("Ruha gecebilmek icin gereken en az enerji (saniye). Tus spamini engeller.")]
        [SerializeField] private float minEnergyToSeparate = 1.5f;

        [Header("Mesafe Cezasi")]
        [Tooltip("Bu mesafeye kadar tukenme normal hizda.")]
        [SerializeField] private float comfortableDistance = 5f;
        [Tooltip("Bu mesafede tukenme hizi 'maxDrainMultiplier' katina cikar.")]
        [SerializeField] private float dangerDistance = 14f;
        [Tooltip("En uzak mesafede tukenmenin kac katina cikacagi.")]
        [SerializeField] private float maxDrainMultiplier = 4f;

        [Header("Gecis")]
        [Tooltip("Ruh bedenden cikarken uygulanan ilk itme.")]
        [SerializeField] private Vector2 separationImpulse = new Vector2(0f, 3f);
        [Tooltip("Zorla geri donusten sonra tekrar ayrilamama suresi.")]
        [SerializeField] private float forcedReturnLockout = 1f;

        [Header("Ruhun Konumunda Bedenlesme")]
        [Tooltip("Ruh formu kapatildiginda bedeni ruhun bulundugu konuma tasir.")]
        [SerializeField] private bool materializeAtSoulPosition = true;
        [Tooltip("Bedenlesme noktasini engelleyen katmanlar. Varsayilan: Ground.")]
        [SerializeField] private LayerMask materializationBlockingLayers = 1 << 8;
        [Tooltip("Ruh bir duvarin icindeyse en fazla bu yaricapta guvenli bosluk aranir.")]
        [SerializeField] private float maxMaterializationSearchRadius = 2.5f;
        [Tooltip("Guvenli nokta aramasinin hassasiyeti. Kucuk deger daha hassas fakat daha masraflidir.")]
        [SerializeField] private float materializationSearchStep = 0.25f;
        [SerializeField] private Color materializationFxColor = new Color(0.35f, 0.9f, 1f, 1f);

        private float _soulEnergy;
        private float _lockoutTimer;
        private Rigidbody2D _bodyRigidbody;
        private CapsuleCollider2D _bodyCollider;
        private Vector2 _bodyStandingColliderSize;
        private Vector2 _bodyStandingColliderOffset;

        /// <summary>Su an ruh formunda miyiz?</summary>
        public bool IsSoulActive { get; private set; }
        /// <summary>Enerji 0-1 arasi; UI bunu okuyor.</summary>
        public float EnergyNormalized => maxSoulDuration <= 0f ? 0f : _soulEnergy / maxSoulDuration;
        /// <summary>Ruh formundayken anlik tukenme carpani; UI ve efektler icin.</summary>
        public float CurrentDrainMultiplier { get; private set; } = 1f;
        /// <summary>Ayrilmaya yetecek enerji var mi?</summary>
        public bool CanSeparate => _soulEnergy >= minEnergyToSeparate && _lockoutTimer <= 0f;
        /// <summary>Prefab gibi sahne disi nesnelerin bedeni guvenle bulabilmesi icin salt okunur hedef.</summary>
        public Transform BodyTransform => body != null ? body.transform : null;
        /// <summary>Prefab gibi sahne disi nesnelerin ruhu guvenle bulabilmesi icin salt okunur hedef.</summary>
        public Transform SoulTransform => soul != null ? soul.transform : null;
        /// <summary>Form degistiginde yeni ruh durumu ve zorunlu donus bilgisiyle tetiklenir.</summary>
        public event System.Action<bool, bool> OnFormChanged;

        /// <summary>Disaridan zorla bedene dondurur (olum, sahne gecisi vb.).</summary>
        public void ForceReturnToBody()
        {
            // Olum/respawn gibi sistem cagrilarinda beden ruhun yanina
            // isinlanmamali; yeniden dogus akisi kendi konumunu belirler.
            if (IsSoulActive) ReturnToBody(forced: false, materializeAtSoul: false);
        }

        /// <summary>Enerjiyi doldurur ve kilidi kaldirir. Yeniden dogusta kullanilir.</summary>
        public void ResetEnergy()
        {
            _soulEnergy = maxSoulDuration;
            _lockoutTimer = 0f;
        }

        private void Awake()
        {
            if (bodyAttack == null && body != null) bodyAttack = body.GetComponent<MeleeAttack>();
            if (soulAttack == null && soul != null) soulAttack = soul.GetComponent<MeleeAttack>();

            if (body != null)
            {
                _bodyRigidbody = body.GetComponent<Rigidbody2D>();
                _bodyCollider = body.GetComponent<CapsuleCollider2D>();
                if (_bodyCollider != null)
                {
                    _bodyStandingColliderSize = _bodyCollider.size;
                    _bodyStandingColliderOffset = _bodyCollider.offset;
                }
            }

            if (!HasRequiredReferences())
            {
                Debug.LogError("[SoulSwitchManager] Zorunlu oyuncu/form referanslari eksik; form degisimi devre disi.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            _soulEnergy = maxSoulDuration;
            IsSoulActive = false;

            if (soulObject != null) soulObject.SetActive(false);
            if (soul != null) soul.enabled = false;
            if (body != null) body.enabled = true;
            if (cameraFollow != null && body != null) cameraFollow.SetTarget(body.transform);
            if (tether != null) tether.SetVisible(false);
            SetCombatForm(soulActive: false);
        }

        private void Update()
        {
            _lockoutTimer -= Time.deltaTime;

            if (IsSoulActive) UpdateSoulForm();
            else UpdateBodyForm();

            HandleSwitchInput();
        }

        private void UpdateSoulForm()
        {
            CurrentDrainMultiplier = CalculateDrainMultiplier();
            _soulEnergy -= Time.deltaTime * CurrentDrainMultiplier;

            if (_soulEnergy <= 0f)
            {
                _soulEnergy = 0f;
                ReturnToBody(forced: true, materializeAtSoul: true);
            }
        }

        private void UpdateBodyForm()
        {
            CurrentDrainMultiplier = 1f;
            _soulEnergy = Mathf.Min(maxSoulDuration, _soulEnergy + Time.deltaTime * rechargeRate);
        }

        /// <summary>Bedene olan uzaklik arttikca tukenme hizlanir.</summary>
        private float CalculateDrainMultiplier()
        {
            if (body == null || soul == null) return 1f;

            float distance = Vector2.Distance(body.transform.position, soul.transform.position);
            float t = Mathf.InverseLerp(comfortableDistance, dangerDistance, distance);
            return Mathf.Lerp(1f, maxDrainMultiplier, t);
        }

        private void HandleSwitchInput()
        {
            if (!input.SoulSwitchPressedThisFrame) return;

            if (IsSoulActive) ReturnToBody(forced: false, materializeAtSoul: true);
            else if (CanSeparate) SeparateSoul();
        }

        private void SeparateSoul()
        {
            IsSoulActive = true;

            // Beden oldugu yerde kalsin; kalan yatay hiziyla kaymasin.
            if (body.TryGetComponent(out Rigidbody2D bodyRb))
            {
                bodyRb.linearVelocity = new Vector2(0f, bodyRb.linearVelocity.y);
            }
            body.enabled = false;

            soulObject.SetActive(true);
            soul.enabled = true;
            soul.Spawn(body.transform.position, body.FacingDirection);
            SetCombatForm(soulActive: true);

            if (soul.TryGetComponent(out Rigidbody2D soulRb))
            {
                soulRb.linearVelocity = separationImpulse;
            }

            cameraFollow.SetTarget(soul.transform);
            if (tether != null) tether.SetVisible(true);
            OnFormChanged?.Invoke(true, false);
        }

        private void ReturnToBody(bool forced, bool materializeAtSoul)
        {
            IsSoulActive = false;

            Vector2 soulPosition = soul.transform.position;
            bool didMaterialize = materializeAtSoul && materializeAtSoulPosition &&
                                  TryMaterializeBody(soulPosition);

            soul.enabled = false;
            soulObject.SetActive(false);
            body.enabled = true;
            SetCombatForm(soulActive: false);

            cameraFollow.SetTarget(body.transform);
            if (tether != null) tether.SetVisible(false);

            if (didMaterialize)
            {
                ParticleFX.Burst(soulPosition, materializationFxColor, count: 12,
                    speed: 3.2f, size: 0.1f, lifetime: 0.28f,
                    spreadAngle: 180f, direction: Vector2.up, gravityScale: 0f);
                CameraFollow.PunchZoom(0.12f, 0.12f);
            }

            // Enerji bitip zorla dondurulduysa hemen tekrar cikilamasin.
            if (forced) _lockoutTimer = forcedReturnLockout;
            OnFormChanged?.Invoke(false, forced);
        }

        /// <summary>
        /// Ruhu kapatirken bedeni ruhun konumuna tasir. Hedef bir tas/duvar
        /// icindeyse ruhun ilerleme yonunden baslayarak en yakin bos kapsul
        /// konumu aranir. Hic guvenli nokta bulunamazsa beden eski yerinde kalir.
        /// </summary>
        private bool TryMaterializeBody(Vector2 desiredPosition)
        {
            if (_bodyRigidbody == null || _bodyCollider == null) return false;

            if (!TryFindSafeMaterializationPosition(desiredPosition, out Vector2 safePosition))
            {
                Debug.LogWarning("[SoulSwitchManager] Ruhun yakininda guvenli bedenlesme noktasi bulunamadi; beden eski konumunda kaldi.", this);
                return false;
            }

            _bodyRigidbody.position = safePosition;
            body.transform.position = safePosition;
            _bodyRigidbody.linearVelocity = Vector2.zero;
            _bodyRigidbody.angularVelocity = 0f;
            body.SetFacingDirection(soul.FacingDirection);
            Physics2D.SyncTransforms();
            return true;
        }

        private bool TryFindSafeMaterializationPosition(Vector2 desiredPosition, out Vector2 safePosition)
        {
            if (IsMaterializationPositionClear(desiredPosition))
            {
                safePosition = desiredPosition;
                return true;
            }

            float step = Mathf.Max(0.1f, materializationSearchStep);
            int ringCount = Mathf.CeilToInt(maxMaterializationSearchRadius / step);
            const int samplesPerRing = 16;

            Vector2 travelDirection = desiredPosition - (Vector2)body.transform.position;
            float baseAngle = travelDirection.sqrMagnitude > 0.001f
                ? Mathf.Atan2(travelDirection.y, travelDirection.x)
                : Mathf.PI * 0.5f;

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float radius = ring * step;
                for (int sample = 0; sample < samplesPerRing; sample++)
                {
                    float angle = baseAngle + sample * (Mathf.PI * 2f / samplesPerRing);
                    Vector2 candidate = desiredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!IsMaterializationPositionClear(candidate)) continue;

                    safePosition = candidate;
                    return true;
                }
            }

            safePosition = body.transform.position;
            return false;
        }

        private bool IsMaterializationPositionClear(Vector2 bodyPosition)
        {
            Vector3 scale = body.transform.lossyScale;
            Vector2 size = new Vector2(
                _bodyStandingColliderSize.x * Mathf.Abs(scale.x),
                _bodyStandingColliderSize.y * Mathf.Abs(scale.y));
            Vector2 center = bodyPosition + new Vector2(
                _bodyStandingColliderOffset.x * scale.x,
                _bodyStandingColliderOffset.y * scale.y);

            Collider2D[] overlaps = Physics2D.OverlapCapsuleAll(
                center, size, _bodyCollider.direction,
                body.transform.eulerAngles.z, materializationBlockingLayers);

            foreach (Collider2D overlap in overlaps)
            {
                if (overlap != null && overlap != _bodyCollider && !overlap.isTrigger) return false;
            }
            return true;
        }

        private void SetCombatForm(bool soulActive)
        {
            if (bodyAttack != null) bodyAttack.SetInputEnabled(!soulActive);
            if (soulAttack != null) soulAttack.SetInputEnabled(soulActive);
        }

        private bool HasRequiredReferences()
        {
            return input != null && body != null && soul != null && soulObject != null && cameraFollow != null;
        }

        private void OnValidate()
        {
            maxSoulDuration = Mathf.Max(0.1f, maxSoulDuration);
            rechargeRate = Mathf.Max(0f, rechargeRate);
            minEnergyToSeparate = Mathf.Clamp(minEnergyToSeparate, 0f, maxSoulDuration);
            comfortableDistance = Mathf.Max(0f, comfortableDistance);
            dangerDistance = Mathf.Max(comfortableDistance + 0.01f, dangerDistance);
            maxDrainMultiplier = Mathf.Max(1f, maxDrainMultiplier);
            forcedReturnLockout = Mathf.Max(0f, forcedReturnLockout);
            maxMaterializationSearchRadius = Mathf.Max(0f, maxMaterializationSearchRadius);
            materializationSearchStep = Mathf.Clamp(materializationSearchStep, 0.1f, 1f);
        }
    }
}

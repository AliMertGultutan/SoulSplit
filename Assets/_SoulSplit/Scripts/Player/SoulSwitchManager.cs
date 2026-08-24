using UnityEngine;
using SoulSplit.Core;

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

        private float _soulEnergy;
        private float _lockoutTimer;

        /// <summary>Su an ruh formunda miyiz?</summary>
        public bool IsSoulActive { get; private set; }
        /// <summary>Enerji 0-1 arasi; UI bunu okuyor.</summary>
        public float EnergyNormalized => maxSoulDuration <= 0f ? 0f : _soulEnergy / maxSoulDuration;
        /// <summary>Ruh formundayken anlik tukenme carpani; UI ve efektler icin.</summary>
        public float CurrentDrainMultiplier { get; private set; } = 1f;
        /// <summary>Ayrilmaya yetecek enerji var mi?</summary>
        public bool CanSeparate => _soulEnergy >= minEnergyToSeparate && _lockoutTimer <= 0f;

        /// <summary>Disaridan zorla bedene dondurur (olum, sahne gecisi vb.).</summary>
        public void ForceReturnToBody()
        {
            if (IsSoulActive) ReturnToBody(forced: false);
        }

        /// <summary>Enerjiyi doldurur ve kilidi kaldirir. Yeniden dogusta kullanilir.</summary>
        public void ResetEnergy()
        {
            _soulEnergy = maxSoulDuration;
            _lockoutTimer = 0f;
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
                ReturnToBody(forced: true);
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

            if (IsSoulActive) ReturnToBody(forced: false);
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

            if (soul.TryGetComponent(out Rigidbody2D soulRb))
            {
                soulRb.linearVelocity = separationImpulse;
            }

            cameraFollow.SetTarget(soul.transform);
            if (tether != null) tether.SetVisible(true);
        }

        private void ReturnToBody(bool forced)
        {
            IsSoulActive = false;

            soul.enabled = false;
            soulObject.SetActive(false);
            body.enabled = true;

            cameraFollow.SetTarget(body.transform);
            if (tether != null) tether.SetVisible(false);

            // Enerji bitip zorla dondurulduysa hemen tekrar cikilamasin.
            if (forced) _lockoutTimer = forcedReturnLockout;
        }
    }
}

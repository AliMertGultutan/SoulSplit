using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Kod-uretimli, tek seferlik parcacik patlamalari icin yardimci.
    /// Onceden hazirlanmis prefab gerektirmez: her cagri kendi ParticleSystem'ini
    /// olusturup ayarlar, oynatir ve omru bitince kendini yok eder.
    ///
    /// 2D GUVENLIGI: Shape modulu "Circle" kullanir — bu sekil local XY
    /// duzleminde yatar ve parcaciklar dogal olarak o duzlemde disariya
    /// sacilir. "Cone" sekli varsayilan olarak local Z ekseninde (kameraya
    /// dogru/tersine, yani EKRANDA GORUNMEZ) yayilir; bu yuzden kasitli
    /// olarak kullanilmadi. Yon onyargisi (bias) icin VelocityOverLifetime
    /// world-space'te dogrudan uygulanir — donme matematigine gerek kalmaz.
    ///
    /// Arastirma notu: "Juice It or Lose It" (Vlambeer) ve Game Maker's
    /// Toolkit "Secrets of Game Feel" konusmalarinin ortak vurgusu:
    /// toz/carpisma parcaciklari, squash-stretch'ten bile daha buyuk bir
    /// "canlilik" farki yaratir — cunku hareketin DUNYAYLA etkilesimini
    /// gorsellestirir, sadece karakterin kendi seklini degil.
    /// </summary>
    public static class ParticleFX
    {
        private static Material _cachedMaterial;

        /// <summary>Kisa omurlu, dagilan bir parcacik patlamasi olusturur.</summary>
        public static void Burst(
            Vector3 position, Color color, int count,
            float speed, float size, float lifetime,
            float spreadAngle = 40f, Vector2 direction = default, float gravityScale = 0.5f)
        {
            if (direction == default) direction = Vector2.up;

            var go = new GameObject("FX_Burst");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravityScale;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            // Circle: local XY duzleminde yatan, parcaciklarin radyal olarak
            // o duzlemde disariya sactigi guvenli 2D sekli.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;
            shape.arc = spreadAngle * 2f;
            shape.rotation = Vector3.zero;

            // Yon onyargisi: dunya-uzayinda dogrudan hiz eklemesi, donme
            // matematigine gerek kalmadan gorsel olarak dogru sonuc verir.
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = direction.x * speed * 0.5f;
            velocityOverLifetime.y = direction.y * speed * 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDefaultMaterial();
            renderer.sortingOrder = 60;
            renderer.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
            Object.Destroy(go, lifetime + 0.3f);
        }

        /// <summary>Yerdeki toz/tozlanma efekti icin kisayol (dusuk, yayilan, kisa omurlu).</summary>
        public static void Dust(Vector3 position, float intensity, Color color)
        {
            Burst(position, color,
                count: Mathf.RoundToInt(Mathf.Lerp(2, 7, intensity)),
                speed: Mathf.Lerp(0.6f, 2.2f, intensity),
                size: Mathf.Lerp(0.08f, 0.18f, intensity),
                lifetime: Mathf.Lerp(0.25f, 0.45f, intensity),
                spreadAngle: 90f,
                direction: Vector2.up,
                gravityScale: 0.8f);
        }

        /// <summary>Vurus carpismasi icin kisayol (hizli, yon belirtilebilir, kisa).</summary>
        public static void Impact(Vector3 position, Color color, Vector2 direction, float intensity = 1f)
        {
            Burst(position, color,
                count: Mathf.RoundToInt(Mathf.Lerp(4, 10, intensity)),
                speed: Mathf.Lerp(2f, 4.5f, intensity),
                size: Mathf.Lerp(0.06f, 0.12f, intensity),
                lifetime: 0.22f,
                spreadAngle: 60f,
                direction: direction,
                gravityScale: 0.2f);
        }

        private static Material GetDefaultMaterial()
        {
            if (_cachedMaterial != null) return _cachedMaterial;
            // "Sprites/Default" Built-in RP shader'i — URP'de sadece geriye
            // donuk uyumluluk katmaniyla calisir (build'de shader stripping
            // ile kaybolabilir) ve Light2D'den etkilenmez. Projenin geri
            // kalani URP 2D sprite shader'larini kullaniyor; parcaciklar da
            // ayni ailenin unlit varyantini kullansin (bilerek unlit —
            // kivilcim/toz kendi isigini yayiyor gibi hissettirir).
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _cachedMaterial = new Material(shader);
            return _cachedMaterial;
        }
    }
}

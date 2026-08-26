using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>Oyuncunun cihazda kalici olarak saklanan oynanis tercihleri.</summary>
    public static class GameplaySettings
    {
        private const string MasterVolumeKey = "SoulSplit.Settings.MasterVolume";
        private const string CameraEffectsKey = "SoulSplit.Settings.CameraEffects";
        private const string HitStopKey = "SoulSplit.Settings.HitStop";
        private const string ContextualHintsKey = "SoulSplit.Settings.ContextualHints";
        private const string MaterializeAtSoulKey = "SoulSplit.Settings.MaterializeAtSoul";
        private const string FullscreenKey = "SoulSplit.Settings.Fullscreen";

        public const float DefaultMasterVolume = 0.8f;
        public const float DefaultCameraEffectsIntensity = 1f;

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumeKey, clamped);
                AudioListener.volume = clamped;
                PlayerPrefs.Save();
            }
        }

        public static float CameraEffectsIntensity
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(CameraEffectsKey, DefaultCameraEffectsIntensity));
            set => SetFloat(CameraEffectsKey, Mathf.Clamp01(value));
        }

        public static bool HitStopEnabled
        {
            get => GetBool(HitStopKey, true);
            set => SetBool(HitStopKey, value);
        }

        public static bool ContextualHintsEnabled
        {
            get => GetBool(ContextualHintsKey, true);
            set => SetBool(ContextualHintsKey, value);
        }

        public static bool HasMaterializationPreference => PlayerPrefs.HasKey(MaterializeAtSoulKey);

        public static bool MaterializeAtSoulPosition
        {
            get => GetBool(MaterializeAtSoulKey, true);
            set => SetBool(MaterializeAtSoulKey, value);
        }

        public static bool Fullscreen
        {
            get => GetBool(FullscreenKey, true);
            set
            {
                SetBool(FullscreenKey, value);
                if (!Application.isEditor) Screen.fullScreen = value;
            }
        }

        public static void ResetMaterializationPreference()
        {
            PlayerPrefs.DeleteKey(MaterializeAtSoulKey);
            PlayerPrefs.Save();
        }

        public static void ResetAllToDefaults()
        {
            PlayerPrefs.DeleteKey(MasterVolumeKey);
            PlayerPrefs.DeleteKey(CameraEffectsKey);
            PlayerPrefs.DeleteKey(HitStopKey);
            PlayerPrefs.DeleteKey(ContextualHintsKey);
            PlayerPrefs.DeleteKey(MaterializeAtSoulKey);
            PlayerPrefs.DeleteKey(FullscreenKey);
            PlayerPrefs.Save();
            ApplyStoredSettings();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStoredSettings()
        {
            AudioListener.volume = MasterVolume;
            if (!Application.isEditor) Screen.fullScreen = Fullscreen;
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}

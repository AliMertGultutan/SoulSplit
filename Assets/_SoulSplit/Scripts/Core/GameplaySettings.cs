using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>Oyuncunun cihazda kalici olarak saklanan oynanis tercihleri.</summary>
    public static class GameplaySettings
    {
        private const string MaterializeAtSoulKey = "SoulSplit.Settings.MaterializeAtSoul";

        public static bool HasMaterializationPreference => PlayerPrefs.HasKey(MaterializeAtSoulKey);

        public static bool MaterializeAtSoulPosition
        {
            get => PlayerPrefs.GetInt(MaterializeAtSoulKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(MaterializeAtSoulKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void ResetMaterializationPreference()
        {
            PlayerPrefs.DeleteKey(MaterializeAtSoulKey);
            PlayerPrefs.Save();
        }
    }
}

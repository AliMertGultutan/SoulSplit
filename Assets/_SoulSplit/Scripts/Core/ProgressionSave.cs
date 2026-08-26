using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Son ulasilan checkpoint'i PlayerPrefs uzerinde saklar. Kayit yalnizca
    /// ileri yonlu guncellenir; oyuncu eski bir bolgeye donerse ilerlemesi
    /// geriye alinmaz. Sahneye dogrudan giris kaydi otomatik tuketmez.
    /// </summary>
    public static class ProgressionSave
    {
        private const int CurrentVersion = 1;
        private const string Prefix = "SoulSplit.Progress.";
        private const string VersionKey = Prefix + "Version";
        private const string SceneKey = Prefix + "Scene";
        private const string CheckpointKey = Prefix + "Checkpoint";
        private const string PositionXKey = Prefix + "PositionX";
        private const string PositionYKey = Prefix + "PositionY";
        private const string PositionZKey = Prefix + "PositionZ";

        private static bool _resumeRequested;

        public static bool HasCheckpoint => TryGetCheckpoint(out _);
        public static event System.Action<CheckpointData> OnCheckpointSaved;

        public readonly struct CheckpointData
        {
            public CheckpointData(string sceneName, string checkpointId, Vector3 position)
            {
                SceneName = sceneName;
                CheckpointId = checkpointId;
                Position = position;
            }

            public string SceneName { get; }
            public string CheckpointId { get; }
            public Vector3 Position { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            _resumeRequested = false;
            OnCheckpointSaved = null;
        }

        public static bool SaveCheckpoint(string sceneName, string checkpointId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(checkpointId)) return false;
            if (!IsFinite(position)) return false;

            if (TryGetCheckpoint(out CheckpointData current) && current.SceneName == sceneName)
            {
                if (position.x < current.Position.x - 0.01f) return false;
                if (checkpointId == current.CheckpointId && Vector3.SqrMagnitude(position - current.Position) < 0.0001f)
                    return false;
            }

            PlayerPrefs.SetInt(VersionKey, CurrentVersion);
            PlayerPrefs.SetString(SceneKey, sceneName);
            PlayerPrefs.SetString(CheckpointKey, checkpointId);
            PlayerPrefs.SetFloat(PositionXKey, position.x);
            PlayerPrefs.SetFloat(PositionYKey, position.y);
            PlayerPrefs.SetFloat(PositionZKey, position.z);
            PlayerPrefs.Save();

            CheckpointData saved = new CheckpointData(sceneName, checkpointId, position);
            OnCheckpointSaved?.Invoke(saved);
            return true;
        }

        public static bool TryGetCheckpoint(out CheckpointData data)
        {
            data = default;
            if (PlayerPrefs.GetInt(VersionKey, 0) != CurrentVersion) return false;

            string sceneName = PlayerPrefs.GetString(SceneKey, string.Empty);
            string checkpointId = PlayerPrefs.GetString(CheckpointKey, string.Empty);
            Vector3 position = new Vector3(
                PlayerPrefs.GetFloat(PositionXKey, float.NaN),
                PlayerPrefs.GetFloat(PositionYKey, float.NaN),
                PlayerPrefs.GetFloat(PositionZKey, float.NaN));

            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(checkpointId) || !IsFinite(position))
                return false;

            data = new CheckpointData(sceneName, checkpointId, position);
            return true;
        }

        public static void RequestResume()
        {
            _resumeRequested = HasCheckpoint;
        }

        public static void RequestNewGame()
        {
            Clear();
            _resumeRequested = false;
        }

        public static bool TryConsumeResume(string sceneName, out CheckpointData data)
        {
            data = default;
            if (!_resumeRequested) return false;

            _resumeRequested = false;
            return TryGetCheckpoint(out data) && data.SceneName == sceneName;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.DeleteKey(SceneKey);
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.DeleteKey(PositionXKey);
            PlayerPrefs.DeleteKey(PositionYKey);
            PlayerPrefs.DeleteKey(PositionZKey);
            PlayerPrefs.Save();
            _resumeRequested = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}

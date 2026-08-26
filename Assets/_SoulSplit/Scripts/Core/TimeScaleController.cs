using System.Collections.Generic;
using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Pause ve hit-stop isteklerini tek yerde birlestirir. Pause her zaman
    /// onceliklidir; hit-stop bittiginde baska bir sistemin pause'u acilmaz.
    /// </summary>
    public static class TimeScaleController
    {
        private static readonly HashSet<object> PauseOwners = new HashSet<object>();
        private static float _hitStopScale = 1f;

        public static bool IsPaused => PauseOwners.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            PauseOwners.Clear();
            _hitStopScale = 1f;
            Time.timeScale = 1f;
        }

        public static void SetPaused(object owner, bool paused)
        {
            if (owner == null) return;

            if (paused) PauseOwners.Add(owner);
            else PauseOwners.Remove(owner);

            Apply();
        }

        public static void SetHitStopScale(float scale)
        {
            _hitStopScale = Mathf.Clamp01(scale);
            Apply();
        }

        public static void ClearHitStop()
        {
            _hitStopScale = 1f;
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = IsPaused ? 0f : _hitStopScale;
        }
    }
}

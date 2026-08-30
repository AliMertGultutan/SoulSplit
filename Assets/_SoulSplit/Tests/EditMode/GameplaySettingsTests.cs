using NUnit.Framework;
using SoulSplit.Core;

namespace SoulSplit.Tests
{
    public class GameplaySettingsTests
    {
        private float _volume;
        private float _cameraEffects;
        private bool _hitStop;
        private bool _hints;
        private bool _materialization;
        private bool _fullscreen;
        private bool _hadMaterializationPreference;

        [SetUp]
        public void SetUp()
        {
            _volume = GameplaySettings.MasterVolume;
            _cameraEffects = GameplaySettings.CameraEffectsIntensity;
            _hitStop = GameplaySettings.HitStopEnabled;
            _hints = GameplaySettings.ContextualHintsEnabled;
            _materialization = GameplaySettings.MaterializeAtSoulPosition;
            _fullscreen = GameplaySettings.Fullscreen;
            _hadMaterializationPreference = GameplaySettings.HasMaterializationPreference;
        }

        [TearDown]
        public void TearDown()
        {
            GameplaySettings.MasterVolume = _volume;
            GameplaySettings.CameraEffectsIntensity = _cameraEffects;
            GameplaySettings.HitStopEnabled = _hitStop;
            GameplaySettings.ContextualHintsEnabled = _hints;
            GameplaySettings.MaterializeAtSoulPosition = _materialization;
            GameplaySettings.Fullscreen = _fullscreen;
            if (!_hadMaterializationPreference) GameplaySettings.ResetMaterializationPreference();
        }

        [Test]
        public void ResetAllToDefaults_AppliesExpectedValues()
        {
            GameplaySettings.MasterVolume = 0.1f;
            GameplaySettings.CameraEffectsIntensity = 0.2f;
            GameplaySettings.HitStopEnabled = false;
            GameplaySettings.ContextualHintsEnabled = false;
            GameplaySettings.MaterializeAtSoulPosition = false;
            GameplaySettings.Fullscreen = false;

            GameplaySettings.ResetAllToDefaults();

            Assert.That(GameplaySettings.MasterVolume, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(GameplaySettings.CameraEffectsIntensity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(GameplaySettings.HitStopEnabled, Is.False);
            Assert.That(GameplaySettings.ContextualHintsEnabled, Is.True);
            Assert.That(GameplaySettings.MaterializeAtSoulPosition, Is.False);
            Assert.That(GameplaySettings.Fullscreen, Is.True);
        }

        [Test]
        public void NumericSettings_AreClampedToValidRange()
        {
            GameplaySettings.MasterVolume = 2f;
            GameplaySettings.CameraEffectsIntensity = -1f;

            Assert.That(GameplaySettings.MasterVolume, Is.EqualTo(1f));
            Assert.That(GameplaySettings.CameraEffectsIntensity, Is.EqualTo(0f));
        }
    }
}

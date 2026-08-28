using SoulSplit.Combat;
using SoulSplit.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.Core
{
    /// <summary>
    /// Prototipin temel eylemlerine kisa ses geri bildirimi ekler. Sesler dis
    /// dosya kullanmadan calisma aninda uretilir; boylece proje tasinabilir ve
    /// ucuncu taraf lisanslarindan bagimsiz kalir.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudioFeedback : MonoBehaviour
    {
        private const string GameplaySceneName = "SampleScene";
        private const int SampleRate = 22050;

        private AudioSource _source;
        private PlayerController _player;
        private SoulSwitchManager _switchManager;
        private MeleeAttack[] _attacks;
        private Health[] _healthPools;

        private AudioClip _jumpClip;
        private AudioClip _wallJumpClip;
        private AudioClip _lightAttackClip;
        private AudioClip _heavyAttackClip;
        private AudioClip _hitClip;
        private AudioClip _deflectClip;
        private AudioClip _deathClip;
        private AudioClip _soulOutClip;
        private AudioClip _soulReturnClip;
        private AudioClip _ultimateClip;
        private AudioClip _checkpointClip;
        private AudioClip _dodgeClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameplaySceneName) return;
            if (FindAnyObjectByType<GameAudioFeedback>() != null) return;
            if (FindAnyObjectByType<PlayerController>() == null) return;

            new GameObject("GameAudioFeedback", typeof(AudioSource), typeof(GameAudioFeedback));
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = false;

            BuildClips();
            FindTargets();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void FindTargets()
        {
            _player = FindAnyObjectByType<PlayerController>();
            _switchManager = FindAnyObjectByType<SoulSwitchManager>();
            _attacks = FindObjectsByType<MeleeAttack>(FindObjectsInactive.Include);
            _healthPools = FindObjectsByType<Health>(FindObjectsInactive.Include);
        }

        private void Subscribe()
        {
            if (_player != null)
            {
                _player.OnJumped += HandleJump;
                _player.OnWallJumped += HandleWallJump;
                _player.OnDodged += HandleDodge;
            }

            if (_switchManager != null)
            {
                _switchManager.OnFormChanged += HandleFormChanged;
                _switchManager.OnUltimateStateChanged += HandleUltimateStateChanged;
            }
            ProgressionSave.OnCheckpointSaved += HandleCheckpointSaved;

            if (_attacks != null)
            {
                foreach (MeleeAttack attack in _attacks)
                {
                    if (attack != null) attack.OnAttackTriggered += HandleAttack;
                }
            }

            if (_healthPools != null)
            {
                foreach (Health health in _healthPools)
                {
                    if (health != null) health.OnHit += HandleHit;
                }
            }
        }

        private void Unsubscribe()
        {
            if (_player != null)
            {
                _player.OnJumped -= HandleJump;
                _player.OnWallJumped -= HandleWallJump;
                _player.OnDodged -= HandleDodge;
            }

            if (_switchManager != null)
            {
                _switchManager.OnFormChanged -= HandleFormChanged;
                _switchManager.OnUltimateStateChanged -= HandleUltimateStateChanged;
            }
            ProgressionSave.OnCheckpointSaved -= HandleCheckpointSaved;

            if (_attacks != null)
            {
                foreach (MeleeAttack attack in _attacks)
                {
                    if (attack != null) attack.OnAttackTriggered -= HandleAttack;
                }
            }

            if (_healthPools != null)
            {
                foreach (Health health in _healthPools)
                {
                    if (health != null) health.OnHit -= HandleHit;
                }
            }
        }

        private void HandleJump() => Play(_jumpClip, 0.38f);
        private void HandleWallJump() => Play(_wallJumpClip, 0.42f);
        private void HandleDodge() => Play(_dodgeClip, 0.46f);

        private void HandleAttack(AttackTier tier)
        {
            Play(tier == AttackTier.Heavy ? _heavyAttackClip : _lightAttackClip,
                tier == AttackTier.Heavy ? 0.56f : 0.38f);
        }

        private void HandleHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            switch (result)
            {
                case HitResult.Deflected:
                    Play(_deflectClip, 0.48f);
                    break;
                case HitResult.Damaged:
                    Play(_hitClip, amount >= 2 ? 0.62f : 0.45f);
                    break;
                case HitResult.Killed:
                    Play(_deathClip, 0.62f);
                    break;
            }
        }

        private void HandleFormChanged(bool soulActive, bool forced)
        {
            Play(soulActive ? _soulOutClip : _soulReturnClip, forced ? 0.62f : 0.50f);
        }

        private void HandleUltimateStateChanged(bool active)
        {
            if (active) Play(_ultimateClip, 0.72f);
        }

        private void HandleCheckpointSaved(ProgressionSave.CheckpointData data)
        {
            Play(_checkpointClip, 0.48f);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (_source != null && clip != null) _source.PlayOneShot(clip, volume);
        }

        private void BuildClips()
        {
            _jumpClip = CreateSweep("Jump", 0.13f, 260f, 430f, 0.05f, 0.25f, 11);
            _wallJumpClip = CreateSweep("WallJump", 0.16f, 330f, 560f, 0.08f, 0.22f, 13);
            _lightAttackClip = CreateSweep("LightAttack", 0.12f, 430f, 120f, 0.62f, 0.18f, 17);
            _heavyAttackClip = CreateSweep("HeavyAttack", 0.22f, 210f, 62f, 0.48f, 0.34f, 19);
            _hitClip = CreateSweep("Hit", 0.10f, 150f, 72f, 0.72f, 0.22f, 23);
            _deflectClip = CreateSweep("Deflect", 0.14f, 980f, 620f, 0.12f, 0.18f, 29);
            _deathClip = CreateSweep("Death", 0.38f, 180f, 42f, 0.32f, 0.45f, 31);
            _soulOutClip = CreateSweep("SoulOut", 0.34f, 240f, 760f, 0.18f, 0.32f, 37);
            _soulReturnClip = CreateSweep("SoulReturn", 0.28f, 690f, 210f, 0.16f, 0.30f, 41);
            _ultimateClip = CreateSweep("SoulSurge", 0.62f, 180f, 1080f, 0.08f, 0.48f, 47);
            _checkpointClip = CreateSweep("Checkpoint", 0.42f, 380f, 820f, 0.04f, 0.42f, 43);
            _dodgeClip = CreateSweep("SoulStep", 0.16f, 720f, 210f, 0.20f, 0.24f, 53);
        }

        private static AudioClip CreateSweep(string name, float duration, float startFrequency,
            float endFrequency, float noiseMix, float overtoneMix, int seed)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(seed);
            float phase = 0f;
            float overtonePhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t * t);
                phase += Mathf.PI * 2f * frequency / SampleRate;
                overtonePhase += Mathf.PI * 2f * frequency * 1.98f / SampleRate;

                float attack = Mathf.Clamp01(t / 0.045f);
                float release = Mathf.Pow(1f - t, 1.8f);
                float envelope = attack * release;
                float tone = Mathf.Sin(phase) + Mathf.Sin(overtonePhase) * overtoneMix;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                samples[i] = Mathf.Clamp((tone * (1f - noiseMix) + noise * noiseMix) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

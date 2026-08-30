using SoulSplit.Combat;
using UnityEngine;

namespace SoulSplit.Enemies
{
    /// <summary>Her dusmanin konumundan gelen, tekrar sinirli ses geri bildirimi.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EnemyAudioFeedback : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private EnemyBase _enemy;
        private Health _health;
        private AudioSource _source;
        private float _nextPatrolSound;
        private AudioClip _patrol;
        private AudioClip _attack;
        private AudioClip _hurt;
        private AudioClip _death;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<Health>();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0.72f;
            _source.minDistance = 2f;
            _source.maxDistance = 17f;
            _source.rolloffMode = AudioRolloffMode.Linear;

            bool ghost = GetComponent<GhostEnemy>() != null;
            float baseFrequency = ghost ? 300f : 150f;
            _patrol = MakeClip("EnemyPatrol", 0.18f, baseFrequency, baseFrequency * 0.8f, 0.22f, 67);
            _attack = MakeClip("EnemyAttack", 0.20f, baseFrequency * 1.6f, baseFrequency * 0.62f, 0.44f, 71);
            _hurt = MakeClip("EnemyHurt", 0.13f, baseFrequency * 1.9f, baseFrequency * 0.9f, 0.48f, 73);
            _death = MakeClip("EnemyDeath", 0.34f, baseFrequency * 1.1f, baseFrequency * 0.28f, 0.38f, 79);
        }

        private void OnEnable()
        {
            if (_enemy != null) _enemy.OnAttackTriggered += HandleAttack;
            if (_health != null)
            {
                _health.OnHit += HandleHit;
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null) _enemy.OnAttackTriggered -= HandleAttack;
            if (_health != null)
            {
                _health.OnHit -= HandleHit;
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_enemy == null || _health == null || _health.IsDead) return;
            bool moving = _enemy.State == EnemyState.Patrol || _enemy.State == EnemyState.Chase;
            if (moving && _enemy.Velocity.sqrMagnitude > 0.18f && Time.time >= _nextPatrolSound)
            {
                Play(_patrol, 0.20f);
                _nextPatrolSound = Time.time + Random.Range(1.8f, 3.5f);
            }
        }

        private void HandleAttack(AttackTier tier) => Play(_attack, tier == AttackTier.Heavy ? 0.48f : 0.36f);

        private void HandleHit(HitResult result, DamageType type, Vector2 direction, int amount)
        {
            if (result == HitResult.Damaged) Play(_hurt, 0.34f);
        }

        private void HandleDeath() => Play(_death, 0.52f);

        private void Play(AudioClip clip, float volume)
        {
            if (_source != null && clip != null) _source.PlayOneShot(clip, volume);
        }

        private static AudioClip MakeClip(string name, float duration, float startFrequency,
            float endFrequency, float noiseMix, int seed)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[count];
            System.Random random = new System.Random(seed);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += Mathf.PI * 2f * frequency / SampleRate;
                float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 0.51f) * 0.35f;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Clamp01(t / 0.025f) * Mathf.Pow(1f - t, 1.35f);
                samples[i] = Mathf.Clamp((tone * (1f - noiseMix) + noise * noiseMix) * envelope * 0.72f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

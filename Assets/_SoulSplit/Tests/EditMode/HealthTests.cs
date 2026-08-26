using NUnit.Framework;
using SoulSplit.Combat;
using UnityEngine;

namespace SoulSplit.Tests
{
    public class HealthTests
    {
        private GameObject _target;
        private Health _health;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject("HealthTestTarget");
            _health = _target.AddComponent<Health>();
            _health.ResetHealth();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
        }

        [Test]
        public void WrongRealm_IsDeflectedWithoutLosingHealth()
        {
            int initial = _health.Current;

            HitResult result = _health.TryTakeDamage(1, DamageType.Spiritual);

            Assert.That(result, Is.EqualTo(HitResult.Deflected));
            Assert.That(_health.Current, Is.EqualTo(initial));
        }

        [Test]
        public void MatchingRealm_AppliesDamage()
        {
            int initial = _health.Current;

            HitResult result = _health.TryTakeDamage(1, DamageType.Physical);

            Assert.That(result, Is.EqualTo(HitResult.Damaged));
            Assert.That(_health.Current, Is.EqualTo(initial - 1));
        }

        [Test]
        public void Kill_IsIdempotent()
        {
            int deathEvents = 0;
            _health.OnDeath += () => deathEvents++;

            _health.Kill();
            _health.Kill();

            Assert.That(_health.IsDead, Is.True);
            Assert.That(deathEvents, Is.EqualTo(1));
        }
    }
}

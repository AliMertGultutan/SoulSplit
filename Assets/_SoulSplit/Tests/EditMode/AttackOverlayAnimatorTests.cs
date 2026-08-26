using NUnit.Framework;
using SoulSplit.Player;

namespace SoulSplit.Tests
{
    public class AttackOverlayAnimatorTests
    {
        [Test]
        public void Evaluate_StartsAtRestPose()
        {
            var timings = new AttackOverlayTimings(0.1f, 0.05f, 0.1f, 0.2f, 20f, 40f, 0.3f);

            AttackOverlayAnimator.Evaluate(timings, 0f, out float angle, out float lunge);

            Assert.That(angle, Is.Zero.Within(0.001f));
            Assert.That(lunge, Is.Zero.Within(0.001f));
        }

        [Test]
        public void Evaluate_ReturnsToRestAfterTotalDuration()
        {
            var timings = new AttackOverlayTimings(0.1f, 0.05f, 0.1f, 0.2f, 20f, 40f, 0.3f);

            AttackOverlayAnimator.Evaluate(timings, timings.TotalDuration, out float angle, out float lunge);

            Assert.That(angle, Is.Zero.Within(0.001f));
            Assert.That(lunge, Is.Zero.Within(0.001f));
        }
    }
}

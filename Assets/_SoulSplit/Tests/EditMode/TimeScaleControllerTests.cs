using NUnit.Framework;
using SoulSplit.Core;
using UnityEngine;

namespace SoulSplit.Tests
{
    public class TimeScaleControllerTests
    {
        private readonly object _pauseOwner = new object();

        [TearDown]
        public void TearDown()
        {
            TimeScaleController.SetPaused(_pauseOwner, false);
            TimeScaleController.ClearHitStop();
            Time.timeScale = 1f;
        }

        [Test]
        public void Pause_TakesPriorityOverHitStop()
        {
            TimeScaleController.SetHitStopScale(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(0.2f).Within(0.001f));

            TimeScaleController.SetPaused(_pauseOwner, true);
            TimeScaleController.ClearHitStop();

            Assert.That(Time.timeScale, Is.Zero);

            TimeScaleController.SetPaused(_pauseOwner, false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}

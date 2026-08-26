using NUnit.Framework;
using SoulSplit.Core;
using UnityEngine;

namespace SoulSplit.Tests
{
    public class ProgressionSaveTests
    {
        private bool _hadExistingSave;
        private ProgressionSave.CheckpointData _existingSave;

        [SetUp]
        public void SetUp()
        {
            _hadExistingSave = ProgressionSave.TryGetCheckpoint(out _existingSave);
            ProgressionSave.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressionSave.Clear();
            if (_hadExistingSave)
            {
                ProgressionSave.SaveCheckpoint(
                    _existingSave.SceneName,
                    _existingSave.CheckpointId,
                    _existingSave.Position);
            }
        }

        [Test]
        public void SaveCheckpoint_DoesNotRegressOnLinearLevel()
        {
            Assert.That(ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_B", new Vector3(50f, 2f)), Is.True);
            Assert.That(ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_A", new Vector3(20f, 2f)), Is.False);

            Assert.That(ProgressionSave.TryGetCheckpoint(out ProgressionSave.CheckpointData saved), Is.True);
            Assert.That(saved.CheckpointId, Is.EqualTo("Checkpoint_B"));
            Assert.That(saved.Position.x, Is.EqualTo(50f));
        }

        [Test]
        public void ResumeRequest_IsExplicitAndConsumedOnlyOnce()
        {
            ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_B", new Vector3(50f, 2f));

            Assert.That(ProgressionSave.TryConsumeResume("SampleScene", out _), Is.False,
                "Sahneye dogrudan giris kaydi tuketmemeli.");

            ProgressionSave.RequestResume();
            Assert.That(ProgressionSave.TryConsumeResume("SampleScene", out ProgressionSave.CheckpointData saved), Is.True);
            Assert.That(saved.Position.x, Is.EqualTo(50f));
            Assert.That(ProgressionSave.TryConsumeResume("SampleScene", out _), Is.False);
        }

        [Test]
        public void RequestNewGame_ClearsPersistedCheckpoint()
        {
            ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_B", new Vector3(50f, 2f));

            ProgressionSave.RequestNewGame();

            Assert.That(ProgressionSave.HasCheckpoint, Is.False);
        }
    }
}

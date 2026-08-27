using System.Collections;
using System.Linq;
using NUnit.Framework;
using SoulSplit.Core;
using SoulSplit.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SoulSplit.Tests
{
    public class MainMenuFlowTests
    {
        private bool _hadExistingSave;
        private ProgressionSave.CheckpointData _existingSave;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hadExistingSave = ProgressionSave.TryGetCheckpoint(out _existingSave);
            ProgressionSave.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ProgressionSave.Clear();
            if (_hadExistingSave)
            {
                ProgressionSave.SaveCheckpoint(
                    _existingSave.SceneName,
                    _existingSave.CheckpointId,
                    _existingSave.Position);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_WithSaveOffersContinueAndNewGameConfirmation()
        {
            ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_Room2", new Vector3(34f, 1f));
            SceneManager.LoadScene("MainMenu");
            yield return null;

            Button playButton = GameObject.Find("OYNAButton")?.GetComponent<Button>();
            Button newGameButton = GameObject.Find("YENİ OYUNButton")?.GetComponent<Button>();
            Button settingsButton = GameObject.Find("AYARLARButton")?.GetComponent<Button>();
            MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>();

            Assert.That(playButton, Is.Not.Null);
            Assert.That(playButton.GetComponentInChildren<Text>().text, Is.EqualTo("DEVAM ET"));
            Assert.That(newGameButton, Is.Not.Null);
            Assert.That(settingsButton, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);

            ExecuteEvents.Execute(newGameButton.gameObject, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
            Transform confirmation = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .FirstOrDefault(item => item.name == "NewGameConfirmation");
            Assert.That(confirmation, Is.Not.Null);
            Assert.That(confirmation.gameObject.activeSelf, Is.True);

            menu.CancelNewGame();
            Assert.That(confirmation.gameObject.activeSelf, Is.False);
            Assert.That(ProgressionSave.HasCheckpoint, Is.True,
                "Vazgecmek mevcut kaydi korumalidir.");

            ExecuteEvents.Execute(settingsButton.gameObject, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
            SettingsPanelUI settingsPanel = Object.FindAnyObjectByType<SettingsPanelUI>();
            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(settingsPanel.IsOpen, Is.True);

            Button controlsButton = GameObject.Find("ControlsButton")?.GetComponent<Button>();
            Assert.That(controlsButton, Is.Not.Null);
            ExecuteEvents.Execute(controlsButton.gameObject, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
            ControlsRebindPanelUI controlsPanel = Object.FindAnyObjectByType<ControlsRebindPanelUI>();
            Assert.That(controlsPanel, Is.Not.Null);
            Assert.That(controlsPanel.IsOpen, Is.True);
            Assert.That(GameObject.Find("Rebind_Move_up"), Is.Not.Null);
            Assert.That(GameObject.Find("Rebind_Ultimate_Primary"), Is.Not.Null);

            controlsPanel.Close();
            settingsPanel.Close();
        }

        [UnityTest]
        public IEnumerator Continue_LoadsPlayerAtSavedCheckpoint()
        {
            Vector3 savedPosition = new Vector3(34f, 1f, 0f);
            ProgressionSave.SaveCheckpoint("SampleScene", "Checkpoint_Room2", savedPosition);
            SceneManager.LoadScene("MainMenu");
            yield return null;

            MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>();
            Assert.That(menu, Is.Not.Null);
            menu.PlayGame();
            yield return null;
            yield return null;

            GameObject player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            Assert.That(Vector2.Distance(player.transform.position, savedPosition), Is.LessThan(0.25f));
        }
    }
}

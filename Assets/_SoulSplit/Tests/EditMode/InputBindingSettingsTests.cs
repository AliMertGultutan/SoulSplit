using NUnit.Framework;
using SoulSplit.Core;
using UnityEngine.InputSystem;

namespace SoulSplit.Tests
{
    public class InputBindingSettingsTests
    {
        private string _originalOverrides;
        private bool _hadSavedOverrides;

        [SetUp]
        public void SetUp()
        {
            _originalOverrides = InputBindingSettings.ExportOverrides();
            _hadSavedOverrides = InputBindingSettings.HasSavedOverrides;
            InputBindingSettings.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedOverrides)
                InputBindingSettings.RestoreOverrides(_originalOverrides, persist: true);
            else
                InputBindingSettings.ResetToDefaults();
        }

        [Test]
        public void KeyboardOverride_IsSavedAndDisplayed()
        {
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("Jump", null,
                out InputAction action, out int bindingIndex), Is.True);

            action.ApplyBindingOverride(bindingIndex, "<Keyboard>/t");
            InputBindingSettings.SaveOverrides();

            Assert.That(InputBindingSettings.HasSavedOverrides, Is.True);
            Assert.That(InputBindingSettings.GetDisplayName(action, bindingIndex), Does.Contain("T"));
        }

        [Test]
        public void KeyboardDisplayHelper_ReflectsCurrentOverride()
        {
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("SoulSwitch", null,
                out InputAction action, out int bindingIndex), Is.True);

            action.ApplyBindingOverride(bindingIndex, "<Keyboard>/r");

            Assert.That(InputBindingSettings.GetKeyboardDisplayName("SoulSwitch"), Is.EqualTo("R"));
            Assert.That(InputBindingSettings.GetKeyboardDisplayName("MissingAction", fallback: "YOK"),
                Is.EqualTo("YOK"));
        }

        [Test]
        public void DuplicateKeyboardBinding_ReportsExistingAction()
        {
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("Jump", null,
                out InputAction jump, out int jumpBinding), Is.True);
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("Move", "up",
                out InputAction move, out int moveBinding), Is.True);

            string movementPath = move.bindings[moveBinding].effectivePath;
            jump.ApplyBindingOverride(jumpBinding, movementPath);

            Assert.That(InputBindingSettings.FindKeyboardConflict(jump, jumpBinding), Is.EqualTo("Move"));
        }

        [Test]
        public void ResetToDefaults_RemovesSavedOverrides()
        {
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("Ultimate", null,
                out InputAction action, out int bindingIndex), Is.True);
            action.ApplyBindingOverride(bindingIndex, "<Keyboard>/u");
            InputBindingSettings.SaveOverrides();

            InputBindingSettings.ResetToDefaults();

            Assert.That(InputBindingSettings.HasSavedOverrides, Is.False);
            Assert.That(InputBindingSettings.GetDisplayName(action, bindingIndex), Does.Contain("Q"));
        }

        [Test]
        public void Dodge_HasRebindableKeyboardDefault()
        {
            Assert.That(InputBindingSettings.TryGetKeyboardBinding("Dodge", null,
                out InputAction action, out int bindingIndex), Is.True);
            Assert.That(InputBindingSettings.GetDisplayName(action, bindingIndex), Does.Contain("SHIFT"));
        }

        [Test]
        public void Jump_AcceptsSpaceAndWByDefault()
        {
            InputAction jump = InputBindingSettings.Actions.FindActionMap("Player")?.FindAction("Jump");
            Assert.That(jump, Is.Not.Null);

            bool hasSpace = false;
            bool hasW = false;
            foreach (InputBinding binding in jump.bindings)
            {
                hasSpace |= binding.effectivePath == "<Keyboard>/space";
                hasW |= binding.effectivePath == "<Keyboard>/w";
            }

            Assert.That(hasSpace, Is.True, "Space varsayilan ziplama tusu olarak kalmalidir.");
            Assert.That(hasW, Is.True, "W alternatif ziplama tusu olarak calismalidir.");
        }
    }
}

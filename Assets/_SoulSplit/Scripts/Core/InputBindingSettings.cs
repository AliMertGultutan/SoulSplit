using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulSplit.Core
{
    /// <summary>
    /// Klavye binding override'larini tek yerde yukler, kaydeder ve sifirlar.
    /// InputActionAsset Resources altinda tutuldugu icin ana menude oyuncu
    /// objesi bulunmadan da kontrol ayarlari degistirilebilir.
    /// </summary>
    public static class InputBindingSettings
    {
        private const string ResourceName = "SoulSplitControls";
        private const string OverridesKey = "SoulSplit.Settings.InputBindings";

        private static InputActionAsset _actions;
        private static bool _loaded;

        public static InputActionAsset Actions
        {
            get
            {
                EnsureLoaded();
                return _actions;
            }
        }

        public static bool HasSavedOverrides => PlayerPrefs.HasKey(OverridesKey);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedOverrides()
        {
            EnsureLoaded();
        }

        public static bool TryGetKeyboardBinding(string actionName, string compositePart,
            out InputAction action, out int bindingIndex)
        {
            action = Actions?.FindActionMap("Player")?.FindAction(actionName);
            bindingIndex = -1;
            if (action == null) return false;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite) continue;
                if (!string.IsNullOrEmpty(compositePart) &&
                    !string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string path = binding.effectivePath;
                if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>/")) continue;

                bindingIndex = i;
                return true;
            }
            return false;
        }

        public static string GetDisplayName(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "ATANMADI";

            string path = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrEmpty(path)) return "ATANMADI";
            return InputControlPath.ToHumanReadableString(path,
                InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
        }

        public static string FindKeyboardConflict(InputAction selectedAction, int selectedBindingIndex)
        {
            if (selectedAction == null || selectedBindingIndex < 0) return null;
            string selectedPath = selectedAction.bindings[selectedBindingIndex].effectivePath;
            if (string.IsNullOrEmpty(selectedPath)) return null;

            InputActionMap map = selectedAction.actionMap;
            if (map == null) return null;

            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action == selectedAction && i == selectedBindingIndex) continue;
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                    if (!binding.effectivePath.StartsWith("<Keyboard>/")) continue;
                    if (!string.Equals(binding.effectivePath, selectedPath,
                            System.StringComparison.OrdinalIgnoreCase)) continue;

                    return action.name;
                }
            }
            return null;
        }

        public static void SaveOverrides()
        {
            if (Actions == null) return;
            PlayerPrefs.SetString(OverridesKey, Actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            if (Actions != null) Actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            PlayerPrefs.Save();
        }

        public static string ExportOverrides()
        {
            return Actions != null ? Actions.SaveBindingOverridesAsJson() : string.Empty;
        }

        public static void RestoreOverrides(string json, bool persist)
        {
            if (Actions == null) return;
            Actions.RemoveAllBindingOverrides();
            if (!string.IsNullOrWhiteSpace(json)) Actions.LoadBindingOverridesFromJson(json);

            if (!persist) return;
            if (string.IsNullOrWhiteSpace(json)) PlayerPrefs.DeleteKey(OverridesKey);
            else PlayerPrefs.SetString(OverridesKey, json);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _actions = Resources.Load<InputActionAsset>(ResourceName);
            if (_actions == null)
            {
                Debug.LogError("[InputBindingSettings] SoulSplitControls Resources altinda bulunamadi.");
                return;
            }

            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                _actions.LoadBindingOverridesFromJson(json);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[InputBindingSettings] Kayitli tus ayarlari okunamadi; varsayilanlar kullaniliyor. {exception.Message}");
                ResetToDefaults();
            }
        }
    }
}

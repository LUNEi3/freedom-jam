using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Foundation
{
    public sealed class InputBindingEntry
    {
        public string ActionMap { get; }
        public string ActionName { get; }
        public int BindingIndex { get; }
        public string DisplayName { get; }
        public string ControlScheme { get; }

        public InputBindingEntry(string actionMap, string actionName, int bindingIndex, string displayName, string controlScheme)
        {
            ActionMap = actionMap;
            ActionName = actionName;
            BindingIndex = bindingIndex;
            DisplayName = displayName;
            ControlScheme = controlScheme;
        }
    }

    public sealed class InputBindingService
    {
        const string PlayerMapName = "Player";
        const string SystemMapName = "System";
        static readonly string[] RebindableMaps = { PlayerMapName, SystemMapName };
        static readonly string[] SupportedSchemes = { "Keyboard&Mouse", "Gamepad" };

        readonly InputActionAsset actions;
        readonly SettingsStore store;
        InputActionRebindingExtensions.RebindingOperation activeRebind;

        [Serializable]
        sealed class OverrideCollection
        {
            public OverrideEntry[] entries;
        }

        [Serializable]
        sealed class OverrideEntry
        {
            public string actionMap;
            public string actionName;
            public string json;
        }

        public InputBindingService(InputActionAsset actions, SettingsStore store)
        {
            this.actions = actions ? actions : throw new ArgumentNullException(nameof(actions));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IReadOnlyList<InputBindingEntry> GetRebindableBindings()
        {
            var result = new List<InputBindingEntry>();
            foreach (var mapName in RebindableMaps)
            {
                var map = actions.FindActionMap(mapName, false);
                if (map == null) continue;

                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (var index = 0; index < bindings.Count; index++)
                    {
                        var binding = bindings[index];
                        if (binding.isComposite || string.IsNullOrWhiteSpace(binding.path)) continue;

                        foreach (var scheme in GetSupportedSchemes(binding.groups))
                        {
                            var displayName = string.IsNullOrWhiteSpace(binding.name) ? action.name : binding.name;
                            result.Add(new InputBindingEntry(map.name, action.name, index, displayName, scheme));
                        }
                    }
                }
            }
            return result;
        }

        public void BeginRebind(string actionMap, string actionName, int bindingIndex, Action completed)
        {
            var entry = GetRebindableBindings().FirstOrDefault(candidate =>
                candidate.ActionMap == actionMap && candidate.ActionName == actionName && candidate.BindingIndex == bindingIndex);
            if (entry == null)
                throw new ArgumentException("The requested binding is outside the supported Player/System keyboard and gamepad bindings.", nameof(bindingIndex));

            CancelActiveRebind();
            var mapState = CaptureGameplayMapState();
            SetGameplayMapsEnabled(false, false);

            var action = actions.FindAction(actionMap + "/" + actionName, false);
            InputActionRebindingExtensions.RebindingOperation operation = null;
            Action finish = () =>
            {
                if (ReferenceEquals(activeRebind, operation)) activeRebind = null;
                operation?.Dispose();
                RestoreGameplayMapState(mapState);
                completed?.Invoke();
            };

            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed();

            if (entry.ControlScheme == "Gamepad")
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else
            {
                operation.WithControlsExcluding("<Gamepad>")
                    .WithControlsExcluding("<Joystick>")
                    .WithControlsExcluding("<XRController>")
                    .WithControlsExcluding("<Touchscreen>")
                    .WithControlsExcluding("<Pen>");
            }

            operation.OnComplete(_ =>
            {
                SaveOverrides();
                finish();
            });
            operation.OnCancel(_ => finish());
            activeRebind = operation;
            operation.Start();
        }

        public void SaveOverrides()
        {
            var entries = new List<OverrideEntry>();
            foreach (var mapName in RebindableMaps)
            {
                var map = actions.FindActionMap(mapName, false);
                if (map == null) continue;

                foreach (var action in map.actions)
                {
                    if (!HasBindingOverrides(action)) continue;
                    entries.Add(new OverrideEntry
                    {
                        actionMap = map.name,
                        actionName = action.name,
                        json = action.SaveBindingOverridesAsJson()
                    });
                }
            }

            var settings = store.LoadSettings(new GameSettings());
            settings.BindingOverridesJson = JsonUtility.ToJson(new OverrideCollection { entries = entries.ToArray() });
            store.SaveSettings(settings);
        }

        public void LoadOverrides()
        {
            RemoveGameplayOverrides();
            var json = store.LoadSettings(new GameSettings()).BindingOverridesJson;
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") || !json.TrimEnd().EndsWith("}")) return;

            try
            {
                var saved = JsonUtility.FromJson<OverrideCollection>(json);
                if (saved?.entries == null) return;

                foreach (var entry in saved.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.json) || !IsRebindableMap(entry.actionMap)) continue;
                    var action = actions.FindAction(entry.actionMap + "/" + entry.actionName, false);
                    if (action == null) continue;
                    action.LoadBindingOverridesFromJson(entry.json, true);
                }
            }
            catch (ArgumentException)
            {
                RemoveGameplayOverrides();
            }
        }

        public void ResetOverrides()
        {
            CancelActiveRebind();
            RemoveGameplayOverrides();
            SaveOverrides();
        }

        void CancelActiveRebind()
        {
            if (activeRebind == null) return;
            activeRebind.Cancel();
            activeRebind.Dispose();
            activeRebind = null;
        }

        void RemoveGameplayOverrides()
        {
            foreach (var mapName in RebindableMaps)
            {
                var map = actions.FindActionMap(mapName, false);
                if (map == null) continue;
                foreach (var action in map.actions) action.RemoveAllBindingOverrides();
            }
        }

        static bool HasBindingOverrides(InputAction action)
        {
            foreach (var binding in action.bindings)
                if (binding.hasOverrides) return true;
            return false;
        }

        static IEnumerable<string> GetSupportedSchemes(string groups)
        {
            if (string.IsNullOrWhiteSpace(groups)) yield break;
            var names = groups.Split(';').Select(group => group.Trim());
            foreach (var scheme in SupportedSchemes)
                if (names.Contains(scheme)) yield return scheme;
        }

        static bool IsRebindableMap(string mapName) => RebindableMaps.Contains(mapName);

        (bool playerEnabled, bool systemEnabled) CaptureGameplayMapState()
        {
            return (actions.FindActionMap(PlayerMapName, false)?.enabled ?? false,
                actions.FindActionMap(SystemMapName, false)?.enabled ?? false);
        }

        void RestoreGameplayMapState((bool playerEnabled, bool systemEnabled) state) =>
            SetGameplayMapsEnabled(state.playerEnabled, state.systemEnabled);

        void SetGameplayMapsEnabled(bool playerEnabled, bool systemEnabled)
        {
            SetMapEnabled(PlayerMapName, playerEnabled);
            SetMapEnabled(SystemMapName, systemEnabled);
        }

        void SetMapEnabled(string mapName, bool enabled)
        {
            var map = actions.FindActionMap(mapName, false);
            if (map == null || map.enabled == enabled) return;
            if (enabled) map.Enable();
            else map.Disable();
        }
    }
}

using System;
using UnityEngine.InputSystem;

namespace FreedomJam.Foundation
{
    public sealed class GameInputController
    {
        InputActionAsset actions;
        InputAction pauseAction;

        public event Action PausePressed;

        public void Initialize(InputActionAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (pauseAction != null) pauseAction.performed -= OnPausePerformed;

            actions = asset;
            var playerMap = actions.FindActionMap("Player", false);
            var systemMap = actions.FindActionMap("System", false);
            if (playerMap == null || systemMap == null)
                throw new ArgumentException("The input asset must contain Player and System action maps.", nameof(asset));

            pauseAction = systemMap.FindAction("Pause", false);
            if (pauseAction == null)
                throw new ArgumentException("The System action map must contain a Pause action.", nameof(asset));

            pauseAction.performed += OnPausePerformed;
            SetGameplayState(GameState.Menu);
        }

        public void SetGameplayState(GameState state)
        {
            if (actions == null) throw new InvalidOperationException("Initialize the input controller before setting its game state.");

            SetMapEnabled("Player", state == GameState.Playing);
            SetMapEnabled("System", state == GameState.Playing || state == GameState.Paused);
        }

        void OnPausePerformed(InputAction.CallbackContext _) => PausePressed?.Invoke();

        void SetMapEnabled(string mapName, bool enabled)
        {
            var map = actions.FindActionMap(mapName, false);
            if (map == null || map.enabled == enabled) return;
            if (enabled) map.Enable();
            else map.Disable();
        }
    }
}

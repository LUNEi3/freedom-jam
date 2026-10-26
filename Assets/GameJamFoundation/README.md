# Game Jam Foundation (PC)

This Unity 6 foundation provides a reusable Main Menu, game flow, pause menu, settings panel, HUD, result screen, input rebinding, audio controls, and saved best score for Keyboard & Mouse and Gamepad.

## Scenes and assets

- `Scenes/MainMenu` is the build entry point and contains the single persistent `GameFoundationBootstrap`.
- `Scenes/Game` contains the HUD, pause overlay, and its own `EventSystem` with `InputSystemUIInputModule`.
- `Scenes/Result` contains the result/settings UI and its own `EventSystem`.
- `Config/GameJamDefaults` controls scene names, starting score, timer mode/duration, best-score key/default, and whether higher or lower scores are better.
- `InputSystem_Actions.inputactions` supplies Player, System, and UI actions for Keyboard & Mouse and Gamepad.

Keep the bootstrap only in Main Menu. It survives scene changes and owns the flow, settings, input, and audio services. The supplied prefabs can be reused in game-specific scenes; keep one EventSystem per loaded scene and use `InputSystemUIInputModule` for UI focus and pointer input.

## Connect game-specific gameplay

Call the flow methods from your own gameplay scripts through `GameFoundationBootstrap.Instance.Flow`:

```csharp
var flow = GameFoundationBootstrap.Instance.Flow;
flow.AddScore(10);       // Add points or subtract points with a negative value.
flow.SetScore(250);      // Replace the current score with an exact value.
flow.FinishRun();        // Save the configured best score and load the Result scene.
```

These methods only change a run while the state is Playing. A configured countdown finishes automatically at zero. Movement, win/loss rules, and other game-specific behavior stay in your game scripts.

## Settings and persistence

The Settings panel lets players adjust Master, Music, and SFX volume, mute each bus, rebind Player/System actions, and restore the default bindings. Settings, binding overrides, and the configured best score are stored with PlayerPrefs. Choose a unique `bestScoreKey` for each game that uses this foundation.

The pause menu resumes or returns to Main Menu. Main Menu and Result Quit exit a standalone player; selecting Quit in the Unity Editor leaves the Editor open.

Build for Windows/PC with `MainMenu`, `Game`, and `Result` in that order. SampleScene remains in the project for reference and is not included in the build list.

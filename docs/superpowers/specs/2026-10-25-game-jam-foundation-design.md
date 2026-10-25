# Game Jam Foundation Systems — Design

## Intent

Create a small, reusable Unity foundation for future PC game jam projects. It should let a team move quickly from a title screen into a game, pause and resume, present a result, and reuse common input, UI, audio, and data behavior without requiring genre-specific gameplay code.

## Goals

- Support Windows/PC with keyboard and mouse plus gamepad.
- Provide separate Main Menu, Game, and Result scenes, with a pause overlay in the Game scene.
- Support starting, pausing, resuming, restarting, returning to the Main Menu, and quitting the built game.
- Provide reusable UI for score, timer, menus, settings, and results.
- Allow keyboard and gamepad binding changes, with a reset-to-defaults action.
- Let each game choose whether a higher or lower score is better.
- Provide master, music, and effects volume controls and mute behavior.
- Keep per-run score and time during play, then persist the best score and user settings between launches.
- Let each jam game supply simple defaults without adding genre-specific mechanics.

## Non-goals

- Player movement, combat, inventory, camera behavior, or other genre-specific gameplay.
- Multiplayer, cloud saves, analytics, achievements, localization, or mobile/console support.
- A general-purpose framework or dependency injection layer.
- Persistent storage of every run or a full statistics dashboard.

## Anti-goals

- Do not require a third-party package for these systems.
- Do not force each game to use a particular score formula or timer mode.
- Do not couple game rules to menu presentation or scene-specific UI objects.

## Existing project and constraints

- Unity 6 project (`6000.6.6f1`).
- `com.unity.inputsystem` and `com.unity.ugui` are already installed.
- `Assets/InputSystem_Actions.inputactions` already contains Player and UI maps and several device bindings. The framework should build on the existing asset and use only Keyboard&Mouse and Gamepad control schemes.
- Build Settings currently contain only `Assets/Scenes/SampleScene.unity`; the three framework scenes will need to replace or supplement it.
- No game-specific scripts or scenes were found, so the framework must expose integration points instead of assuming a genre.
- Target: standalone PC build. Quit in a built game exits the application; when tested in the Unity Editor it must not close the Editor.

## Approaches considered

1. **One scene with UI panels for every state.** Fast to prototype, but it does not exercise scene transitions and makes the requested menu/game/result flow harder to reuse.
2. **Separate Main Menu, Game, and Result scenes with a small persistent services object.** Adds a little scene setup, while keeping transitions explicit and allowing settings/audio to survive scene loads. **Chosen.** It matches the requested flow and uses the existing Unity packages without a large framework.

## Design

### Scene flow and game state

- `MainMenu` is the startup scene. It offers Play, Settings, and Quit.
- `Game` starts a fresh run. It contains the HUD and pause overlay; the overlay offers Resume and Quit to Main Menu.
- `Result` displays the completed run and offers Play Again, Main Menu, and Quit.
- A lightweight persistent services object owns the session flow and the audio/settings services. It survives scene loads and prevents duplicate instances.
- The session state is `Menu`, `Playing`, `Paused`, or `Result`. Scene changes go through one flow API so UI buttons do not load arbitrary scenes directly.
- Pause is available while Playing through Escape and the gamepad Start button. Pausing stops gameplay time while leaving menu input and audio available. Resume restores the previous time scale.
- Play Again resets per-run values before loading Game. Returning to Main Menu clears per-run values. Quit to Main Menu restores normal time scale first.

### Input and controls

- Extend the existing Input Actions asset with the framework actions it needs, reusing the UI map for menu navigation and adding/using a Pause action for Escape and gamepad Start.
- Support Keyboard&Mouse and Gamepad control schemes on PC. Ignore the asset's touch, joystick, and XR schemes for this foundation.
- The Settings screen provides interactive rebinding for supported keyboard and gamepad actions, a reset-to-defaults button, and persistence of overrides.
- Rebinding covers the existing Player actions and the framework actions, including Pause. New game-specific action maps are outside this foundation's rebinding UI.
- UI navigation must work with mouse, keyboard, and gamepad through `InputSystemUIInputModule`.

### UI, score, timer, and result

- Use UGUI, already installed in the project.
- Provide reusable menu, settings, pause, HUD, and result panels, with controls selectable by keyboard/gamepad.
- The HUD displays current score and elapsed or countdown time. A small game configuration asset selects timer mode and, for countdown, starting duration; it also supplies the initial score, best-score key/default, and whether a higher or lower score is better.
- Expose a narrow game-facing API to add/set score and finish the current run. The foundation does not decide when a game is won or lost.
- On game completion, capture final score and elapsed time in session data, update the stored best score if needed, and load Result.
- A countdown reaching zero automatically finishes the run and loads Result.
- Result shows final score, best score, and run time. Play Again creates a clean run.

### Audio and settings

- An AudioMixer provides Master, Music, and SFX groups. Audio playback is requested through a small audio service so scenes do not own global audio state.
- The Settings screen has a volume slider and mute toggle per group. Settings and input overrides persist between launches; missing or invalid stored values fall back to defaults.
- The Main Menu and Result scene can navigate to Settings; the Game pause overlay may also open Settings and return to pause on close.

### Data ownership

- Per-run values (score, elapsed time, state) live only for the current run.
- Persist user audio settings, input overrides, and a best score. Do not persist every run or add a statistics history.
- A ScriptableObject supplies configurable defaults for timer mode/duration and initial score. Runtime session data remains separate from this asset.
- Use Unity PlayerPrefs for this small local PC foundation. Do not store secret or platform-account data.

## Acceptance criteria

- A standalone PC build starts in Main Menu and can reach Game and Result through the described flow.
- Keyboard/mouse and gamepad can navigate menus, start a run, pause, resume, and request quit-to-menu.
- The Settings screen can rebind and reset the Player and framework actions for keyboard and gamepad.
- A sample integration can update score, display either timer mode, end a run, show results, and restart without embedding genre rules into the framework; a countdown reaching zero ends the run automatically.
- Best-score updates follow the configured higher-is-better or lower-is-better rule.
- User audio levels/mute, binding overrides, and best score survive application restart; reset/default behavior works when no saved values exist.
- Scene transitions do not duplicate persistent services, pause state does not leak into a new run/menu, and quitting the Editor does not terminate Unity.

## Calls for review

- “Quit” on the pause overlay is interpreted as “Quit to Main Menu”; the Main Menu and Result Quit buttons exit the built application.
- Rebinding covers the existing Player map and framework actions. A future game-specific action map needs an explicit integration step.
- The persistent statistic is best score only. Session score and run time are shown on Result but not kept as a history.
- The timer can count up or down, selected by the game’s configuration asset. A countdown reaching zero ends the run.
- Each game configures whether a higher or lower score is best.
- Settings are reachable from Main Menu and Result, and can also be opened while paused.

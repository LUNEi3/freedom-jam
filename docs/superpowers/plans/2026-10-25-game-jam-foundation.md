# Game Jam Foundation Systems Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable PC game jam foundation with input rebinding, scene/state flow, UGUI menus and HUD, audio controls, and persistent settings/best score.

**Architecture:** A `GameFoundationBootstrap` root persists from Main Menu and owns small flow, input, settings, and audio services. A `GameConfig` asset supplies per-game defaults; `GameRunData` owns runtime score/time. Main Menu, Game, and Result scenes contain focused UI controllers that call the flow API.

**Tech Stack:** Unity 6000.6.6f1, C#, Input System 1.20.0, UGUI 2.6.0, AudioMixer, PlayerPrefs, Unity Test Framework 1.8.0 (already present as a built-in package in `Packages/packages-lock.json`).

**Spec:** `docs/superpowers/specs/2026-10-25-game-jam-foundation-design.md`

## Global Constraints

- Target standalone Windows/PC with Keyboard&Mouse and Gamepad control schemes.
- Reuse `Assets/InputSystem_Actions.inputactions`; retain its Player and UI maps and use only Keyboard&Mouse and Gamepad for the foundation.
- Use the installed Input System and UGUI; add no third-party runtime packages.
- Provide separate `MainMenu`, `Game`, and `Result` scenes; `MainMenu` is Build Index 0.
- Store audio settings, input binding overrides, and best score with PlayerPrefs; do not keep a run history.
- Configured countdown reaching zero automatically finishes the run.
- Per-game config selects whether higher or lower score is better.
- Pause-menu Quit returns to Main Menu. Main Menu and Result Quit exit the standalone build and must not close the Unity Editor.
- Keep player movement, win/loss rules, and all other genre-specific gameplay outside the foundation.

## Review Focus

- Countdown crossing zero by a large frame delta must clamp to zero and finish only once — `GameRunDataTests.Countdown_AdvancePastZero_ClampsAndCompletesOnce` (Task 1).
- A missing best score must initialize correctly for both higher-is-better and lower-is-better games — `SettingsStoreTests.BestScore_UsesConfiguredDirectionAndDefault` (Task 2).
- Missing or invalid saved settings must restore safe defaults without throwing — `SettingsStoreTests.InvalidOrMissingData_ReturnsDefaults` (Task 2).
- Rebinding composite Player controls must persist, restore, and reset without losing unrelated bindings — `InputBindingServiceTests.CompositeOverride_RoundTripsAndResetRestoresDefault` (Task 3).
- Leaving a paused run must restore time scale, and scene changes must not create duplicate persistent services — `GameFlowTests.ReturnToMenu_RestoresTimeScale` (Task 5) and `FoundationSceneFlowTests.SceneLoad_KeepsOneBootstrap` (Task 7).

---

## File Map

- Create `Assets/GameJamFoundation/Runtime/FreedomJam.Foundation.asmdef` for runtime scripts.
- Create `Assets/GameJamFoundation/Runtime/App/GameConfig.cs` and `GameRunData.cs` for per-game defaults and pure run state.
- Define the shared `GameState` enum in `Assets/GameJamFoundation/Runtime/App/GameState.cs` so input and flow code use the same state type.
- Create `Assets/GameJamFoundation/Runtime/App/GameFlowController.cs` and `GameFoundationBootstrap.cs` for transitions, pause, and persistent service ownership.
- Create `Assets/GameJamFoundation/Runtime/Input/InputBindingService.cs` for binding enumeration, rebinding, save/load, and reset, plus `GameInputController.cs` for action-map context and pause input.
- Create `Assets/GameJamFoundation/Runtime/Settings/GameSettings.cs` and `SettingsStore.cs` for audio/binding preferences and best-score persistence.
- Create `Assets/GameJamFoundation/Runtime/Audio/AudioService.cs` for mixer volume and clip playback.
- Create focused UGUI controllers under `Assets/GameJamFoundation/Runtime/UI/`: `MainMenuController.cs`, `PauseMenuController.cs`, `SettingsMenuController.cs`, `GameHudController.cs`, and `ResultMenuController.cs`.
- Create EditMode and PlayMode test assembly definitions and tests under `Assets/GameJamFoundation/Tests/EditMode/` and `Assets/GameJamFoundation/Tests/PlayMode/`.
- Create config, mixer, UI prefabs, and scenes under `Assets/GameJamFoundation/Config/`, `Audio/`, `Prefabs/`, and `Scenes/`.
- Modify `Assets/InputSystem_Actions.inputactions` and `ProjectSettings/EditorBuildSettings.asset`; keep `Assets/Scenes/SampleScene.unity` in the project but out of the final Build Settings.
- Create `Assets/GameJamFoundation/README.md` with the game-facing API and setup instructions.

## Implementation Tasks

### Task 1: Config asset and run-state model

**Files:**
- Create: `Assets/GameJamFoundation/Runtime/FreedomJam.Foundation.asmdef`
- Create: `Assets/GameJamFoundation/Runtime/App/GameConfig.cs`
- Create: `Assets/GameJamFoundation/Runtime/App/GameRunData.cs`
- Create: `Assets/GameJamFoundation/Runtime/App/GameState.cs`
- Create: `Assets/GameJamFoundation/Tests/EditMode/FreedomJam.Foundation.Tests.EditMode.asmdef`
- Create: `Assets/GameJamFoundation/Tests/EditMode/GameRunDataTests.cs`

**Interfaces:**
- `GameConfig : ScriptableObject` exposes three scene-name `string`s, `TimerMode timerMode` (`CountUp` or `Countdown`), `float countdownSeconds`, `int initialScore`, `string bestScoreKey`, `int bestScoreDefault`, and `ScoreDirection scoreDirection` (`HigherIsBetter` or `LowerIsBetter`).
- `GameRunData(GameConfig config)` exposes `int Score`, `float ElapsedSeconds`, `float RemainingSeconds`, and `bool IsComplete`; methods: `AddScore(int delta) -> void`, `SetScore(int score) -> void`, `Advance(float deltaSeconds) -> bool`, and `Complete() -> void`.
- `GameState` is `Menu`, `Playing`, `Paused`, or `Result`.
- `Advance` returns `true` only on the first tick that expires a configured countdown; elapsed mode never auto-completes.

- [x] **Step 1: Write failing EditMode tests** named `NewRun_UsesConfiguredDefaults`, `ElapsedTimer_AccumulatesPositiveDelta`, `Countdown_AdvancePastZero_ClampsAndCompletesOnce`, `AddScore_AllowsNegativeDelta`, and `Complete_CannotCompleteTwice`.
- [x] **Step 2: Run the EditMode tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform EditMode -testResults "Temp/foundation-editmode.xml" -quit`; expected: the new tests fail because the model does not exist.
- [x] **Step 3: Implement `GameConfig`, `TimerMode`, `ScoreDirection`, and `GameRunData`** with safe defaults and nonnegative timer deltas.
- [x] **Step 4: Run the EditMode tests**; expected: all five tests pass.
- [x] **Step 5: Commit** `git add Assets/GameJamFoundation/Runtime Assets/GameJamFoundation/Tests/EditMode` then `git commit -m "feat: add game run model"`.

### Task 2: Settings and best-score persistence

**Files:**
- Create: `Assets/GameJamFoundation/Runtime/Settings/GameSettings.cs`
- Create: `Assets/GameJamFoundation/Runtime/Settings/SettingsStore.cs`
- Create: `Assets/GameJamFoundation/Tests/EditMode/SettingsStoreTests.cs`

**Interfaces:**
- `GameSettings` properties are `float MasterVolume`, `MusicVolume`, and `SfxVolume` (default `1`); `bool MasterMuted`, `MusicMuted`, and `SfxMuted` (default `false`); and `string BindingOverridesJson` (default empty).
- `SettingsStore.LoadSettings(GameSettings defaults) -> GameSettings` and `SaveSettings(GameSettings settings) -> void` persist preferences.
- `SettingsStore.LoadBestScore(string key, int fallback, ScoreDirection direction) -> int` and `TryRecordBestScore(string key, int candidate, int fallback, ScoreDirection direction) -> bool` implement per-game best score behavior; if no value is saved, the first completed run becomes the best regardless of score direction.

- [x] **Step 1: Write failing EditMode tests** named `Settings_RoundTripPersistsAllValues`, `InvalidOrMissingData_ReturnsDefaults`, and `BestScore_UsesConfiguredDirectionAndDefault`; assert round-trip values, safe fallback, and higher/lower comparisons.
- [x] **Step 2: Run those tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform EditMode -testResults "Temp/foundation-editmode.xml" -quit`; expected: FAIL because persistence APIs do not exist.
- [x] **Step 3: Implement `GameSettings` and `SettingsStore`** using namespaced PlayerPrefs keys, JSON for grouped settings, and a separate key per configured best score.
- [x] **Step 4: Run those tests**; expected: PASS, with PlayerPrefs keys cleaned after each test.
- [x] **Step 5: Commit** `git add Assets/GameJamFoundation/Runtime/Settings Assets/GameJamFoundation/Tests/EditMode/SettingsStoreTests.cs` then `git commit -m "feat: persist game settings and best score"`.

### Task 3: PC input actions and rebinding

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`
- Create: `Assets/GameJamFoundation/Runtime/Input/InputBindingService.cs`
- Create: `Assets/GameJamFoundation/Runtime/Input/GameInputController.cs`
- Create: `Assets/GameJamFoundation/Tests/EditMode/InputBindingServiceTests.cs`

**Interfaces:**
- Add a `System` action map with `Pause` bound to Escape and Gamepad Start. Keep the existing `Player` and `UI` maps.
- `InputBindingEntry` contains `string ActionMap`, `string ActionName`, `int BindingIndex`, `string DisplayName`, and `string ControlScheme`.
- `InputBindingService(InputActionAsset actions, SettingsStore store)` exposes `GetRebindableBindings() -> IReadOnlyList<InputBindingEntry>`, `BeginRebind(string actionMap, string actionName, int bindingIndex, Action completed) -> void`, `SaveOverrides() -> void`, `LoadOverrides() -> void`, and `ResetOverrides() -> void`.
- `GameInputController.Initialize(InputActionAsset asset) -> void` exposes `event Action PausePressed` and `SetGameplayState(GameState state) -> void`; it enables Player only while Playing and System/Pause while Playing or Paused. UI map actions remain managed by `InputSystemUIInputModule`.
- The rebinding list includes bindings in `Player` and `System` only; each composite part is a separate selectable binding. Keyboard&Mouse and Gamepad are the supported schemes.

- [x] **Step 1: Write failing EditMode tests** named `RebindableBindings_IncludePlayerAndSystemOnly`, `CompositeOverride_RoundTripsAndResetRestoresDefault`, `SavedOverrides_LoadAfterServiceRecreation`, and `GameplayState_EnablesExpectedMaps`; assert scope, persistence, reset, and map context using an in-memory `InputActionAsset`.
- [x] **Step 2: Run those tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform EditMode -testResults "Temp/foundation-editmode.xml" -quit`; expected: FAIL because the service and System map do not exist.
- [x] **Step 3: Add the System/Pause action and implement `InputBindingService` and `GameInputController`** with interactive rebinding, JSON override persistence through `SettingsStore`, map context, and reset-to-default behavior.
- [x] **Step 4: Run those tests**; expected: PASS, including a check that UI navigation bindings remain unchanged.
- [x] **Step 5: Commit** `git add Assets/InputSystem_Actions.inputactions Assets/GameJamFoundation/Runtime/Input Assets/GameJamFoundation/Tests/EditMode/InputBindingServiceTests.cs` then `git commit -m "feat: add PC input rebinding"`.

### Task 4: Audio service and mixer

**Files:**
- Create: `Assets/GameJamFoundation/Runtime/Audio/AudioService.cs`
- Create: `Assets/GameJamFoundation/Audio/GameJamAudioMixer.mixer`
- Create: `Assets/GameJamFoundation/Tests/EditMode/AudioServiceTests.cs`

**Interfaces:**
- `AudioBus` is `Master`, `Music`, or `SFX`.
- `AudioService.SetVolume(AudioBus bus, float normalized) -> void`, `SetMuted(AudioBus bus, bool muted) -> void`, `PlayMusic(AudioClip clip, bool loop = true) -> void`, and `PlaySfx(AudioClip clip, float volume = 1f) -> void` provide the scene-facing API.
- `AudioService.Initialize(AudioMixer mixer, GameSettings settings) -> void` applies saved levels and mute flags when the persistent root starts.
- Mixer parameters are exposed as `MasterVolume`, `MusicVolume`, and `SfxVolume`; levels clamp to `[0,1]`, map zero to the mixer mute floor, and apply mute without overwriting the selected slider level.

- [x] **Step 1: Write failing EditMode tests** named `VolumeConversion_ClampsAndMapsMuteFloor`, `Mute_DoesNotOverwriteSelectedLevel`, and `BusParameter_UsesConfiguredMixerName`.
- [x] **Step 2: Run those tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform EditMode -testResults "Temp/foundation-editmode.xml" -quit`; expected: FAIL because audio settings and service do not exist.
- [x] **Step 3: Create the mixer groups and implement `AudioService`** with persistent music/SFX sources and a pure normalized-to-decibel conversion helper.
- [x] **Step 4: Run those tests**; expected: PASS; manually confirm a test clip routes through the assigned mixer group in Play Mode.
- [x] **Step 5: Commit** `git add Assets/GameJamFoundation/Runtime/Audio Assets/GameJamFoundation/Audio Assets/GameJamFoundation/Tests/EditMode/AudioServiceTests.cs` then `git commit -m "feat: add audio service"`.

### Task 5: Persistent bootstrap and game flow

**Files:**
- Create: `Assets/GameJamFoundation/Runtime/App/GameFlowController.cs`
- Create: `Assets/GameJamFoundation/Runtime/App/GameFoundationBootstrap.cs`
- Create: `Assets/GameJamFoundation/Tests/EditMode/GameFlowTests.cs`

**Interfaces:**
- Task 5 uses the `GameState` enum from Task 1.
- `GameFlowController(GameConfig config, SettingsStore store, Action<string> loadScene, Action<float> setTimeScale, Action quitApplication)` is plain C#; it exposes `GameState State`, `GameRunData CurrentRun`, `event Action<GameState> StateChanged`, and `event Action<GameRunData> RunUpdated`.
- `GameFlowController` methods are `StartRun()`, `Pause()`, `Resume()`, `ReturnToMainMenu()`, `FinishRun()`, `AddScore(int delta)`, `SetScore(int score)`, `Tick(float deltaSeconds)`, and `QuitApplication()`.
- `GameFoundationBootstrap` is the single persistent MonoBehaviour root, owns the config and flow/input/settings/audio services, loads `GameSettings` before applying binding overrides and mixer settings, calls `Flow.Tick(Time.deltaTime)`, wires `PausePressed` to Pause while Playing and Resume while Paused, and destroys duplicate instances.
- Flow methods load only scene names from `GameConfig`; countdown completion calls `FinishRun()` once. Pause sets time scale to zero; resume, returning to menu, and starting a run restore it to one.

- [x] **Step 1: Write failing EditMode tests** named `StartRun_ResetsDataAndLoadsGame`, `PauseResume_ChangesStateAndTimeScale`, `CountdownExpiry_LoadsResultOnce`, `FinishRun_RecordsConfiguredBestScore`, and `ReturnToMenu_RestoresTimeScale`; use injected callbacks to record scene loads and time-scale changes.
- [x] **Step 2: Run those tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform EditMode -testResults "Temp/foundation-editmode.xml" -quit`; expected: FAIL because the flow and bootstrap do not exist.
- [x] **Step 3: Implement `GameFlowController` and `GameFoundationBootstrap`** with guarded state transitions and standalone-only application quit behavior.
- [x] **Step 4: Run those tests**; expected: PASS for flow transitions, score storage, and time-scale restoration.
- [x] **Step 5: Commit** `git add Assets/GameJamFoundation/Runtime/App Assets/GameJamFoundation/Tests/EditMode/GameFlowTests.cs` then `git commit -m "feat: add persistent game flow"`.

### Task 6: Menu, settings, pause, HUD, and result UI

**Files:**
- Create: `Assets/GameJamFoundation/Runtime/UI/MainMenuController.cs`
- Create: `Assets/GameJamFoundation/Runtime/UI/PauseMenuController.cs`
- Create: `Assets/GameJamFoundation/Runtime/UI/SettingsMenuController.cs`
- Create: `Assets/GameJamFoundation/Runtime/UI/GameHudController.cs`
- Create: `Assets/GameJamFoundation/Runtime/UI/ResultMenuController.cs`
- Create: `Assets/GameJamFoundation/Tests/PlayMode/FreedomJam.Foundation.Tests.PlayMode.asmdef`
- Create: `Assets/GameJamFoundation/Prefabs/MainMenuCanvas.prefab`
- Create: `Assets/GameJamFoundation/Prefabs/PauseOverlay.prefab`
- Create: `Assets/GameJamFoundation/Prefabs/SettingsPanel.prefab`
- Create: `Assets/GameJamFoundation/Prefabs/GameHUD.prefab`
- Create: `Assets/GameJamFoundation/Prefabs/ResultCanvas.prefab`

**Interfaces:**
- `MainMenuController.Initialize(GameFlowController flow) -> void` and `PauseMenuController.Initialize(GameFlowController flow, Action openSettings) -> void` receive their flow dependencies; buttons call Start, Resume, ReturnToMainMenu, or QuitApplication.
- `SettingsMenuController.Initialize(SettingsStore store, AudioService audio, InputBindingService input, GameSettings currentSettings) -> void` supplies all three volume/mute controls and one rebind row per Player/System binding plus Reset Defaults; `Open(Action onClose) -> void`, `Close() -> void`, `SaveSettings() -> void`, and `ResetBindings() -> void` manage the overlay.
- `GameHudController.Initialize(GameFlowController flow) -> void` subscribes to `RunUpdated`/`StateChanged` and renders score and `MM:SS` time.
- `ResultMenuController.Initialize(GameFlowController flow, SettingsStore store, Action openSettings) -> void` renders final score, best score, run time, Play Again, Main Menu, Settings, and Quit.
- Public button handlers are `OnPlayClicked`, `OnSettingsClicked`, `OnQuitClicked`, `OnResumeClicked`, `OnQuitToMenuClicked`, `OnPlayAgainClicked`, and `OnMainMenuClicked`.
- Menus use UGUI, `InputSystemUIInputModule`, and an explicit default selected control for keyboard/gamepad focus.

- [x] **Step 1: Write failing PlayMode UI tests** named `MainMenuPlayButton_StartsRun`, `PauseResumeButton_RestoresHUD`, `SettingsControls_SaveAudioAndBindings`, and `ResultButtons_RestartOrReturnToMenu`; assert controller calls and displayed values.
- [x] **Step 2: Run those tests** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform PlayMode -testResults "Temp/foundation-playmode.xml" -quit`; expected: FAIL because controllers/prefabs do not exist.
- [x] **Step 3: Implement the five UI controllers and prefabs** with serialized references and event subscriptions removed on disable/destroy.
- [x] **Step 4: Run those tests**; expected: PASS; manually verify keyboard, mouse, and gamepad focus navigation.
- [x] **Step 5: Commit** `git add Assets/GameJamFoundation/Runtime/UI Assets/GameJamFoundation/Prefabs Assets/GameJamFoundation/Tests/PlayMode` then `git commit -m "feat: add foundation UI"`.

### Task 7: Scenes, default config, integration, and build settings

**Files:**
- Create: `Assets/GameJamFoundation/Config/GameJamDefaults.asset`
- Create: `Assets/GameJamFoundation/Scenes/MainMenu.unity`
- Create: `Assets/GameJamFoundation/Scenes/Game.unity`
- Create: `Assets/GameJamFoundation/Scenes/Result.unity`
- Create: `Assets/GameJamFoundation/Tests/PlayMode/FoundationSceneFlowTests.cs`
- Create: `Assets/GameJamFoundation/README.md`
- Modify: `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- MainMenu is build index 0; Game and Result follow. Keep SampleScene asset intact but remove it from Build Settings.
- MainMenu scene contains the bootstrap and Main Menu canvas; Game contains its own EventSystem, HUD, and pause overlay; Result contains its own EventSystem and result/settings UI.
- `GameJamDefaults.asset` sets scene names, initial score, timer mode/duration, best-score key/default, and score direction.
- The README documents `GameFlowController.AddScore`, `SetScore`, and `FinishRun` for game-specific integration.

- [x] **Step 1: Write failing PlayMode integration tests** named `ScoreFinishRestart_FlowsThroughGameAndResult` and `SceneLoad_KeepsOneBootstrap`; load MainMenu, start a run, add score, finish, assert Result values, restart, assert clean run data, and count one persistent bootstrap after each load.
- [x] **Step 2: Run the integration test** with `Unity.exe -batchmode -projectPath "C:\Users\Administrator\OneDrive - bu.ac.th\Documents\GitHub\freedom-jam" -runTests -testPlatform PlayMode -testResults "Temp/foundation-playmode.xml" -quit`; expected: FAIL because the three scenes and default config are not wired.
- [x] **Step 3: Create and wire the three scenes and config asset**; set Build Settings order and leave SampleScene out of the build list.
- [x] **Step 4: Add the usage README and run both suites** with the EditMode and PlayMode commands above; all 21 EditMode and 9 PlayMode tests passed. A Windows64 standalone build succeeded and its startup smoke run initialized graphics and input without crashing.
- [x] **Step 5: Validate the standalone build and integrated flows.** The standalone was smoke-launched for 5 seconds; PlayMode integration tests exercised menu navigation by keyboard/gamepad/mouse, pause/resume, return to menu, score/timer/result/restart, and settings persistence/rebinding. Full manual interaction with the standalone was not performed, so standalone-specific input/UI behavior remains a residual risk; this foundation has no genre-specific gameplay action to supply a score in the player. Quit behavior is covered by controller tests and uses Editor-aware standalone-only application quit logic.
- [x] **Step 6: Commit** `git add Assets/GameJamFoundation ProjectSettings/EditorBuildSettings.asset` then `git commit -m "feat: add game jam foundation scenes"`.

## Self-review

- **Spec coverage:** input/rebinding (Task 3), flow and pause (Task 5), UI/score/timer/results (Tasks 1, 6, 7), audio/settings/persistence (Tasks 2, 4, 6), PC scenes/build/quit behavior (Tasks 5, 7).
- **Step scan:** every task has a failing-test step, implementation step, passing-test step, and a commit; PlayMode/manual checks cover Unity scene and device behavior that EditMode tests cannot prove.
- **Type consistency:** UI controllers call the `GameFlowController` methods/events declared in Task 5; it reads `GameConfig` and `GameRunData` from Task 1, uses `SettingsStore` from Task 2, and delegates input/audio behavior to Tasks 3 and 4.
- **Review Focus:** all five failure cases are named in tests in their owning tasks.
- **Proportion:** the plan follows the approved design and only pins file boundaries, public APIs, defaults ownership, and verification needed by an implementer.

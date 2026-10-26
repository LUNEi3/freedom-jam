# Player Ability Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add manually tuned, cooldown-based Dash and Suck progression and a reusable level-up choice modal that pauses the run.

**Architecture:** Store each ability's level values in serialized per-level entries on the existing player ability controller; level 0 remains locked and entry 1 unlocks the ability. A reusable uGUI choice panel displays options supplied by an interface, while a level-up flow component queues choices from PlayerExperience and owns pause/resume behavior. Dash movement and cooldown timing use scaled gameplay time.

**Tech Stack:** Unity C#, Unity Input System, Unity UI (uGUI), existing `PlayerExperience` level-up event.

**Spec:** `docs/superpowers/specs/2026-10-26-player-ability-progression-design.md`

## Global Constraints

- Abilities start at level 0; the level 1 entry unlocks the skill.
- Each ability's level 1 cooldown starts at 10 seconds; cooldown values are manually configurable at each level.
- Dash moves over time at a configured speed and distance.
- Level-up choices pause gameplay, support one to three options, and process every gained level.
- Preserve full HP healing on level up.
- Do not add or run automated tests unless requested.

## Review Focus

- Ability at level 0: J/K must not activate it; level 1 choice unlocks it.
- Empty or short per-level configuration: no invalid option or out-of-range lookup; only configured next levels are offered.
- Cooldown readiness: starts ready after unlock, blocks repeated activation, and pauses while the selection modal is open.
- Dash movement: configured speed and distance move the player over time without an instantaneous position jump.
- Multiple simultaneous level gains: show one choice per gained level, remain paused between choices, restore prior time scale when complete.

---

### Task 1: Per-level ability configuration and timed ability state

**Files:**
- Modify: `Assets/Game/Runtime/Player/PlayerAbilityController.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerCharacter.cs`

**Interfaces:**
- Produces: nested `[Serializable]` `DashLevelSettings` and `SuckLevelSettings` classes in `PlayerAbilityController`, stored as `List<DashLevelSettings> dashLevels` and `List<SuckLevelSettings> suckLevels`. Public accessors are `DashLevel`, `SuckLevel`, `DashCooldownRemaining`, `SuckCooldownRemaining`, `CanUpgradeDash`, `CanUpgradeSuck`, `TryUpgradeDash()`, and `TryUpgradeSuck()`.
- `DashLevelSettings`: `float distance`, `float speed`, `float cooldown`; `SuckLevelSettings`: `float range`, `float coneAngle`, `float pullDuration`, `float pullSpeed`, `float cooldown`.
- Level N reads list index N - 1; level 0 reads no entry and is locked. Missing entries are never indexed or offered.

- [x] Replace base-plus-increment settings and level-1 defaults with serialized per-level entry lists; default each first unlocked entry's cooldown to 10 seconds.
- [x] Make upgrade operations increment from 0 to 1 and thereafter only when a configured next entry exists.
- [x] Gate J/K input on unlocked level and cooldown availability; start the configured cooldown only after activating.
- [x] Change Dash to advance by configured speed over distance in scaled gameplay time; prevent a second dash while one is active.
- [x] Preserve Suck cone filtering and apply the current entry's pull settings.

### Task 2: Reusable level-up choice contract and selector

**Files:**
- Create: `Assets/Game/Runtime/Player/ILevelUpChoiceSource.cs`
- Create: `Assets/Game/Runtime/Player/LevelUpChoicePanel.cs`
- Modify: `Assets/Game/Runtime/FreedomJam.Game.asmdef`
- Modify: `Assets/Game/Runtime/Player/PlayerAbilityController.cs`

**Interfaces:**
- Create `LevelUpChoice` in `ILevelUpChoiceSource.cs` with readonly `string id`, `string title`, and `string description` fields.
- `ILevelUpChoiceSource`: `void AppendAvailableChoices(List<LevelUpChoice> choices)` and `bool TryApplyChoice(string id)`.
- `PlayerAbilityController` implements `ILevelUpChoiceSource`; choice IDs are `dash` and `suck`.
- `LevelUpChoicePanel`: public `Show(IReadOnlyList<LevelUpChoice> choices, Action<string> onSelected)` and `Hide()` methods; serialize three uGUI `Button` components with title/description `Text` fields and a modal root.

- [x] Add the choice source interface with stable choice ID, display title, next-level description, and apply operation.
- [x] Have the ability controller expose Dash and Suck upgrade choices only while a next configured level exists.
- [x] Add the `Unity.ugui` assembly reference to `FreedomJam.Game.asmdef`.
- [x] Create a centered uGUI modal with a title, up to three generated buttons, and choice descriptions.
- [x] On choice, call the supplied selection callback and hide the panel; do not show it for an empty choice list.

### Task 3: XP level-up queue and time-scale lifecycle

**Files:**
- Create: `Assets/Game/Runtime/Player/PlayerLevelUpFlow.cs`
- Modify: `Assets/Game/Runtime/Player/LevelUpChoicePanel.cs`
- Modify: `Assets/GameJamFoundation/Runtime/Input/GameInputController.cs`

**Interfaces:**
- `PlayerLevelUpFlow` serializes `PlayerCharacter`, a `LevelUpChoicePanel` prefab, and `MonoBehaviour[] choiceSources`; it resolves sources to `ILevelUpChoiceSource` and subscribes in `Start()` to `PlayerCharacter.Experience.LeveledUp`.
- Flow queues `int` level events, calls `LevelUpChoicePanel.Show(IReadOnlyList<LevelUpChoice>, Action<string>)`, applies the matching source choice, and advances the queue.
- Produces: one queued selection per level gained, gameplay pause while choices are pending, restoration of the pre-modal `Time.timeScale`.
- The modal pauses `Time.timeScale` without changing `GameFlowController.State`, temporarily disables foundation pause input to prevent Escape from opening a competing pause state, and restores both prior values after selection.

- [x] Instantiate the panel prefab and subscribe/unsubscribe to the player's experience event with the flow component lifecycle.
- [x] Queue each level-up and present one set of choices at a time, even when one XP grant gains multiple levels.
- [x] Save `Time.timeScale` and `GameInputController.PauseInputEnabled` before opening the first modal; pause time, suppress the Escape pause action, then restore both prior values after the queue drains.

### Task 4: Sandbox and prefab integration

**Files:**
- Modify: `Assets/Game/PlayerSandbox/Editor/PlayerSandboxBuilder.cs`
- Modify: `Assets/Game/PlayerSandbox/PlayerDebugOverlay.cs`
- Modify: `Assets/Game/Prefabs/Player.prefab`
- Create or modify: reusable level-up panel prefab and its metadata under `Assets/Game/Presentation/LevelUp/`

**Interfaces:**
- Builder adds `PlayerLevelUpFlow` to the player prefab and wires its panel prefab and ability controller as a choice source; both sandbox scenes already instantiate that prefab.
- Debug overlay displays ability level and cooldown remaining; remove the direct ability-upgrade buttons so XP level-ups drive upgrades.
- The reusable panel prefab is a screen-space overlay Canvas; `LevelUpChoicePanel` owns its display and three Button references.

- [x] Extend the sandbox builder to create/update the selector panel prefab with a centered panel and three-option button layout.
- [x] Wire the selector and controller on the player prefab without resetting unrelated serialized gameplay tuning, intentionally empty level lists, or existing choice sources.
- [x] Update the debug overlay to show locked/unlocked level and cooldown state while retaining XP and damage controls.

### Task 5: Unity compile verification and plan closeout

**Files:**
- Modify: this plan's completion checkboxes and verification note.

- [x] Allow Unity to compile and inspect the editor console for compilation errors or warnings introduced by this change.
- [x] Inspect the player prefab and generated UI asset references; confirm unrelated creature and player tuning assets remain unchanged.
- [x] Record the Unity compile result and manual verification limits; do not run automated tests.

## Completion

- Unity 6000.6.6f1 script compilation completed with no C# errors or warnings. The only console warning was the Unity MCP WebSocket's `Unexpected receive error: WebSocket is not initialised` message.
- Player prefab references its ability controller, level-up flow, and generated three-slot screen-space panel prefab. Serialized Dash/Suck level 1 cooldowns are 10 seconds; level 2 cooldowns are 9 seconds.
- No automated tests or Play Mode checks were run.


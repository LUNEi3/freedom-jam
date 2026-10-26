# Player Abilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add tunable, multi-level Dash and Suck abilities to the player, bound to J and K.

**Architecture:** A `PlayerAbilityController` on the player handles Input System key presses, tunable ability levels, and target selection. Dash delegates movement to `PlayerCharacter`; Suck uses the existing cone utility, orb homing, and a temporary pull override on enemies. Sandbox buttons expose level upgrades for testing.

**Tech Stack:** Unity 6000.6, C#, Unity Input System.

**Spec:** `docs/superpowers/specs/2026-10-26-player-abilities-design.md`

## Global Constraints

- Do not modify `Assets/GameJamFoundation/`.
- Keep player movement, enemy movement, and pooled pickup behavior authoritative in their existing runtime components.
- Use `ConeMath` for Suck targeting.
- Ability distances and upgrade increments are serialized/configurable.
- Do not add or run automated tests unless the user requests them.

## Review Focus

- Presses while the player is dead or the keyboard is unavailable do nothing.
- Targets outside the XZ-plane cone or range are unaffected.
- Pooled enemies and XP orbs clear temporary suction state on reuse.
- Ability levels stop at their configured maximum.

---

### Task 1: Ability input and tunable levels

**Files:**
- Create: `Assets/Game/Runtime/Player/PlayerAbilityController.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerCharacter.cs`
- Modify: `Assets/Game/PlayerSandbox/Editor/PlayerSandboxBuilder.cs`
- Modify: `Assets/Game/PlayerSandbox/PlayerDebugOverlay.cs`
- Modify: `Assets/Game/Prefabs/Player.prefab` through the existing builder

**Interfaces:**
- `PlayerCharacter.DashForward(float distance)` applies a bounded, horizontal forward displacement while alive.
- `PlayerAbilityController` exposes `DashLevel`, `SuckLevel`, current effective distances, and `UpgradeDash()` / `UpgradeSuck()` methods.

- [x] Add the controller to the player prefab through the sandbox builder; read J/K through `Keyboard.current`.
- [x] Implement one-shot forward dash and level-based distance scaling.
- [x] Add level/distance readouts and upgrade buttons to the sandbox debug overlay.

### Task 2: Suck targeting and movement

**Files:**
- Modify: `Assets/Game/Runtime/Enemy/EnemyCharacter.cs`
- Modify: `Assets/Game/Runtime/Pickups/XpOrb.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerAbilityController.cs`

**Interfaces:**
- `EnemyCharacter.ApplySuction(PlayerCharacter source, float speed, float duration)` temporarily overrides chase movement toward the player.
- `XpOrb.StartSuction(float speed)` starts its existing homing behavior at the supplied speed.

- [x] Use `ConeMath` to select alive enemies and active XP orbs inside the configured forward cone/range.
- [x] Apply the configured pull pulse to enemies and start orb homing; reset overrides on pooling/character reset.
- [x] Enforce the maximum skill level and calculate effective range from serialized base and per-level values.

### Task 3: Unity verification

- [x] Confirm Unity compilation and no new Console errors.
- [x] Manually exercise J/K, upgrade buttons, cone/range filtering, and pooled reuse in IntegrationSandbox.
- [x] Record verification results; automated tests are omitted by instruction.

Verification performed: Unity compilation completed without warnings or errors. A manual Play Mode check measured a 3-unit Dash; upgrading increased effective distance from 3 to 4. Suck moved an enemy from 5 units to 0.85 units (contact range) after three movement steps and began XP-orb homing (6 units to 5.85 after one frame). Automated tests were not run per instruction.

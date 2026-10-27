# Player Revive, Explosion, and Magnet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one-time Revive and Magnet unlocks plus a configurable, level-based Explosion ability, integrated with level-up choices and existing player feedback.

**Architecture:** Add focused ability components that implement `ILevelUpChoiceSource`. Add a lethal-damage interception hook to `Character`, a player-owned time-scale coordinator used by ability effects and level-up pauses, and update the level-up flow to draw up to three choices from all eligible upgrades.

**Tech Stack:** Unity 6, C#, uGUI, Unity Input System, existing `Character`, `ILevelUpChoiceSource`, `XpOrb`, `CameraShake`, and `CharacterVisualFeedback` APIs.

**Spec:** `docs/superpowers/specs/2026-10-27-player-revive-explosion-magnet-design.md`

## Global Constraints

- Revive restores 50% max HP after a 0.8-second gameplay freeze.
- Revive then grants one second of green flash, invulnerability, and boosted movement speed, and triggers camera shake.
- Explosion uses L, the Dash VFX placeholder, 10% time scale for 0.7 seconds, then configurable area damage and camera shake.
- Magnet is a one-time passive unlock that pulls all active XP orbs, including later drops.
- The existing level-up panel shows no more than three choices.
- Keep existing Dash/Suck behavior and player/enemy death semantics intact.
- Do not add or run tests unless explicitly requested; validate code and prefab wiring with project tooling/inspection.

## Review Focus

- Lethal damage with Revive unlocked must not call `OnDied` or finish the run; lethal damage without Revive must retain current behavior.
- Ability time-scale leases must compose correctly when level-up pause, Revive freeze, or Explosion slow motion overlap.
- Revive invulnerability and speed boost must last the configured real-time duration after the freeze.
- Explosion must skip dead/inactive enemies, apply damage once per living enemy inside the radius, and not prevent player movement while in slowdown.
- Magnet must acquire newly spawned orbs and must not repeatedly affect collected/pooled orbs.
- Random level-up selection must contain at most three unique available choices and allow every eligible ability to become selectable over successive levels.

---

### Task 1: Add composable player gameplay time-scale requests

**Files:**
- Create: `Assets/Game/Runtime/Player/GameplayTimeScaleCoordinator.cs`
- Create: `Assets/Game/Runtime/Player/GameplayTimeScaleCoordinator.cs.meta`
- Create: `Assets/GameJamFoundation/Runtime/App/IGameplayTimeScaleOverride.cs`
- Create: `Assets/GameJamFoundation/Runtime/App/IGameplayTimeScaleOverride.cs.meta`
- Modify: `Assets/GameJamFoundation/Runtime/App/GameFoundationBootstrap.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerLevelUpFlow.cs`
- Modify: `Assets/Game/Prefabs/Player.prefab`

**Interfaces:**
- Produces `int Acquire(float scale)` and `void Release(int requestId)` on the player-owned coordinator. The effective scale is the minimum active request; with no requests, restore the scale captured on the first acquisition.
- Implements the Foundation-owned `IGameplayTimeScaleOverride` bridge so Foundation pause/resume requests use a coordinator pause lease.
- `PlayerLevelUpFlow` uses one request at scale `0`; ability effects acquire temporary requests at their configured scales and release them on completion/cancellation.

- [x] Add coordinator with unique request IDs, baseline scale capture, minimum-scale composition, and cleanup that releases outstanding requests.
- [x] Route Foundation pause/resume through its own coordinator lease while preserving direct time-scale behavior when no gameplay coordinator is present.
- [x] Replace `PlayerLevelUpFlow`'s direct `Time.timeScale` save/write/restore logic with a coordinator pause request, including cancellation and disable paths.
- [x] Add/configure the coordinator on `Player.prefab` and wire the level-up flow to it.
- [x] Inspect serialized references and confirm the coordinator begins with no active requests and the existing flow still owns only its pause lease.

### Task 2: Add a pre-death interception hook and temporary revive movement state

**Files:**
- Modify: `Assets/Game/Runtime/Common/Character.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerCharacter.cs`
- Modify: `Assets/Game/Presentation/Feedback/CharacterVisualFeedback.cs`

**Interfaces:**
- `Character` adds `protected virtual bool TryPreventDeath(DamageInfo killingBlow)`, called after damage events when HP reaches zero and before `Die`; `true` skips `Die`.
- `PlayerCharacter` adds a reference to `ReviveAbility`, delegates lethal interception, exposes a configured temporary movement multiplier setter/lease, and supports explicit timed invulnerability based on `Time.unscaledTime`.
- `CharacterVisualFeedback` remains responsible for presentation and exposes `PlayFlash(Color color, float duration)` for Revive.

- [x] Add the death interception hook with a default `false` implementation so enemy death remains unchanged.
- [x] Add player-only movement multiplier and unscaled timed-invulnerability support; preserve the existing hit invulnerability behavior.
- [x] Add/confirm a white-to-green revive flash presentation entry point without altering ordinary damage flash colors.
- [x] Inspect damage flow paths for revived and non-revived lethal hits and confirm only the former bypasses `OnDied`.

### Task 3: Implement one-time Revive and passive Magnet components

**Files:**
- Create: `Assets/Game/Runtime/Player/ReviveAbility.cs`
- Create: `Assets/Game/Runtime/Player/ReviveAbility.cs.meta`
- Create: `Assets/Game/Runtime/Player/MagnetAbility.cs`
- Create: `Assets/Game/Runtime/Player/MagnetAbility.cs.meta`
- Modify: `Assets/Game/Prefabs/Player.prefab`

**Interfaces:**
- Both components implement `ILevelUpChoiceSource`, with stable choice IDs `revive` and `magnet`.
- `ReviveAbility.TryPreventLethalDamage(DamageInfo info)` succeeds once when unlocked. Its serialized defaults are freeze `0.8f`, active duration `1f`, restored HP ratio `0.5f`, movement multiplier `1.5f`, green feedback color, and shake settings.
- `MagnetAbility` has serialized pull speed and scan interval settings; while unlocked it calls `XpOrb.StartSuction(speed)` on active positive-XP orbs.

- [x] Implement Revive upgrade availability/application, lethal-damage claim, unscaled freeze, half-max-HP restore, one-second boost/invincibility/green flash, and camera shake.
- [x] Implement Magnet's one-time upgrade and periodic active-orb scan, including future orbs.
- [x] Add both components to `Player.prefab` and serialize their player, coordinator, feedback, and camera-shake dependencies.
- [x] Inspect the configured component fields and confirm each one-time choice disappears after unlock.

### Task 4: Implement configurable level-based Explosion

**Files:**
- Create: `Assets/Game/Runtime/Player/ExplosionAbility.cs`
- Create: `Assets/Game/Runtime/Player/ExplosionAbility.cs.meta`
- Modify: `Assets/Game/Prefabs/Player.prefab`

**Interfaces:**
- `ExplosionAbility` implements `ILevelUpChoiceSource` with choice ID `explosion` and a serialized ordered list of `ExplosionLevelSettings` containing radius, damage, cooldown, slow-motion scale, and slow-motion duration.
- Serialized defaults start with scale `0.1f`, duration `0.7f`; the existing Dash VFX ParticleSystem is a placeholder reference.

- [x] Implement configurable level availability/application and L-key activation while unlocked, alive, and off cooldown.
- [x] On activation play the assigned Dash VFX, request slowdown, wait 0.7 unscaled seconds, release slowdown, damage each living active enemy in radius once, and shake once.
- [x] Add Explosion component to prefab, assign the existing Dash VFX reference, and author editable starting and next-level settings.
- [x] Inspect serialized level entries, input binding behavior, and target filtering.

### Task 5: Expand level-up source wiring and make choice draws scalable

**Files:**
- Modify: `Assets/Game/Runtime/Player/PlayerLevelUpFlow.cs`
- Modify: `Assets/Game/Prefabs/Player.prefab`
- Inspect: `Assets/Game/Runtime/Player/LevelUpChoicePanel.cs`

**Interfaces:**
- `PlayerLevelUpFlow` gathers the existing Dash/Suck source and the three new sources. It chooses a random unique subset of up to three choices from the full eligible list.
- Choice application dispatches to the matching source as it does today; `LevelUpChoicePanel` layout and three-card maximum remain unchanged.

- [x] Implement selection without replacement across all collected available choices and retain original choices for ID-based application.
- [x] Add serialized source references for Revive, Explosion, and Magnet on the player prefab.
- [x] Confirm the panel still receives at most three choices and each source remains eligible according to its own upgrade state.

### Task 6: Project integration review

**Files:**
- Review modified scripts and `Assets/Game/Prefabs/Player.prefab`
- Update: `docs/superpowers/specs/2026-10-27-player-revive-explosion-magnet-design.md` only if implementation decisions materially differ

- [x] Review all new serialized fields and prefab references for null-safe behavior and stable IDs.
- [x] Inspect Unity Editor console/compiler status and resolve compile or serialization errors without adding/running tests.
- [x] Manually trace overlap scenarios: level-up pause with Explosion, revive during level-up pause, and a lethal hit without Revive unlocked.
- [x] Summarize changed files and any runtime verification limitations.

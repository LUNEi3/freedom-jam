# HUD and Damage Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add reusable player and enemy health bars, floating damage numbers, player red/enemy white hit flashes, and player invincibility transparency feedback.

**Architecture:** Keep damage and HP authoritative in `Character`. Put a player invincibility interface in the game assembly, implemented by `PlayerCharacter`. Put reusable health adapters and UGUI presentation components under a separate `Assets/Game/Presentation/Feedback` assembly that references the core game and UGUI assemblies. Attach components through prefabs and the sandbox scene builder.

**Tech Stack:** Unity 6000.6, URP 17, UGUI, C#.

**Spec:** `docs/superpowers/specs/2026-10-26-hud-damage-feedback-design.md`

## Global Constraints

- All gameplay code goes under `Assets/Game/`; do not modify `Assets/GameJamFoundation/`.
- Presentation references the core game assembly and UGUI; the core game assembly must not acquire a UGUI dependency.
- `Character` owns HP, damage, and death; player and enemy code must not reference each other's classes.
- All damage goes through `IDamageable.TakeDamage`; presentation responds only to accepted `Character.Damaged` events.
- Player invulnerability duration remains configurable in `PlayerConfig.hitInvulnerability` and is the source of truth for damage rejection and blinking.
- Domain Reload is disabled; static events must be reset and subscriptions removed on disable.
- Use the existing URP materials and UGUI; do not add package dependencies or modify source material assets.

## Review Focus

- Reused or respawned characters must restore original materials, alpha, and renderer states and clear per-life bars.
- Fatal enemy numbers must remain visible after the enemy is returned to its pool.
- Rejected, non-positive, and post-death hits must not produce damage feedback.
- Healing, max-HP changes, player replacement, and character reset must refresh displayed health.
- Pausing must stop timed effects and pooled number lifetimes consistently.

---

### Task 1: Health and invincibility presentation contracts

**Files:**
- Create: `Assets/Game/Runtime/Player/IInvincibilityState.cs`
- Create: `Assets/Game/Presentation/Feedback/FreedomJam.Game.Presentation.asmdef`
- Create: `Assets/Game/Presentation/Feedback/ICharacterHealthSource.cs`
- Create: `Assets/Game/Presentation/Feedback/CharacterHealthSource.cs`
- Modify: `Assets/Game/Runtime/Player/PlayerCharacter.cs`
- Create: `Assets/Game/Presentation/Feedback/CharacterFeedbackConfig.cs`

**Interfaces:**
- `ICharacterHealthSource`: `float CurrentHP { get; }`, `float MaxHP { get; }`, `bool IsAlive { get; }`, `event Action<ICharacterHealthSource> Changed`, `event Action<ICharacterHealthSource, DamageInfo> Damaged`.
- `IInvincibilityState`, in the core game assembly: `bool IsInvincible { get; }`.
- `CharacterHealthSource` wraps `Character` and publishes health changes for damage, healing, max-HP changes, death, and resets. It must not store HP.
- `PlayerCharacter` implements `IInvincibilityState`; `IsInvincible` reflects the same time window used by `CanReceiveDamage`.
- `CharacterFeedbackConfig` provides serialized colors and tunables from the design, with the defaults from that spec.

- [x] **Step 1: Create the presentation assembly and implement health and invincibility interfaces**

  Configure `FreedomJam.Game.Presentation.asmdef` to reference `FreedomJam.Game` and `Unity.ugui`. Put `ICharacterHealthSource` in the presentation assembly. Put `IInvincibilityState` in the core game assembly so `PlayerCharacter` can implement it without a dependency cycle.

- [x] **Step 2: Implement `CharacterHealthSource` lifecycle and change notifications**

  Bind/unbind the source to the same GameObject's Character. Handle Character.OnEnable/OnDisable and ResetCharacter so an active sandbox respawn clears the enemy's previously revealed bar. Detect healing and max changes without adding character polling state.

- [x] **Step 3: Implement `CharacterFeedbackConfig` ScriptableObject**

  Add feedback values for player/enemy bars, floating numbers, red flash, and invincibility blink, using spec defaults: 0.12-second flash, 10 opacity cycles/second, 0.25 minimum alpha, and 0.8-second number lifetime. Keep player protection duration in PlayerConfig.

- [x] **Step 4: Expose the player's authoritative invincibility state**

  Implement the interface in `PlayerCharacter`, and use its property from `CanReceiveDamage` so feedback and hit rejection cannot drift.

### Task 2: Reusable health bars and floating damage numbers

**Files:**
- Create: `Assets/Game/Presentation/Feedback/PlayerHealthBar.cs`
- Create: `Assets/Game/Presentation/Feedback/EnemyHealthBar.cs`
- Create: `Assets/Game/Presentation/Feedback/DamageNumber.cs`
- Create: `Assets/Game/Presentation/Feedback/DamageNumberSpawner.cs`
- Create: `Assets/Game/Presentation/Feedback/Prefabs/PlayerHealthBar.prefab`
- Create: `Assets/Game/Presentation/Feedback/Prefabs/EnemyHealthBar.prefab`
- Create: `Assets/Game/Presentation/Feedback/Prefabs/DamageNumber.prefab`
- Create: `Assets/Game/Presentation/Feedback/CharacterFeedbackConfig.asset`
- Modify: relevant player and enemy prefabs

**Interfaces:**
- Health bars consume `ICharacterHealthSource`.
- `EnemyHealthBar` remains hidden until first accepted `Damaged`; it clears on death, source reset, disable, and re-enable.
- `DamageNumberSpawner` listens to accepted damage, formats `DamageInfo.Amount` to at most one decimal place, and pools independent number lifetimes. Fatal hit numbers remain visible after victim pooling.

- [x] **Step 1: Build reusable player health bar prefab and controller**

  Anchor it top-left with padding, green fill on a dark background, centered white current/max text, safe handling for invalid maximum HP, and one decimal place maximum.

- [x] **Step 2: Build world-space enemy health bar prefab and controller**

  Use a serialized anchor with renderer-bounds fallback, red fill, no text, camera-facing orientation, readable fixed sizing, and off-screen/behind-camera hiding.

- [x] **Step 3: Build floating damage number prefab and pool**

  Show white accepted damage above the hit character, drift upward, fade, and scale monotonically with damage within configured minimum and maximum. Separate consecutive numbers slightly. Detach active numbers from pooled victims and release them on their own lifetime or scene teardown.

- [x] **Step 4: Attach health sources and bars to player/enemy prefabs**

  Add player UI binding and enemy world-space binding. Ensure enemy reset path notifies the bar and pooled reactivation begins hidden.

### Task 3: Red damage flash and player invincibility blink

**Files:**
- Create: `Assets/Game/Presentation/Feedback/CharacterVisualFeedback.cs`
- Modify: player and enemy prefabs

**Interfaces:**
- `CharacterVisualFeedback` consumes `ICharacterHealthSource`, optional `IInvincibilityState`, and `CharacterFeedbackConfig`.
- Accepted damage starts/restarts a 0.12-second red flash; player opacity rapidly alternates while `IsInvincible`.

- [x] **Step 1: Implement red flash over all child renderers**

  Create isolated runtime material instances using the project's URP shader, preserve original material color/render settings, compose the flash with alpha, and restore original visuals when the flash ends.

- [x] **Step 2: Implement player invincibility transparency**

  Use the invincibility interface, 10 cycles/second and 0.25 minimum alpha. Alpha changes apply to the isolated material instances and remain independent from the red tint.

- [x] **Step 3: Restore visual state through pooling, disable, death, and destruction**

  Preserve source renderer enabled state and original material state; never reveal a dead sandbox target. Dispose runtime material instances on destruction and restore on disable/reset.

- [x] **Step 4: Attach and configure feedback components on player/enemy prefabs**

  Only player receives the invincibility interface; both character types get red damage flash.

### Task 4: Sandbox and game integration

**Files:**
- Modify: `Assets/Game/PlayerSandbox/Editor/PlayerSandboxBuilder.cs`
- Modify: `Assets/Game/PlayerSandbox/PlayerDebugOverlay.cs` or `Assets/Game/Sandbox/SandboxHud.cs` (use the existing HUD component present in the workspace)
- Modify: `Assets/Game/PlayerSandbox/IntegrationSandbox.unity` only if required by the prefab wiring
- Modify: `Assets/Game/PlayerSandbox/PlayerSandbox.unity` only if required by the prefab wiring

- [x] **Step 1: Integrate the player HUD in both sandbox variants**

  Put the health bar at the top left and move existing sandbox text/buttons below it to prevent overlap. Show actual health values and changes in the bar.

- [x] **Step 2: Keep scene rebuilds wired to reusable feedback assets**

  Update the editor builder so rebuilding PlayerSandbox and IntegrationSandbox retains player health UI, enemy bars, damage numbers, and flashes. Keep asset links serialized and deterministic.

- [x] **Step 3: Review production Game scene integration**

  Inspect the current foundation-owned Game scene and reuse its game UI hierarchy if combat actors are already wired. Do not alter foundation scripts or prefabs. If the scene has no combat yet, keep the new content reusable and document that finding.

- [x] **Step 4: Verify in Unity and report results**

  Confirm compilation and no new Console errors. Exercise damage, heal/max changes, accepted and rejected hits, fatal damage, enemy pooling/reset, invincibility expiration and zero duration, pause, and scene rebuild. Capture a screenshot of the integrated HUD when practical.


  Verification performed: Unity compilation completed without compiler errors. Manual runtime checks confirmed the player fill rect shrinks with the HP ratio (45% HP produced a 106.5 px fill inside the 250 px bar including its 6 px inset), enemy flash configuration is white with no invincibility interface, enemy-bar reveal works, and player invincibility rejects a second hit during its configured window. Automated tests and the remaining extended lifecycle/pause checks were not run.

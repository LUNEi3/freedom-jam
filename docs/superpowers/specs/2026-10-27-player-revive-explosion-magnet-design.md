# Player Revive, Explosion, and Magnet Design

## Goal

Add three unlockable player abilities that participate in the existing level-up choice flow:

- **Revive**: one-time unlock. On lethal damage, pause gameplay, wait 0.8 seconds, restore half of max HP, then provide one second of green feedback, invincibility, and boosted movement speed. Trigger camera shake.
- **Explosion**: level-based upgrade. On key `L`, play the existing Dash VFX as a placeholder, slow gameplay to 10% speed for 0.7 seconds, then damage enemies in a configurable area and shake the camera.
- **Magnet**: one-time unlock. Once selected, continuously pull all active XP orbs toward the player, including orbs spawned later.

## Existing project constraints

- `PlayerAbilityController` currently owns Dash and Suck level settings, input, VFX, and upgrade choices.
- `PlayerLevelUpFlow` gathers `ILevelUpChoiceSource` instances, pauses by writing `Time.timeScale = 0`, and restores a saved value after choices.
- `LevelUpChoicePanel` displays at most three choices, currently in source order.
- `Character.TakeDamage` invokes death immediately when HP reaches zero. Revive must intercept lethal damage before `Die` runs.
- `PlayerCharacter` owns movement and invulnerability. XP orbs expose `StartSuction(float speed)`.
- Camera shake is available through `CameraShake`.

## Recommended design

Use separate `ReviveAbility`, `ExplosionAbility`, and `MagnetAbility` components. Each implements `ILevelUpChoiceSource`; Revive and Magnet offer a single unlock, while Explosion exposes serialized per-level settings (at minimum radius, damage, and cooldown). This keeps one-time unlock behavior and future abilities out of the existing Dash/Suck controller.

Add a protected virtual lethal-damage interception hook to `Character`. `PlayerCharacter` delegates the hook to its unlocked Revive component. When Revive claims a lethal hit, the player remains alive and the ordinary death/run-finish path is skipped. Revive waits using unscaled time while the world is frozen, restores half max HP, then applies its one-second green feedback, temporary movement multiplier, and invulnerability. Its available state is consumed once it activates.

Use a small shared time-scale request service for gameplay pause and temporary slow motion. Requests compose by taking the lowest requested scale, so Explosion finishing its slow-motion window cannot unpause a level-up screen, and closing a level-up screen cannot cancel an active Explosion slowdown. Revive's freeze uses the same service. Route the Foundation pause menu through a small Foundation-owned interface implemented by the player coordinator so menu pause also composes with active effects. Durations for effects that must progress while frozen use unscaled time.

For upgrades, gather all eligible choices and randomly select up to three when a level-up opens. This preserves the panel's three-card layout while allowing all five abilities to appear over successive level-ups. A selected choice is applied to the originating source. Keep the draw injectable/testable through a small selection helper or deterministic random source.

Explosion checks active living enemies within its configured radius after the slowdown window, applies its configured damage through the existing damage API, and triggers one camera shake per explosion. Magnet periodically finds active XP orbs and calls `StartSuction` at a configurable speed; periodic discovery includes future drops without a per-orb subscription system.

## Alternatives considered

1. **Put all abilities into `PlayerAbilityController`**: fewer components and prefab references, but couples permanent unlocks, revive/death handling, area damage, and cooldown input to one growing controller.
2. **Separate components (recommended)**: follows the existing choice-source interface, keeps each ability configurable and independently reusable, and requires adding components/references to the player prefab.
3. **Keep first-three ordering**: smallest change, but later abilities can remain permanently unavailable while earlier abilities can still upgrade.

## Decisions

- Revive restores **50% of max HP**.
- Explosion uses **L** and starts at **10% time scale for 0.7 seconds**.
- Magnet is **passive after unlock** and continuously pulls all XP orbs.
- Abilities are separate components implementing the existing choice-source interface.
- Level-up choices are a random sample of up to three eligible options.
- Shared time-scale requests coordinate level-up pause, revive freeze, and Explosion slow motion.
- Foundation pause/resume acquires and releases its own coordinator pause request.

## Open configuration defaults

Explosion's starting radius, damage, cooldown, and per-level values should be serialized settings on the Explosion component and use conservative gameplay defaults that can be tuned in the Inspector. Revive's temporary speed multiplier, Magnet pull speed, and polling interval should also be serialized settings. These are tuning defaults rather than player-facing decisions.

## Acceptance criteria

- Revive appears as an upgrade only while locked, prevents one lethal hit from ending the run, freezes gameplay for 0.8 seconds, restores half max HP, then applies green flash, invincibility, boosted movement for one second, and camera shake. It cannot activate again.
- Explosion appears while another configured level exists, uses `L` while unlocked and off cooldown, plays Dash VFX, applies 10% time scale for 0.7 seconds, damages living enemies in range afterward, and shakes the camera. Its level settings are Inspector-configurable.
- Magnet appears only while locked and, once unlocked, pulls every active XP orb, including future drops.
- Level-up selection can expose all eligible abilities over successive level-ups despite the three-choice UI cap.
- Level-up pause, revive freeze, and Explosion slowdown do not incorrectly restore or overwrite each other's time scale.
- Pausing or resuming through the Foundation pause menu during an ability effect does not unpause gameplay or leave a stale slow-motion scale.
- No existing Dash/Suck behavior, death flow, or ability upgrade behavior regresses.

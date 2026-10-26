# Player Dash and Suck Abilities

Date: 2026-10-26
Status: Implemented

## Intent

Add two starter abilities to exercise future player upgrades: Dash on J and Suck on K. Both have levels and tunable distances so future level-up choices can strengthen them.

## User requirements

- Dash moves the player forward by a variable distance.
- Suck pulls XP orbs and enemies inside a forward cone and variable range toward the player.
- J activates Dash; K activates Suck.
- Skills support multiple upgrade levels.

## Implementation choices

- Add a player ability controller on the existing player prefab. It reads J/K through the project's Input System.
- Dash is a one-press forward displacement, with distance increasing per level.
- Suck is a one-press pulse. Eligible enemies are pulled toward the player for a short configurable duration; eligible XP orbs begin their existing homing movement. Range increases per level and cone angle, pull speed, and duration remain inspector tunables.
- Expose upgrade buttons and current levels in the sandbox debug overlay for immediate iteration. Do not add a level-up selection screen or ability damage/cooldowns in this task.
- Preserve pooling: reset an orb's suction override when reused and clear enemy pull state on character reset.

## Constraints

- Keep player, enemy, and pickup systems in the game runtime assembly; do not modify foundation scripts.
- Reuse `ConeMath` for the forward cone and keep enemy movement authoritative in `EnemyCharacter`.
- Keep skill parameters serialized and mutable so upgrades are not tied to constants.

## Verification

- Compile in Unity.
- In IntegrationSandbox, verify J moves the player by the configured forward distance; upgrades increase the distance.
- Verify K affects only enemies and XP orbs in front and within range; enemies move inward and orbs home to the player; upgrades increase range.
- Verify pooled enemies/orbs do not retain an old suction state.
- Do not add or run automated tests unless requested.

# HUD and Damage Feedback

Date: 2026-10-26
Status: Proposed for approval

## Intent and scope

Make health, accepted hits, and player invincibility readable during combat. Implement reusable presentation components on top of existing combat. HP, damage acceptance, and death remain owned by Character; the existing player hit-invulnerability timer remains authoritative.

User requirements: player HP bar at the top left with a centered number; red enemy HP bars above their heads with no numbers, visible only after damage; white floating damage numbers sized by damage; red player and white enemy damage flashes; rapid player transparency changes throughout configurable invincibility.

## Approaches

The user selected reusable components over character-specific feedback scripts. Reusable components consume health and invincibility interfaces and accepted-damage notifications. Character-specific scripts would require fewer components but duplicate presentation across player and enemy classes.

Use the project's existing UGUI system. This is an implementation recommendation, not a separate user requirement.

## Health and interface boundaries

- Provide a small read-only health interface exposing current HP, maximum HP, alive state, and health/damage notifications. A Character adapter can wrap existing Character properties and Damaged/Died events; it must also notice healing, stat-driven maximum changes, and ResetCharacter. Do not add a second HP store or change damage acceptance.
- Provide an invincibility-state interface implemented by PlayerCharacter, exposing the authoritative timer's current state. Clamp configured negative durations to zero. Duration remains PlayerConfig.hitInvulnerability, currently 0.5 seconds; zero disables protection and its transparency flash.
- Presentation consumes interfaces. Existing IDamageable, damage callers, enemy behavior, and gameplay event signatures remain compatible. Do not require Player code to reference Enemy code or vice versa.
- Keep reusable UI and feedback under Assets/Game. Use a presentation assembly referencing the game assembly and UGUI if needed, avoiding changes to the shared game assembly definition. Small interfaces consumed by player code must remain in the core game assembly to avoid a dependency cycle.

## Health bars

- Player: green fill on a dark background, anchored to the screen's top-left corner with padding. Fill is clamped currentHP/maxHP, with a safe empty result for a non-positive maximum. Centered white text shows current / maximum, with up to one fractional digit.
- Enemy: red fill on a dark background, no text. The bar follows an adjustable head anchor or renderer-bounds fallback, faces the camera, and maintains readable sizing as the character grows. Hide when off-screen or behind the camera.
- An enemy bar starts hidden, appears after its first accepted damaging hit, and remains visible for the rest of that life. Hide on death, disable/despawn, and reset/reuse. Reset must also work for sandbox targets that respawn without disabling their GameObject.
- Player bars update after healing, maximum-HP growth, respawn, and damage. Rebind safely when the player changes or is unavailable.
- Use reusable prefabs and serialized bindings rather than placing combat behavior inside UI code. Avoid overlap with the existing sandbox debug overlay by moving that overlay below the player bar.

## Floating damage numbers

- Spawn one white number above the victim for each accepted damage event, including fatal hits. Ignored hits during invincibility, non-positive hits, and hits on dead characters produce no feedback.
- Proposed default: display DamageInfo.Amount, matching the existing combat log, including overkill. Normal and fire hits remain separate events/numbers. Format up to one fractional digit.
- Rise and fade over a configurable lifetime, with slight configurable horizontal separation for consecutive hits. Size increases monotonically with damage and is clamped to configurable minimum and maximum sizes.
- Pool number objects. Their lifetime is independent of the victim so fatal numbers survive immediate enemy pool return. Release active numbers on scene teardown.

## Character visuals

- A shared renderer-feedback component subscribes to accepted hits and briefly tints the player red and enemies white. Repeated accepted hits restart that flash.
- The player additionally alternates between full and partial opacity rapidly while the invincibility interface reports protection. The red tint and opacity may overlap; neither changes the other effect's duration.
- Reuse the existing gameplay protection window rather than starting a presentation-owned protection timer. Rejected hits neither extend protection nor restart feedback.
- Support the project's actual URP materials with isolated runtime material instances and an appropriate transparent rendering configuration for player opacity. Do not edit shared material assets or replace genuine alpha changes with toggling Renderer.enabled.
- Preserve original colors, alpha, render settings, and renderer visibility. Restore on completion, death, disable, reset, and pool reuse; dispose runtime materials when destroyed. Never accidentally reveal a dead sandbox target.

## Configuration and integration

- Keep tunable visual values in a feedback ScriptableObject: bar colors/sizing/offsets, number lifetime/rise/size mapping, red-flash duration, blink frequency, and minimum opacity. Combat invincibility duration stays in PlayerConfig.
- Proposed initial visual values: 0.12-second red flash, 10 opacity cycles per second with a minimum alpha of 0.25, and 0.8-second floating numbers. Exact layout and size values are left to the builder to tune in Unity.
- Wire the real player and enemy prefabs and IntegrationSandbox, and support the PlayerSandbox dummy targets. Update sandbox generation code so rebuilding a scene preserves feedback integration.
- Inspect the production Game scene integration before changing it. Place new game-owned HUD content in its existing UI hierarchy without altering foundation controllers or foundation prefabs. If the production scene does not yet contain combat, document that condition and provide reusable assets ready for its later combat integration.
- Use scaled gameplay time so pause freezes floating numbers, flashes, and protection together. Static subscriptions must be cleaned up because domain reload is disabled.

## Constraints and non-goals

Preserve existing HP/death ownership, enemy pooling, player/enemy code separation, and foundation behavior. Do not introduce XP UI, audio, camera shake, damage-type colors, health regeneration, new combat rules, or animation redesign in this change. Existing project ownership guidance is respected by preferring adapters and prefab components to enemy or shared-contract edits.

## Verification

- Verify fill/text after damage, healing, maximum changes, death, and respawn.
- Verify enemy bars stay hidden until an accepted hit and reset for pooled enemies and sandbox targets.
- Verify accepted-hit feedback and fatal floating numbers; rejected hits generate nothing.
- Verify player protection prevents repeated damage for the configured duration, zero disables it, and transparency follows the same state.
- Verify flash composition, material/visibility restoration, pool reuse, and pause behavior.
- Run relevant existing combat tests plus focused tests for new behavior; check Unity compilation and Console errors. Inspect the integrated scene through MCP and capture the visible HUD/feedback where practical.

## Decisions left to the builder

Exact component/file names, interface notification shapes, safe adapter refresh strategy, pool capacity, font choice consistent with installed UI, and visual tuning within this design. No new dependency or generated raster artwork is required.

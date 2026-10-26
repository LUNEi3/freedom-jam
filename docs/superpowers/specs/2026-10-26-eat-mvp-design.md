# Eat MVP — Design & Requirements

**Game:** Freedom Jam — *Eat Everything, Become Apex Predator*
**Jam theme:** Space Exploration
**Status:** Draft for Phase 1 (MVP). Creature stats/multipliers TBD.
**Source diagram:** `docs/superpowers/specs/claude/eat-mvp-diagram.png`

> **For Claude Code / teammates:** This is the source of truth for the gameplay MVP. Build on top of the existing foundation in `Assets/GameJamFoundation/` (see its `README.md` and `docs/superpowers/specs/2026-10-25-game-jam-foundation-design.md`) — do not modify foundation code unless a requirement below says so. Put all gameplay code under `Assets/Game/`. Anything marked **TBD** must stay data-driven / Inspector-tunable rather than hard-coded.

---

## 1. Concept

The player is a small creature that has landed on a mysterious planet. They grow by eating other creatures, absorbing their traits, until they become the Apex Predator and conquer the planet.

- **Genre / feel:** Survivor-like (Vampire Survivors, Megabonk) — many enemies, rising pressure, run-based progression.
- **View:** 3D, fixed camera angle following the player.
- **Platform:** PC (Keyboard & Mouse, Gamepad) — inherited from foundation.

## 2. Goal, Obstacles, Rules

| | |
|---|---|
| **Goal** | Eat everything and become Apex Predator |
| **Obstacles** | Enemies; scaling difficulty over time |
| **Rules** | Stats are capped by level · Power up by eating · Start with nothing; abilities unlock by leveling up · Player always moves forward and only steers with A / D |

## 3. MVP definition

> **MVP = the player can eat enemies, and enemies can damage the player.**

Phase 1 delivers the full eat/kill → XP → level / multiplier loop with placeholder art. Phase 2 adds the level-up ability selection and the first ability (Dash).

---

## 4. Core mechanics

### 4.1 Player movement
- **REQ-MOVE-1** The player always moves forward at `moveSpeed`. There is no stop or reverse.
- **REQ-MOVE-2** A / D (gamepad left stick X) rotates the player left/right at `steerSpeed` (degrees/sec). Use the X axis of the existing `Player/Move` action in `Assets/InputSystem_Actions.inputactions`.
- **REQ-MOVE-3** `moveSpeed` and `steerSpeed` are player stats and can be affected by multipliers (§5.1).
- **REQ-MOVE-4** Movement stops when the game is paused (foundation already sets `Time.timeScale = 0`).

### 4.2 The Mouth (eat cone)
The player's only attack at the start of a run.

- **REQ-MOUTH-1** A cone-shaped zone in front of the player's body, defined by `mouthRange` (distance) and `mouthAngle` (full cone angle in degrees).
- **REQ-MOUTH-2** An enemy is inside the cone when it is within `mouthRange` **and** `Vector3.Dot(playerForward, dirToEnemy) >= cos(mouthAngle / 2)` (direction flattened on the XZ plane).
- **REQ-MOUTH-3** Every `mouthTickInterval` seconds, every enemy inside the cone takes `baseDamage` (after multipliers). Automatic — no button.
- **REQ-MOUTH-4** `mouthRange` / `mouthAngle` scale with the player's size stat ("Hitbox Size" in the diagram).
- **REQ-MOUTH-5** Debug gizmo drawing the cone in the Scene view.

### 4.3 Eat vs. Kill
Resolved **at the moment an enemy's HP reaches 0**, regardless of what dealt the final hit.

| Enemy dies… | Result |
|---|---|
| **Inside the mouth cone** | **Eaten:** grant the creature's status multiplier(s) (§5.1) **and** absorb its XP immediately. No orb dropped. |
| **Outside the cone** | **Killed:** drop an XP orb at its position. No multiplier. |

- **REQ-EAT-1** For now the player can eat **any** enemy regardless of size or HP (no size gate).
- **REQ-EAT-2** Eaten and killed enemies are returned to the pool, never `Destroy`ed.
- **REQ-EAT-3** Raise events `Eaten(CreatureData)` and `Killed(CreatureData, position)` so VFX/SFX/score can hook in.
- **REQ-EAT-4** Eating or killing adds score through `GameFoundationBootstrap.Instance.Flow.AddScore(creature.scoreValue)`.

> Note: Early in a run the mouth is the only damage source, so outside-cone kills mostly happen once damaging abilities exist (Phase 2+). The rule must still be implemented in Phase 1.

### 4.4 Enemies
- **REQ-ENEMY-1** Enemies chase the player at their `moveSpeed`.
- **REQ-ENEMY-2** Each enemy has HP and a hurtbox; it takes damage through a shared `IDamageable`-style API.
- **REQ-ENEMY-3** Each enemy has a damage hitbox sized to its body ("Hitbox (Enemy size)"). On contact with the player's hurtbox it deals `contactDamage`, at most once per `contactDamageInterval` per enemy.
- **REQ-ENEMY-4** All enemy stats come from a `CreatureData` asset (§6).

### 4.5 Player health
- **REQ-HP-1** Player has `maxHP` and a hurtbox. Enemy hits reduce HP.
- **REQ-HP-2** Short invulnerability window after being hit (`hitInvulnerability` seconds, tunable; may be 0).
- **REQ-HP-3** At 0 HP the run ends: call `GameFoundationBootstrap.Instance.Flow.FinishRun()` (loads Result scene, saves best score).

### 4.6 Spawning & scaling difficulty
- **REQ-SPAWN-1** Enemies spawn off-screen in a ring around the player, from an object pool.
- **REQ-SPAWN-2** Spawn rate, max alive count, and creature mix scale with run time via `AnimationCurve`s on a `SpawnConfig` asset.
- **REQ-SPAWN-3** Enemies too far behind the player are recycled to keep density near the player (player can't turn around quickly).

### 4.7 XP orbs
- **REQ-XP-1** Killed (not eaten) enemies drop an XP orb carrying the creature's `xpValue`.
- **REQ-XP-2** Orbs are collected on contact with the player, optionally pulled in within `pickupRadius` (player stat).
- **REQ-XP-3** Orbs are pooled.

---

## 5. Progression — two pillars

### 5.1 Pillar 1: Status multipliers (from eating only)
- **REQ-MULT-1** Each creature type grants one or more stat multipliers when **eaten**. Examples: Fire creature → `fireDamage` ×; Speedy creature → `moveSpeed` or `steerSpeed` ×.
- **REQ-MULT-2** Multipliers stack per stat (stacking rule — additive vs. multiplicative — **TBD**; default: additive bonuses, e.g. `1 + Σ bonus`).
- **REQ-MULT-3** **Stats are capped by level:** each stat's effective multiplier is clamped to a per-level cap from a `LevelCaps` table/curve. Bonuses above the cap are kept (stored) and become effective once the level rises. *(Confirm with design — alternative is to discard overflow.)*
- **REQ-MULT-4** Final stat = `baseValue × clampedMultiplier`. All stat reads go through `PlayerStats`.

**Stat list (initial, extendable):** `maxHP`, `moveSpeed`, `steerSpeed`, `baseDamage`, `fireDamage`, `size` (mouth/hitbox scale), `pickupRadius`, `mouthTickInterval`. Exact list per creature **TBD**.

### 5.2 Pillar 2: Abilities (from XP → level-up)
- **REQ-LVL-1** XP comes from eating (absorbed instantly) and from collected orbs.
- **REQ-LVL-2** Level thresholds come from an XP curve asset (values **TBD**).
- **REQ-LVL-3** Player starts at level 1 with **no abilities**.
- **REQ-LVL-4 (Phase 2)** On level-up, pause the game and show a roguelike choice (default: pick 1 of 3) of new abilities or upgrades to owned abilities.
- **REQ-LVL-5 (Phase 2)** First ability: **Dash**. Ability list beyond that **TBD**.
- **REQ-LVL-6** Phase 1 only needs: XP bar on HUD, level number, and a `LeveledUp(int level)` event.

---

## 6. Data (ScriptableObjects)

All tuning lives in assets under `Assets/Game/Data/`. Values below are placeholders.

**`CreatureData`** (one per creature type)
| Field | Type | Notes |
|---|---|---|
| `displayName` | string | |
| `prefab` | GameObject | pooled |
| `maxHP` | float | |
| `moveSpeed` | float | chase speed |
| `contactDamage` | float | |
| `contactDamageInterval` | float | |
| `bodySize` | float | scales hitbox/hurtbox |
| `xpValue` | int | |
| `scoreValue` | int | |
| `eatMultipliers` | list of `{ stat, bonus }` | granted only when eaten |

**`PlayerConfig`**: base value for every stat in §5.1, `mouthRange`, `mouthAngle`, `hitInvulnerability`.
**`LevelConfig`**: XP-per-level curve, per-stat multiplier caps per level.
**`SpawnConfig`**: spawn ring radius, despawn distance, spawn-rate / max-alive curves over time, weighted creature list over time.

**Phase 1 placeholder creatures:**
- **Ember** (fire creature) → `fireDamage` bonus
- **Zipper** (speedy creature) → `moveSpeed` or `steerSpeed` bonus

---

## 7. Architecture

```
Assets/Game/
  Data/            CreatureData, PlayerConfig, LevelConfig, SpawnConfig (+ assets)
  Runtime/
    Common/        IDamageable, ObjectPool<T>, StatType enum
    Player/        PlayerMovement, PlayerMouth, PlayerHealth, PlayerStats, PlayerExperience
    Enemy/         EnemyController, EnemyHealth, EnemyContactDamage, EnemySpawner
    Pickups/       XpOrb, XpOrbSpawner
    Camera/        FixedAngleFollowCamera
    UI/            XP bar / level / HP additions to HUD
  Prefabs/         Player, Enemy_Ember, Enemy_Zipper, XpOrb
  Tests/EditMode/  PlayerStats (caps/stacking), PlayerExperience (thresholds), cone check
```

- Own assembly definition (e.g. `FreedomJam.Game`) referencing `FreedomJam.Foundation`.
- Keep pure logic (stats math, XP thresholds, cone test) in plain C# classes so it is EditMode-testable, matching the foundation's style.
- Communicate via C# events, not direct cross-folder references, so Player and Enemy work can proceed independently.
- Gameplay lives in the foundation's `Game` scene. **Always press Play from `MainMenu`** — the bootstrap only exists there.

## 8. Work split (2 devs, not yet assigned)

Designed so each track touches separate folders/prefabs to avoid merge conflicts. Agree on `Common/` (IDamageable, pool, StatType) first.

| Track A — Player | Track B — Enemy & World |
|---|---|
| PlayerMovement, camera | CreatureData, EnemyController (chase) |
| PlayerMouth (cone tick damage) | EnemyHealth (eat/kill resolution, events) |
| PlayerHealth (+ FinishRun) | EnemyContactDamage |
| PlayerStats + multipliers + caps | Pooling, EnemySpawner + SpawnConfig |
| PlayerExperience + HUD XP/level | XpOrb drop & pickup |

**Scene ownership:** only one person edits `Game.unity` at a time — Unity scene merges conflict badly. Prefer working in prefabs.

## 9. Acceptance criteria (Phase 1 done when…)

1. From MainMenu → Start, the player moves forward constantly and steers with A/D.
2. Enemies spawn around the player, chase it, and spawn faster over time.
3. Enemies in the mouth cone lose HP every tick; the cone is visible as a gizmo.
4. An enemy dying in the cone is eaten: multiplier applied (visible in a debug readout) and XP added instantly, no orb.
5. An enemy dying outside the cone drops an XP orb that can be collected.
6. XP fills a bar; reaching the threshold raises the level (multiplier caps rise accordingly).
7. Enemy contact damages the player; at 0 HP the Result screen shows the score.
8. Pause/Resume from the foundation still works; no errors in the Console.
9. EditMode tests pass for stats/caps, XP thresholds, and the cone check.

## 10. Open questions

- Exact stats and multipliers per creature (designer will provide).
- Multiplier stacking: additive or multiplicative? Overflow above level cap: stored or discarded?
- XP curve and level caps values.
- Ability list beyond Dash; choice count on level-up (default 3).
- Should eating ever be gated by size later (eat smaller, avoid bigger)?
- Win condition for "Apex Predator" (time survived, level reached, boss?).
- Fixed camera angle/height values.

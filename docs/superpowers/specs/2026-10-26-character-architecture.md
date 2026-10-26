# Character Architecture (OOP) — Design & Requirements

**Game:** Freedom Jam — *Eat Everything, Become Apex Predator*
**Status:** Phase 1 contract. Read together with `2026-10-26-eat-mvp-design.md` (the gameplay rules); this file defines **how the code is structured** and **who owns what**.

> **For Claude Code (both devs):**
> 1. Find your track in §2. Only create or edit files your track owns. If a task needs a change to a file owned by the other track or to the **shared contract** (§3), stop and tell your human — do not edit it yourself.
> 2. Every player and enemy is a `Character`. Do not write HP, damage or death logic anywhere except `Character.cs`. Subclasses customise behaviour by **overriding the hooks** in §3.3 — never by copying code or adding their own `Update()`.
> 3. The two tracks talk only through the contract types in `Assets/Game/Runtime/Common/` (`IPlayer`, `IDamageable`, `GameplayContext`, `GameplayEvents`). Player code must not reference `Enemy/` classes and enemy code must not reference `Player/` classes.
> 4. All numbers live in ScriptableObjects (§3.5). A new creature is a new **asset**, not a new class, unless its behaviour is different (§4.2).
> 5. Foundation rules from the MVP doc still apply: gameplay code goes under `Assets/Game/`, don't modify `Assets/GameJamFoundation/`, press Play from `MainMenu`.

---

## 1. Class hierarchy

```
IDamageable (interface)
└── Character  (abstract MonoBehaviour)                      SHARED CONTRACT
    ├── PlayerCharacter : Character, IPlayer                 Track A — Player
    └── EnemyCharacter  (abstract) : Character               Track B — Enemy & World
        └── BasicEnemy : EnemyCharacter                      Track B  (Ember + Zipper in Phase 1)
            └── (future) e.g. DashingEnemy, RangedEnemy      Track B  only when behaviour differs

CharacterData (abstract ScriptableObject)                    SHARED CONTRACT
├── PlayerConfig  : CharacterData                            Track A
└── CreatureData  : CharacterData                            SHARED CONTRACT (both tracks read it)
```

- **REQ-OOP-1** `Character` owns everything every creature has: HP, taking damage, dying, base stats (`MaxHP`, `MoveSpeed`, `BodySize`), the per-frame tick, and pool reset.
- **REQ-OOP-2** Subclasses only add what is unique to them: player = steering, mouth, stats with multipliers, XP; enemy = chase, contact damage, eat-vs-kill, pooling.
- **REQ-OOP-3** Stat properties on `Character` are `virtual`. `PlayerCharacter` overrides them to apply multipliers (MVP §5.1); enemies use the raw `CreatureData` values. Any code that reads a stat reads the **property**, never the data asset directly.
- **REQ-OOP-4** Composition is fine for focused parts: `PlayerMouth` and `EnemyContactDamage` are separate components on the same prefab, and pure math lives in plain C# classes (`PlayerStats`, `PlayerExperience`, `ConeMath`) so it can be EditMode-tested. Inheritance is for "is a Character"; composition is for "has a mouth".

## 2. Ownership

| Area | Owner | Files |
|---|---|---|
| **Shared contract** | Created once in Step 0 (§5), then changed only when **both** devs agree | `Runtime/Common/*`, `Runtime/Data/CharacterData.cs`, `Runtime/Data/CreatureData.cs`, `Runtime/Data/StatType.cs`, `Runtime/FreedomJam.Game.asmdef`, layers in `ProjectSettings/TagManager.asset` |
| **Track A — Player** (teammate) | Player dev | `Runtime/Player/*`, `Runtime/Camera/*`, `Runtime/UI/*`, `Runtime/Data/PlayerConfig.cs`, `Runtime/Data/LevelConfig.cs`, `Prefabs/Player.prefab`, `Data/Player/*` assets, `Tests/EditMode/Player*` |
| **Track B — Enemy & World** (LUNEi3) | Enemy dev | `Runtime/Enemy/*`, `Runtime/Pickups/*`, `Runtime/Data/SpawnConfig.cs`, `Prefabs/Enemy_*.prefab`, `Prefabs/XpOrb.prefab`, `Data/Creatures/*` and `Data/Spawn/*` assets, `Art/Materials/*` (placeholder), `Sandbox/*` (test-only scene and stand-in player, not used by the game), `Tests/EditMode/Enemy*` |
| **`Game.unity` scene** | One person at a time — say so in chat before opening it | `Assets/GameJamFoundation/Scenes/Game.unity` |

All paths above are under `Assets/Game/` unless written in full.

**Git:** each track works on its own branch (`feature/player`, `feature/enemy`), merges to `main` in small PRs, and pulls `main` often. A contract change is its own small PR that both devs review.

---

## 3. Shared contract (Step 0)

Namespace for everything in `Assets/Game/`: `FreedomJam.Game` (sub-namespaces `FreedomJam.Game.Player`, `FreedomJam.Game.Enemy`, `FreedomJam.Game.Pickups` are fine). Assembly: `FreedomJam.Game`, referencing `FreedomJam.Foundation` and `Unity.InputSystem`.

The signatures below **are the contract** — implement them exactly. Bodies are guidance.

### 3.1 Damage

```csharp
// Runtime/Common/DamageInfo.cs
public enum DamageType { Normal, Fire }

public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly Character Source;   // who dealt it; may be null (e.g. hazards)

    public DamageInfo(float amount, DamageType type, Character source)
    {
        Amount = amount; Type = type; Source = source;
    }
}

// Runtime/Common/IDamageable.cs
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(DamageInfo info);
}
```

- **REQ-CHAR-1** All damage in the game goes through `IDamageable.TakeDamage`. Find a target from a collider with `collider.GetComponentInParent<IDamageable>()`.

### 3.2 Character data

```csharp
// Runtime/Data/CharacterData.cs
public abstract class CharacterData : ScriptableObject
{
    public float maxHP = 10f;
    public float moveSpeed = 3f;
    public float bodySize = 1f;     // scales hurtbox (and mouth for the player)
}
```

### 3.3 Character base class

```csharp
// Runtime/Common/Character.cs
public abstract class Character : MonoBehaviour, IDamageable
{
    public float CurrentHP { get; private set; }
    public bool IsAlive { get; private set; }

    public virtual float MaxHP => Data.maxHP;
    public virtual float MoveSpeed => Data.moveSpeed;
    public virtual float BodySize => Data.bodySize;

    public event Action<Character, DamageInfo> Damaged;
    public event Action<Character, DamageInfo> Died;     // DamageInfo = the killing blow

    /// <summary>Subclass returns its own typed data asset (PlayerConfig / CreatureData).</summary>
    protected abstract CharacterData Data { get; }

    protected virtual void OnEnable() => ResetCharacter();

    /// <summary>Full HP, alive again. Called on enable, so pooled enemies reset on reuse.
    /// Does nothing while Data is null (pooled enemy enabled before Initialize).</summary>
    public void ResetCharacter()
    {
        if (Data == null) return;
        CurrentHP = MaxHP;
        IsAlive = true;
        OnReset();
    }

    void Update()
    {
        if (IsAlive) Tick(Time.deltaTime);   // pause = timeScale 0, so Tick gets 0
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive || info.Amount <= 0f || !CanReceiveDamage(info)) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - info.Amount);
        OnDamaged(info);
        Damaged?.Invoke(this, info);
        if (CurrentHP <= 0f) Die(info);
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    void Die(DamageInfo killingBlow)
    {
        IsAlive = false;
        OnDied(killingBlow);
        Died?.Invoke(this, killingBlow);
    }

    // ---- Hooks for subclasses ----
    protected abstract void Tick(float deltaTime);                       // movement / AI
    protected virtual bool CanReceiveDamage(DamageInfo info) => true;    // player: invulnerability window
    protected virtual void OnReset() { }
    protected virtual void OnDamaged(DamageInfo info) { }                // flash, SFX
    protected abstract void OnDied(DamageInfo killingBlow);              // player: FinishRun; enemy: eat/kill + pool
}
```

- **REQ-CHAR-2** Subclasses must not declare `Update`, `OnEnable` without calling `base.OnEnable()`, or their own HP fields.
- **REQ-CHAR-3** `TakeDamage` after death is ignored, so a creature can only die once per life.
- **REQ-CHAR-4** `ResetCharacter()` makes a pooled object reusable: whatever a subclass sets during a life must be cleared in `OnReset()`.
- **REQ-CHAR-4b** While `Data` is null, `ResetCharacter()` does nothing and the character stays not alive (no `Tick`). Whoever sets the data afterwards must call `ResetCharacter()` again (see REQ-ECHAR-6).

### 3.4 Player access and gameplay events

The enemy track needs a few things from the player (where it is, whether a point is inside the mouth, the pickup radius). It gets them through an interface, never through `PlayerCharacter` directly.

```csharp
// Runtime/Common/IPlayer.cs
public interface IPlayer
{
    Character Character { get; }               // position, IDamageable, IsAlive
    bool IsInMouth(Vector3 worldPosition);     // MVP REQ-MOUTH-2 cone test
    float PickupRadius { get; }                // MVP REQ-XP-2
}

// Runtime/Common/GameplayContext.cs
public static class GameplayContext
{
    public static IPlayer Player { get; private set; }   // null when no player is alive in the scene

    public static void RegisterPlayer(IPlayer player) => Player = player;
    public static void UnregisterPlayer(IPlayer player) { if (Player == player) Player = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Player = null; }
}

// Runtime/Common/GameplayEvents.cs
public static class GameplayEvents
{
    public static event Action<CreatureData> CreatureEaten;              // died inside the mouth
    public static event Action<CreatureData, Vector3> CreatureKilled;    // died outside the mouth
    public static event Action<int> XpCollected;                         // orb picked up

    public static void RaiseCreatureEaten(CreatureData creature) => CreatureEaten?.Invoke(creature);
    public static void RaiseCreatureKilled(CreatureData creature, Vector3 position) => CreatureKilled?.Invoke(creature, position);
    public static void RaiseXpCollected(int amount) => XpCollected?.Invoke(amount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { CreatureEaten = null; CreatureKilled = null; XpCollected = null; }
}
```

> **Domain Reload is disabled** in this project (`EditorSettings` → Enter Play Mode Options). Static fields and events are **not** cleared between Play sessions. Every new static in `Assets/Game/` must be reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method like the ones above.

| Event / call | Raised by | Listened to by |
|---|---|---|
| `GameplayContext.RegisterPlayer` / `UnregisterPlayer` | `PlayerCharacter` in `OnEnable` / `OnDisable` (Track A) | Enemies, spawner, orbs (Track B) |
| `CreatureEaten` | `EnemyCharacter.OnDied` (Track B) | Player: apply `eatMultipliers` + add `xpValue` (Track A) |
| `CreatureKilled` | `EnemyCharacter.OnDied` (Track B) | `XpOrbSpawner` drops an orb (Track B); VFX/SFX |
| `XpCollected` | `XpOrb` on pickup (Track B) | Player: add XP (Track A) |

- **REQ-CHAR-5** Subscribe to static events in `OnEnable` and unsubscribe in `OnDisable`, every time — static events outlive scenes.
- **REQ-CHAR-6** Code that uses `GameplayContext.Player` must handle `null` (player dead or not spawned yet) and `!Player.Character.IsAlive`.

### 3.5 Creature data and stats

```csharp
// Runtime/Data/StatType.cs
// Serialized as int: only ADD new values at the end, never reorder or remove.
public enum StatType { MaxHP, MoveSpeed, SteerSpeed, BaseDamage, FireDamage, Size, PickupRadius, MouthTickInterval }

[Serializable]
public struct StatBonus
{
    public StatType stat;
    public float bonus;          // e.g. 0.1 = +10 %
}

// Runtime/Data/CreatureData.cs
[CreateAssetMenu(menuName = "Freedom Jam/Creature Data")]
public sealed class CreatureData : CharacterData
{
    public string displayName;
    public GameObject prefab;               // must have an EnemyCharacter component
    public float contactDamage = 1f;
    public float contactDamageInterval = 1f;
    public int xpValue = 1;
    public int scoreValue = 10;
    public List<StatBonus> eatMultipliers = new();
}
```

`PlayerConfig : CharacterData` (Track A) adds the player-only base values from MVP §6 (`steerSpeed`, `baseDamage`, `fireDamage`, `pickupRadius`, `mouthRange`, `mouthAngle`, `mouthTickInterval`, `hitInvulnerability`).

### 3.6 Layers and physics

| Layer | Used by | Collider |
|---|---|---|
| `Player` | Player hurtbox | trigger, radius × `BodySize` |
| `Enemy` | Enemy hurtbox / contact hitbox | trigger, radius × `BodySize` |
| `Pickup` | XP orbs | trigger |

- Player and enemies each have a **kinematic `Rigidbody`** (needed for trigger events) and move by setting `transform` position/rotation inside `Tick`. `Tick` runs from `Update`, where `Rigidbody.MovePosition` would only apply once per physics step and lose movement at high frame rates.
- In the collision matrix, turn **off** `Enemy`↔`Enemy` and `Pickup`↔`Enemy`. Keep `Player`↔`Enemy` and `Player`↔`Pickup`.
- **Body radius rule:** every Character's body is a circle on the XZ plane with radius `BodySize × 0.5`. Prefabs are built so that a `BodySize` of 1 is a 1-unit-wide body (Unity's default sphere / capsule). Enemies use this to stop at the player's surface, to keep apart from each other and to detect contact (REQ-ECHAR-1/3), so the **player prefab must follow it too**. Enemy↔enemy collision is this same distance rule, not physics (the Enemy↔Enemy layer pair stays off).
- The player's mouth finds enemies with `Physics.OverlapSphere(position, mouthRange, enemyMask)` and then the cone test. It does not need its own collider.

---

## 4. Track requirements

### 4.1 Track A — `PlayerCharacter : Character, IPlayer`

Implements MVP §4.1, §4.2, §4.5, §5 on top of the contract.

- **REQ-PCHAR-1** `Data` returns a serialized `PlayerConfig`. Overrides `MaxHP`, `MoveSpeed`, `BodySize` to return `base value × clamped multiplier` from `PlayerStats` (MVP REQ-MULT-4).
- **REQ-PCHAR-2** `Tick`: always move forward at `MoveSpeed`, rotate with the X axis of `Player/Move` at the steer speed stat (MVP REQ-MOVE-1/2).
- **REQ-PCHAR-3** `OnEnable` calls `base.OnEnable()`, registers with `GameplayContext`, subscribes to `CreatureEaten` and `XpCollected`. `OnDisable` undoes all of it.
- **REQ-PCHAR-4** `IsInMouth` delegates to `PlayerMouth` (cone test from MVP REQ-MOUTH-2, implemented in a pure `ConeMath` helper).
- **REQ-PCHAR-5** `PlayerMouth` damages enemies with `new DamageInfo(damage, DamageType.Normal, player)`. Fire damage, if any, is a separate `DamageInfo` with `DamageType.Fire`.
- **REQ-PCHAR-6** `CanReceiveDamage` returns `false` during the hit invulnerability window (MVP REQ-HP-2).
- **REQ-PCHAR-7** `OnDied` calls `GameFoundationBootstrap.Instance.Flow.FinishRun()` (MVP REQ-HP-3).
- **REQ-PCHAR-8** When the `MaxHP` multiplier increases, heal by the difference so current HP is not suddenly a lower percentage.

### 4.2 Track B — `EnemyCharacter : Character` (abstract)

Implements MVP §4.3, §4.4, §4.6, §4.7 on top of the contract.

```csharp
public abstract class EnemyCharacter : Character
{
    public CreatureData Creature { get; private set; }
    protected override CharacterData Data => Creature;

    /// <summary>Called by the spawner. Sets Creature, then calls ResetCharacter() itself,
    /// because OnEnable may already have run with no data (prefab instantiated while active).</summary>
    public void Initialize(CreatureData creature, Action<EnemyCharacter> releaseToPool) { ... }

    protected override void Tick(float deltaTime) => Move(deltaTime);
    protected virtual void Move(float deltaTime) { /* chase GameplayContext.Player */ }

    protected override void OnDied(DamageInfo killingBlow) { /* eat-vs-kill, score, release */ }
}
```

- **REQ-ECHAR-1** `Move` (virtual) chases `GameplayContext.Player.Character` at a constant `MoveSpeed` on the XZ plane (no acceleration or momentum), facing the player. It stops when the bodies touch (`ContactDistance` = both radii, §3.6) and is never inside the player: if it ends up closer, it moves straight back out to the surface that frame. If there is no living player, it stands still.
  After `Move`, `Tick` separates the enemy from every other active, living enemy it overlaps (each moves half the overlap, so neighbours end up touching), then moves it back out of the player if that push put it inside. Separation runs in `Tick`, not `Move`, so subclasses that override `Move` still collide.
- **REQ-ECHAR-2** `OnDied` resolves eat-vs-kill **at that moment**, whatever dealt the killing blow (MVP §4.3):
  - `GameplayContext.Player?.IsInMouth(transform.position) == true` → `GameplayEvents.RaiseCreatureEaten(Creature)`.
  - otherwise → `GameplayEvents.RaiseCreatureKilled(Creature, transform.position)`.
  - Both cases: `GameFoundationBootstrap.Instance.Flow.AddScore(Creature.scoreValue)` (MVP REQ-EAT-4), then release to the pool. Never `Destroy`.
- **REQ-ECHAR-3** `EnemyContactDamage` (separate component) damages the player with `new DamageInfo(Creature.contactDamage, DamageType.Normal, enemy)` while `EnemyCharacter.IsTouchingPlayer()` is true (body distance, not trigger events — an enemy resting exactly at the surface may not register a physics overlap), at most once per `contactDamageInterval` per enemy. Its timer resets when the enemy is re-enabled from the pool.
- **REQ-ECHAR-4** `BasicEnemy : EnemyCharacter` uses the default chase. **Ember and Zipper are both `BasicEnemy` prefabs with different `CreatureData` assets.**
- **REQ-ECHAR-5** Make a new subclass only for new **behaviour**: override `Move` (e.g. zig-zag, dash), `OnDamaged` (e.g. flee when hurt) or `OnDied` (call `base.OnDied` to keep the eat/kill rule). A different number is a data change, not a subclass.
- **REQ-ECHAR-6** `EnemySpawner` uses `UnityEngine.Pool.ObjectPool<EnemyCharacter>` per `CreatureData` and reads rates from `SpawnConfig` (MVP §4.6). `Initialize` must set `Creature` **and then call `ResetCharacter()`** — never rely on `OnEnable` alone, since it may run before the data is set (REQ-CHAR-4b).
- **REQ-ECHAR-7** `XpOrbSpawner` listens to `CreatureKilled`; `XpOrb` moves toward the player inside `GameplayContext.Player.PickupRadius` and calls `GameplayEvents.RaiseXpCollected(xp)` on touch (MVP §4.7). Orbs are pooled.

---

## 5. Build order

**Step 0 — Contract (Enemy track creates it, first PR to `main`):**
1. `Assets/Game/Runtime/FreedomJam.Game.asmdef`.
2. Everything in §3: `DamageInfo`, `IDamageable`, `Character`, `CharacterData`, `StatType` + `StatBonus`, `CreatureData`, `IPlayer`, `GameplayContext`, `GameplayEvents`.
3. Layers `Player`, `Enemy`, `Pickup` and the collision matrix from §3.6.
4. `Tests/EditMode/FreedomJam.Game.Tests.EditMode.asmdef` with `CharacterTests` (damage, death fires once, heal clamps, reset, reset without data) using a small test subclass of `Character`.

The Player track doesn't wait for Step 0: pure classes (`PlayerStats`, `PlayerExperience`, `ConeMath`) and their tests need nothing from it.

**Step 1 — in parallel, after Step 0 is on `main`:**

| Track A — Player | Track B — Enemy & World |
|---|---|
| `PlayerConfig`, `PlayerStats`, `PlayerExperience`, `ConeMath` + tests | `EnemyCharacter`, `BasicEnemy`, `EnemyContactDamage` |
| `PlayerCharacter` (move, steer, register) + `Player.prefab` | Ember + Zipper `CreatureData` assets and prefabs |
| `PlayerMouth` + gizmo | `SpawnConfig`, `EnemySpawner` with pooling |
| Fixed follow camera | `XpOrb`, `XpOrbSpawner` |
| HUD: HP, XP bar, level | |

**Testing each track alone:** the enemy dev can drop a throwaway `DummyPlayer : Character, IPlayer` into a test scene (never committed into `Runtime/`), and the player dev can test the mouth against a `DummyTarget : Character`. Integration happens in `Game.unity` once both are on `main`.

## 6. Done when

1. Step 0 compiles and `CharacterTests` pass.
2. No class other than `Character` stores HP or decides death.
3. `Player/` has no reference to `Enemy/` and the reverse (search for the namespaces).
4. Adding a third creature takes only a new `CreatureData` asset and prefab — no code.
5. The MVP doc's acceptance criteria (§9) pass.

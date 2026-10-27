using System;
using FreedomJam.Foundation;
using FreedomJam.Game.CameraRig;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// The player (architecture §4.1). Always moves forward, steers with ISteeringInput,
    /// grows from eating through PlayerStats and levels up through PlayerExperience.
    /// HP, damage and death live in Character.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerCharacter : Character, IPlayer, IInvincibilityState
    {
        const float MovementCollisionSkin = 0.04f;
        const int MaxMovementSlideIterations = 3;

        [SerializeField] PlayerConfig config;
        [SerializeField] LevelConfig levelConfig;
        [SerializeField] PlayerMouth mouth;
        [SerializeField] CharacterController controller;
        [Tooltip("Eat N of a creature → a stat steps up. Creatures not listed give XP only. Empty = eating gives XP only.")]
        [SerializeField] EatStackConfig eatStackConfig;
        [Tooltip("Without a foundation bootstrap (test scene), respawn after this many seconds instead of ending the run.")]
        [SerializeField] float sandboxRespawnDelay = 1f;
        [Tooltip("Log hits taken and creatures eaten to the Console.")]
        [SerializeField] bool logEvents = true;

        ISteeringInput steering;
        SphereCollider movementCollider;
        Vector3 prefabScale = Vector3.one;
        float invulnerableUntil;
        float lastMaxHP;
        Vector3 dashDirection;
        float dashDistanceRemaining;
        float dashSpeed;
        float reviveMoveSpeedMultiplier = 1f;
        bool reviveInvulnerability;
        [SerializeField] ReviveAbility reviveAbility;

        public PlayerStats Stats { get; private set; }
        public PlayerExperience Experience { get; private set; }
        /// <summary>Eat counts per creature and their tiers. Null without an EatStackConfig.</summary>
        public EatStack EatStack { get; private set; }
        public event Action UpgradeApplied;

        public void NotifyUpgradeApplied() => UpgradeApplied?.Invoke();

        protected override CharacterData Data => config;

        public override float MaxHP => GetStat(StatType.MaxHP);
        public override float MoveSpeed => GetStat(StatType.MoveSpeed);
        public override float BodySize => GetStat(StatType.Size);
        public float SteerSpeed => GetStat(StatType.SteerSpeed);
        public float BaseDamage => GetStat(StatType.BaseDamage);
        public float FireDamage => GetStat(StatType.FireDamage);
        public float MouthTickInterval => GetStat(StatType.MouthTickInterval);
        public float PickupRadius => GetStat(StatType.PickupRadius);
        /// <summary>Current size ÷ starting size (1 = not grown yet). Used to scale abilities with size.</summary>
        public float SizeRatio => Stats != null ? Stats.SizeRatio : 1f;
        /// <summary>Mouth cone length; scales with body size (MVP REQ-MOUTH-4).</summary>
        public float MouthRange => config != null ? config.MouthRange * BodySize : 0f;
        public float MouthAngle => config != null ? config.MouthAngle : 0f;
        public float MouthTargetRadius => config != null ? Mathf.Max(0f, config.MouthTargetRadius) : 0f;

        public Character Character => this;
        public bool IsInMouth(Vector3 worldPosition) => mouth != null && mouth.Contains(worldPosition);

        void Awake()
        {
            // Keep the kinematic body for trigger events; movement uses a sphere sweep so the trigger
            // collider can still detect gameplay overlaps without passing through solid map geometry.
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            prefabScale = transform.localScale;
            steering = GetComponent<ISteeringInput>();
            movementCollider = GetComponent<SphereCollider>();
            if (reviveAbility == null) reviveAbility = GetComponent<ReviveAbility>();

            if (config == null) Debug.LogError("PlayerCharacter needs a PlayerConfig. The player will not move or take damage.", this);
            if (controller == null) Debug.LogError("PlayerCharacter needs a CharacterController. The player will not move.", this);
            if (levelConfig == null) Debug.LogError("PlayerCharacter needs a LevelConfig. Stats and XP are disabled.", this);
            if (steering == null) Debug.LogWarning("PlayerCharacter has no ISteeringInput component, so it cannot turn.", this);
            if (mouth == null) Debug.LogWarning("PlayerCharacter has no PlayerMouth assigned, so it cannot eat.", this);
            if (eatStackConfig == null) Debug.LogWarning("PlayerCharacter has no EatStackConfig, so eating gives XP only.", this);

            if (eatStackConfig != null)
            {
                EatStack = new EatStack(eatStackConfig);
                EatStack.TierReached += OnEatTierReached;
            }

            if (config != null && levelConfig != null)
            {
                Stats = new PlayerStats(config.GetBaseValue, levelConfig, EatStack, config, levelConfig);
                Experience = new PlayerExperience(levelConfig);
                Experience.LeveledUp += Stats.SetLevel;
                Experience.LeveledUp += OnLevelUp;
                Stats.Changed += OnStatsChanged;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameplayContext.RegisterPlayer(this);
            GameplayEvents.CreatureEaten += OnCreatureEaten;
            GameplayEvents.XpCollected += OnXpCollected;
        }

        void OnDisable()
        {
            GameplayContext.UnregisterPlayer(this);
            GameplayEvents.CreatureEaten -= OnCreatureEaten;
            GameplayEvents.XpCollected -= OnXpCollected;
            CancelInvoke();
        }

        void OnDestroy()
        {
            if (Experience != null)
            {
                if (Stats != null) Experience.LeveledUp -= Stats.SetLevel;
                Experience.LeveledUp -= OnLevelUp;
            }
            if (Stats != null) Stats.Changed -= OnStatsChanged;
            if (EatStack != null) EatStack.TierReached -= OnEatTierReached;
        }

        protected override void OnReset()
        {
            invulnerableUntil = 0f;
            dashDistanceRemaining = 0f;
            dashSpeed = 0f;
            reviveMoveSpeedMultiplier = 1f;
            reviveInvulnerability = false;
            lastMaxHP = MaxHP;
            ApplyBodyScale();
        }

        protected override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            var steer = steering != null ? steering.Steer : 0f;
            var rotation = transform.rotation * Quaternion.Euler(0f, steer * SteerSpeed * deltaTime, 0f);

            var displacement = rotation * Vector3.forward * (MoveSpeed * reviveMoveSpeedMultiplier * deltaTime);
            if (dashDistanceRemaining > 0f && dashSpeed > 0f)
            {
                var dashStep = Mathf.Min(dashDistanceRemaining, dashSpeed * deltaTime);
                displacement += dashDirection * dashStep;
                dashDistanceRemaining -= dashStep;
            }

            transform.rotation = rotation;
            controller.Move(displacement);
        }

        Vector3 ResolveMovement(Vector3 displacement)
        {
            if (movementCollider == null || displacement.sqrMagnitude <= 0.000001f)
                return displacement;

            var scale = transform.lossyScale;
            var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var worldRadius = movementCollider.radius * maxScale;
            var castRadius = Mathf.Max(0.01f, worldRadius - Mathf.Min(MovementCollisionSkin, worldRadius * 0.1f));
            var castCenter = movementCollider.transform.TransformPoint(movementCollider.center);
            var blockingLayers = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            var remaining = displacement;
            var resolved = Vector3.zero;

            for (var i = 0; i < MaxMovementSlideIterations && remaining.sqrMagnitude > 0.000001f; i++)
            {
                var distance = remaining.magnitude;
                var direction = remaining / distance;
                if (!Physics.SphereCast(castCenter + resolved, castRadius, direction, out var hit,
                        distance, blockingLayers, QueryTriggerInteraction.Ignore))
                {
                    resolved += remaining;
                    break;
                }

                var travel = Mathf.Clamp(hit.distance, 0f, distance);
                resolved += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, hit.normal);
            }

            return resolved;
        }

        /// <summary>Starts a forward dash that travels over time at the configured speed.</summary>
        public bool TryDashForward(float distance, float speed)
        {
            if (!IsAlive || dashDistanceRemaining > 0f || !(distance > 0f) || !(speed > 0f)) return false;
            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return false;

            dashDirection = forward.normalized;
            dashDistanceRemaining = distance;
            dashSpeed = speed;
            return true;
        }

        /// <summary>Body radius = BodySize × 0.5 (architecture §3.6): a prefab built 1 unit wide scales with BodySize.</summary>
        void ApplyBodyScale() => transform.localScale = prefabScale * BodySize;

        public bool IsInvincible => IsAlive && (Time.time < invulnerableUntil || reviveInvulnerability ||
            (reviveAbility != null && reviveAbility.BlocksDamage));

        protected override bool CanReceiveDamage(DamageInfo info) => !IsInvincible;

        protected override bool TryPreventDeath(DamageInfo killingBlow) =>
            reviveAbility != null && reviveAbility.TryPreventLethalDamage(killingBlow);

        public void SetReviveProtection(bool active, float speedMultiplier = 1f)
        {
            reviveInvulnerability = active;
            reviveMoveSpeedMultiplier = active ? Mathf.Max(1f, speedMultiplier) : 1f;
        }

        protected override void OnDamaged(DamageInfo info)
        {
            if (config != null) invulnerableUntil = Time.time + config.HitInvulnerability;
            var camera = Camera.main;
            if (camera != null)
            {
                var shake = camera.GetComponent<CameraShake>() ?? camera.gameObject.AddComponent<CameraShake>();
                shake.Shake();
            }
            if (logEvents)
            {
                var source = info.Source != null ? info.Source.name : "unknown";
                Debug.Log($"[Player] hit by {source} -{info.Amount:0.##} ({info.Type}) → HP {CurrentHP:0.##}/{MaxHP:0.##}", this);
            }
        }

        protected override void OnDied(DamageInfo killingBlow)
        {
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Flow != null)
            {
                bootstrap.Flow.FinishRun();
                return;
            }
            Debug.Log($"Player died (no foundation bootstrap). Respawning in {sandboxRespawnDelay}s.", this);
            Invoke(nameof(SandboxRespawn), sandboxRespawnDelay);
        }

        void SandboxRespawn() => ResetCharacter();

        void OnCreatureEaten(CreatureData creature)
        {
            if (!IsAlive || Stats == null || creature == null) return;
            // Stats come only from the eat stack; CreatureData.eatMultipliers is no longer used.
            // XP is not given here: the creature drops an XP orb that the player has to pick up.
            var stacked = EatStack != null && EatStack.Record(creature);

            if (logEvents)
            {
                var stack = stacked ? $"eaten {EatStack.GetCount(creature)}{NextTierText(creature)}" : "not in Eat Stack Config";
                Debug.Log($"[Player] ate {creature.displayName} ({stack}), dropped {creature.xpValue} XP orb", this);
            }
        }

        string NextTierText(CreatureData creature)
        {
            var next = EatStack.GetNextThreshold(creature);
            return next > 0 ? $"/{next} for next tier" : ", max tier";
        }

        void OnEatTierReached(CreatureData creature, EatStackTier tier, int tierNumber)
        {
            if (logEvents)
                Debug.Log($"[Player] {creature.displayName} tier {tierNumber} ({tier.EatCount} eaten): {tier.Describe()}", this);
        }

        void OnXpCollected(int amount)
        {
            if (IsAlive && Experience != null) Experience.AddXp(amount);
        }

        /// <summary>When max HP grows, heal by the difference (architecture REQ-PCHAR-8).</summary>
        void OnStatsChanged()
        {
            var maxHP = MaxHP;
            if (maxHP > lastMaxHP) Heal(maxHP - lastMaxHP);
            lastMaxHP = maxHP;
            ApplyBodyScale();
        }

        void OnLevelUp(int _) => Heal(MaxHP);

        float GetStat(StatType stat)
        {
            if (Stats != null) return Stats.Get(stat);
            return config != null ? config.GetBaseValue(stat) : 0f;
        }
    }
}

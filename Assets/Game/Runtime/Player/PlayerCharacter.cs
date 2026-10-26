using FreedomJam.Foundation;
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
        [SerializeField] PlayerConfig config;
        [SerializeField] LevelConfig levelConfig;
        [SerializeField] PlayerMouth mouth;
        [Tooltip("Eat N of a creature → a stat steps up. Creatures not listed give XP only. Empty = eating gives XP only.")]
        [SerializeField] EatStackConfig eatStackConfig;
        [Tooltip("Without a foundation bootstrap (test scene), respawn after this many seconds instead of ending the run.")]
        [SerializeField] float sandboxRespawnDelay = 1f;
        [Tooltip("Log hits taken and creatures eaten to the Console.")]
        [SerializeField] bool logEvents = true;

        ISteeringInput steering;
        Vector3 prefabScale = Vector3.one;
        float invulnerableUntil;
        float lastMaxHP;
        Vector3 dashDirection;
        float dashDistanceRemaining;
        float dashSpeed;

        public PlayerStats Stats { get; private set; }
        public PlayerExperience Experience { get; private set; }
        /// <summary>Eat counts per creature and their tiers. Null without an EatStackConfig.</summary>
        public EatStack EatStack { get; private set; }

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
            // Kinematic body only for trigger events; movement sets the transform (architecture §3.6).
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            prefabScale = transform.localScale;
            steering = GetComponent<ISteeringInput>();

            if (config == null) Debug.LogError("PlayerCharacter needs a PlayerConfig. The player will not move or take damage.", this);
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
                Stats = new PlayerStats(config.GetBaseValue, levelConfig, EatStack, config);
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
            lastMaxHP = MaxHP;
            ApplyBodyScale();
        }

        protected override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            var steer = steering != null ? steering.Steer : 0f;
            var rotation = transform.rotation * Quaternion.Euler(0f, steer * SteerSpeed * deltaTime, 0f);

            var displacement = rotation * Vector3.forward * (MoveSpeed * deltaTime);
            if (dashDistanceRemaining > 0f && dashSpeed > 0f)
            {
                var dashStep = Mathf.Min(dashDistanceRemaining, dashSpeed * deltaTime);
                displacement += dashDirection * dashStep;
                dashDistanceRemaining -= dashStep;
            }

            transform.SetPositionAndRotation(
                transform.position + displacement,
                rotation);
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

        public bool IsInvincible => IsAlive && Time.time < invulnerableUntil;

        protected override bool CanReceiveDamage(DamageInfo info) => !IsInvincible;

        protected override void OnDamaged(DamageInfo info)
        {
            if (config != null) invulnerableUntil = Time.time + config.HitInvulnerability;
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
            var stacked = EatStack != null && EatStack.Record(creature);
            Experience.AddXp(creature.xpValue);

            if (logEvents)
            {
                var stack = stacked ? $"eaten {EatStack.GetCount(creature)}{NextTierText(creature)}" : "not in Eat Stack Config, XP only";
                Debug.Log($"[Player] ate {creature.displayName} ({stack}), +{creature.xpValue} XP → level {Experience.Level}, XP {Experience.CurrentXp}/{Experience.XpToNext}", this);
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

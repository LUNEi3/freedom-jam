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
    public sealed class PlayerCharacter : Character, IPlayer
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] LevelConfig levelConfig;
        [SerializeField] PlayerMouth mouth;
        [Tooltip("Without a foundation bootstrap (test scene), respawn after this many seconds instead of ending the run.")]
        [SerializeField] float sandboxRespawnDelay = 1f;
        [Tooltip("Log hits taken and creatures eaten to the Console.")]
        [SerializeField] bool logEvents = true;

        ISteeringInput steering;
        Vector3 prefabScale = Vector3.one;
        float invulnerableUntil;
        float lastMaxHP;

        public PlayerStats Stats { get; private set; }
        public PlayerExperience Experience { get; private set; }

        protected override CharacterData Data => config;

        public override float MaxHP => GetStat(StatType.MaxHP);
        public override float MoveSpeed => GetStat(StatType.MoveSpeed);
        public override float BodySize => GetStat(StatType.Size);
        public float SteerSpeed => GetStat(StatType.SteerSpeed);
        public float BaseDamage => GetStat(StatType.BaseDamage);
        public float FireDamage => GetStat(StatType.FireDamage);
        public float MouthTickInterval => GetStat(StatType.MouthTickInterval);
        public float PickupRadius => GetStat(StatType.PickupRadius);
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

            if (config != null && levelConfig != null)
            {
                Stats = new PlayerStats(config.GetBaseValue, levelConfig);
                Experience = new PlayerExperience(levelConfig);
                Experience.LeveledUp += Stats.SetLevel;
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
            if (Experience != null && Stats != null) Experience.LeveledUp -= Stats.SetLevel;
            if (Stats != null) Stats.Changed -= OnStatsChanged;
        }

        protected override void OnReset()
        {
            invulnerableUntil = 0f;
            lastMaxHP = MaxHP;
            ApplyBodyScale();
        }

        protected override void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            var steer = steering != null ? steering.Steer : 0f;
            var rotation = transform.rotation * Quaternion.Euler(0f, steer * SteerSpeed * deltaTime, 0f);
            transform.SetPositionAndRotation(
                transform.position + rotation * Vector3.forward * (MoveSpeed * deltaTime),
                rotation);
        }

        /// <summary>Body radius = BodySize × 0.5 (architecture §3.6): a prefab built 1 unit wide scales with BodySize.</summary>
        void ApplyBodyScale() => transform.localScale = prefabScale * BodySize;

        protected override bool CanReceiveDamage(DamageInfo info) => Time.time >= invulnerableUntil;

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
            foreach (var bonus in creature.eatMultipliers) Stats.AddBonus(bonus.stat, bonus.bonus);
            Experience.AddXp(creature.xpValue);

            if (logEvents)
            {
                var bonuses = string.Join(", ", creature.eatMultipliers.ConvertAll(b => $"{b.bonus:+0.##;-0.##} {b.stat}"));
                Debug.Log($"[Player] ate {creature.displayName}: {bonuses}, +{creature.xpValue} XP → level {Experience.Level}, XP {Experience.CurrentXp}/{Experience.XpToNext}", this);
            }
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

        float GetStat(StatType stat)
        {
            if (Stats != null) return Stats.Get(stat);
            return config != null ? config.GetBaseValue(stat) : 0f;
        }
    }
}

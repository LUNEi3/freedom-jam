using System.Collections.Generic;
using FreedomJam.Game.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Game.Sandbox
{
    /// <summary>
    /// Throwaway stand-in for PlayerCharacter so the Enemy track can be tested alone.
    /// Moves forward, steers with A/D, bites enemies in a cone. The real player lives in Runtime/Player (Track A).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SandboxPlayer : Character, IPlayer
    {
        [SerializeField] SandboxPlayerData data;
        [Tooltip("Off: stand still and turn in place with A/D. On: always move forward like the real player.")]
        [SerializeField] bool moveForward;
        [SerializeField] float steerSpeed = 140f;
        [SerializeField] float mouthRange = 2.5f;
        [SerializeField] float mouthAngle = 90f;
        [SerializeField] float mouthDamage = 4f;
        [SerializeField] float mouthTickInterval = 0.25f;
        [SerializeField] float pickupRadius = 4f;
        [SerializeField] LayerMask enemyMask;

        readonly Collider[] hits = new Collider[64];
        readonly Dictionary<StatType, float> bonuses = new();
        float nextBite;

        public bool MoveForward { get => moveForward; set => moveForward = value; }
        public Character Character => this;
        public float PickupRadius => pickupRadius;
        public int EatenCount { get; private set; }
        public int KilledCount { get; private set; }
        public int TotalXp { get; private set; }
        public IReadOnlyDictionary<StatType, float> Bonuses => bonuses;
        protected override CharacterData Data => data;

        protected override void OnEnable()
        {
            base.OnEnable();
            GameplayContext.RegisterPlayer(this);
            GameplayEvents.CreatureEaten += OnCreatureEaten;
            GameplayEvents.CreatureKilled += OnCreatureKilled;
            GameplayEvents.XpCollected += OnXpCollected;
        }

        void OnDisable()
        {
            GameplayContext.UnregisterPlayer(this);
            GameplayEvents.CreatureEaten -= OnCreatureEaten;
            GameplayEvents.CreatureKilled -= OnCreatureKilled;
            GameplayEvents.XpCollected -= OnXpCollected;
        }

        public bool IsInMouth(Vector3 worldPosition)
        {
            var offset = worldPosition - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance > mouthRange) return false;
            if (distance < 0.001f) return true;
            var forward = transform.forward;
            forward.y = 0f;
            return Vector3.Dot(forward.normalized, offset / distance) >= Mathf.Cos(mouthAngle * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>Test key: kills every enemy outside the mouth, so the killed → orb path can be seen.</summary>
        public void KillEnemiesOutsideMouth()
        {
            foreach (var enemy in FindObjectsByType<EnemyCharacter>())
                if (enemy.IsAlive && !IsInMouth(enemy.transform.position))
                    enemy.TakeDamage(new DamageInfo(enemy.CurrentHP, DamageType.Normal, this));
        }

        protected override void Tick(float deltaTime)
        {
            var keyboard = Keyboard.current;
            var steer = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) steer -= 1f;
                if (keyboard.dKey.isPressed) steer += 1f;
            }
            transform.Rotate(0f, steer * steerSpeed * deltaTime, 0f);
            if (moveForward) transform.position += transform.forward * (MoveSpeed * deltaTime);

            if (Time.time < nextBite) return;
            nextBite = Time.time + mouthTickInterval;
            Bite();
        }

        void Bite()
        {
            var count = Physics.OverlapSphereNonAlloc(transform.position, mouthRange, hits, enemyMask, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var target = hits[i].GetComponentInParent<Character>();
                if (target != null && target != this && target.IsAlive && IsInMouth(target.transform.position))
                    target.TakeDamage(new DamageInfo(mouthDamage, DamageType.Normal, this));
            }
        }

        protected override void OnDied(DamageInfo killingBlow) => Debug.Log("[Sandbox] Player died. Press R to revive.");

        void OnCreatureEaten(CreatureData creature)
        {
            EatenCount++;
            TotalXp += creature.xpValue;
            foreach (var bonus in creature.eatMultipliers)
                bonuses[bonus.stat] = (bonuses.TryGetValue(bonus.stat, out var total) ? total : 0f) + bonus.bonus;
        }

        void OnCreatureKilled(CreatureData creature, Vector3 position) => KilledCount++;
        void OnXpCollected(int xp) => TotalXp += xp;

        void OnDrawGizmos()
        {
            var origin = transform.position;
            var half = mouthAngle * 0.5f;
            var left = Quaternion.Euler(0f, -half, 0f) * transform.forward * mouthRange;
            var right = Quaternion.Euler(0f, half, 0f) * transform.forward * mouthRange;
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
            const int segments = 16;
            var previous = origin + left;
            for (var i = 1; i <= segments; i++)
            {
                var next = origin + Quaternion.Euler(0f, -half + mouthAngle * i / segments, 0f) * transform.forward * mouthRange;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(origin, pickupRadius);
        }
    }
}

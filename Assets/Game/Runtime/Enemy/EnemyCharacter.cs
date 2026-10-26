using System;
using FreedomJam.Foundation;
using UnityEngine;

namespace FreedomJam.Game.Enemy
{
    /// <summary>
    /// Base class for every enemy: chases the player, resolves eat-vs-kill on death and returns to its pool.
    /// Subclass only for new behaviour (override Move / OnDamaged / OnDied); new numbers go in a CreatureData asset.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class EnemyCharacter : Character
    {
        Action<EnemyCharacter> releaseToPool;
        Vector3 prefabScale;
        bool prefabScaleCaptured;
        Character suctionTarget;
        float suctionUntil;
        float suctionSpeed;

        public CreatureData Creature { get; private set; }
        protected override CharacterData Data => Creature;

        /// <summary>
        /// Called by the spawner after taking the enemy from its pool. Sets the data, then resets,
        /// because OnEnable may already have run with no data (REQ-ECHAR-6).
        /// </summary>
        public void Initialize(CreatureData creature, Action<EnemyCharacter> releaseToPool)
        {
            Creature = creature;
            this.releaseToPool = releaseToPool;
            ResetCharacter();
        }

        /// <summary>Return to the pool without eat/kill, e.g. when left too far behind the player.</summary>
        public void Despawn()
        {
            if (releaseToPool != null) releaseToPool(this);
            else gameObject.SetActive(false);
        }

        /// <summary>Center-to-center distance on the XZ plane at which this enemy's body touches the player's (§3.6).</summary>
        public float ContactDistance(Character player) => BodySize * 0.5f + player.BodySize * 0.5f;

        /// <summary>True while this enemy's body touches a living player's body.</summary>
        public bool IsTouchingPlayer(float tolerance = 0.05f)
        {
            var player = GameplayContext.Player;
            if (player == null || player.Character == null || !player.Character.IsAlive) return false;
            var offset = player.Character.transform.position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= ContactDistance(player.Character) + tolerance;
        }

        protected override void Tick(float deltaTime) => Move(deltaTime);

        /// <summary>Temporarily pull this enemy toward a shared Character target, without changing damage state.</summary>
        public void ApplySuction(Character target, float speed, float duration)
        {
            if (!IsAlive || target == null || !target.IsAlive || !(speed > 0f) || !(duration > 0f)) return;
            suctionTarget = target;
            suctionSpeed = speed;
            suctionUntil = Time.time + duration;
        }

        /// <summary>
        /// Default: chase the player on the XZ plane at a constant speed, facing it, and stop at its surface.
        /// An enemy that ends up inside the player's body is moved straight back out to the surface.
        /// </summary>
        protected virtual void Move(float deltaTime)
        {
            var player = GameplayContext.Player;
            if (player == null || player.Character == null || !player.Character.IsAlive || deltaTime <= 0f) return;

            if (suctionTarget != null && suctionTarget.IsAlive && Time.time < suctionUntil)
            {
                MoveToward(suctionTarget, suctionSpeed, deltaTime);
                return;
            }
            suctionTarget = null;

            MoveToward(player.Character, MoveSpeed, deltaTime);
        }

        void MoveToward(Character target, float speed, float deltaTime)
        {
            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            var direction = distance > 0.001f ? toTarget / distance : transform.forward;
            var targetDistance = Mathf.Max(ContactDistance(target), distance - speed * deltaTime);
            transform.SetPositionAndRotation(
                transform.position + direction * (distance - targetDistance),
                Quaternion.LookRotation(direction, Vector3.up));
        }

        protected override void OnReset()
        {
            suctionTarget = null;
            suctionUntil = 0f;
            suctionSpeed = 0f;
            if (!prefabScaleCaptured)
            {
                prefabScale = transform.localScale;
                prefabScaleCaptured = true;
            }
            transform.localScale = prefabScale * BodySize;
        }

        /// <summary>Eat-vs-kill is decided where the enemy dies, whatever dealt the killing blow (MVP §4.3).</summary>
        protected override void OnDied(DamageInfo killingBlow)
        {
            var position = transform.position;
            var player = GameplayContext.Player;
            if (player != null && player.IsInMouth(position)) GameplayEvents.RaiseCreatureEaten(Creature);
            else GameplayEvents.RaiseCreatureKilled(Creature, position);

            AddScore(Creature.scoreValue);
            Despawn();
        }

        static void AddScore(int score)
        {
            // No bootstrap when a scene is played directly (e.g. the sandbox).
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Flow != null) bootstrap.Flow.AddScore(score);
        }
    }
}

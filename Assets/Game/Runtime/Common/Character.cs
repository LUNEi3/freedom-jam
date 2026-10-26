using System;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>
    /// Base class for the player and every enemy. Owns HP, damage and death.
    /// Subclasses customise behaviour through the hooks at the bottom; they must not declare Update or their own HP.
    /// </summary>
    public abstract class Character : MonoBehaviour, IDamageable
    {
        public float CurrentHP { get; private set; }
        public bool IsAlive { get; private set; }

        public virtual float MaxHP => Data.maxHP;
        public virtual float MoveSpeed => Data.moveSpeed;
        public virtual float BodySize => Data.bodySize;

        public event Action<Character, DamageInfo> Damaged;
        /// <summary>DamageInfo is the killing blow.</summary>
        public event Action<Character, DamageInfo> Died;

        /// <summary>Subclass returns its own typed data asset (PlayerConfig / CreatureData).</summary>
        protected abstract CharacterData Data { get; }

        protected virtual void OnEnable() => ResetCharacter();

        /// <summary>Full HP, alive again. Called on enable, so pooled enemies reset on reuse.</summary>
        public void ResetCharacter()
        {
            CurrentHP = MaxHP;
            IsAlive = true;
            OnReset();
        }

        void Update()
        {
            if (IsAlive) Tick(Time.deltaTime);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || !(info.Amount > 0f) || !CanReceiveDamage(info)) return;
            CurrentHP = Mathf.Max(0f, CurrentHP - info.Amount);
            OnDamaged(info);
            Damaged?.Invoke(this, info);
            if (CurrentHP <= 0f) Die(info);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || !(amount > 0f)) return;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }

        void Die(DamageInfo killingBlow)
        {
            IsAlive = false;
            OnDied(killingBlow);
            Died?.Invoke(this, killingBlow);
        }

        // ---- Hooks for subclasses ----
        /// <summary>Movement / AI. Only called while alive; deltaTime is 0 while paused.</summary>
        protected abstract void Tick(float deltaTime);
        /// <summary>Return false to ignore a hit (e.g. player invulnerability window).</summary>
        protected virtual bool CanReceiveDamage(DamageInfo info) => true;
        /// <summary>Clear anything set during a life, so pooled objects are reusable.</summary>
        protected virtual void OnReset() { }
        protected virtual void OnDamaged(DamageInfo info) { }
        /// <summary>Player: FinishRun. Enemy: eat-vs-kill and return to pool.</summary>
        protected abstract void OnDied(DamageInfo killingBlow);
    }
}

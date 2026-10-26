using System;
using FreedomJam.Game;
using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Adapts Character state/events for presentation without owning health.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Character))]
    public sealed class CharacterHealthSource : MonoBehaviour, ICharacterHealthSource
    {
        Character character;
        float currentHP;
        float maxHP;
        bool isAlive;
        bool initialized;

        public float CurrentHP => character != null ? character.CurrentHP : currentHP;
        public float MaxHP => character != null && character.IsAlive ? character.MaxHP : maxHP;
        public bool IsAlive => character != null && character.IsAlive;

        public event Action<ICharacterHealthSource> Changed;
        public event Action<ICharacterHealthSource, DamageInfo> Damaged;
        public event Action<ICharacterHealthSource> LifeStarted;
        public event Action<ICharacterHealthSource> LifeEnded;

        void OnEnable()
        {
            character = GetComponent<Character>();
            if (character == null) return;
            character.Damaged += OnDamaged;
            currentHP = character.CurrentHP;
            isAlive = character.IsAlive;
            if (isAlive) maxHP = character.MaxHP;
            initialized = true;
            Changed?.Invoke(this);
            if (isAlive) LifeStarted?.Invoke(this);
        }

        void OnDisable()
        {
            if (character != null) character.Damaged -= OnDamaged;
            if (initialized && isAlive) LifeEnded?.Invoke(this);
            initialized = false;
        }

        void LateUpdate()
        {
            if (character == null) return;
            var nextHp = character.CurrentHP;
            var nextAlive = character.IsAlive;
            var nextMax = nextAlive ? character.MaxHP : maxHP;
            var changed = !Mathf.Approximately(nextHp, currentHP) || !Mathf.Approximately(nextMax, maxHP) || nextAlive != isAlive;
            var refreshedToFull = nextAlive && initialized && nextHp > currentHP && Mathf.Approximately(nextHp, nextMax);
            var began = nextAlive && initialized && (!isAlive || refreshedToFull);
            var ended = !nextAlive && initialized && isAlive;
            currentHP = nextHp;
            maxHP = nextMax;
            isAlive = nextAlive;
            initialized = true;
            if (changed) Changed?.Invoke(this);
            if (began) LifeStarted?.Invoke(this);
            if (ended) LifeEnded?.Invoke(this);
        }

        void OnDamaged(Character _, DamageInfo info)
        {
            currentHP = character.CurrentHP;
            maxHP = character.MaxHP;
            isAlive = character.IsAlive;
            Changed?.Invoke(this);
            Damaged?.Invoke(this, info);
        }
    }
}

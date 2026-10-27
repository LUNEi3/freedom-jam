using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Optional for an IEatStackTable: lists the creatures it has tiers for, in display order.</summary>
    public interface IEatStackCreatureList
    {
        IReadOnlyList<CreatureData> Creatures { get; }
    }

    /// <summary>What the eat-progress panel shows for one creature type.</summary>
    public readonly struct EatProgress
    {
        public readonly CreatureData Creature;
        public readonly int Count;
        /// <summary>Eat count of the tier already reached (0 before the first tier).</summary>
        public readonly int PreviousThreshold;
        /// <summary>Eat count of the next tier, or -1 when every tier is reached.</summary>
        public readonly int NextThreshold;
        public readonly int TierNumber;
        public readonly string CurrentReward;
        public readonly string NextReward;

        public EatProgress(CreatureData creature, int count, int previousThreshold, int nextThreshold, int tierNumber,
            string currentReward, string nextReward)
        {
            Creature = creature;
            Count = count;
            PreviousThreshold = previousThreshold;
            NextThreshold = nextThreshold;
            TierNumber = tierNumber;
            CurrentReward = currentReward ?? string.Empty;
            NextReward = nextReward ?? string.Empty;
        }

        public bool IsMax => NextThreshold < 0;
        /// <summary>Progress from the reached tier to the next one (1 at max).</summary>
        public float Fraction => IsMax ? 1f
            : NextThreshold > PreviousThreshold ? Mathf.Clamp01((float)(Count - PreviousThreshold) / (NextThreshold - PreviousThreshold)) : 0f;
        public string Name => Creature == null ? string.Empty
            : string.IsNullOrEmpty(Creature.displayName) ? Creature.name : Creature.displayName;
    }
}

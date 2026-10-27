using System;
using System.Collections.Generic;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// Counts eaten creatures per type and turns the count into stepped stat multipliers.
    /// Each type's highest reached tier is active (it replaces lower tiers); different types multiply together.
    /// Pure C#, EditMode-testable. Lives on the player, so a new run (new scene) starts at zero.
    /// </summary>
    public sealed class EatStack : IStatMultiplierSource
    {
        readonly IEatStackTable table;
        readonly Dictionary<CreatureData, int> counts = new();
        readonly StatMultiplierSet multipliers = new();

        /// <summary>Raised when any creature reaches a new tier.</summary>
        public event Action Changed;
        /// <summary>Creature, new tier, tier number (1 = first tier).</summary>
        public event Action<CreatureData, EatStackTier, int> TierReached;

        public EatStack(IEatStackTable table) => this.table = table ?? throw new ArgumentNullException(nameof(table));

        public bool Tracks(CreatureData creature) => GetTiers(creature) != null;

        /// <summary>Counts one eaten creature. Returns false (and does nothing) when the creature has no eat stack.</summary>
        public bool Record(CreatureData creature)
        {
            if (!Tracks(creature)) return false;
            var before = GetCurrentTier(creature);
            counts[creature] = GetCount(creature) + 1;
            var after = GetCurrentTier(creature);
            if (after != before)
            {
                Rebuild();
                TierReached?.Invoke(creature, after, GetTierNumber(creature));
                Changed?.Invoke();
            }
            return true;
        }

        public int GetCount(CreatureData creature) =>
            creature != null && counts.TryGetValue(creature, out var count) ? count : 0;

        /// <summary>The highest tier reached, or null below the first tier.</summary>
        public EatStackTier GetCurrentTier(CreatureData creature)
        {
            var tiers = GetTiers(creature);
            if (tiers == null) return null;
            var count = GetCount(creature);
            EatStackTier best = null;
            foreach (var tier in tiers)
                if (tier != null && tier.EatCount <= count && (best == null || tier.EatCount > best.EatCount))
                    best = tier;
            return best;
        }

        /// <summary>How many tiers have been reached (0 = none yet).</summary>
        public int GetTierNumber(CreatureData creature)
        {
            var tiers = GetTiers(creature);
            if (tiers == null) return 0;
            var count = GetCount(creature);
            var reached = 0;
            foreach (var tier in tiers)
                if (tier != null && tier.EatCount <= count) reached++;
            return reached;
        }

        /// <summary>Eat count needed for the next tier, or -1 when every tier is reached (or none exist).</summary>
        public int GetNextThreshold(CreatureData creature)
        {
            var tiers = GetTiers(creature);
            if (tiers == null) return -1;
            var count = GetCount(creature);
            var next = -1;
            foreach (var tier in tiers)
                if (tier != null && tier.EatCount > count && (next < 0 || tier.EatCount < next))
                    next = tier.EatCount;
            return next;
        }

        public float GetMultiplier(StatType stat) => multipliers.Get(stat);

        /// <summary>Creatures with tiers, in the table's order (empty if the table can't list them). For the eat-progress panel.</summary>
        public IReadOnlyList<CreatureData> TrackedCreatures =>
            table is IEatStackCreatureList list && list.Creatures != null ? list.Creatures : System.Array.Empty<CreatureData>();

        /// <summary>The next tier to reach, or null when every tier is reached.</summary>
        public EatStackTier GetNextTier(CreatureData creature)
        {
            var tiers = GetTiers(creature);
            if (tiers == null) return null;
            var count = GetCount(creature);
            EatStackTier next = null;
            foreach (var tier in tiers)
                if (tier != null && tier.EatCount > count && (next == null || tier.EatCount < next.EatCount))
                    next = tier;
            return next;
        }

        /// <summary>Read-only summary of one creature's progress, for the eat-progress panel.</summary>
        public EatProgress GetProgress(CreatureData creature)
        {
            var current = GetCurrentTier(creature);
            var next = GetNextTier(creature);
            return new EatProgress(creature, GetCount(creature), current != null ? current.EatCount : 0,
                next != null ? next.EatCount : -1, GetTierNumber(creature),
                current != null ? current.Describe() : null, next != null ? next.Describe() : null);
        }

        IReadOnlyList<EatStackTier> GetTiers(CreatureData creature) =>
            creature != null ? table.GetTiers(creature) : null;

        void Rebuild()
        {
            multipliers.Clear();
            foreach (var creature in counts.Keys)
                GetCurrentTier(creature)?.ApplyTo(multipliers);
        }
    }
}

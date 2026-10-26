using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>
    /// Eat stack tiers per creature: eat N of a creature → a stat steps up.
    /// Creatures not listed give XP only. List entries and tiers name themselves in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Eat Stack Config")]
    public sealed class EatStackConfig : ScriptableObject, IEatStackTable
    {
        [Serializable]
        sealed class CreatureStack
        {
            [SerializeField, HideInInspector] string label;
            [Tooltip("The creature to count. Only read, never changed.")]
            [SerializeField] CreatureData creature;
            [Tooltip("Steps in any order. The highest reached tier is active and replaces the ones below it.")]
            [SerializeField] List<EatStackTier> tiers = new();

            public CreatureData Creature => creature;
            public List<EatStackTier> Tiers => tiers;
            public void SetLabel(string value) => label = value;
        }

        [Tooltip("One entry per creature. Two creatures that boost the same stat multiply (×1.5 and ×2 = ×3).")]
        [SerializeField] List<CreatureStack> creatures = new();

        Dictionary<CreatureData, IReadOnlyList<EatStackTier>> lookup;

        public IReadOnlyList<EatStackTier> GetTiers(CreatureData creature)
        {
            if (creature == null) return null;
            lookup ??= BuildLookup();
            return lookup.TryGetValue(creature, out var tiers) ? tiers : null;
        }

        Dictionary<CreatureData, IReadOnlyList<EatStackTier>> BuildLookup()
        {
            var result = new Dictionary<CreatureData, IReadOnlyList<EatStackTier>>();
            foreach (var entry in creatures)
                if (entry != null && entry.Creature != null && !result.ContainsKey(entry.Creature))
                    result.Add(entry.Creature, entry.Tiers);   // first entry wins if listed twice
            return result;
        }

        void OnEnable() => lookup = null;

        /// <summary>Names list entries for the Inspector and flags setup mistakes in the names.</summary>
        void OnValidate()
        {
            lookup = null;
            var seen = new HashSet<CreatureData>();
            foreach (var entry in creatures)
            {
                if (entry == null) continue;
                entry.SetLabel(EntryLabel(entry, seen));
                var counts = new HashSet<int>();
                foreach (var tier in entry.Tiers)
                {
                    if (tier == null) continue;
                    var duplicate = !counts.Add(tier.EatCount) ? "  [WARNING: same count as another tier]" : "";
                    tier.SetLabel($"{tier.EatCount} eaten → {tier.Describe()}{duplicate}");
                }
            }
        }

        static string EntryLabel(CreatureStack entry, HashSet<CreatureData> seen)
        {
            if (entry.Creature == null) return "(pick a creature)";
            var name = string.IsNullOrEmpty(entry.Creature.displayName) ? entry.Creature.name : entry.Creature.displayName;
            if (!seen.Add(entry.Creature)) return $"{name}  [WARNING: listed twice, this entry is ignored]";
            var count = entry.Tiers.Count;
            return $"{name}  ({count} tier{(count == 1 ? "" : "s")})";
        }
    }
}

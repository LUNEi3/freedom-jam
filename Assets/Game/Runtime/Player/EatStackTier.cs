using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>One step of a creature's eat stack: once this many are eaten, these rewards are active (replacing the previous step's).</summary>
    [Serializable]
    public sealed class EatStackTier
    {
        // First string field = the element's name in the Inspector list. Filled in by EatStackConfig.OnValidate.
        [SerializeField, HideInInspector] string label;
        [Tooltip("How many of this creature must be eaten to reach this tier.")]
        [SerializeField, Min(1)] int eatCount = 5;
        [Tooltip("All rewards of this tier. They replace the previous tier's rewards, they don't add to them.")]
        [SerializeField] List<StatMultiplierReward> rewards = new();

        public EatStackTier() { }

        public EatStackTier(int eatCount, params StatMultiplierReward[] rewards)
        {
            this.eatCount = eatCount;
            this.rewards = new List<StatMultiplierReward>(rewards);
        }

        public int EatCount => eatCount;
        public IEnumerable<IEatReward> Rewards => rewards;

        public void ApplyTo(StatMultiplierSet multipliers)
        {
            foreach (var reward in rewards)
                if (reward != null) reward.ApplyTo(multipliers);
        }

        public string Describe()
        {
            var parts = new List<string>();
            foreach (var reward in rewards)
                if (reward != null) parts.Add(reward.Describe());
            return parts.Count > 0 ? string.Join(", ", parts) : "no reward";
        }

        internal void SetLabel(string value) => label = value;
    }
}

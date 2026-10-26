using System;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Eat-stack reward: multiply one player stat, e.g. BaseDamage ×1.5.</summary>
    [Serializable]
    public sealed class StatMultiplierReward : IEatReward
    {
        [SerializeField] StatType stat = StatType.BaseDamage;
        [Tooltip("Total multiplier while this tier is active. 1.5 = +50 %. For MouthTickInterval, 2 = bites twice as often.")]
        [SerializeField] float multiplier = 1.5f;

        public StatMultiplierReward() { }

        public StatMultiplierReward(StatType stat, float multiplier)
        {
            this.stat = stat;
            this.multiplier = multiplier;
        }

        public StatType Stat => stat;
        public float Multiplier => multiplier;

        public bool IsValid => multiplier > 0f && !float.IsInfinity(multiplier);

        public void ApplyTo(StatMultiplierSet multipliers)
        {
            if (IsValid) multipliers.Multiply(stat, multiplier);
        }

        public string Describe() => IsValid ? $"{stat} ×{multiplier:0.##}" : $"{stat} (multiplier must be above 0)";
    }
}

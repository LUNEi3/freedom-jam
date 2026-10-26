using System;
using System.Collections.Generic;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// Player stat multipliers from eating (MVP §5.1). Pure C#, EditMode-testable.
    /// Bonuses add up (1 + sum), are clamped to the level cap, and anything above the cap
    /// is kept so it applies once the level rises.
    /// </summary>
    public sealed class PlayerStats
    {
        /// <summary>Lowest effective multiplier, so negative bonuses can't zero a stat or divide by zero.</summary>
        public const float MinMultiplier = 0.1f;

        readonly Func<StatType, float> baseValue;
        readonly IStatCaps caps;
        readonly Dictionary<StatType, float> bonuses = new();

        public int Level { get; private set; } = 1;

        /// <summary>Raised after a bonus is added or the level changes.</summary>
        public event Action Changed;

        public PlayerStats(Func<StatType, float> baseValue, IStatCaps caps)
        {
            this.baseValue = baseValue ?? throw new ArgumentNullException(nameof(baseValue));
            this.caps = caps ?? throw new ArgumentNullException(nameof(caps));
        }

        public void AddBonus(StatType stat, float bonus)
        {
            if (bonus == 0f || float.IsNaN(bonus) || float.IsInfinity(bonus)) return;
            bonuses[stat] = GetBonus(stat) + bonus;
            Changed?.Invoke();
        }

        public void SetLevel(int level)
        {
            level = Math.Max(1, level);
            if (level == Level) return;
            Level = level;
            Changed?.Invoke();
        }

        /// <summary>Sum of all bonuses for this stat, including any amount above the cap.</summary>
        public float GetBonus(StatType stat) => bonuses.TryGetValue(stat, out var sum) ? sum : 0f;

        /// <summary>1 + bonus, clamped to the level cap.</summary>
        public float GetMultiplier(StatType stat)
        {
            var multiplier = 1f + GetBonus(stat);
            var cap = caps.GetCap(stat, Level);
            if (!float.IsNaN(cap) && !float.IsInfinity(cap)) multiplier = Math.Min(multiplier, cap);
            return Math.Max(MinMultiplier, multiplier);
        }

        /// <summary>Final value: base × multiplier, or base ÷ multiplier for interval stats.</summary>
        public float Get(StatType stat)
        {
            var value = baseValue(stat);
            var multiplier = GetMultiplier(stat);
            return IsInverse(stat) ? value / multiplier : value * multiplier;
        }

        /// <summary>Stats where a bigger bonus should make the number smaller (shorter interval = bites more often).</summary>
        public static bool IsInverse(StatType stat) => stat == StatType.MouthTickInterval;
    }
}

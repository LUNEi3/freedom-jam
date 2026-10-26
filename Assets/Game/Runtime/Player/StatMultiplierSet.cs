using System.Collections.Generic;

namespace FreedomJam.Game.Player
{
    /// <summary>One multiplier per stat. Multiplying the same stat twice multiplies the values (×1.5 then ×2 = ×3).</summary>
    public sealed class StatMultiplierSet
    {
        readonly Dictionary<StatType, float> multipliers = new();

        public void Multiply(StatType stat, float multiplier) => multipliers[stat] = Get(stat) * multiplier;

        /// <summary>1 for any stat that was never multiplied.</summary>
        public float Get(StatType stat) => multipliers.TryGetValue(stat, out var value) ? value : 1f;

        public void Clear() => multipliers.Clear();
    }
}

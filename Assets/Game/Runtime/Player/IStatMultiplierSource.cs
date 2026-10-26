using System;

namespace FreedomJam.Game.Player
{
    /// <summary>Extra stat multipliers PlayerStats applies on top of eat bonuses (e.g. the eat stack).</summary>
    public interface IStatMultiplierSource
    {
        /// <summary>1 = no change.</summary>
        float GetMultiplier(StatType stat);

        event Action Changed;
    }
}

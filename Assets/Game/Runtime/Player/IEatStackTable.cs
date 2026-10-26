using System.Collections.Generic;

namespace FreedomJam.Game.Player
{
    /// <summary>Which creatures have an eat stack and their tiers. Implemented by EatStackConfig.</summary>
    public interface IEatStackTable
    {
        /// <summary>The creature's tiers in any order, or null when the creature has no eat stack.</summary>
        IReadOnlyList<EatStackTier> GetTiers(CreatureData creature);
    }
}

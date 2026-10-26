using System;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// The one size formula: multiplier = 1 + (sizeRatio − 1) × strength.
    /// sizeRatio = current size ÷ starting size. Strength 0 = no scaling, 1 = grows at the same rate as size.
    /// Shared by player stats and abilities (and enemies later), so nobody writes their own version.
    /// </summary>
    public static class SizeScaling
    {
        public static float Multiplier(float sizeRatio, float strength)
        {
            if (float.IsNaN(sizeRatio) || float.IsInfinity(sizeRatio) || float.IsNaN(strength) || float.IsInfinity(strength)) return 1f;
            return Math.Max(PlayerStats.MinMultiplier, 1f + (sizeRatio - 1f) * strength);
        }
    }
}

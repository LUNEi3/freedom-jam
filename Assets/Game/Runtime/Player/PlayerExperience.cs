using System;

namespace FreedomJam.Game.Player
{
    /// <summary>XP and level for one run (MVP §5.2). Pure C#, EditMode-testable.</summary>
    public sealed class PlayerExperience
    {
        /// <summary>Stops a huge XP gain from looping forever.</summary>
        public const int MaxLevel = 999;

        readonly IXpCurve curve;

        public int Level { get; private set; } = 1;
        /// <summary>XP inside the current level (resets on level-up).</summary>
        public int CurrentXp { get; private set; }
        public int XpToNext => Math.Max(1, curve.XpToNextLevel(Level));
        /// <summary>0..1 for the HUD XP bar.</summary>
        public float Progress => Level >= MaxLevel ? 1f : (float)CurrentXp / XpToNext;

        /// <summary>Raised once per level gained, with the new level.</summary>
        public event Action<int> LeveledUp;

        public PlayerExperience(IXpCurve curve)
        {
            this.curve = curve ?? throw new ArgumentNullException(nameof(curve));
        }

        /// <summary>Adds XP and returns how many levels were gained. Zero or negative amounts are ignored.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || Level >= MaxLevel) return 0;

            var xp = (long)CurrentXp + amount;
            var gained = 0;
            while (Level < MaxLevel && xp >= XpToNext)
            {
                xp -= XpToNext;
                Level++;
                gained++;
                CurrentXp = (int)Math.Min(xp, int.MaxValue);   // listeners see up-to-date values
                LeveledUp?.Invoke(Level);
            }
            CurrentXp = Level >= MaxLevel ? 0 : (int)xp;
            return gained;
        }
    }
}

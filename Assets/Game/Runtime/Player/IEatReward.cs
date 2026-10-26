namespace FreedomJam.Game.Player
{
    /// <summary>Something an eat-stack tier gives the player. New reward kinds implement this; no type switches.</summary>
    public interface IEatReward
    {
        /// <summary>False when the reward is set up wrong in the Inspector; it is then skipped.</summary>
        bool IsValid { get; }

        void ApplyTo(StatMultiplierSet multipliers);

        /// <summary>Short text for logs and Inspector labels, e.g. "BaseDamage ×1.5".</summary>
        string Describe();
    }
}

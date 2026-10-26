namespace FreedomJam.Game.Player
{
    /// <summary>Automatic stat growth from player level. Implemented by LevelConfig.</summary>
    public interface IStatGrowth
    {
        /// <summary>1 at level 1, and for any stat that doesn't grow.</summary>
        float GetLevelMultiplier(StatType stat, int level);
    }
}

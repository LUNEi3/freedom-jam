namespace FreedomJam.Game.Player
{
    /// <summary>How much XP each level needs. Implemented by LevelConfig.</summary>
    public interface IXpCurve
    {
        /// <summary>XP needed to go from <paramref name="level"/> to the next level.</summary>
        int XpToNextLevel(int level);
    }
}

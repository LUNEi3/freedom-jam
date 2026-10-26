namespace FreedomJam.Game.Player
{
    /// <summary>How strongly each stat follows the player's size. Implemented by PlayerConfig.</summary>
    public interface ISizeScaling
    {
        /// <summary>0 = this stat ignores size. Size itself must return 0.</summary>
        float GetSizeStrength(StatType stat);
    }
}

namespace FreedomJam.Game.Player
{
    /// <summary>Highest multiplier a stat may reach at a level (MVP REQ-MULT-3). Implemented by LevelConfig.</summary>
    public interface IStatCaps
    {
        float GetCap(StatType stat, int level);
    }
}

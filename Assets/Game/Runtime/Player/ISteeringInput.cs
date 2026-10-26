namespace FreedomJam.Game.Player
{
    /// <summary>Where the player's turn command comes from. -1 = full left, 1 = full right.</summary>
    public interface ISteeringInput
    {
        float Steer { get; }
    }
}

namespace FreedomJam.Game.Player
{
    /// <summary>Read-only presentation view of the player's damage protection window.</summary>
    public interface IInvincibilityState
    {
        bool IsInvincible { get; }
    }
}

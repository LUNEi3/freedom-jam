namespace FreedomJam.Foundation
{
    /// <summary>Optional bridge for game flow pause state to compose with gameplay time-scale effects.</summary>
    public interface IGameplayTimeScaleOverride
    {
        float CurrentScale { get; }
        void SetFlowPaused(bool paused);
    }
}

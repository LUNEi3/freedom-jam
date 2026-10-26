using System;
using FreedomJam.Game;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Read-only character health and accepted-damage notifications for presentation.</summary>
    public interface ICharacterHealthSource
    {
        float CurrentHP { get; }
        float MaxHP { get; }
        bool IsAlive { get; }
        event Action<ICharacterHealthSource> Changed;
        event Action<ICharacterHealthSource, DamageInfo> Damaged;
        event Action<ICharacterHealthSource> LifeStarted;
        event Action<ICharacterHealthSource> LifeEnded;
    }
}

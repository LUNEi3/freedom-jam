using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>What non-player code may know about the player. Implemented by PlayerCharacter.</summary>
    public interface IPlayer
    {
        /// <summary>Position, IDamageable, IsAlive.</summary>
        Character Character { get; }
        /// <summary>Mouth cone test (MVP REQ-MOUTH-2).</summary>
        bool IsInMouth(Vector3 worldPosition);
        /// <summary>XP orb pull radius (MVP REQ-XP-2).</summary>
        float PickupRadius { get; }
    }
}

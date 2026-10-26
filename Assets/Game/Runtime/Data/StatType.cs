using System;

namespace FreedomJam.Game
{
    /// <summary>Serialized as int: only ADD new values at the end, never reorder or remove.</summary>
    public enum StatType { MaxHP, MoveSpeed, SteerSpeed, BaseDamage, FireDamage, Size, PickupRadius, MouthTickInterval }

    [Serializable]
    public struct StatBonus
    {
        public StatType stat;
        /// <summary>e.g. 0.1 = +10 %.</summary>
        public float bonus;
    }
}

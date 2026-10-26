using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>Base stats every Character has. PlayerConfig and CreatureData extend it.</summary>
    public abstract class CharacterData : ScriptableObject
    {
        public float maxHP = 10f;
        public float moveSpeed = 3f;
        [Tooltip("Scales the hurtbox (and the mouth for the player).")]
        public float bodySize = 1f;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>One asset per creature type. A new creature is a new asset, not a new class.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Creature Data")]
    public sealed class CreatureData : CharacterData
    {
        public string displayName;
        [Tooltip("Must have an EnemyCharacter component.")]
        public GameObject prefab;
        public float contactDamage = 1f;
        public float contactDamageInterval = 1f;
        public int xpValue = 1;
        public int scoreValue = 10;
        [Tooltip("Granted only when eaten.")]
        public List<StatBonus> eatMultipliers = new();
    }
}

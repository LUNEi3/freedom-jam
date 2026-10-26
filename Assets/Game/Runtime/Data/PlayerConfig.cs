using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>Player base values before multipliers (MVP §6). maxHP / moveSpeed / bodySize come from CharacterData.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Player Config")]
    public sealed class PlayerConfig : CharacterData
    {
        [Header("Movement")]
        [Tooltip("Turn speed in degrees per second.")]
        [SerializeField] float steerSpeed = 180f;

        [Header("Mouth")]
        [SerializeField] float baseDamage = 1f;
        [SerializeField] float fireDamage = 0f;
        [Tooltip("Cone length at body size 1.")]
        [SerializeField] float mouthRange = 1.5f;
        [Tooltip("Full cone angle in degrees.")]
        [SerializeField] float mouthAngle = 90f;
        [Tooltip("Seconds between bites on the same target.")]
        [SerializeField] float mouthTickInterval = 0.5f;
        [Tooltip("Assumed target radius: a target counts as in the mouth once its edge, not only its centre, touches the cone.")]
        [SerializeField] float mouthTargetRadius = 0.5f;

        [Header("Other")]
        [SerializeField] float pickupRadius = 2f;
        [Tooltip("Seconds of invulnerability after being hit. 0 = none.")]
        [SerializeField] float hitInvulnerability = 0.5f;

        public float MouthRange => mouthRange;
        public float MouthAngle => mouthAngle;
        public float MouthTargetRadius => mouthTargetRadius;
        public float HitInvulnerability => hitInvulnerability;

        /// <summary>Base value of a stat, before multipliers.</summary>
        public float GetBaseValue(StatType stat) => stat switch
        {
            StatType.MaxHP => maxHP,
            StatType.MoveSpeed => moveSpeed,
            StatType.SteerSpeed => steerSpeed,
            StatType.BaseDamage => baseDamage,
            StatType.FireDamage => fireDamage,
            StatType.Size => bodySize,
            StatType.PickupRadius => pickupRadius,
            StatType.MouthTickInterval => mouthTickInterval,
            _ => 0f,
        };
    }
}

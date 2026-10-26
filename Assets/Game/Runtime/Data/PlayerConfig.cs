using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>Player base values before multipliers (MVP §6). maxHP / moveSpeed / bodySize come from CharacterData.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Player Config")]
    public sealed class PlayerConfig : CharacterData, ISizeScaling
    {
        [Serializable]
        sealed class SizeScalingEntry
        {
            // First string field = the element's name in the Inspector list. Filled in by OnValidate.
            [SerializeField, HideInInspector] string label;
            [SerializeField] StatType stat = StatType.MaxHP;
            [Tooltip("0 = ignores size. 1 = same rate as size (size ×1.5 → stat ×1.5). 0.5 = half (size ×1.5 → stat ×1.25).")]
            [SerializeField] float strength = 1f;

            public StatType Stat => stat;
            public float Strength => strength;
            public void SetLabel(string value) => label = value;
        }

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

        [Header("Size scaling")]
        [Tooltip("Stats that grow with the player's size (size from eating). Formula: 1 + (size ÷ starting size − 1) × strength. Mouth reach always follows size.")]
        [SerializeField] List<SizeScalingEntry> sizeScaling = new();

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

        /// <summary>First matching entry wins. Size never scales itself.</summary>
        public float GetSizeStrength(StatType stat)
        {
            if (stat == StatType.Size) return 0f;
            foreach (var entry in sizeScaling)
                if (entry != null && entry.Stat == stat) return entry.Strength;
            return 0f;
        }

        void OnValidate()
        {
            var seen = new HashSet<StatType>();
            foreach (var entry in sizeScaling)
            {
                if (entry == null) continue;
                string label;
                if (entry.Stat == StatType.Size) label = "Size  [WARNING: size can't scale itself, ignored]";
                else if (!seen.Add(entry.Stat)) label = $"{entry.Stat}  [WARNING: listed twice, this entry is ignored]";
                else label = $"{entry.Stat}  ×{entry.Strength:0.##} strength (size ×1.5 → ×{SizeScaling.Multiplier(1.5f, entry.Strength):0.##})";
                entry.SetLabel(label);
            }
        }
    }
}

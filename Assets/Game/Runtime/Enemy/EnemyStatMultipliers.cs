using System;
using UnityEngine;

namespace FreedomJam.Game.Enemy
{
    /// <summary>Per-spawn multipliers on top of a creature's own stats, set per spawn phase on EnemySpawner.</summary>
    [Serializable]
    public struct EnemyStatMultipliers
    {
        [Tooltip("× max HP (enemies spawn at full scaled HP).")]
        [Min(0.01f)] public float hp;
        [Tooltip("× contact damage.")]
        [Min(0.01f)] public float damage;
        [Tooltip("× chase speed.")]
        [Min(0.01f)] public float speed;
        [Tooltip("× body size: visual scale, collision and contact distance.")]
        [Min(0.01f)] public float size;

        public static EnemyStatMultipliers One => new EnemyStatMultipliers { hp = 1f, damage = 1f, speed = 1f, size = 1f };

        /// <summary>A zero or invalid multiplier (e.g. a new, unset entry) counts as ×1.</summary>
        public static float Safe(float multiplier) => multiplier > 0f && !float.IsInfinity(multiplier) ? multiplier : 1f;
    }
}

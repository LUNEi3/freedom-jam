using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>Spawn ring, recycle distance and difficulty curves. Curve X axis is run time in seconds.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Spawn Config")]
    public sealed class SpawnConfig : ScriptableObject
    {
        [Serializable]
        public struct CreatureEntry
        {
            public CreatureData creature;
            [Tooltip("Relative spawn weight over run time (seconds).")]
            public AnimationCurve weight;
        }

        [Tooltip("Enemies appear on this ring around the player; keep it outside the camera view.")]
        public float spawnRingRadius = 18f;
        [Tooltip("Enemies further than this from the player are recycled (MVP REQ-SPAWN-3).")]
        public float despawnDistance = 30f;
        [Tooltip("Spawns per second over run time (seconds).")]
        public AnimationCurve spawnsPerSecond = AnimationCurve.Linear(0f, 1f, 120f, 5f);
        [Tooltip("Max enemies alive at once over run time (seconds).")]
        public AnimationCurve maxAlive = AnimationCurve.Linear(0f, 15f, 120f, 80f);
        public List<CreatureEntry> creatures = new();
    }
}

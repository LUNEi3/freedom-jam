using System;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>
    /// Cross-track gameplay events. Static events outlive scenes:
    /// subscribe in OnEnable and unsubscribe in OnDisable, every time.
    /// </summary>
    public static class GameplayEvents
    {
        /// <summary>A creature died inside the player's mouth.</summary>
        public static event Action<CreatureData> CreatureEaten;
        /// <summary>A creature died outside the mouth, at this position.</summary>
        public static event Action<CreatureData, Vector3> CreatureKilled;
        /// <summary>An XP orb was picked up.</summary>
        public static event Action<int> XpCollected;

        public static void RaiseCreatureEaten(CreatureData creature) => CreatureEaten?.Invoke(creature);
        public static void RaiseCreatureKilled(CreatureData creature, Vector3 position) => CreatureKilled?.Invoke(creature, position);
        public static void RaiseXpCollected(int amount) => XpCollected?.Invoke(amount);

        // Domain Reload is disabled in this project, so subscribers survive between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CreatureEaten = null;
            CreatureKilled = null;
            XpCollected = null;
        }
    }
}

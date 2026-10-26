using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Enemy
{
    /// <summary>Pure spawn helpers, kept out of MonoBehaviours so they are EditMode-testable.</summary>
    public static class SpawnMath
    {
        /// <summary>Point on a flat (XZ) ring around center. angle01 in [0, 1] is one full turn.</summary>
        public static Vector3 RingPoint(Vector3 center, float radius, float angle01)
        {
            var angle = angle01 * Mathf.PI * 2f;
            return new Vector3(center.x + Mathf.Cos(angle) * radius, center.y, center.z + Mathf.Sin(angle) * radius);
        }

        /// <summary>Index chosen by roll01 in [0, 1] among positive weights, or -1 if none is positive.</summary>
        public static int PickWeighted(IReadOnlyList<float> weights, float roll01)
        {
            var total = 0f;
            var last = -1;
            for (var i = 0; i < weights.Count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                total += weights[i];
                last = i;
            }
            if (last < 0) return -1;

            var remaining = Mathf.Clamp01(roll01) * total;
            for (var i = 0; i < weights.Count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                remaining -= weights[i];
                if (remaining < 0f) return i;
            }
            return last;
        }
    }
}

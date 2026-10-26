using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Mouth cone test on the XZ plane (MVP REQ-MOUTH-2). Pure math, no state.</summary>
    public static class ConeMath
    {
        const float Epsilon = 1e-6f;

        /// <param name="fullAngleDeg">Full cone angle; 90 means 45 degrees to each side.</param>
        public static bool IsInCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float fullAngleDeg) =>
            IsInCone(origin, forward, target, range, fullAngleDeg, 0f);

        /// <summary>
        /// Same test for a round target of <paramref name="targetRadius"/>: true when any part of it
        /// touches the cone, not only its centre.
        /// </summary>
        public static bool IsInCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float fullAngleDeg, float targetRadius)
        {
            if (targetRadius > 0f) return IsCircleInCone(origin, forward, target, range, fullAngleDeg, targetRadius);

            if (!(range > 0f)) return false;

            var toTarget = target - origin;
            toTarget.y = 0f;
            var sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > range * range) return false;
            if (sqrDistance < Epsilon) return true;   // target overlaps the mouth

            forward.y = 0f;
            if (forward.sqrMagnitude < Epsilon) return false;

            var halfAngle = Mathf.Clamp(fullAngleDeg, 0f, 360f) * 0.5f;
            if (halfAngle >= 180f) return true;

            var dot = Vector3.Dot(forward.normalized, toTarget / Mathf.Sqrt(sqrDistance));
            return dot >= Mathf.Cos(halfAngle * Mathf.Deg2Rad) - Epsilon;
        }

        static bool IsCircleInCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float fullAngleDeg, float radius)
        {
            if (!(range > 0f)) return false;

            var toTarget = target - origin;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance - radius > range) return false;   // nearest edge is out of reach
            if (distance <= radius) return true;           // target overlaps the mouth

            forward.y = 0f;
            if (forward.sqrMagnitude < Epsilon) return false;

            // Widen the cone by the angle the target's radius covers at this distance.
            var halfAngle = Mathf.Clamp(fullAngleDeg, 0f, 360f) * 0.5f + Mathf.Asin(radius / distance) * Mathf.Rad2Deg;
            if (halfAngle >= 180f) return true;

            var dot = Vector3.Dot(forward.normalized, toTarget / distance);
            return dot >= Mathf.Cos(halfAngle * Mathf.Deg2Rad) - Epsilon;
        }
    }
}

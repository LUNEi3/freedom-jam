using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// The eat cone in front of the player (MVP §4.2). A target is bitten the moment it touches the cone,
    /// then again every MouthTickInterval while it stays inside (cooldown per target).
    /// No collider of its own: OverlapSphere + ConeMath. Bite test and IsInMouth use the same check.
    /// </summary>
    public sealed class PlayerMouth : MonoBehaviour
    {
        const int MaxHits = 64;
        const float BiteFlashSeconds = 0.2f;

        [SerializeField] PlayerCharacter player;
        [Tooltip("Layers the mouth can bite. Normally only Enemy.")]
        [SerializeField] LayerMask targetMask;

        [Header("Debug (Scene view, and Game view with Gizmos on)")]
        [SerializeField] Color gizmoColor = new(1f, 0.5f, 0f, 0.9f);
        [SerializeField] Color biteColor = new(1f, 0.1f, 0.1f, 1f);

        readonly Collider[] hits = new Collider[MaxHits];
        readonly HashSet<IDamageable> seenThisFrame = new();
        readonly Dictionary<IDamageable, float> nextBiteTime = new();
        readonly List<IDamageable> expired = new();
        readonly List<(Vector3 position, float time)> recentBites = new();
        float lastBiteTime = float.NegativeInfinity;

        /// <summary>True when a target of the configured radius at this position touches the cone.</summary>
        public bool Contains(Vector3 worldPosition) =>
            player != null && ConeMath.IsInCone(player.transform.position, player.transform.forward,
                worldPosition, player.MouthRange, player.MouthAngle, player.MouthTargetRadius);

        void OnDisable()
        {
            nextBiteTime.Clear();
            recentBites.Clear();
        }

        void Update()
        {
            if (player == null || !player.IsAlive || Time.deltaTime <= 0f) return;

            var now = Time.time;
            var origin = player.transform.position;
            var searchRadius = player.MouthRange + player.MouthTargetRadius;
            var count = Physics.OverlapSphereNonAlloc(origin, searchRadius, hits, targetMask, QueryTriggerInteraction.Collide);
            seenThisFrame.Clear();

            for (var i = 0; i < count; i++)
            {
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || ReferenceEquals(target, player)) continue;
                if (!seenThisFrame.Add(target)) continue;   // several colliders on one target
                if (target is not Component component) continue;

                var position = component.transform.position;
                if (!Contains(position)) continue;
                if (nextBiteTime.TryGetValue(target, out var next) && now < next) continue;

                nextBiteTime[target] = now + Mathf.Max(0.01f, player.MouthTickInterval);
                Bite(target, position, now);
            }

            ForgetExpiredCooldowns(now);
        }

        void Bite(IDamageable target, Vector3 position, float now)
        {
            lastBiteTime = now;
            recentBites.Add((position, now));

            target.TakeDamage(new DamageInfo(player.BaseDamage, DamageType.Normal, player));
            if (player.FireDamage > 0f && target.IsAlive)
                target.TakeDamage(new DamageInfo(player.FireDamage, DamageType.Fire, player));
        }

        /// <summary>Drop cooldowns that have run out, so the dictionary doesn't grow with pooled enemies.</summary>
        void ForgetExpiredCooldowns(float now)
        {
            expired.Clear();
            foreach (var pair in nextBiteTime)
                if (now >= pair.Value) expired.Add(pair.Key);
            foreach (var target in expired) nextBiteTime.Remove(target);

            recentBites.RemoveAll(bite => now - bite.time > BiteFlashSeconds);
        }

        void OnDrawGizmos()
        {
            if (player == null) return;
            var range = player.MouthRange;
            if (range <= 0f) return;

            var origin = player.transform.position;
            var forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;
            forward.Normalize();

            var biting = Application.isPlaying && Time.time - lastBiteTime < BiteFlashSeconds;
            Gizmos.color = biting ? biteColor : gizmoColor;
            DrawCone(origin, forward, range, Mathf.Clamp(player.MouthAngle, 0f, 360f) * 0.5f);

            if (!Application.isPlaying) return;
            Gizmos.color = biteColor;
            foreach (var bite in recentBites)
            {
                Gizmos.DrawLine(origin, bite.position);
                Gizmos.DrawWireSphere(bite.position, Mathf.Max(0.1f, player.MouthTargetRadius));
            }
        }

        static void DrawCone(Vector3 origin, Vector3 forward, float range, float halfAngle)
        {
            const int segments = 16;
            var previous = origin + Quaternion.Euler(0f, -halfAngle, 0f) * forward * range;
            Gizmos.DrawLine(origin, previous);
            for (var i = 1; i <= segments; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                var point = origin + Quaternion.Euler(0f, angle, 0f) * forward * range;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawLine(origin, previous);
        }
    }
}

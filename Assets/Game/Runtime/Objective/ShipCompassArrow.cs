using UnityEngine;

namespace FreedomJam.Game.Objective
{
    /// <summary>Flat-ground compass maths, kept separate so EditMode tests can check it.</summary>
    public static class CompassMath
    {
        /// <summary>Direction from → to on the XZ plane (unit length) and the flat distance. False when they overlap.</summary>
        public static bool TryGetFlatDirection(Vector3 from, Vector3 to, out Vector3 direction, out float distance)
        {
            var offset = to - from;
            offset.y = 0f;
            distance = offset.magnitude;
            direction = distance > 0.0001f ? offset / distance : Vector3.zero;
            return distance > 0.0001f;
        }

        /// <summary>Point on a flat circle of this radius around center, in this direction, lifted by height.</summary>
        public static Vector3 PointOnCircle(Vector3 center, Vector3 direction, float radius, float height) =>
            center + direction * radius + Vector3.up * height;
    }

    /// <summary>
    /// Yellow arrow lying on the ground that circles the player and points at the Lost Spaceship.
    /// Shown only once the ship is ready (from 1:30) and hidden when the player is close or the run is over.
    /// Put it on a child of the player; the arrow model (a flat quad) is a child of this object, tip along +Z.
    /// </summary>
    public sealed class ShipCompassArrow : MonoBehaviour
    {
        [Tooltip("The player this arrow circles. Defaults to the parent's Character.")]
        [SerializeField] Character player;
        [Tooltip("The renderer of the arrow quad (hidden while the arrow shouldn't show).")]
        [SerializeField] Renderer arrowRenderer;

        [Header("Placement")]
        [Tooltip("Distance from the player's centre, multiplied by the player's body size so it stays outside the body.")]
        [SerializeField, Min(0f)] float radius = 2.2f;
        [Tooltip("Height above the player's feet, so it sits just on the ground.")]
        [SerializeField] float height = 0.05f;
        [Tooltip("Arrow size in world units (not affected by the player's size).")]
        [SerializeField, Min(0.01f)] float size = 1.2f;

        [Header("Show / hide")]
        [Tooltip("Hide the arrow when the ship is closer than this (it's on screen by then).")]
        [SerializeField, Min(0f)] float hideWithin = 4f;

        [Header("Look")]
        [SerializeField] Color color = new Color(1f, 0.9f, 0.2f, 1f);
        [Tooltip("Size and opacity pulse. 0 = steady.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.12f;
        [SerializeField, Min(0f)] float pulseSpeed = 4f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        LostSpaceship ship;
        MaterialPropertyBlock block;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<Character>();
            if (arrowRenderer == null) arrowRenderer = GetComponentInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (ship == null) ship = FindAnyObjectByType<LostSpaceship>();
            if (player == null || ship == null || !player.IsAlive || !ship.IsActive || ship.IsReached ||
                !CompassMath.TryGetFlatDirection(player.transform.position, ship.transform.position, out var direction, out var distance) ||
                distance <= hideWithin)
            {
                SetVisible(false);
                return;
            }

            var bodySize = Mathf.Max(0.01f, player.BodySize);
            transform.SetPositionAndRotation(
                CompassMath.PointOnCircle(player.transform.position, direction, radius * bodySize, height),
                Quaternion.LookRotation(direction, Vector3.up));

            // Keep a fixed world size although the player (our parent) grows when eating.
            var pulse = 1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(SafeDivide(size, parentScale.x), SafeDivide(size, parentScale.y), SafeDivide(size, parentScale.z)) * pulse;

            var tint = color;
            tint.a *= Mathf.Lerp(1f - pulseAmount * 2f, 1f, (pulse - (1f - pulseAmount)) / Mathf.Max(0.0001f, 2f * pulseAmount));
            ApplyColor(tint);
            SetVisible(true);
        }

        static float SafeDivide(float value, float by) => Mathf.Abs(by) > 0.0001f ? value / by : value;

        void ApplyColor(Color tint)
        {
            if (arrowRenderer == null) return;
            arrowRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            block.SetColor(ColorId, tint);
            arrowRenderer.SetPropertyBlock(block);
        }

        void SetVisible(bool visible)
        {
            if (arrowRenderer != null && arrowRenderer.enabled != visible) arrowRenderer.enabled = visible;
        }
    }
}

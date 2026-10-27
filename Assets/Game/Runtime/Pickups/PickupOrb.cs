using UnityEngine;

namespace FreedomJam.Game.Pickups
{
    /// <summary>
    /// Shared pickup behaviour: once inside the player's PickupRadius the orb flies to the player, and it is
    /// collected when it touches the player's body. Distance-based on the XZ plane, so it needs no collider.
    /// </summary>
    public abstract class PickupOrb : MonoBehaviour
    {
        [SerializeField] float pullSpeed = 12f;
        [Tooltip("Collected when the orb is this close to the player's body surface (≈ the orb's own radius).")]
        [SerializeField, Min(0f)] float collectDistance = 0.25f;

        bool pulled;
        float suctionSpeed;

        void Update() => Step(Time.deltaTime);

        /// <summary>One frame of pickup logic. Called by Update; public so EditMode tests can drive it.</summary>
        public void Step(float deltaTime)
        {
            OnStep(deltaTime);

            var player = GameplayContext.Player;
            if (player == null || player.Character == null || !player.Character.IsAlive || deltaTime <= 0f) return;
            if (!CanCollect(player)) return;

            var offset = player.Character.transform.position - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance <= collectDistance + player.Character.BodySize * 0.5f)
            {
                ResetPull();
                Collect(player);
                return;
            }

            if (distance <= player.PickupRadius) pulled = true;
            if (pulled)
            {
                var speed = suctionSpeed > 0f ? suctionSpeed : pullSpeed;
                transform.position += offset / distance * Mathf.Min(distance, speed * deltaTime);
            }
        }

        /// <summary>Start (or speed up) flying to the player regardless of PickupRadius, e.g. the Suck ability.</summary>
        protected void Pull(float speed)
        {
            suctionSpeed = Mathf.Max(suctionSpeed, speed);
            pulled = true;
        }

        protected void ResetPull()
        {
            pulled = false;
            suctionSpeed = 0f;
        }

        /// <summary>Runs every frame before the pickup check (timers etc.).</summary>
        protected virtual void OnStep(float deltaTime) { }
        /// <summary>False to ignore the player for now (e.g. already used up, or nothing to give).</summary>
        protected virtual bool CanCollect(IPlayer player) => true;
        protected abstract void Collect(IPlayer player);
    }
}

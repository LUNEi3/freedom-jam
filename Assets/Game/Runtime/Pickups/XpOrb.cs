using System;
using UnityEngine;

namespace FreedomJam.Game.Pickups
{
    /// <summary>
    /// Dropped by killed (not eaten) enemies. Flies to the player once inside PickupRadius
    /// and is collected on reaching it (MVP §4.7). Distance-based, so it needs no collider.
    /// </summary>
    public sealed class XpOrb : MonoBehaviour
    {
        [SerializeField] float pullSpeed = 12f;
        [SerializeField] float collectDistance = 0.6f;

        Action<XpOrb> releaseToPool;
        bool pulled;

        public int Xp { get; private set; }

        public void Drop(Vector3 position, int xp, Action<XpOrb> releaseToPool)
        {
            transform.position = position;
            Xp = xp;
            this.releaseToPool = releaseToPool;
            pulled = false;
        }

        void Update()
        {
            var player = GameplayContext.Player;
            var deltaTime = Time.deltaTime;
            if (player == null || player.Character == null || !player.Character.IsAlive || deltaTime <= 0f) return;

            var offset = player.Character.transform.position - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance <= collectDistance + player.Character.BodySize * 0.5f)
            {
                Collect();
                return;
            }

            if (distance <= player.PickupRadius) pulled = true;
            if (pulled) transform.position += offset / distance * Mathf.Min(distance, pullSpeed * deltaTime);
        }

        void Collect()
        {
            GameplayEvents.RaiseXpCollected(Xp);
            Xp = 0;
            if (releaseToPool != null) releaseToPool(this);
            else gameObject.SetActive(false);
        }
    }
}

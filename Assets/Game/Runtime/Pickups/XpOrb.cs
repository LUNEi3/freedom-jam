using System;
using UnityEngine;

namespace FreedomJam.Game.Pickups
{
    /// <summary>
    /// Dropped wherever a creature dies, eaten or killed (MVP §4.7). The player must touch it (or pull it in with
    /// PickupRadius / Suck) to get the XP.
    /// </summary>
    public sealed class XpOrb : PickupOrb
    {
        Action<XpOrb> releaseToPool;

        public int Xp { get; private set; }

        public void Drop(Vector3 position, int xp, Action<XpOrb> releaseToPool)
        {
            transform.position = position;
            Xp = xp;
            this.releaseToPool = releaseToPool;
            ResetPull();
        }

        /// <summary>Starts or speeds up this orb's normal homing movement after it is sucked in.</summary>
        public void StartSuction(float speed)
        {
            if (Xp <= 0 || !(speed > 0f)) return;
            Pull(speed);
        }

        protected override bool CanCollect(IPlayer player) => Xp > 0;

        protected override void Collect(IPlayer player)
        {
            GameplayEvents.RaiseXpCollected(Xp);
            Xp = 0;
            if (releaseToPool != null) releaseToPool(this);
            else gameObject.SetActive(false);
        }
    }
}

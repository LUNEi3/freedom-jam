using System;
using UnityEngine;
using UnityEngine.Pool;

namespace FreedomJam.Game.Pickups
{
    /// <summary>Drops a pooled XP orb wherever a creature is killed outside the mouth (MVP REQ-XP-1).</summary>
    public sealed class XpOrbSpawner : MonoBehaviour
    {
        [SerializeField] XpOrb orbPrefab;

        ObjectPool<XpOrb> pool;
        Action<XpOrb> release;

        void Awake()
        {
            pool = new ObjectPool<XpOrb>(
                () => Instantiate(orbPrefab, transform),
                orb => orb.gameObject.SetActive(true),
                orb => orb.gameObject.SetActive(false),
                orb => { if (orb != null) Destroy(orb.gameObject); });
            release = pool.Release;
        }

        void OnEnable() => GameplayEvents.CreatureKilled += OnCreatureKilled;
        void OnDisable() => GameplayEvents.CreatureKilled -= OnCreatureKilled;
        void OnDestroy() => pool?.Dispose();

        void OnCreatureKilled(CreatureData creature, Vector3 position)
        {
            if (orbPrefab == null || creature == null || creature.xpValue <= 0) return;
            pool.Get().Drop(position, creature.xpValue, release);
        }
    }
}

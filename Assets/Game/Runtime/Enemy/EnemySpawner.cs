using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace FreedomJam.Game.Enemy
{
    /// <summary>
    /// Spawns pooled enemies on a ring around the player, ramping up over run time (MVP §4.6),
    /// and recycles enemies left too far behind.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] SpawnConfig config;

        readonly Dictionary<CreatureData, ObjectPool<EnemyCharacter>> pools = new();
        readonly List<EnemyCharacter> alive = new();
        readonly List<float> weights = new();
        Action<EnemyCharacter> release;
        float spawnBudget;

        public int AliveCount => alive.Count;
        public float ElapsedSeconds { get; private set; }

        void Awake() => release = Release;

        void Update()
        {
            var player = GameplayContext.Player;
            var deltaTime = Time.deltaTime;
            if (config == null || player == null || player.Character == null || !player.Character.IsAlive || deltaTime <= 0f) return;

            ElapsedSeconds += deltaTime;
            var center = player.Character.transform.position;
            RecycleFarEnemies(center);

            spawnBudget += Mathf.Max(0f, config.spawnsPerSecond.Evaluate(ElapsedSeconds)) * deltaTime;
            var maxAlive = Mathf.FloorToInt(config.maxAlive.Evaluate(ElapsedSeconds));
            while (spawnBudget >= 1f)
            {
                spawnBudget -= 1f;
                if (alive.Count >= maxAlive)
                {
                    spawnBudget = 0f;
                    break;
                }
                if (!Spawn(center)) break;
            }
        }

        void OnDestroy()
        {
            foreach (var pool in pools.Values) pool.Dispose();
            pools.Clear();
            alive.Clear();
        }

        bool Spawn(Vector3 center)
        {
            weights.Clear();
            foreach (var entry in config.creatures)
                weights.Add(entry.creature != null && entry.creature.prefab != null && entry.weight != null
                    ? entry.weight.Evaluate(ElapsedSeconds) : 0f);

            var index = SpawnMath.PickWeighted(weights, Random.value);
            if (index < 0) return false;

            var creature = config.creatures[index].creature;
            var enemy = GetPool(creature).Get();
            if (enemy == null) return false;

            enemy.transform.SetPositionAndRotation(
                SpawnMath.RingPoint(center, config.spawnRingRadius, Random.value), Quaternion.identity);
            enemy.Initialize(creature, release);
            alive.Add(enemy);
            return true;
        }

        void RecycleFarEnemies(Vector3 center)
        {
            var maxSqr = config.despawnDistance * config.despawnDistance;
            for (var i = alive.Count - 1; i >= 0; i--)
            {
                var offset = alive[i].transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > maxSqr) alive[i].Despawn();
            }
        }

        void Release(EnemyCharacter enemy)
        {
            alive.Remove(enemy);
            if (enemy.Creature != null && pools.TryGetValue(enemy.Creature, out var pool)) pool.Release(enemy);
            else enemy.gameObject.SetActive(false);
        }

        ObjectPool<EnemyCharacter> GetPool(CreatureData creature)
        {
            if (pools.TryGetValue(creature, out var pool)) return pool;

            pool = new ObjectPool<EnemyCharacter>(
                () => CreateEnemy(creature),
                enemy => { if (enemy != null) enemy.gameObject.SetActive(true); },
                enemy => enemy.gameObject.SetActive(false),
                enemy => { if (enemy != null) Destroy(enemy.gameObject); });
            pools.Add(creature, pool);
            return pool;
        }

        EnemyCharacter CreateEnemy(CreatureData creature)
        {
            var instance = Instantiate(creature.prefab, transform);
            var enemy = instance.GetComponent<EnemyCharacter>();
            if (enemy == null)
            {
                Debug.LogError($"Creature '{creature.name}' prefab has no EnemyCharacter component.", creature);
                Destroy(instance);
            }
            return enemy;
        }
    }
}

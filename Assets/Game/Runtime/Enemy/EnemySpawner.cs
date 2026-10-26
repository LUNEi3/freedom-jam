using System;
using System.Collections.Generic;
using FreedomJam.Foundation;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace FreedomJam.Game.Enemy
{
    /// <summary>
    /// Spawns pooled enemies on a ring around the player following a timeline of spawn phases (MVP §4.6),
    /// and recycles enemies left too far behind. Run time comes from the foundation run timer when there is one.
    /// The phase table is edited on this component; Prefabs/EnemySpawner.prefab holds the game's table.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        public struct CreatureShare
        {
            public CreatureData creature;
            [Tooltip("Share of this phase's spawns in %. Shares are scaled if they don't add up to 100.")]
            [Min(0f)] public float percent;
        }

        [Serializable]
        public sealed class SpawnPhase
        {
            [Tooltip("Filled in automatically from the phase times.")]
            public string label;
            [Tooltip("Run time in seconds when this phase begins.")]
            [Min(0f)] public float startTime;
            [Min(0f)] public float spawnsPerSecond = 1f;
            [Tooltip("Max enemies alive when the phase begins. Rises smoothly to Max Alive End by the time the phase ends.")]
            [Min(0)] public int maxAliveStart = 10;
            [Min(0)] public int maxAliveEnd = 12;
            public List<CreatureShare> creatures = new List<CreatureShare>();
        }

        [Header("Spawn area")]
        [Tooltip("Enemies appear on this ring around the player; keep it outside the camera view.")]
        [SerializeField, Min(0f)] float spawnRingRadius = 18f;
        [Tooltip("Enemies further than this from the player are recycled (MVP REQ-SPAWN-3).")]
        [SerializeField, Min(0f)] float despawnDistance = 30f;

        [Header("Phases (run time in seconds; each runs until the next one starts)")]
        [Tooltip("The last phase has no next phase to end it: its max alive reaches Max Alive End after this many seconds, then holds.")]
        [SerializeField, Min(0f)] float lastPhaseRampSeconds = 30f;
        [SerializeField] List<SpawnPhase> phases = new List<SpawnPhase>
        {
            new SpawnPhase { startTime = 0f, spawnsPerSecond = 1f, maxAliveStart = 10, maxAliveEnd = 12 },
            new SpawnPhase { startTime = 30f, spawnsPerSecond = 1.5f, maxAliveStart = 12, maxAliveEnd = 20 },
            new SpawnPhase { startTime = 60f, spawnsPerSecond = 2.5f, maxAliveStart = 20, maxAliveEnd = 35 },
            new SpawnPhase { startTime = 90f, spawnsPerSecond = 4f, maxAliveStart = 35, maxAliveEnd = 50 },
        };

        readonly Dictionary<CreatureData, ObjectPool<EnemyCharacter>> pools = new();
        readonly List<EnemyCharacter> alive = new();
        readonly List<float> weights = new();
        Action<EnemyCharacter> release;
        float spawnBudget;

        public int AliveCount => alive.Count;
        public float ElapsedSeconds { get; private set; }
        /// <summary>Phase active right now, or null if there are no phases.</summary>
        public SpawnPhase CurrentPhase => SpawnMath.PhaseAt(phases, ElapsedSeconds);
        /// <summary>Max enemies alive right now.</summary>
        public int CurrentMaxAlive => SpawnMath.MaxAliveAt(phases, lastPhaseRampSeconds, ElapsedSeconds);

        void Awake() => release = Release;

        void Update()
        {
            var player = GameplayContext.Player;
            var deltaTime = Time.deltaTime;
            if (player == null || player.Character == null || !player.Character.IsAlive || deltaTime <= 0f) return;

            ElapsedSeconds = SpawnMath.NextElapsedSeconds(ElapsedSeconds, deltaTime, RunElapsedSeconds());
            var center = player.Character.transform.position;
            RecycleFarEnemies(center);

            var phase = CurrentPhase;
            if (phase == null) return;

            spawnBudget += Mathf.Max(0f, phase.spawnsPerSecond) * deltaTime;
            var maxAlive = CurrentMaxAlive;
            while (spawnBudget >= 1f)
            {
                spawnBudget -= 1f;
                if (alive.Count >= maxAlive)
                {
                    spawnBudget = 0f;
                    break;
                }
                if (!Spawn(center, phase)) break;
            }
        }

        /// <summary>Seconds since the run started, so spawn waves match the HUD countdown. Null without a run (sandbox).</summary>
        static float? RunElapsedSeconds()
        {
            var bootstrap = GameFoundationBootstrap.Instance;
            var run = bootstrap != null && bootstrap.Flow != null ? bootstrap.Flow.CurrentRun : null;
            return run != null ? run.ElapsedSeconds : null;
        }

        void OnDestroy()
        {
            foreach (var pool in pools.Values) pool.Dispose();
            pools.Clear();
            alive.Clear();
        }

        // Labels each phase "0:30 → 1:00" in the Inspector and flags shares that don't add up to 100 %.
        void OnValidate()
        {
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null) continue;

                var end = SpawnMath.PhaseEnd(phases, i);
                var label = $"{FormatTime(phase.startTime)} → {(float.IsPositiveInfinity(end) ? "end" : FormatTime(end))}";

                var total = 0f;
                foreach (var share in phase.creatures)
                    if (share.creature != null) total += share.percent;
                if (total <= 0f) label += "   ⚠ no creatures";
                else if (Mathf.Abs(total - 100f) > 0.01f) label += $"   ⚠ shares add up to {total:0.#}% (scaled to 100%)";

                phase.label = label;
            }
        }

        static string FormatTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        bool Spawn(Vector3 center, SpawnPhase phase)
        {
            // Percent shares are relative weights, so they need not add up to exactly 100.
            weights.Clear();
            foreach (var share in phase.creatures)
                weights.Add(share.creature != null && share.creature.prefab != null ? share.percent : 0f);

            var index = SpawnMath.PickWeighted(weights, Random.value);
            if (index < 0) return false;

            var creature = phase.creatures[index].creature;
            var enemy = GetPool(creature).Get();
            if (enemy == null) return false;

            enemy.transform.SetPositionAndRotation(
                SpawnMath.RingPoint(center, spawnRingRadius, Random.value), Quaternion.identity);
            enemy.Initialize(creature, release);
            alive.Add(enemy);
            return true;
        }

        void RecycleFarEnemies(Vector3 center)
        {
            var maxSqr = despawnDistance * despawnDistance;
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

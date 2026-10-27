using System.Collections.Generic;
using FreedomJam.Game.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class EnemyTests
    {
        sealed class FakePlayer : IPlayer
        {
            public bool inMouth;
            public Character Character => null;
            public bool IsInMouth(Vector3 worldPosition) => inMouth;
            public float PickupRadius => 0f;
        }

        sealed class TestPlayerData : CharacterData { }

        sealed class TestPlayer : Character, IPlayer
        {
            public CharacterData data;
            protected override CharacterData Data => data;
            protected override void Tick(float deltaTime) { }
            protected override void OnDied(DamageInfo killingBlow) { }
            public Character Character => this;
            public bool IsInMouth(Vector3 worldPosition) => false;
            public float PickupRadius => 0f;
        }

        class TestEnemy : BasicEnemy
        {
            public void Step(float deltaTime) => Move(deltaTime);
            public void Frame(float deltaTime) => Tick(deltaTime);
        }

        GameObject go;
        CreatureData creature;
        BasicEnemy enemy;
        FakePlayer player;
        int released;

        [SetUp] public void SetUp()
        {
            creature = ScriptableObject.CreateInstance<CreatureData>();
            creature.maxHP = 5; creature.bodySize = 2; creature.xpValue = 3;
            go = new GameObject("Enemy");
            enemy = go.AddComponent<BasicEnemy>();
            player = new FakePlayer();
            GameplayContext.RegisterPlayer(player);
            released = 0;
        }
        [TearDown] public void TearDown()
        {
            GameplayContext.UnregisterPlayer(player);
            Object.DestroyImmediate(go); Object.DestroyImmediate(creature);
        }

        static DamageInfo Hit(float amount) => new DamageInfo(amount, DamageType.Normal, null);

        [Test] public void Initialize_SetsCreatureFullHPAndBodyScale()
        {
            enemy.Initialize(creature, _ => released++);
            Assert.That(enemy.Creature, Is.SameAs(creature)); Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.CurrentHP, Is.EqualTo(5)); Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one * 2));
        }
        [Test] public void Initialize_Twice_DoesNotCompoundBodyScale()
        {
            enemy.Initialize(creature, null); enemy.Initialize(creature, null);
            Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one * 2));
        }
        [Test] public void Initialize_WithStatMultipliers_ScalesHpSpeedSizeAndDamage()
        {
            creature.moveSpeed = 3; creature.contactDamage = 1;
            enemy.Initialize(creature, null, new EnemyStatMultipliers { hp = 2f, damage = 1.5f, speed = 1.5f, size = 1.25f });
            Assert.That(enemy.MaxHP, Is.EqualTo(10)); Assert.That(enemy.CurrentHP, Is.EqualTo(10));
            Assert.That(enemy.MoveSpeed, Is.EqualTo(4.5f).Within(1e-5)); Assert.That(enemy.ContactDamage, Is.EqualTo(1.5f).Within(1e-5));
            Assert.That(enemy.BodySize, Is.EqualTo(2.5f).Within(1e-5)); Assert.That(go.transform.localScale.x, Is.EqualTo(2.5f).Within(1e-5));
        }
        [Test] public void Reuse_TakesNewStatMultipliers()
        {
            enemy.Initialize(creature, null, new EnemyStatMultipliers { hp = 2f, damage = 2f, speed = 2f, size = 2f });
            enemy.Initialize(creature, null);
            Assert.That(enemy.MaxHP, Is.EqualTo(5)); Assert.That(enemy.CurrentHP, Is.EqualTo(5));
            Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one * 2));
        }
        [Test] public void UnsetStatMultipliers_CountAsOne()
        {
            enemy.Initialize(creature, null, default);
            Assert.That(enemy.MaxHP, Is.EqualTo(5)); Assert.That(enemy.BodySize, Is.EqualTo(2));
        }
        [Test] public void DyingInsideMouth_RaisesEatenOnlyAndReleases()
        {
            CreatureData eaten = null; var killed = 0;
            System.Action<CreatureData> onEaten = c => eaten = c;
            System.Action<CreatureData, Vector3> onKilled = (_, _) => killed++;
            GameplayEvents.CreatureEaten += onEaten; GameplayEvents.CreatureKilled += onKilled;
            try
            {
                player.inMouth = true; enemy.Initialize(creature, _ => released++);
                enemy.TakeDamage(Hit(99));
                Assert.That(eaten, Is.SameAs(creature)); Assert.That(killed, Is.Zero); Assert.That(released, Is.EqualTo(1));
            }
            finally { GameplayEvents.CreatureEaten -= onEaten; GameplayEvents.CreatureKilled -= onKilled; }
        }
        [Test] public void DyingOutsideMouth_RaisesKilledAtPositionAndReleases()
        {
            var eaten = 0; CreatureData killed = null; var at = Vector3.zero;
            System.Action<CreatureData> onEaten = _ => eaten++;
            System.Action<CreatureData, Vector3> onKilled = (c, p) => { killed = c; at = p; };
            GameplayEvents.CreatureEaten += onEaten; GameplayEvents.CreatureKilled += onKilled;
            try
            {
                go.transform.position = new Vector3(3, 0, 4);
                enemy.Initialize(creature, _ => released++);
                enemy.TakeDamage(Hit(99));
                Assert.That(killed, Is.SameAs(creature)); Assert.That(at, Is.EqualTo(new Vector3(3, 0, 4)));
                Assert.That(eaten, Is.Zero); Assert.That(released, Is.EqualTo(1));
            }
            finally { GameplayEvents.CreatureEaten -= onEaten; GameplayEvents.CreatureKilled -= onKilled; }
        }
        [Test] public void Dying_EatenOrKilled_RaisesCreatureDiedAtPosition()
        {
            var died = 0; var at = Vector3.zero;
            System.Action<CreatureData, Vector3> onDied = (_, p) => { died++; at = p; };
            GameplayEvents.CreatureDied += onDied;
            try
            {
                player.inMouth = true; go.transform.position = new Vector3(1, 0, 2);
                enemy.Initialize(creature, null); enemy.TakeDamage(Hit(99));
                Assert.That(died, Is.EqualTo(1)); Assert.That(at, Is.EqualTo(new Vector3(1, 0, 2)));
                go.SetActive(true); player.inMouth = false;
                enemy.Initialize(creature, null); enemy.TakeDamage(Hit(99));
                Assert.That(died, Is.EqualTo(2));
            }
            finally { GameplayEvents.CreatureDied -= onDied; }
        }
        [Test] public void NonLethalDamage_DoesNotRelease()
        {
            enemy.Initialize(creature, _ => released++); enemy.TakeDamage(Hit(2));
            Assert.That(enemy.CurrentHP, Is.EqualTo(3)); Assert.That(released, Is.Zero);
        }
        [Test] public void Despawn_WithoutPool_DeactivatesInsteadOfDestroying()
        {
            enemy.Initialize(creature, null); enemy.Despawn();
            Assert.That(go.activeSelf, Is.False);
        }

        // Enemy bodySize 2 (radius 1) + player bodySize 1 (radius 0.5) → contact at 1.5.
        void WithChaseSetup(System.Action<TestEnemy, GameObject> test)
        {
            var playerData = ScriptableObject.CreateInstance<TestPlayerData>(); playerData.bodySize = 1;
            var playerGo = new GameObject("Player"); var testPlayer = playerGo.AddComponent<TestPlayer>();
            testPlayer.data = playerData; testPlayer.ResetCharacter();
            var enemyGo = new GameObject("ChaseEnemy"); var chaser = enemyGo.AddComponent<TestEnemy>();
            creature.moveSpeed = 3;
            chaser.Initialize(creature, null);
            GameplayContext.RegisterPlayer(testPlayer);
            try { test(chaser, enemyGo); }
            finally
            {
                GameplayContext.UnregisterPlayer(testPlayer);
                Object.DestroyImmediate(enemyGo); Object.DestroyImmediate(playerGo); Object.DestroyImmediate(playerData);
            }
        }

        [Test] public void Move_ChasesAtConstantSpeedAndStopsAtContact() => WithChaseSetup((chaser, enemyGo) =>
        {
            enemyGo.transform.position = new Vector3(0, 0, 10);
            chaser.Step(1f);
            Assert.That(enemyGo.transform.position.z, Is.EqualTo(7).Within(1e-4));
            chaser.Step(100f);
            Assert.That(enemyGo.transform.position.z, Is.EqualTo(1.5f).Within(1e-4));
            Assert.That(enemyGo.transform.forward.z, Is.EqualTo(-1).Within(1e-4));
        });
        [Test] public void Move_FromInsidePlayer_MovesOutToSurface() => WithChaseSetup((chaser, enemyGo) =>
        {
            enemyGo.transform.position = new Vector3(0, 0, 0.5f);
            chaser.Step(0.01f);
            Assert.That(enemyGo.transform.position.z, Is.EqualTo(1.5f).Within(1e-4));
        });
        [Test] public void IsTouchingPlayer_TrueAtContactFalseWhenFar() => WithChaseSetup((chaser, enemyGo) =>
        {
            enemyGo.transform.position = new Vector3(1.5f, 0, 0);
            Assert.That(chaser.IsTouchingPlayer(), Is.True);
            enemyGo.transform.position = new Vector3(3f, 0, 0);
            Assert.That(chaser.IsTouchingPlayer(), Is.False);
        });

        // Suction target (bodySize 1) at x = 20; the player from WithChaseSetup stays at the origin.
        void WithSuctionTarget(System.Action<TestPlayer> test)
        {
            var targetData = ScriptableObject.CreateInstance<TestPlayerData>(); targetData.bodySize = 1;
            var targetGo = new GameObject("SuctionTarget"); var target = targetGo.AddComponent<TestPlayer>();
            target.data = targetData; target.ResetCharacter();
            targetGo.transform.position = new Vector3(20, 0, 0);
            try { test(target); }
            finally { Object.DestroyImmediate(targetGo); Object.DestroyImmediate(targetData); }
        }

        [Test] public void Suction_PullsTowardTargetAtItsSpeedAndStopsAtContact() => WithChaseSetup((chaser, enemyGo) =>
            WithSuctionTarget(target =>
            {
                enemyGo.transform.position = new Vector3(10, 0, 0);
                chaser.ApplySuction(target, 4f, 10f);
                chaser.Step(1f);
                Assert.That(enemyGo.transform.position.x, Is.EqualTo(14f).Within(1e-4));
                chaser.Step(100f);
                Assert.That(enemyGo.transform.position.x, Is.EqualTo(18.5f).Within(1e-4));
            }));
        [Test] public void Suction_IsClearedWhenReused() => WithChaseSetup((chaser, enemyGo) =>
            WithSuctionTarget(target =>
            {
                enemyGo.transform.position = new Vector3(10, 0, 0);
                chaser.ApplySuction(target, 4f, 10f);
                chaser.Initialize(creature, null);
                chaser.Step(1f);
                Assert.That(enemyGo.transform.position.x, Is.EqualTo(7f).Within(1e-4));
            }));

        // Second enemy (bodySize 2, radius 1) next to the chaser: the two touch at 2 units apart.
        void WithNeighbour(Vector3 at, System.Action<TestEnemy, GameObject> test)
        {
            var neighbourGo = new GameObject("Neighbour"); var neighbour = neighbourGo.AddComponent<TestEnemy>();
            neighbour.Initialize(creature, null);
            neighbourGo.transform.position = at;
            try { test(neighbour, neighbourGo); }
            finally { Object.DestroyImmediate(neighbourGo); }
        }

        // deltaTime 0: Move does nothing, so only separation runs.
        [Test] public void Separation_PushesOverlappingEnemiesApartUntilTouching() => WithChaseSetup((chaser, enemyGo) =>
            WithNeighbour(new Vector3(0.5f, 0, 20), (neighbour, neighbourGo) =>
            {
                enemyGo.transform.position = new Vector3(0, 0, 20);
                for (var i = 0; i < 30; i++) { chaser.Frame(0f); neighbour.Frame(0f); }
                Assert.That(Vector3.Distance(enemyGo.transform.position, neighbourGo.transform.position), Is.EqualTo(2f).Within(1e-3));
                Assert.That(enemyGo.transform.position.z, Is.EqualTo(20f).Within(1e-4));
            }));
        [Test] public void Separation_NeverPushesIntoPlayer() => WithChaseSetup((chaser, enemyGo) =>
            WithNeighbour(new Vector3(0, 0, 2f), (neighbour, neighbourGo) =>
            {
                enemyGo.transform.position = new Vector3(0, 0, 1.5f);
                chaser.Frame(0f);
                Assert.That(enemyGo.transform.position.magnitude, Is.EqualTo(1.5f).Within(1e-4));
            }));
        [Test] public void Separation_IgnoresInactiveAndDeadEnemies() => WithChaseSetup((chaser, enemyGo) =>
            WithNeighbour(new Vector3(0.5f, 0, 20), (neighbour, neighbourGo) =>
            {
                enemyGo.transform.position = new Vector3(0, 0, 20);
                neighbourGo.SetActive(false);
                chaser.Frame(0f);
                Assert.That(enemyGo.transform.position, Is.EqualTo(new Vector3(0, 0, 20)));

                neighbourGo.SetActive(true);
                neighbour.TakeDamage(Hit(99));
                chaser.Frame(0f);
                Assert.That(enemyGo.transform.position, Is.EqualTo(new Vector3(0, 0, 20)));
            }));

        [Test] public void RingPoint_IsOnRingAtCenterHeight()
        {
            var center = new Vector3(5, 2, -3);
            foreach (var angle in new[] { 0f, 0.25f, 0.6f, 1f })
            {
                var point = SpawnMath.RingPoint(center, 10, angle);
                Assert.That(point.y, Is.EqualTo(2));
                Assert.That(Vector3.Distance(point, center), Is.EqualTo(10).Within(1e-4));
            }
        }
        [Test] public void NextElapsedSeconds_FollowsRunTimerOrCountsOnItsOwn()
        {
            Assert.That(SpawnMath.NextElapsedSeconds(5f, 0.5f, null), Is.EqualTo(5.5f));
            Assert.That(SpawnMath.NextElapsedSeconds(5f, 0.5f, 42f), Is.EqualTo(42f));
        }
        static List<EnemySpawner.SpawnPhase> Phases(params (float start, int maxStart, int maxEnd)[] specs)
        {
            var list = new List<EnemySpawner.SpawnPhase>();
            foreach (var (start, maxStart, maxEnd) in specs)
                list.Add(new EnemySpawner.SpawnPhase { startTime = start, maxAliveStart = maxStart, maxAliveEnd = maxEnd });
            return list;
        }

        [Test] public void FindPhase_PicksLatestStartedPhaseInAnyOrder()
        {
            var phases = Phases((60, 0, 0), (0, 0, 0), (30, 0, 0));
            Assert.That(SpawnMath.FindPhase(phases, 0f), Is.EqualTo(1));
            Assert.That(SpawnMath.FindPhase(phases, 29.9f), Is.EqualTo(1));
            Assert.That(SpawnMath.FindPhase(phases, 30f), Is.EqualTo(2));
            Assert.That(SpawnMath.FindPhase(phases, 999f), Is.EqualTo(0));
            Assert.That(SpawnMath.FindPhase(Phases((10, 0, 0), (20, 0, 0)), 5f), Is.EqualTo(0));
            Assert.That(SpawnMath.FindPhase(new List<EnemySpawner.SpawnPhase> { null }, 5f), Is.EqualTo(-1));
        }
        [Test] public void PhaseEnd_IsNextStartOrInfinity()
        {
            var phases = Phases((60, 0, 0), (0, 0, 0), (30, 0, 0));
            Assert.That(SpawnMath.PhaseEnd(phases, 1), Is.EqualTo(30f));
            Assert.That(SpawnMath.PhaseEnd(phases, 2), Is.EqualTo(60f));
            Assert.That(SpawnMath.PhaseEnd(phases, 0), Is.EqualTo(float.PositiveInfinity));
        }
        [Test] public void MaxAliveAt_RampsWithinEachPhase()
        {
            var phases = Phases((0, 10, 12), (30, 12, 20));
            Assert.That(SpawnMath.PhaseAt(phases, 10f), Is.SameAs(phases[0]));
            Assert.That(SpawnMath.PhaseAt(phases, 45f), Is.SameAs(phases[1]));
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 0f), Is.EqualTo(10));
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 15f), Is.EqualTo(11));
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 30f), Is.EqualTo(12));
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 45f), Is.EqualTo(16));
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 999f), Is.EqualTo(20));
        }
        [Test] public void NoPhases_SpawnsNothing()
        {
            var phases = new List<EnemySpawner.SpawnPhase>();
            Assert.That(SpawnMath.PhaseAt(phases, 5f), Is.Null);
            Assert.That(SpawnMath.MaxAliveAt(phases, 30f, 5f), Is.Zero);
        }
        [Test] public void PickWeighted_SplitsRollByWeight()
        {
            var weights = new List<float> { 1, 3 };
            Assert.That(SpawnMath.PickWeighted(weights, 0f), Is.EqualTo(0));
            Assert.That(SpawnMath.PickWeighted(weights, 0.24f), Is.EqualTo(0));
            Assert.That(SpawnMath.PickWeighted(weights, 0.26f), Is.EqualTo(1));
            Assert.That(SpawnMath.PickWeighted(weights, 1f), Is.EqualTo(1));
        }
        [Test] public void PickWeighted_SkipsZeroNegativeAndNaN()
        {
            var weights = new List<float> { 0, -2, float.NaN, 5, 0 };
            Assert.That(SpawnMath.PickWeighted(weights, 0f), Is.EqualTo(3));
            Assert.That(SpawnMath.PickWeighted(weights, 1f), Is.EqualTo(3));
            Assert.That(SpawnMath.PickWeighted(new List<float> { 0, -1 }, 0.5f), Is.EqualTo(-1));
            Assert.That(SpawnMath.PickWeighted(new List<float>(), 0.5f), Is.EqualTo(-1));
        }
    }
}

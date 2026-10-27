using FreedomJam.Game.Objective;
using FreedomJam.Game.Pickups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class PickupAndObjectiveTests
    {
        sealed class TestData : CharacterData { }

        sealed class TestPlayer : Character, IPlayer
        {
            public CharacterData data;
            public float pickupRadius;
            protected override CharacterData Data => data;
            protected override void Tick(float deltaTime) { }
            protected override void OnDied(DamageInfo killingBlow) { }
            public Character Character => this;
            public bool IsInMouth(Vector3 worldPosition) => false;
            public float PickupRadius => pickupRadius;
        }

        TestData data;
        GameObject playerGo;
        TestPlayer player;
        GameObject orbGo;

        // Player: max HP 10, body size 1 (radius 0.5) at the origin.
        [SetUp] public void SetUp()
        {
            data = ScriptableObject.CreateInstance<TestData>(); data.maxHP = 10; data.bodySize = 1;
            playerGo = new GameObject("Player"); player = playerGo.AddComponent<TestPlayer>();
            player.data = data; player.ResetCharacter();
            GameplayContext.RegisterPlayer(player);
        }
        [TearDown] public void TearDown()
        {
            GameplayContext.UnregisterPlayer(player);
            if (orbGo != null) Object.DestroyImmediate(orbGo);
            Object.DestroyImmediate(playerGo); Object.DestroyImmediate(data);
        }

        static DamageInfo Hit(float amount) => new DamageInfo(amount, DamageType.Normal, null);

        HpOrb NewHpOrb(Vector3 at, float heal, float respawn)
        {
            orbGo = new GameObject("HpOrb"); orbGo.transform.position = at;
            var orb = orbGo.AddComponent<HpOrb>();
            var so = new SerializedObject(orb);
            so.FindProperty("healAmount").floatValue = heal;
            so.FindProperty("respawnSeconds").floatValue = respawn;
            so.ApplyModifiedPropertiesWithoutUndo();
            return orb;
        }

        [Test] public void HpOrb_OnTouch_HealsCappedAtMaxAndHides()
        {
            player.TakeDamage(Hit(2));
            var orb = NewHpOrb(new Vector3(0.6f, 0, 0), heal: 3, respawn: 20);
            orb.Step(0.1f);
            Assert.That(player.CurrentHP, Is.EqualTo(10)); Assert.That(orb.IsAvailable, Is.False);
        }
        [Test] public void HpOrb_AtFullHp_IsNotUsed()
        {
            var orb = NewHpOrb(new Vector3(0.6f, 0, 0), heal: 3, respawn: 20);
            orb.Step(0.1f);
            Assert.That(orb.IsAvailable, Is.True);
        }
        [Test] public void HpOrb_RespawnsAfterRespawnSeconds()
        {
            player.TakeDamage(Hit(5));
            var orb = NewHpOrb(new Vector3(0.6f, 0, 0), heal: 3, respawn: 20);
            orb.Step(0.1f);
            Assert.That(player.CurrentHP, Is.EqualTo(8));
            playerGo.transform.position = new Vector3(50, 0, 0);   // walk away while it respawns
            orb.Step(19f); Assert.That(orb.IsAvailable, Is.False);
            orb.Step(1f); Assert.That(orb.IsAvailable, Is.True);
            Assert.That(orbGo.transform.position, Is.EqualTo(new Vector3(0.6f, 0, 0)));
            playerGo.transform.position = Vector3.zero;
            orb.Step(0.1f); Assert.That(player.CurrentHP, Is.EqualTo(10));
        }
        [Test] public void HpOrb_ZeroRespawn_IsOneUseOnly()
        {
            player.TakeDamage(Hit(5));
            var orb = NewHpOrb(new Vector3(0.6f, 0, 0), heal: 1, respawn: 0);
            orb.Step(0.1f); orb.Step(999f); orb.Step(0.1f);
            Assert.That(player.CurrentHP, Is.EqualTo(6)); Assert.That(orb.IsAvailable, Is.False);
        }
        [Test] public void HpOrb_OutsidePickupRadius_StaysPut()
        {
            player.TakeDamage(Hit(5)); player.pickupRadius = 0.75f;
            var orb = NewHpOrb(new Vector3(3, 0, 0), heal: 3, respawn: 20);
            orb.Step(0.5f);
            Assert.That(orbGo.transform.position, Is.EqualTo(new Vector3(3, 0, 0))); Assert.That(player.CurrentHP, Is.EqualTo(5));
        }

        [Test] public void XpOrb_OnTouch_RaisesXpCollectedOnce()
        {
            orbGo = new GameObject("XpOrb"); var orb = orbGo.AddComponent<XpOrb>();
            var collected = 0; System.Action<int> onXp = amount => collected += amount;
            GameplayEvents.XpCollected += onXp;
            try
            {
                orb.Drop(new Vector3(0.6f, 0, 0), 3, null);
                orb.Step(0.1f); orb.Step(0.1f);
                Assert.That(collected, Is.EqualTo(3)); Assert.That(orbGo.activeSelf, Is.False);
            }
            finally { GameplayEvents.XpCollected -= onXp; }
        }

        LostSpaceship NewSpaceship(Vector3 at, out GameObject visual)
        {
            orbGo = new GameObject("Spaceship"); orbGo.transform.position = at;
            visual = new GameObject("Visual"); visual.transform.SetParent(orbGo.transform);
            var ship = orbGo.AddComponent<LostSpaceship>();
            var so = new SerializedObject(ship);
            so.FindProperty("appearAtSeconds").floatValue = 90;
            so.FindProperty("reachRadius").floatValue = 2;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.SetActive(false);
            return ship;
        }

        [Test] public void Spaceship_AppearsAtItsTimeAndRaisesAppearedOnce()
        {
            var ship = NewSpaceship(new Vector3(25, 0, 0), out var visual);
            var appeared = 0; ship.Appeared += _ => appeared++;
            ship.Step(89f); Assert.That(ship.HasAppeared, Is.False); Assert.That(visual.activeSelf, Is.False);
            ship.Step(90f); ship.Step(95f);
            Assert.That(ship.HasAppeared, Is.True); Assert.That(visual.activeSelf, Is.True); Assert.That(appeared, Is.EqualTo(1));
            Assert.That(ship.IsReached, Is.False);
        }
        [Test] public void Spaceship_ReachedOnlyAfterItAppears()
        {
            var ship = NewSpaceship(new Vector3(2, 0, 0), out _);
            var reached = 0; ship.Reached += _ => reached++;
            ship.Step(10f); Assert.That(ship.IsReached, Is.False);
            ship.Step(90f); ship.Step(91f);
            Assert.That(ship.IsReached, Is.True); Assert.That(reached, Is.EqualTo(1));
        }
        [Test] public void Spaceship_DeadPlayerCannotReachIt()
        {
            var ship = NewSpaceship(new Vector3(1, 0, 0), out _);
            player.TakeDamage(Hit(99));
            ship.Step(90f);
            Assert.That(ship.IsReached, Is.False);
        }
    }
}

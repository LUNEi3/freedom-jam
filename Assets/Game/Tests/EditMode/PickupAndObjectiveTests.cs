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

        [Test] public void AbilityStatus_FractionAndSecondsText()
        {
            var cooling = new FreedomJam.Game.Player.AbilityStatus("Dash", "J", 1, 2.5f, 10f);
            Assert.That(cooling.IsLocked, Is.False); Assert.That(cooling.IsReady, Is.False);
            Assert.That(cooling.CooldownFraction, Is.EqualTo(0.25f).Within(1e-5));
            Assert.That(FreedomJam.Game.Player.AbilityStatus.FormatSeconds(3.24f), Is.EqualTo("3.2"));
            Assert.That(FreedomJam.Game.Player.AbilityStatus.FormatSeconds(12.1f), Is.EqualTo("13"));
            Assert.That(FreedomJam.Game.Player.AbilityStatus.FormatSeconds(0f), Is.Empty);
            var locked = new FreedomJam.Game.Player.AbilityStatus("Suck", "K", 0, 0f, 10f);
            Assert.That(locked.IsLocked, Is.True); Assert.That(locked.IsReady, Is.False);
        }
        [Test] public void PlayerAbilities_ReportDashAndSuck_LockedUntilUpgraded()
        {
            var go = new GameObject("Abilities");
            try
            {
                var abilities = go.AddComponent<FreedomJam.Game.Player.PlayerAbilityController>();
                var list = new System.Collections.Generic.List<FreedomJam.Game.Player.AbilityStatus>();
                abilities.AppendAbilityStatus(list);
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list[0].Name, Is.EqualTo("Dash")); Assert.That(list[0].Key, Is.EqualTo("J")); Assert.That(list[0].IsLocked, Is.True);
                Assert.That(list[1].Name, Is.EqualTo("Suck")); Assert.That(list[1].Key, Is.EqualTo("K")); Assert.That(list[1].IsLocked, Is.True);
                abilities.TryUpgradeDash(); list.Clear(); abilities.AppendAbilityStatus(list);
                Assert.That(list[0].Level, Is.EqualTo(1)); Assert.That(list[0].IsReady, Is.True); Assert.That(list[0].Cooldown, Is.EqualTo(10f));
                Assert.That(list[1].IsLocked, Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }

        LostSpaceship NewSpaceship(Vector3 at, out GameObject effect)
        {
            orbGo = new GameObject("Spaceship"); orbGo.transform.position = at;
            effect = new GameObject("ReadyEffect"); effect.transform.SetParent(orbGo.transform);
            var ship = orbGo.AddComponent<LostSpaceship>();
            var so = new SerializedObject(ship);
            so.FindProperty("activateAtSeconds").floatValue = 90;
            so.FindProperty("reachRadius").floatValue = 2;
            so.FindProperty("readyEffect").objectReferenceValue = effect;
            so.ApplyModifiedPropertiesWithoutUndo();
            effect.SetActive(false);
            return ship;
        }

        [Test] public void Spaceship_ActivatesAtItsTimeAndRaisesActivatedOnce()
        {
            var ship = NewSpaceship(new Vector3(25, 0, 0), out var effect);
            var activated = 0; ship.Activated += _ => activated++;
            ship.Step(89f); Assert.That(ship.IsActive, Is.False); Assert.That(effect.activeSelf, Is.False);
            ship.Step(90f); ship.Step(95f);
            Assert.That(ship.IsActive, Is.True); Assert.That(effect.activeSelf, Is.True); Assert.That(activated, Is.EqualTo(1));
            Assert.That(ship.IsReached, Is.False);
        }
        [Test] public void Spaceship_TouchedBeforeActivation_DoesNothing_ThenWinsOnceActive()
        {
            var ship = NewSpaceship(new Vector3(2, 0, 0), out _);
            var reached = 0; ship.Reached += _ => reached++;
            ship.Step(10f); ship.Step(89.9f); Assert.That(ship.IsReached, Is.False);
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

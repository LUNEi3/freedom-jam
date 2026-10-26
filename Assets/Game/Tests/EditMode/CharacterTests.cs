using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class CharacterTests
    {
        sealed class TestData : CharacterData { }

        sealed class TestCharacter : Character, IPlayer
        {
            public CharacterData data;
            public bool acceptDamage = true;
            public int damagedCount, diedCount, resetCount;
            protected override CharacterData Data => data;
            protected override void Tick(float deltaTime) { }
            protected override bool CanReceiveDamage(DamageInfo info) => acceptDamage;
            protected override void OnReset() => resetCount++;
            protected override void OnDamaged(DamageInfo info) => damagedCount++;
            protected override void OnDied(DamageInfo killingBlow) => diedCount++;
            public Character Character => this;
            public bool IsInMouth(Vector3 worldPosition) => false;
            public float PickupRadius => 0f;
        }

        GameObject go;
        TestData data;
        TestCharacter character;

        [SetUp] public void SetUp()
        {
            data = ScriptableObject.CreateInstance<TestData>(); data.maxHP = 10;
            go = new GameObject("TestCharacter");
            character = go.AddComponent<TestCharacter>(); character.data = data;
            character.ResetCharacter(); character.resetCount = 0;
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(go); Object.DestroyImmediate(data); }

        static DamageInfo Hit(float amount) => new DamageInfo(amount, DamageType.Normal, null);

        [Test] public void Reset_StartsAliveAtMaxHP()
        {
            Assert.That(character.IsAlive, Is.True); Assert.That(character.CurrentHP, Is.EqualTo(10));
        }
        [Test] public void TakeDamage_LowersHPAndRaisesDamaged()
        {
            var raised = 0; character.Damaged += (_, _) => raised++;
            character.TakeDamage(Hit(3));
            Assert.That(character.CurrentHP, Is.EqualTo(7)); Assert.That(character.damagedCount, Is.EqualTo(1));
            Assert.That(raised, Is.EqualTo(1)); Assert.That(character.IsAlive, Is.True);
        }
        [Test] public void TakeDamage_ZeroNegativeOrNaN_IsIgnored()
        {
            character.TakeDamage(Hit(0)); character.TakeDamage(Hit(-5)); character.TakeDamage(Hit(float.NaN));
            Assert.That(character.CurrentHP, Is.EqualTo(10)); Assert.That(character.damagedCount, Is.Zero);
        }
        [Test] public void LethalDamage_ClampsToZeroAndDiesOnce()
        {
            DamageInfo blow = default; var raised = 0;
            character.Died += (_, info) => { raised++; blow = info; };
            character.TakeDamage(new DamageInfo(25, DamageType.Fire, null));
            character.TakeDamage(Hit(5));
            Assert.That(character.CurrentHP, Is.Zero); Assert.That(character.IsAlive, Is.False);
            Assert.That(character.diedCount, Is.EqualTo(1)); Assert.That(raised, Is.EqualTo(1));
            Assert.That(blow.Type, Is.EqualTo(DamageType.Fire)); Assert.That(character.damagedCount, Is.EqualTo(1));
        }
        [Test] public void CanReceiveDamageFalse_BlocksDamage()
        {
            character.acceptDamage = false; character.TakeDamage(Hit(4));
            Assert.That(character.CurrentHP, Is.EqualTo(10)); Assert.That(character.damagedCount, Is.Zero);
        }
        [Test] public void Heal_ClampsToMaxHPAndDoesNothingWhenDead()
        {
            character.TakeDamage(Hit(4)); character.Heal(100);
            Assert.That(character.CurrentHP, Is.EqualTo(10));
            character.TakeDamage(Hit(10)); character.Heal(5);
            Assert.That(character.CurrentHP, Is.Zero);
        }
        [Test] public void ResetCharacter_RevivesAtMaxHPAndCallsOnReset()
        {
            character.TakeDamage(Hit(10)); data.maxHP = 20; character.ResetCharacter();
            Assert.That(character.IsAlive, Is.True); Assert.That(character.CurrentHP, Is.EqualTo(20));
            Assert.That(character.resetCount, Is.EqualTo(1));
        }
        [Test] public void ResetCharacter_WithoutData_DoesNothingUntilDataIsSet()
        {
            var emptyGo = new GameObject("NoData"); var empty = emptyGo.AddComponent<TestCharacter>();
            try
            {
                Assert.DoesNotThrow(() => empty.ResetCharacter());
                Assert.That(empty.IsAlive, Is.False); Assert.That(empty.resetCount, Is.Zero);
                empty.data = data; empty.ResetCharacter();
                Assert.That(empty.IsAlive, Is.True); Assert.That(empty.CurrentHP, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(emptyGo); }
        }
        [Test] public void GameplayContext_OnlyRegisteredPlayerCanUnregister()
        {
            var otherGo = new GameObject("Other"); var other = otherGo.AddComponent<TestCharacter>();
            try
            {
                GameplayContext.RegisterPlayer(character);
                GameplayContext.UnregisterPlayer(other);
                Assert.That(GameplayContext.Player, Is.SameAs(character));
                GameplayContext.UnregisterPlayer(character);
                Assert.That(GameplayContext.Player, Is.Null);
            }
            finally { GameplayContext.UnregisterPlayer(character); Object.DestroyImmediate(otherGo); }
        }
    }
}

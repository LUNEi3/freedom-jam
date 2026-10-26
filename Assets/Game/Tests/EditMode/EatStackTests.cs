using System.Collections.Generic;
using FreedomJam.Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class EatStackTests
    {
        sealed class FakeTable : IEatStackTable
        {
            readonly Dictionary<CreatureData, IReadOnlyList<EatStackTier>> tiers = new();
            public void Add(CreatureData creature, params EatStackTier[] creatureTiers) => tiers[creature] = creatureTiers;
            public IReadOnlyList<EatStackTier> GetTiers(CreatureData creature) =>
                creature != null && tiers.TryGetValue(creature, out var list) ? list : null;
        }

        sealed class NoCaps : IStatCaps { public float GetCap(StatType stat, int level) => float.PositiveInfinity; }

        readonly List<CreatureData> created = new();
        FakeTable table;
        CreatureData ember;
        CreatureData zipper;

        [SetUp] public void SetUp()
        {
            table = new FakeTable();
            ember = NewCreature("Ember");
            zipper = NewCreature("Zipper");
            // Ember: 5 → Damage ×1.5, 15 → Damage ×2
            table.Add(ember,
                new EatStackTier(5, new StatMultiplierReward(StatType.BaseDamage, 1.5f)),
                new EatStackTier(15, new StatMultiplierReward(StatType.BaseDamage, 2f)));
        }

        [TearDown] public void TearDown()
        {
            foreach (var creature in created) Object.DestroyImmediate(creature);
            created.Clear();
        }

        CreatureData NewCreature(string name)
        {
            var creature = ScriptableObject.CreateInstance<CreatureData>();
            creature.name = name;
            creature.displayName = name;
            created.Add(creature);
            return creature;
        }

        static void Eat(EatStack stack, CreatureData creature, int times)
        {
            for (var i = 0; i < times; i++) stack.Record(creature);
        }

        [Test] public void NothingEaten_MultiplierIsOne()
        {
            var stack = new EatStack(table);
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(1f));
            Assert.That(stack.GetTierNumber(ember), Is.EqualTo(0));
            Assert.That(stack.GetNextThreshold(ember), Is.EqualTo(5));
        }

        [Test] public void BelowFirstTier_MultiplierIsOne()
        {
            var stack = new EatStack(table);
            Eat(stack, ember, 4);
            Assert.That(stack.GetCount(ember), Is.EqualTo(4));
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(1f));
        }

        [Test] public void ReachingTier_AppliesItsMultiplier()
        {
            var stack = new EatStack(table);
            Eat(stack, ember, 5);
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(1.5f));
            Assert.That(stack.GetTierNumber(ember), Is.EqualTo(1));
            Assert.That(stack.GetNextThreshold(ember), Is.EqualTo(15));
            Assert.That(stack.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(1f));   // other stats untouched
        }

        [Test] public void NextTier_ReplacesPreviousTier()
        {
            var stack = new EatStack(table);
            Eat(stack, ember, 15);
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(2f));   // not 1.5 × 2
            Assert.That(stack.GetTierNumber(ember), Is.EqualTo(2));
            Assert.That(stack.GetNextThreshold(ember), Is.EqualTo(-1));
        }

        [Test] public void EachCreature_CountsSeparately()
        {
            table.Add(zipper, new EatStackTier(3, new StatMultiplierReward(StatType.MoveSpeed, 1.3f)));
            var stack = new EatStack(table);
            Eat(stack, ember, 3);
            Eat(stack, zipper, 2);
            Assert.That(stack.GetCount(ember), Is.EqualTo(3));
            Assert.That(stack.GetCount(zipper), Is.EqualTo(2));
            Assert.That(stack.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(1f));
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(1f));
        }

        [Test] public void TwoCreatures_SameStat_Multiply()
        {
            table.Add(zipper, new EatStackTier(1, new StatMultiplierReward(StatType.BaseDamage, 2f)));
            var stack = new EatStack(table);
            Eat(stack, ember, 5);
            Eat(stack, zipper, 1);
            Assert.That(stack.GetMultiplier(StatType.BaseDamage), Is.EqualTo(3f).Within(1e-4f));   // 1.5 × 2
        }

        [Test] public void UnlistedCreature_IsIgnored()
        {
            var stack = new EatStack(table);
            var changed = 0;
            stack.Changed += () => changed++;
            Assert.That(stack.Record(zipper), Is.False);
            Assert.That(stack.Record(null), Is.False);
            Assert.That(stack.GetCount(zipper), Is.EqualTo(0));
            Assert.That(changed, Is.EqualTo(0));
        }

        [Test] public void TiersOutOfOrder_StillWork()
        {
            table.Add(zipper,
                new EatStackTier(10, new StatMultiplierReward(StatType.MoveSpeed, 2f)),
                new EatStackTier(2, new StatMultiplierReward(StatType.MoveSpeed, 1.2f)));
            var stack = new EatStack(table);
            Eat(stack, zipper, 2);
            Assert.That(stack.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(1.2f));
            Assert.That(stack.GetNextThreshold(zipper), Is.EqualTo(10));
            Eat(stack, zipper, 8);
            Assert.That(stack.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(2f));
        }

        [Test] public void InvalidMultiplier_IsSkipped()
        {
            table.Add(zipper, new EatStackTier(1, new StatMultiplierReward(StatType.MoveSpeed, 0f)));
            var stack = new EatStack(table);
            Eat(stack, zipper, 1);
            Assert.That(stack.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(1f));
        }

        [Test] public void Events_RaisedOnlyWhenTierChanges()
        {
            var stack = new EatStack(table);
            var changed = 0;
            var reachedTiers = new List<int>();
            stack.Changed += () => changed++;
            stack.TierReached += (creature, tier, number) => reachedTiers.Add(number);
            Eat(stack, ember, 16);
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(reachedTiers, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test] public void PlayerStats_MultipliesEatStackOnTop()
        {
            var stack = new EatStack(table);
            var stats = new PlayerStats(_ => 10f, new NoCaps(), stack);
            stats.AddBonus(StatType.BaseDamage, 1f);   // old eat bonus ×2
            Eat(stack, ember, 5);                      // stack ×1.5
            Assert.That(stats.Get(StatType.BaseDamage), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(stats.GetMultiplier(StatType.BaseDamage), Is.EqualTo(2f));   // capped eat bonus only
        }

        [Test] public void PlayerStats_InverseStat_DividesByStack()
        {
            table.Add(zipper, new EatStackTier(1, new StatMultiplierReward(StatType.MouthTickInterval, 2f)));
            var stack = new EatStack(table);
            var stats = new PlayerStats(_ => 10f, new NoCaps(), stack);
            Eat(stack, zipper, 1);
            Assert.That(stats.Get(StatType.MouthTickInterval), Is.EqualTo(5f));
        }

        [Test] public void PlayerStats_RaisesChangedWhenTierReached()
        {
            var stack = new EatStack(table);
            var stats = new PlayerStats(_ => 10f, new NoCaps(), stack);
            var raised = 0;
            stats.Changed += () => raised++;
            Eat(stack, ember, 4);
            Assert.That(raised, Is.EqualTo(0));
            Eat(stack, ember, 1);
            Assert.That(raised, Is.EqualTo(1));
        }
    }
}

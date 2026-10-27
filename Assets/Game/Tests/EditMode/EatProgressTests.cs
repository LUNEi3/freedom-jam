using System.Collections.Generic;
using FreedomJam.Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class EatProgressTests
    {
        sealed class ListedTable : IEatStackTable, IEatStackCreatureList
        {
            readonly Dictionary<CreatureData, IReadOnlyList<EatStackTier>> tiers = new();
            readonly List<CreatureData> order = new();
            public void Add(CreatureData creature, params EatStackTier[] creatureTiers) { tiers[creature] = creatureTiers; order.Add(creature); }
            public IReadOnlyList<EatStackTier> GetTiers(CreatureData creature) =>
                creature != null && tiers.TryGetValue(creature, out var list) ? list : null;
            public IReadOnlyList<CreatureData> Creatures => order;
        }

        sealed class UnlistedTable : IEatStackTable
        {
            public IReadOnlyList<EatStackTier> GetTiers(CreatureData creature) => null;
        }

        CreatureData blob, gyro;

        [SetUp] public void SetUp()
        {
            blob = ScriptableObject.CreateInstance<CreatureData>(); blob.displayName = "Blob";
            gyro = ScriptableObject.CreateInstance<CreatureData>(); gyro.name = "Gyro";
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(blob); Object.DestroyImmediate(gyro); }

        EatStack NewStack()
        {
            var table = new ListedTable();
            // Listed out of order on purpose: progress follows the counts, not the list order.
            table.Add(blob,
                new EatStackTier(15, new StatMultiplierReward(StatType.BaseDamage, 2f)),
                new EatStackTier(5, new StatMultiplierReward(StatType.BaseDamage, 1.5f), new StatMultiplierReward(StatType.Size, 1.2f)));
            table.Add(gyro, new EatStackTier(5, new StatMultiplierReward(StatType.SteerSpeed, 1.3f)));
            return new EatStack(table);
        }

        [Test] public void TrackedCreatures_FollowTheTableOrder()
        {
            var stack = NewStack();
            Assert.That(stack.TrackedCreatures, Is.EqualTo(new[] { blob, gyro }));
            Assert.That(new EatStack(new UnlistedTable()).TrackedCreatures, Is.Empty);
        }
        [Test] public void Progress_BeforeFirstTier()
        {
            var stack = NewStack();
            for (var i = 0; i < 3; i++) stack.Record(blob);
            var p = stack.GetProgress(blob);
            Assert.That(p.Name, Is.EqualTo("Blob")); Assert.That(p.Count, Is.EqualTo(3));
            Assert.That(p.PreviousThreshold, Is.Zero); Assert.That(p.NextThreshold, Is.EqualTo(5));
            Assert.That(p.Fraction, Is.EqualTo(0.6f).Within(1e-5)); Assert.That(p.IsMax, Is.False);
            Assert.That(p.NextReward, Is.EqualTo("BaseDamage ×1.5, Size ×1.2")); Assert.That(p.CurrentReward, Is.Empty);
        }
        [Test] public void Progress_BetweenTiers_MeasuresFromTheReachedTier()
        {
            var stack = NewStack();
            for (var i = 0; i < 10; i++) stack.Record(blob);
            var p = stack.GetProgress(blob);
            Assert.That(p.TierNumber, Is.EqualTo(1)); Assert.That(p.PreviousThreshold, Is.EqualTo(5)); Assert.That(p.NextThreshold, Is.EqualTo(15));
            Assert.That(p.Fraction, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(p.CurrentReward, Is.EqualTo("BaseDamage ×1.5, Size ×1.2")); Assert.That(p.NextReward, Is.EqualTo("BaseDamage ×2"));
        }
        [Test] public void Progress_AfterLastTier_IsMax()
        {
            var stack = NewStack();
            for (var i = 0; i < 7; i++) stack.Record(gyro);
            var p = stack.GetProgress(gyro);
            Assert.That(p.Name, Is.EqualTo("Gyro")); Assert.That(p.IsMax, Is.True); Assert.That(p.Fraction, Is.EqualTo(1f));
            Assert.That(p.CurrentReward, Is.EqualTo("SteerSpeed ×1.3")); Assert.That(p.NextReward, Is.Empty);
        }
    }
}

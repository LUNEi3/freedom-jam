using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using NUnit.Framework;

namespace FreedomJam.Game.Tests
{
    public class SizeScalingTests
    {
        sealed class NoCaps : IStatCaps { public float GetCap(StatType stat, int level) => float.PositiveInfinity; }

        /// <summary>Stand-in for the eat stack: set a multiplier per stat by hand.</summary>
        sealed class FakeMultipliers : IStatMultiplierSource
        {
            readonly Dictionary<StatType, float> values = new();
            public event Action Changed;
            public float GetMultiplier(StatType stat) => values.TryGetValue(stat, out var value) ? value : 1f;
            public void Set(StatType stat, float value) { values[stat] = value; Changed?.Invoke(); }
        }

        sealed class FakeScaling : ISizeScaling
        {
            readonly Dictionary<StatType, float> strengths = new();
            public FakeScaling With(StatType stat, float strength) { strengths[stat] = strength; return this; }
            public float GetSizeStrength(StatType stat) => strengths.TryGetValue(stat, out var value) ? value : 0f;
        }

        /// <summary>Every stat starts at 10, Size starts at 1.</summary>
        static float Base(StatType stat) => stat == StatType.Size ? 1f : 10f;

        [Test] public void Formula_StrengthZeroHalfAndOne()
        {
            Assert.That(SizeScaling.Multiplier(1.5f, 0f), Is.EqualTo(1f));
            Assert.That(SizeScaling.Multiplier(1.5f, 0.5f), Is.EqualTo(1.25f));
            Assert.That(SizeScaling.Multiplier(1.5f, 1f), Is.EqualTo(1.5f));
            Assert.That(SizeScaling.Multiplier(1f, 3f), Is.EqualTo(1f));   // not grown → no change
        }

        [Test] public void Formula_NeverBelowMinimum_AndIgnoresBadInput()
        {
            Assert.That(SizeScaling.Multiplier(0.1f, 5f), Is.EqualTo(PlayerStats.MinMultiplier));
            Assert.That(SizeScaling.Multiplier(float.NaN, 1f), Is.EqualTo(1f));
            Assert.That(SizeScaling.Multiplier(2f, float.PositiveInfinity), Is.EqualTo(1f));
        }

        [Test] public void NotGrown_StatsUnchanged()
        {
            var stats = new PlayerStats(Base, new NoCaps(), null, new FakeScaling().With(StatType.MaxHP, 1f));
            Assert.That(stats.SizeRatio, Is.EqualTo(1f));
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(10f));
        }

        [Test] public void SizeFromEatStack_GrowsListedStats()
        {
            var stack = new FakeMultipliers();
            var scaling = new FakeScaling().With(StatType.MaxHP, 1f).With(StatType.BaseDamage, 0.5f);
            var stats = new PlayerStats(Base, new NoCaps(), stack, scaling);
            stack.Set(StatType.Size, 1.5f);

            Assert.That(stats.SizeRatio, Is.EqualTo(1.5f));
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(15f).Within(1e-4f));        // ×1.5
            Assert.That(stats.Get(StatType.BaseDamage), Is.EqualTo(12.5f).Within(1e-4f)); // ×1.25
            Assert.That(stats.Get(StatType.MoveSpeed), Is.EqualTo(10f));                  // not listed
        }

        [Test] public void SizeScaling_MultipliesWithEatStack()
        {
            var stack = new FakeMultipliers();
            var stats = new PlayerStats(Base, new NoCaps(), stack, new FakeScaling().With(StatType.BaseDamage, 1f));
            stack.Set(StatType.BaseDamage, 2f);
            stack.Set(StatType.Size, 1.5f);
            Assert.That(stats.Get(StatType.BaseDamage), Is.EqualTo(30f).Within(1e-4f));   // 10 × 2 × 1.5
        }

        [Test] public void SizeItself_IsNeverScaled()
        {
            var stack = new FakeMultipliers();
            var stats = new PlayerStats(Base, new NoCaps(), stack, new FakeScaling().With(StatType.Size, 1f));
            stack.Set(StatType.Size, 2f);
            Assert.That(stats.Get(StatType.Size), Is.EqualTo(2f));
            Assert.That(stats.GetSizeMultiplier(StatType.Size), Is.EqualTo(1f));
        }

        [Test] public void InverseStat_GetsShorterWithSize()
        {
            var stack = new FakeMultipliers();
            var stats = new PlayerStats(Base, new NoCaps(), stack, new FakeScaling().With(StatType.MouthTickInterval, 1f));
            stack.Set(StatType.Size, 2f);
            Assert.That(stats.Get(StatType.MouthTickInterval), Is.EqualTo(5f));
        }

        [Test] public void SizeRatio_UsesStartingSize()
        {
            var stack = new FakeMultipliers();
            var stats = new PlayerStats(stat => stat == StatType.Size ? 2f : 10f, new NoCaps(), stack,
                new FakeScaling().With(StatType.MaxHP, 1f));
            Assert.That(stats.SizeRatio, Is.EqualTo(1f));            // starting size 2 is "not grown"
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(10f));
            stack.Set(StatType.Size, 1.5f);
            Assert.That(stats.SizeRatio, Is.EqualTo(1.5f));
        }
    }
}

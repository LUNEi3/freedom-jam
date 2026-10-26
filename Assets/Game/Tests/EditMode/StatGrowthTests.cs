using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using NUnit.Framework;

namespace FreedomJam.Game.Tests
{
    public class StatGrowthTests
    {
        sealed class NoCaps : IStatCaps { public float GetCap(StatType stat, int level) => float.PositiveInfinity; }

        /// <summary>Same formula as LevelConfig, with per-stat values set by hand.</summary>
        sealed class FakeGrowth : IStatGrowth
        {
            readonly Dictionary<StatType, float> perLevel = new();
            public FakeGrowth With(StatType stat, float value) { perLevel[stat] = value; return this; }
            public float GetLevelMultiplier(StatType stat, int level) =>
                perLevel.TryGetValue(stat, out var value) ? LevelConfig.GrowthMultiplier(value, level) : 1f;
        }

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

        static float Base(StatType stat) => stat == StatType.Size ? 1f : 10f;

        [Test] public void Formula_LevelOneIsOne_ThenFlatPerLevel()
        {
            Assert.That(LevelConfig.GrowthMultiplier(0.05f, 1), Is.EqualTo(1f));
            Assert.That(LevelConfig.GrowthMultiplier(0.05f, 5), Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(LevelConfig.GrowthMultiplier(0.05f, 0), Is.EqualTo(1f));   // level below 1 treated as 1
        }

        [Test] public void Formula_NegativeGrowth_NeverBelowMinimum_AndIgnoresBadInput()
        {
            Assert.That(LevelConfig.GrowthMultiplier(-1f, 10), Is.EqualTo(PlayerStats.MinMultiplier));
            Assert.That(LevelConfig.GrowthMultiplier(float.NaN, 10), Is.EqualTo(1f));
        }

        [Test] public void Stats_GrowWithLevel_UnlistedUnchanged()
        {
            var stats = new PlayerStats(Base, new NoCaps(), null, null, new FakeGrowth().With(StatType.MaxHP, 0.05f));
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(10f));
            stats.SetLevel(5);
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(stats.Get(StatType.MoveSpeed), Is.EqualTo(10f));
        }

        [Test] public void LevelGrowth_MultipliesWithEatStack()
        {
            var stack = new FakeMultipliers();
            var stats = new PlayerStats(Base, new NoCaps(), stack, null, new FakeGrowth().With(StatType.BaseDamage, 0.1f));
            stack.Set(StatType.BaseDamage, 2f);
            stats.SetLevel(6);   // ×1.5
            Assert.That(stats.Get(StatType.BaseDamage), Is.EqualTo(30f).Within(1e-4f));   // 10 × 2 × 1.5
        }

        [Test] public void SizeGrowthFromLevel_AlsoScalesHP()
        {
            var stats = new PlayerStats(Base, new NoCaps(), null,
                new FakeScaling().With(StatType.MaxHP, 1f),
                new FakeGrowth().With(StatType.Size, 0.1f));
            stats.SetLevel(6);   // Size ×1.5
            Assert.That(stats.SizeRatio, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(stats.Get(StatType.MaxHP), Is.EqualTo(15f).Within(1e-4f));
        }

        [Test] public void InverseStat_GetsShorterWithLevel()
        {
            var stats = new PlayerStats(Base, new NoCaps(), null, null, new FakeGrowth().With(StatType.MouthTickInterval, 0.25f));
            stats.SetLevel(5);   // ×2
            Assert.That(stats.Get(StatType.MouthTickInterval), Is.EqualTo(5f));
        }

        [Test] public void LevelUp_RaisesChanged()
        {
            var stats = new PlayerStats(Base, new NoCaps(), null, null, new FakeGrowth().With(StatType.MaxHP, 0.05f));
            var raised = 0;
            stats.Changed += () => raised++;
            stats.SetLevel(2);
            Assert.That(raised, Is.EqualTo(1));
        }
    }
}

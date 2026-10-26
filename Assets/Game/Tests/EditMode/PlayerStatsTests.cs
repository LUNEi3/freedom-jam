using FreedomJam.Game.Player;
using NUnit.Framework;

namespace FreedomJam.Game.Tests
{
    public class PlayerStatsTests
    {
        /// <summary>Cap = 1 + 0.5 × level (L1 = 1.5, L2 = 2.0, L3 = 2.5).</summary>
        sealed class StepCaps : IStatCaps { public float GetCap(StatType stat, int level) => 1f + 0.5f * level; }
        sealed class NoCaps : IStatCaps { public float GetCap(StatType stat, int level) => float.PositiveInfinity; }

        static PlayerStats Create(IStatCaps caps = null) => new PlayerStats(_ => 10f, caps ?? new NoCaps());

        [Test] public void NoBonus_ReturnsBaseValue()
        {
            var stats = Create();
            Assert.That(stats.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(1f)); Assert.That(stats.Get(StatType.MoveSpeed), Is.EqualTo(10f));
        }
        [Test] public void Bonuses_StackAdditively()
        {
            var stats = Create(); stats.AddBonus(StatType.MoveSpeed, 0.1f); stats.AddBonus(StatType.MoveSpeed, 0.2f);
            Assert.That(stats.Get(StatType.MoveSpeed), Is.EqualTo(13f).Within(1e-4f));
            Assert.That(stats.Get(StatType.FireDamage), Is.EqualTo(10f));   // other stats untouched
        }
        [Test] public void Cap_ClampsMultiplier_OverflowAppliesAfterLevelUp()
        {
            var stats = Create(new StepCaps()); stats.AddBonus(StatType.BaseDamage, 1.5f);   // wants x2.5
            Assert.That(stats.GetMultiplier(StatType.BaseDamage), Is.EqualTo(1.5f));       // L1 cap
            stats.SetLevel(3);
            Assert.That(stats.GetMultiplier(StatType.BaseDamage), Is.EqualTo(2.5f));       // L3 cap is 2.5
            Assert.That(stats.GetBonus(StatType.BaseDamage), Is.EqualTo(1.5f));
        }
        [Test] public void MouthTickInterval_BonusMakesIntervalShorter()
        {
            var stats = Create(); stats.AddBonus(StatType.MouthTickInterval, 1f);   // x2 speed
            Assert.That(stats.Get(StatType.MouthTickInterval), Is.EqualTo(5f));
        }
        [Test] public void Changed_RaisedOnBonusAndLevel_NotOnNoOps()
        {
            var stats = Create(); var raised = 0; stats.Changed += () => raised++;
            stats.AddBonus(StatType.Size, 0.1f); stats.SetLevel(2);
            stats.AddBonus(StatType.Size, 0f); stats.AddBonus(StatType.Size, float.NaN); stats.SetLevel(2);
            Assert.That(raised, Is.EqualTo(2));
        }
        [Test] public void NegativeBonus_NeverDropsBelowMinMultiplier()
        {
            var stats = Create(); stats.AddBonus(StatType.MoveSpeed, -5f);
            Assert.That(stats.GetMultiplier(StatType.MoveSpeed), Is.EqualTo(PlayerStats.MinMultiplier));
        }
    }
}

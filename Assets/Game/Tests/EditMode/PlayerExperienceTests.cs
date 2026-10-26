using System.Collections.Generic;
using FreedomJam.Game.Player;
using NUnit.Framework;

namespace FreedomJam.Game.Tests
{
    public class PlayerExperienceTests
    {
        /// <summary>Level N needs N * 10 XP.</summary>
        sealed class LinearCurve : IXpCurve { public int XpToNextLevel(int level) => level * 10; }
        sealed class ZeroCurve : IXpCurve { public int XpToNextLevel(int level) => 0; }

        [Test] public void StartsAtLevelOneWithNoXp()
        {
            var xp = new PlayerExperience(new LinearCurve());
            Assert.That(xp.Level, Is.EqualTo(1)); Assert.That(xp.CurrentXp, Is.Zero); Assert.That(xp.XpToNext, Is.EqualTo(10));
        }
        [Test] public void AddXp_BelowThreshold_StaysAtLevel()
        {
            var xp = new PlayerExperience(new LinearCurve());
            Assert.That(xp.AddXp(7), Is.Zero);
            Assert.That(xp.Level, Is.EqualTo(1)); Assert.That(xp.CurrentXp, Is.EqualTo(7)); Assert.That(xp.Progress, Is.EqualTo(0.7f));
        }
        [Test] public void AddXp_LargeAmount_GainsSeveralLevelsAndKeepsRemainder()
        {
            var xp = new PlayerExperience(new LinearCurve()); var raised = new List<int>();
            xp.LeveledUp += raised.Add;
            Assert.That(xp.AddXp(35), Is.EqualTo(2));   // 10 (L1) + 20 (L2) = 30, 5 left
            Assert.That(xp.Level, Is.EqualTo(3)); Assert.That(xp.CurrentXp, Is.EqualTo(5));
            Assert.That(raised, Is.EqualTo(new[] { 2, 3 }));
        }
        [Test] public void AddXp_ZeroOrNegative_IsIgnored()
        {
            var xp = new PlayerExperience(new LinearCurve()); xp.AddXp(0); xp.AddXp(-5);
            Assert.That(xp.CurrentXp, Is.Zero); Assert.That(xp.Level, Is.EqualTo(1));
        }
        [Test] public void CurveReturningZero_TreatedAsOneXpPerLevel()
        {
            var xp = new PlayerExperience(new ZeroCurve());
            Assert.That(xp.AddXp(3), Is.EqualTo(3)); Assert.That(xp.Level, Is.EqualTo(4));
        }
    }
}

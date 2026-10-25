using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Foundation.Tests
{
    public class GameRunDataTests
    {
        GameConfig config;
        [SetUp] public void SetUp() { config = ScriptableObject.CreateInstance<GameConfig>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(config); }

        [Test] public void NewRun_UsesConfiguredDefaults()
        {
            config.initialScore = 12; config.timerMode = TimerMode.Countdown; config.countdownSeconds = 10;
            var run = new GameRunData(config);
            Assert.That(run.Score, Is.EqualTo(12)); Assert.That(run.RemainingSeconds, Is.EqualTo(10));
        }
        [Test] public void ElapsedTimer_AccumulatesPositiveDelta()
        {
            var run = new GameRunData(config);
            run.Advance(2); run.Advance(-5); run.Advance(float.NaN); run.Advance(3);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(5)); Assert.That(run.IsComplete, Is.False);
        }
        [Test] public void Countdown_AdvancePastZero_ClampsAndCompletesOnce()
        {
            config.timerMode = TimerMode.Countdown; config.countdownSeconds = 10;
            var run = new GameRunData(config);
            Assert.That(run.Advance(12), Is.True); Assert.That(run.RemainingSeconds, Is.Zero);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(10)); Assert.That(run.Advance(5), Is.False);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(10));
        }
        [Test] public void AddScore_AllowsNegativeDelta()
        {
            var run = new GameRunData(config); run.AddScore(20); run.AddScore(-7);
            Assert.That(run.Score, Is.EqualTo(13)); run.SetScore(-4); Assert.That(run.Score, Is.EqualTo(-4));
        }
        [Test] public void Complete_CannotCompleteTwice()
        {
            var run = new GameRunData(config); run.Advance(2); run.Complete(); run.Complete();
            run.Advance(5); run.AddScore(10); run.SetScore(99);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(2)); Assert.That(run.Score, Is.Zero); Assert.That(run.IsComplete, Is.True);
        }
    }
}

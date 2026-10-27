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
        [Test] public void Countdown_Expiry_IsTimeUp()
        {
            config.timerMode = TimerMode.Countdown; config.countdownSeconds = 10;
            var run = new GameRunData(config);
            run.Advance(9); Assert.That(run.Outcome, Is.EqualTo(RunOutcome.None));
            run.Advance(1); Assert.That(run.Outcome, Is.EqualTo(RunOutcome.TimeUp));
        }
        [Test] public void Countdown_Expiry_ReportsExactCountdownAsElapsed()
        {
            config.timerMode = TimerMode.Countdown; config.countdownSeconds = 120;
            var run = new GameRunData(config);
            for (var i = 0; i < 7200; i++) run.Advance(1f / 60f);   // float drift leaves it a hair short
            run.Advance(1f);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.TimeUp)); Assert.That(run.ElapsedSeconds, Is.EqualTo(120f));
        }
        [Test] public void Complete_KeepsFirstOutcome()
        {
            config.timerMode = TimerMode.Countdown; config.countdownSeconds = 10;
            var timeUp = new GameRunData(config); timeUp.Advance(10); timeUp.Complete(RunOutcome.Won);
            Assert.That(timeUp.Outcome, Is.EqualTo(RunOutcome.TimeUp));
            var won = new GameRunData(config); won.Complete(RunOutcome.Won); won.Advance(10); won.Complete(RunOutcome.Lost);
            Assert.That(won.Outcome, Is.EqualTo(RunOutcome.Won)); Assert.That(won.ElapsedSeconds, Is.Zero);
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

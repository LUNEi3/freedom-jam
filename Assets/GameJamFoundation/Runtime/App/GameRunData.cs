using System;

namespace FreedomJam.Foundation
{
    /// <summary>Won = the countdown ran out; Lost = the run was finished early (e.g. the player died).</summary>
    public enum RunOutcome { None, Won, Lost }

    /// <summary>One run's values; never mutates the configuration asset.</summary>
    public sealed class GameRunData
    {
        public int Score { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsComplete { get; private set; }
        public RunOutcome Outcome { get; private set; }
        public TimerMode TimerMode { get; }
        public float DisplaySeconds => TimerMode == TimerMode.Countdown ? RemainingSeconds : ElapsedSeconds;

        public GameRunData(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Score = config.initialScore;
            TimerMode = config.timerMode;
            RemainingSeconds = IsFinite(config.countdownSeconds) ? Math.Max(0, config.countdownSeconds) : 0;
        }

        public void AddScore(int delta)
        {
            if (!IsComplete) Score = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)Score + delta));
        }
        public void SetScore(int score) { if (!IsComplete) Score = score; }
        /// <summary>Only the first call counts, so a death after the countdown ran out keeps the win.</summary>
        public void Complete(RunOutcome outcome = RunOutcome.None)
        {
            if (IsComplete) return;
            IsComplete = true;
            Outcome = outcome;
        }

        public bool Advance(float deltaSeconds)
        {
            if (IsComplete || !IsFinite(deltaSeconds) || deltaSeconds < 0) return false;
            if (TimerMode == TimerMode.CountUp) { ElapsedSeconds += deltaSeconds; return false; }
            var consumed = Math.Min(RemainingSeconds, deltaSeconds);
            ElapsedSeconds += consumed;
            RemainingSeconds = Math.Max(0, RemainingSeconds - consumed);
            if (RemainingSeconds > 0) return false;
            Complete(RunOutcome.Won);
            return true;
        }
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

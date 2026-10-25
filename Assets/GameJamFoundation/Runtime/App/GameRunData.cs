using System;

namespace FreedomJam.Foundation
{
    /// <summary>One run's values; never mutates the configuration asset.</summary>
    public sealed class GameRunData
    {
        public int Score { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsComplete { get; private set; }
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
        public void Complete() { IsComplete = true; }

        public bool Advance(float deltaSeconds)
        {
            if (IsComplete || !IsFinite(deltaSeconds) || deltaSeconds < 0) return false;
            if (TimerMode == TimerMode.CountUp) { ElapsedSeconds += deltaSeconds; return false; }
            var consumed = Math.Min(RemainingSeconds, deltaSeconds);
            ElapsedSeconds += consumed;
            RemainingSeconds = Math.Max(0, RemainingSeconds - consumed);
            if (RemainingSeconds > 0) return false;
            Complete();
            return true;
        }
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

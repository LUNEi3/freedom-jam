using UnityEngine;

namespace FreedomJam.Foundation
{
    public enum TimerMode { CountUp, Countdown }
    public enum ScoreDirection { HigherIsBetter, LowerIsBetter }

    [CreateAssetMenu(menuName = "Freedom Jam/Game Configuration")]
    public sealed class GameConfig : ScriptableObject
    {
        public string mainMenuSceneName = "MainMenu";
        public string gameSceneName = "Game";
        public string resultSceneName = "Result";
        public TimerMode timerMode = TimerMode.CountUp;
        [Min(0)] public float countdownSeconds = 60;
        public int initialScore;
        public string bestScoreKey = "default";
        public int bestScoreDefault;
        public ScoreDirection scoreDirection = ScoreDirection.HigherIsBetter;
    }
}

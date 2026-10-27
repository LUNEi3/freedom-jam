using UnityEngine;

namespace FreedomJam.Foundation
{
    public enum TimerMode { CountUp, Countdown }
    public enum ScoreDirection { HigherIsBetter, LowerIsBetter }
    /// <summary>Scene = load the Result scene when a run ends; Popup = show the result over the Game scene.</summary>
    public enum ResultDisplay { Scene, Popup }

    [CreateAssetMenu(menuName = "Freedom Jam/Game Configuration")]
    public sealed class GameConfig : ScriptableObject
    {
        public string mainMenuSceneName = "MainMenu";
        public string gameSceneName = "Game";
        public string resultSceneName = "Result";
        [Tooltip("Scene: load the Result scene. Popup: freeze the Game scene and show its ResultCanvas over it.")]
        public ResultDisplay resultDisplay = ResultDisplay.Scene;
        [Tooltip("Popup only: seconds the game keeps running after the run ends before the popup freezes it. 0 = immediately.")]
        [Min(0)] public float resultPopupDelay = 1f;
        public TimerMode timerMode = TimerMode.CountUp;
        [Min(0)] public float countdownSeconds = 60;
        public int initialScore;
        public string bestScoreKey = "default";
        public int bestScoreDefault;
        public ScoreDirection scoreDirection = ScoreDirection.HigherIsBetter;
    }
}

using System;

namespace FreedomJam.Foundation
{
    public sealed class GameFlowController
    {
        readonly GameConfig config;
        readonly SettingsStore store;
        readonly Action<string> loadScene;
        readonly Action<float> setTimeScale;
        readonly Action quitApplication;
        readonly Func<float> getTimeScale;
        float timeScaleBeforePause = 1f;

        public GameState State { get; private set; } = GameState.Menu;
        public GameConfig Config => config;
        public GameRunData CurrentRun { get; private set; }
        public event Action<GameState> StateChanged;
        public event Action<GameRunData> RunUpdated;

        public GameFlowController(GameConfig config, SettingsStore store, Action<string> loadScene,
            Action<float> setTimeScale, Action quitApplication)
            : this(config, store, loadScene, setTimeScale, quitApplication, () => 1f)
        {
        }

        public GameFlowController(GameConfig config, SettingsStore store, Action<string> loadScene,
            Action<float> setTimeScale, Action quitApplication, Func<float> getTimeScale)
        {
            this.config = config ? config : throw new ArgumentNullException(nameof(config));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.loadScene = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
            this.setTimeScale = setTimeScale ?? throw new ArgumentNullException(nameof(setTimeScale));
            this.quitApplication = quitApplication ?? throw new ArgumentNullException(nameof(quitApplication));
            this.getTimeScale = getTimeScale ?? throw new ArgumentNullException(nameof(getTimeScale));
        }

        public void StartRun()
        {
            if (State != GameState.Menu && State != GameState.Result) return;

            RestoreNormalTimeScale();
            CurrentRun = new GameRunData(config);
            SetState(GameState.Playing);
            RunUpdated?.Invoke(CurrentRun);
            loadScene(config.gameSceneName);
        }

        public void Pause()
        {
            if (State != GameState.Playing || CurrentRun == null) return;

            timeScaleBeforePause = SanitizeTimeScale(getTimeScale());
            setTimeScale(0f);
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;

            setTimeScale(timeScaleBeforePause);
            SetState(GameState.Playing);
        }

        public void ReturnToMainMenu()
        {
            if (State == GameState.Menu && CurrentRun == null) return;

            RestoreNormalTimeScale();
            CurrentRun = null;
            SetState(GameState.Menu);
            loadScene(config.mainMenuSceneName);
        }

        /// <summary>
        /// Ends the run and loads the Result scene. Lost by default (e.g. the player died); pass Won when the game's
        /// goal was reached. If the countdown just ran out, the run is already TimeUp and keeps that outcome.
        /// </summary>
        public void FinishRun(RunOutcome outcome = RunOutcome.Lost)
        {
            if (CurrentRun == null || (State != GameState.Playing && State != GameState.Paused)) return;

            CurrentRun.Complete(outcome);
            RestoreNormalTimeScale();
            store.TryRecordBestScore(config.bestScoreKey, CurrentRun.Score, config.bestScoreDefault, config.scoreDirection);
            SetState(GameState.Result);
            RunUpdated?.Invoke(CurrentRun);
            loadScene(config.resultSceneName);
        }

        public void AddScore(int delta)
        {
            if (State != GameState.Playing || CurrentRun == null) return;
            CurrentRun.AddScore(delta);
            RunUpdated?.Invoke(CurrentRun);
        }

        public void SetScore(int score)
        {
            if (State != GameState.Playing || CurrentRun == null) return;
            CurrentRun.SetScore(score);
            RunUpdated?.Invoke(CurrentRun);
        }

        public void Tick(float deltaSeconds)
        {
            if (State != GameState.Playing || CurrentRun == null) return;
            if (CurrentRun.Advance(deltaSeconds))
            {
                FinishRun();
                return;
            }
            RunUpdated?.Invoke(CurrentRun);
        }

        public void QuitApplication() => quitApplication();

        void RestoreNormalTimeScale()
        {
            timeScaleBeforePause = 1f;
            setTimeScale(1f);
        }

        void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(State);
        }

        static float SanitizeTimeScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return 1f;
            return value;
        }
    }
}

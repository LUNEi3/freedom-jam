using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreedomJam.Foundation
{
    public sealed class ResultMenuController : MonoBehaviour
    {
        public const string WonTitle = "YOU ESCAPED!";
        public const string LostTitle = "GAME OVER";
        public const string TimeUpTitle = "TIME'S UP";

        public Text titleLabel;
        public Text scoreLabel;
        public Text bestScoreLabel;
        public Text timeLabel;
        public Button playAgainButton;
        public Button mainMenuButton;
        public Button settingsButton;
        public Button quitButton;
        public SettingsMenuController settingsMenu;
        public Selectable defaultSelection;

        GameFlowController flow;
        SettingsStore store;
        Action openSettings;
        bool initialized;
        bool listenersBound;
        CanvasGroup popupGroup;

        /// <summary>Popup mode (GameConfig.resultDisplay): this lives in the Game scene and stays hidden until the run's result is shown.</summary>
        bool IsPopup => flow != null && flow.Config != null && flow.Config.resultDisplay == ResultDisplay.Popup;

        public void Initialize(GameFlowController flow, SettingsStore store, Action openSettings)
        {
            if (this.flow != null)
            {
                this.flow.RunUpdated -= OnRunUpdated;
                this.flow.StateChanged -= OnStateChanged;
                this.flow.ResultShown -= OnResultShown;
            }
            this.flow = flow;
            this.store = store;
            this.openSettings = openSettings;
            initialized = this.flow != null && this.store != null;
            if (initialized)
            {
                this.flow.RunUpdated += OnRunUpdated;
                this.flow.StateChanged += OnStateChanged;
                this.flow.ResultShown += OnResultShown;
            }
            BindListeners();
            Render();
            UpdatePopupVisibility();
            if (!IsPopup || IsPopupVisible) SelectDefault();
        }

        void Awake()
        {
            // Hide before the first frame when this is the Game scene's result popup.
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Flow != null && bootstrap.Flow.Config != null &&
                bootstrap.Flow.Config.resultDisplay == ResultDisplay.Popup)
                SetPopupVisible(false);
        }

        void Start()
        {
            if (flow == null && GameFoundationBootstrap.Instance != null)
            {
                var menu = settingsMenu;
                var bootstrap = GameFoundationBootstrap.Instance;
                Initialize(bootstrap.Flow, bootstrap.SettingsStore, () => menu?.Open(null));
            }
        }

        public void OnPlayAgainClicked() => flow?.StartRun();

        public void OnMainMenuClicked() => flow?.ReturnToMainMenu();

        public void OnSettingsClicked()
        {
            if (settingsMenu != null) settingsMenu.Open(SelectDefault);
            else openSettings?.Invoke();
        }

        public void OnQuitClicked() => flow?.QuitApplication();

        void OnRunUpdated(GameRunData _) => Render();

        void OnStateChanged(GameState _)
        {
            Render();
            UpdatePopupVisibility();
        }

        void OnResultShown()
        {
            Render();
            UpdatePopupVisibility();
            SelectDefault();
        }

        bool IsPopupVisible => flow != null && flow.State == GameState.Result && flow.IsResultShown;

        void UpdatePopupVisibility()
        {
            if (IsPopup) SetPopupVisible(IsPopupVisible);
        }

        void SetPopupVisible(bool visible)
        {
            if (popupGroup == null)
            {
                // Not ??: in the Editor a missing component comes back as a "fake null" object.
                popupGroup = GetComponent<CanvasGroup>();
                if (popupGroup == null) popupGroup = gameObject.AddComponent<CanvasGroup>();
            }
            popupGroup.alpha = visible ? 1f : 0f;
            popupGroup.interactable = visible;
            popupGroup.blocksRaycasts = visible;
        }

        void Render()
        {
            if (flow == null || flow.CurrentRun == null) return;
            var run = flow.CurrentRun;
            if (titleLabel != null && run.Outcome != RunOutcome.None)
                titleLabel.text = run.Outcome == RunOutcome.Won ? WonTitle : run.Outcome == RunOutcome.TimeUp ? TimeUpTitle : LostTitle;
            if (scoreLabel != null) scoreLabel.text = run.Score.ToString();
            if (timeLabel != null) timeLabel.text = FormatTime(run.ElapsedSeconds);
            if (bestScoreLabel != null && store != null)
                bestScoreLabel.text = store.LoadBestScore(flow.Config.bestScoreKey, flow.Config.bestScoreDefault, flow.Config.scoreDirection).ToString();
        }

        void OnEnable()
        {
            if (initialized && flow != null)
            {
                flow.RunUpdated += OnRunUpdated;
                flow.StateChanged += OnStateChanged;
                flow.ResultShown += OnResultShown;
            }
            BindListeners();
        }

        void OnDisable()
        {
            UnbindListeners();
            if (flow != null)
            {
                flow.RunUpdated -= OnRunUpdated;
                flow.StateChanged -= OnStateChanged;
                flow.ResultShown -= OnResultShown;
            }
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.RunUpdated -= OnRunUpdated;
            flow.StateChanged -= OnStateChanged;
            flow.ResultShown -= OnResultShown;
        }

        void BindListeners()
        {
            if (!initialized || listenersBound) return;
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
            listenersBound = false;
        }

        void SelectDefault()
        {
            if (defaultSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
        }

        static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        }
    }
}

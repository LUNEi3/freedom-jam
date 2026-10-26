using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreedomJam.Foundation
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        public Button resumeButton;
        public Button settingsButton;
        public Button quitToMenuButton;
        public SettingsMenuController settingsMenu;
        public GameObject pausePanel;
        public Selectable defaultSelection;

        GameFlowController flow;
        Action openSettings;
        bool initialized;
        bool listenersBound;

        public void Initialize(GameFlowController flow, Action openSettings)
        {
            if (this.flow != null) this.flow.StateChanged -= OnStateChanged;
            this.flow = flow;
            this.openSettings = openSettings;
            initialized = flow != null;
            if (initialized) flow.StateChanged += OnStateChanged;
            BindListeners();
            OnStateChanged(flow != null ? flow.State : GameState.Menu);
        }

        void Start()
        {
            if (flow == null && GameFoundationBootstrap.Instance != null)
            {
                var menu = settingsMenu;
                Initialize(GameFoundationBootstrap.Instance.Flow, () => menu?.Open(null));
            }
        }

        public void OnResumeClicked() => flow?.Resume();

        public void OnSettingsClicked()
        {
            if (openSettings != null) openSettings();
            else if (settingsMenu != null) settingsMenu.Open(null);
        }

        public void OnQuitToMenuClicked() => flow?.ReturnToMainMenu();

        void OnStateChanged(GameState state)
        {
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (state == GameState.Paused && defaultSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
        }

        void OnEnable()
        {
            if (initialized && flow != null) flow.StateChanged += OnStateChanged;
            BindListeners();
        }

        void OnDisable()
        {
            UnbindListeners();
            if (flow != null) flow.StateChanged -= OnStateChanged;
        }

        void OnDestroy()
        {
            if (flow != null) flow.StateChanged -= OnStateChanged;
        }

        void BindListeners()
        {
            if (!initialized || listenersBound) return;
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.AddListener(OnQuitToMenuClicked);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.RemoveListener(OnQuitToMenuClicked);
            listenersBound = false;
        }
    }
}

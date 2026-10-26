using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreedomJam.Foundation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public Button playButton;
        public Button settingsButton;
        public Button quitButton;
        public SettingsMenuController settingsMenu;
        public GameObject settingsPanel;
        public Selectable defaultSelection;

        GameFlowController flow;
        bool initialized;
        bool listenersBound;

        public void Initialize(GameFlowController flow)
        {
            this.flow = flow;
            initialized = flow != null;
            BindListeners();
            SelectDefault();
        }

        void Start()
        {
            if (flow == null && GameFoundationBootstrap.Instance != null)
                Initialize(GameFoundationBootstrap.Instance.Flow);
        }

        public void OnPlayClicked() => flow?.StartRun();

        public void OnSettingsClicked()
        {
            if (settingsMenu != null) settingsMenu.Open(null);
            else if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void OnQuitClicked() => flow?.QuitApplication();

        void OnEnable() => BindListeners();

        void OnDisable() => UnbindListeners();

        void BindListeners()
        {
            if (!initialized || listenersBound) return;
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
            listenersBound = false;
        }

        void SelectDefault()
        {
            if (defaultSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
        }
    }
}

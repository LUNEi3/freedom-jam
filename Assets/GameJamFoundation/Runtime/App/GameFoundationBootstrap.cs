using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FreedomJam.Foundation
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameFoundationBootstrap : MonoBehaviour
    {
        static GameFoundationBootstrap instance;

        [SerializeField] GameConfig config;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] AudioMixer audioMixer;
        [SerializeField] string settingsPrefix = "FreedomJam.";

        public static GameFoundationBootstrap Instance => instance;
        public GameConfig Config => config;
        public SettingsStore SettingsStore { get; private set; }
        public GameSettings CurrentSettings { get; private set; }
        public GameFlowController Flow { get; private set; }
        public GameInputController InputController { get; private set; }
        public InputBindingService InputBindings { get; private set; }
        public AudioService Audio { get; private set; }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (config == null || inputActions == null || audioMixer == null)
            {
                Debug.LogError("GameFoundationBootstrap requires a GameConfig, InputActionAsset, and AudioMixer.", this);
                enabled = false;
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            SettingsStore = new SettingsStore(settingsPrefix);
            CurrentSettings = SettingsStore.LoadSettings(new GameSettings());
            InputBindings = new InputBindingService(inputActions, SettingsStore);
            InputBindings.LoadOverrides();
            InputController = new GameInputController();
            InputController.Initialize(inputActions);
            InputController.PausePressed += OnPausePressed;

            Flow = new GameFlowController(config, SettingsStore,
                sceneName => SceneManager.LoadScene(sceneName),
                value => Time.timeScale = value,
                QuitBuiltApplication,
                () => Time.timeScale);
            Flow.StateChanged += InputController.SetGameplayState;
            Audio = new AudioService();
        }

        void Start()
        {
            if (Audio != null) Audio.Initialize(audioMixer, CurrentSettings);
        }

        void Update()
        {
            if (Flow != null) Flow.Tick(Time.deltaTime);
        }

        public void SaveSettings(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            CurrentSettings = settings.Copy();
            SettingsStore.SaveSettings(CurrentSettings);
        }

        void OnPausePressed()
        {
            if (Flow == null) return;
            if (Flow.State == GameState.Playing) Flow.Pause();
            else if (Flow.State == GameState.Paused) Flow.Resume();
        }

        static void QuitBuiltApplication()
        {
            if (Application.isEditor) return;
            Application.Quit();
        }

        void OnDestroy()
        {
            if (Flow != null && InputController != null) Flow.StateChanged -= InputController.SetGameplayState;
            if (InputController != null) InputController.PausePressed -= OnPausePressed;
            if (Audio != null) Audio.Dispose();
            if (instance == this) instance = null;
        }
    }
}

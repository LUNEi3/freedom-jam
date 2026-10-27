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
        [Header("Background Music")]
        [SerializeField] AudioClip backgroundMusic;
        [SerializeField, Range(0f, 1f)] float backgroundMusicVolume = 1f;

        public static GameFoundationBootstrap Instance => instance;
        public GameConfig Config => config;
        public SettingsStore SettingsStore { get; private set; }
        public GameSettings CurrentSettings { get; private set; }
        public GameFlowController Flow { get; private set; }
        public GameInputController InputController { get; private set; }
        public InputBindingService InputBindings { get; private set; }
        public AudioService Audio { get; private set; }
        bool audioInitialized;

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
                SetFlowTimeScale,
                QuitBuiltApplication,
                GetCurrentTimeScale);
            Flow.StateChanged += InputController.SetGameplayState;
            Flow.StateChanged += OnFlowStateChanged;
            Flow.ResultShown += EnsureResultPopup;
            Audio = new AudioService();
        }

        void Start()
        {
            if (Audio == null) return;
            Audio.Initialize(audioMixer, CurrentSettings);
            audioInitialized = true;
            if (Flow != null && Flow.State == GameState.Playing)
                PlayBackgroundMusic();
        }

        void OnFlowStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                PlayBackgroundMusic();
                return;
            }

            if (state == GameState.Menu ||
                state == GameState.Result && Flow.CurrentRun != null && Flow.CurrentRun.Outcome != RunOutcome.Won)
                Audio?.StopMusic();
        }

        void PlayBackgroundMusic()
        {
            if (!audioInitialized || Audio == null || backgroundMusic == null) return;
            Audio.PlayMusic(backgroundMusic, true, backgroundMusicVolume);
        }

        /// <summary>Popup mode: if the Game scene has no result popup placed in it, spawn one from GameConfig.</summary>
        void EnsureResultPopup()
        {
            if (config.resultDisplay != ResultDisplay.Popup || config.resultPopupPrefab == null) return;
            if (FindAnyObjectByType<ResultMenuController>() != null) return;
            Instantiate(config.resultPopupPrefab);
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

        static void SetFlowTimeScale(float value)
        {
            var scaleOverride = FindTimeScaleOverride();
            if (scaleOverride != null)
            {
                scaleOverride.SetFlowPaused(value <= 0f);
                return;
            }
            Time.timeScale = value;
        }

        static float GetCurrentTimeScale()
        {
            var scaleOverride = FindTimeScaleOverride();
            return scaleOverride != null ? scaleOverride.CurrentScale : Time.timeScale;
        }

        static IGameplayTimeScaleOverride FindTimeScaleOverride()
        {
            foreach (var component in FindObjectsByType<MonoBehaviour>())
                if (component is IGameplayTimeScaleOverride scaleOverride)
                    return scaleOverride;
            return null;
        }

        static void QuitBuiltApplication()
        {
            if (Application.isEditor) return;
            Application.Quit();
        }

        void OnDestroy()
        {
            if (Flow != null)
            {
                if (InputController != null) Flow.StateChanged -= InputController.SetGameplayState;
                Flow.StateChanged -= OnFlowStateChanged;
            }
            if (InputController != null) InputController.PausePressed -= OnPausePressed;
            if (Audio != null) Audio.Dispose();
            if (instance == this) instance = null;
        }
    }
}

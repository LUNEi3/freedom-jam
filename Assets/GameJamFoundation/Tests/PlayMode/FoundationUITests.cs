using System;
using System.Collections;
using System.Collections.Generic;
using FreedomJam.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace FreedomJam.Foundation.Tests
{
    public sealed class FoundationUITests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        GameConfig config;
        SettingsStore store;
        GameFlowController flow;
        InputActionAsset inputAsset;
        string preferencePrefix;
        float timeScale = 1f;

        [SetUp]
        public void SetUp()
        {
            objects.Clear();
            preferencePrefix = "FoundationUITests." + Guid.NewGuid() + ".";
            config = ScriptableObject.CreateInstance<GameConfig>();
            config.mainMenuSceneName = "MainMenu";
            config.gameSceneName = "Game";
            config.resultSceneName = "Result";
            config.initialScore = 2;
            config.bestScoreKey = "ui-test";
            config.bestScoreDefault = 0;
            config.countdownSeconds = 90;
            store = new SettingsStore(preferencePrefix);
            flow = new GameFlowController(config, store, _ => { }, value => timeScale = value, () => { }, () => timeScale);
            inputAsset = CreateInputAsset();
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects)
                if (item != null) UnityEngine.Object.Destroy(item);
            if (inputAsset != null) UnityEngine.Object.Destroy(inputAsset);
            if (config != null) UnityEngine.Object.Destroy(config);
            UnityEngine.PlayerPrefs.DeleteKey(preferencePrefix + "settings");
            UnityEngine.PlayerPrefs.DeleteKey(preferencePrefix + "best." + config.bestScoreKey);
            UnityEngine.PlayerPrefs.Save();
            Time.timeScale = 1f;
            yield return null;
        }

        [Test]
        public void MainMenuPlayButton_StartsRun()
        {
            var menu = NewObject("Main Menu").AddComponent<MainMenuController>();
            menu.playButton = NewButton("Play");
            menu.Initialize(flow);

            menu.playButton.onClick.Invoke();

            Assert.That(flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(flow.CurrentRun.Score, Is.EqualTo(config.initialScore));
        }

        [Test]
        public void PauseResumeButton_RestoresHUD()
        {
            var hud = NewObject("HUD").AddComponent<GameHudController>();
            hud.scoreLabel = NewText("Score");
            hud.timeLabel = NewText("Time");
            hud.Initialize(flow);

            var pause = NewObject("Pause Menu").AddComponent<PauseMenuController>();
            pause.pausePanel = NewObject("Pause Panel");
            pause.resumeButton = NewButton("Resume");
            pause.Initialize(flow, () => { });
            flow.StartRun();
            flow.AddScore(5);
            flow.Tick(65f);
            timeScale = 0.5f;
            flow.Pause();

            Assert.That(pause.pausePanel.activeSelf, Is.True);
            Assert.That(hud.scoreLabel.text, Is.EqualTo("7"));
            Assert.That(hud.timeLabel.text, Is.EqualTo("01:05"));

            pause.resumeButton.onClick.Invoke();

            Assert.That(flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(timeScale, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(pause.pausePanel.activeSelf, Is.False);
            Assert.That(hud.scoreLabel.text, Is.EqualTo("7"));
        }

        [Test]
        public void SettingsControls_SaveAudioAndBindings()
        {
            var input = new InputBindingService(inputAsset, store);
            inputAsset.FindAction("Player/Jump").ApplyBindingOverride(0, "<Keyboard>/enter");
            input.SaveOverrides();
            var audio = new AudioService();
            var settingsRoot = NewObject("Settings");
            var menu = settingsRoot.AddComponent<SettingsMenuController>();
            menu.masterVolumeSlider = NewSlider("Master Volume");
            menu.musicVolumeSlider = NewSlider("Music Volume");
            menu.sfxVolumeSlider = NewSlider("SFX Volume");
            menu.masterMuteToggle = NewToggle("Master Mute");
            menu.musicMuteToggle = NewToggle("Music Mute");
            menu.sfxMuteToggle = NewToggle("SFX Mute");
            menu.bindingsRoot = NewObject("Bindings", settingsRoot.transform).transform;
            menu.closeButton = NewButton("Close");
            menu.Initialize(store, audio, input, store.LoadSettings(new GameSettings()));

            menu.musicVolumeSlider.value = 0.35f;
            menu.masterMuteToggle.isOn = true;
            menu.SaveSettings();

            var saved = store.LoadSettings(new GameSettings());
            var restoredAsset = InputActionAsset.FromJson(inputAsset.ToJson());
            var restoredInput = new InputBindingService(restoredAsset, store);
            restoredInput.LoadOverrides();
            Assert.That(saved.MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(saved.MasterMuted, Is.True);
            Assert.That(restoredAsset.FindAction("Player/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(menu.bindingsRoot.childCount, Is.EqualTo(input.GetRebindableBindings().Count));

            menu.ResetBindings();
            var resetAsset = InputActionAsset.FromJson(inputAsset.ToJson());
            var resetInput = new InputBindingService(resetAsset, store);
            resetInput.LoadOverrides();
            Assert.That(resetAsset.FindAction("Player/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(store.LoadSettings(new GameSettings()).MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            UnityEngine.Object.Destroy(restoredAsset);
            UnityEngine.Object.Destroy(resetAsset);
        }

        [Test]
        public void ResultButtons_RestartOrReturnToMenu()
        {
            var result = NewObject("Result Menu").AddComponent<ResultMenuController>();
            result.scoreLabel = NewText("Final Score");
            result.bestScoreLabel = NewText("Best Score");
            result.timeLabel = NewText("Run Time");
            result.playAgainButton = NewButton("Play Again");
            result.mainMenuButton = NewButton("Main Menu");
            result.Initialize(flow, store, () => { });
            flow.StartRun();
            flow.SetScore(25);
            flow.FinishRun();

            Assert.That(result.scoreLabel.text, Is.EqualTo("25"));
            Assert.That(result.bestScoreLabel.text, Is.EqualTo("25"));

            result.playAgainButton.onClick.Invoke();
            Assert.That(flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(flow.CurrentRun.Score, Is.EqualTo(config.initialScore));

            flow.FinishRun();
            result.mainMenuButton.onClick.Invoke();
            Assert.That(flow.State, Is.EqualTo(GameState.Menu));
            Assert.That(flow.CurrentRun, Is.Null);
        }

        GameObject NewObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null) gameObject.transform.SetParent(parent, false);
            objects.Add(gameObject);
            return gameObject;
        }

        Button NewButton(string name)
        {
            var gameObject = NewObject(name);
            var image = gameObject.AddComponent<Image>();
            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        Text NewText(string name)
        {
            var text = NewObject(name).AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        Slider NewSlider(string name)
        {
            var slider = NewObject(name).AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        Toggle NewToggle(string name)
        {
            var toggle = NewObject(name).AddComponent<Toggle>();
            return toggle;
        }

        static InputActionAsset CreateInputAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = asset.AddActionMap("Player");
            player.AddAction("Jump", InputActionType.Button).AddBinding("<Keyboard>/space").WithGroup("Keyboard&Mouse");
            var system = asset.AddActionMap("System");
            system.AddAction("Pause", InputActionType.Button).AddBinding("<Keyboard>/escape").WithGroup("Keyboard&Mouse");
            var ui = asset.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter").WithGroup("Keyboard&Mouse");
            return asset;
        }
    }
}

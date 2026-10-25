using System;
using System.Collections.Generic;
using FreedomJam.Foundation;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Foundation.Tests
{
    public sealed class GameFlowTests
    {
        readonly List<string> loadedScenes = new List<string>();
        GameConfig config;
        SettingsStore store;
        GameFlowController flow;
        string preferencePrefix;
        float timeScale;
        int quitCalls;

        [SetUp]
        public void SetUp()
        {
            loadedScenes.Clear();
            preferencePrefix = "FoundationFlowTests." + Guid.NewGuid() + ".";
            config = ScriptableObject.CreateInstance<GameConfig>();
            config.mainMenuSceneName = "MainMenu";
            config.gameSceneName = "Game";
            config.resultSceneName = "Result";
            config.initialScore = 3;
            config.countdownSeconds = 1f;
            config.bestScoreKey = "test-run";
            config.bestScoreDefault = 0;
            config.scoreDirection = ScoreDirection.HigherIsBetter;
            store = new SettingsStore(preferencePrefix);
            timeScale = 1f;
            quitCalls = 0;
            flow = new GameFlowController(config, store, loadedScenes.Add, value => timeScale = value, () => quitCalls++, () => timeScale);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.PlayerPrefs.DeleteKey(preferencePrefix + "settings");
            UnityEngine.PlayerPrefs.DeleteKey(preferencePrefix + "best." + config.bestScoreKey);
            UnityEngine.PlayerPrefs.Save();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void StartRun_ResetsDataAndLoadsGame()
        {
            flow.StartRun();
            flow.AddScore(10);
            flow.FinishRun();
            flow.StartRun();

            Assert.That(flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(flow.CurrentRun.Score, Is.EqualTo(config.initialScore));
            Assert.That(flow.CurrentRun.ElapsedSeconds, Is.EqualTo(0f));
            Assert.That(loadedScenes, Is.EqualTo(new[] { "Game", "Result", "Game" }));
            Assert.That(timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void PauseResume_ChangesStateAndTimeScale()
        {
            flow.StartRun();
            timeScale = 0.35f;

            flow.Pause();
            Assert.That(flow.State, Is.EqualTo(GameState.Paused));
            Assert.That(timeScale, Is.EqualTo(0f));

            flow.Resume();
            Assert.That(flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(timeScale, Is.EqualTo(0.35f).Within(0.001f));
        }

        [Test]
        public void CountdownExpiry_LoadsResultOnce()
        {
            config.timerMode = TimerMode.Countdown;
            flow.StartRun();

            flow.Tick(0.6f);
            flow.Tick(0.6f);
            flow.Tick(1f);

            Assert.That(flow.State, Is.EqualTo(GameState.Result));
            Assert.That(flow.CurrentRun.RemainingSeconds, Is.EqualTo(0f));
            Assert.That(loadedScenes.FindAll(scene => scene == "Result").Count, Is.EqualTo(1));
        }

        [Test]
        public void FinishRun_RecordsConfiguredBestScore()
        {
            flow.StartRun();
            flow.SetScore(42);

            flow.FinishRun();

            Assert.That(flow.State, Is.EqualTo(GameState.Result));
            Assert.That(store.LoadBestScore(config.bestScoreKey, config.bestScoreDefault, config.scoreDirection), Is.EqualTo(42));
            Assert.That(loadedScenes.FindAll(scene => scene == "Result").Count, Is.EqualTo(1));
        }

        [Test]
        public void ReturnToMenu_RestoresTimeScale()
        {
            flow.StartRun();
            timeScale = 0.5f;
            flow.Pause();

            flow.ReturnToMainMenu();

            Assert.That(flow.State, Is.EqualTo(GameState.Menu));
            Assert.That(flow.CurrentRun, Is.Null);
            Assert.That(timeScale, Is.EqualTo(1f));
            Assert.That(loadedScenes[loadedScenes.Count - 1], Is.EqualTo("MainMenu"));
        }
    }
}

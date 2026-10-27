using System.Collections;
using FreedomJam.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreedomJam.Foundation.Tests
{
    public sealed class FoundationSceneFlowTests
    {
        Keyboard virtualKeyboard;
        Gamepad virtualGamepad;
        Mouse virtualMouse;

        [UnityTearDown]
        public IEnumerator RemoveVirtualDevices()
        {
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Flow != null && bootstrap.Flow.State != GameState.Menu)
                bootstrap.Flow.ReturnToMainMenu();
            if (virtualKeyboard != null) InputSystem.RemoveDevice(virtualKeyboard);
            if (virtualGamepad != null) InputSystem.RemoveDevice(virtualGamepad);
            if (virtualMouse != null) InputSystem.RemoveDevice(virtualMouse);
            virtualKeyboard = null;
            virtualGamepad = null;
            virtualMouse = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuFocus_NavigatesWithKeyboardAndGamepad()
        {
            yield return LoadScene("MainMenu");
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var eventSystem = EventSystem.current;
            Assert.That(menu, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(menu.defaultSelection.gameObject));

            var module = eventSystem.currentInputModule as InputSystemUIInputModule;
            Assert.That(module, Is.Not.Null);
            Assert.That(module.move.action.name, Is.EqualTo("Navigate"));
            Assert.That(module.submit.action.name, Is.EqualTo("Submit"));
            Assert.That(module.point.action.name, Is.EqualTo("Point"));
            Assert.That(module.leftClick.action.name, Is.EqualTo("Click"));

            virtualKeyboard = InputSystem.AddDevice<Keyboard>();
            var keyboardState = new KeyboardState();
            keyboardState.Press(Key.DownArrow);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(menu.settingsButton.gameObject));

            keyboardState.Release(Key.DownArrow);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            virtualGamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueDeltaStateEvent(virtualGamepad.leftStick, Vector2.down);
            yield return null;
            yield return null;
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(menu.quitButton.gameObject));
        }

        [UnityTest]
        public IEnumerator MainMenuMouse_ClickStartsRun()
        {
            yield return LoadScene("MainMenu");
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            virtualMouse = InputSystem.AddDevice<Mouse>();
            var buttonRect = menu.playButton.transform as RectTransform;
            var state = new MouseState
            {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    buttonRect.TransformPoint(buttonRect.rect.center))
            };
            InputSystem.QueueStateEvent(virtualMouse, state);
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(virtualMouse, state.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(virtualMouse, state.WithButton(MouseButton.Left, false));
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Game"));
            Assert.That(GameFoundationBootstrap.Instance.Flow.State, Is.EqualTo(GameState.Playing));
        }

        [UnityTest]
        public IEnumerator GamePauseInput_EscapeTogglesAndQuitReturnsToMenu()
        {
            yield return LoadScene("MainMenu");
            var bootstrap = GameFoundationBootstrap.Instance;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            menu.playButton.onClick.Invoke();
            yield return WaitForScene("Game");

            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            Assert.That(pause, Is.Not.Null);
            Assert.That(pause.pausePanel.activeSelf, Is.False);
            virtualKeyboard = InputSystem.AddDevice<Keyboard>();
            var keyboardState = new KeyboardState();
            keyboardState.Press(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;

            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Paused));
            Assert.That(pause.pausePanel.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(pause.defaultSelection.gameObject));

            keyboardState.Release(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            keyboardState.Press(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;

            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(pause.pausePanel.activeSelf, Is.False);
            pause.quitToMenuButton.onClick.Invoke();
            yield return WaitForScene("MainMenu");
            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Menu));
            Assert.That(bootstrap.Flow.CurrentRun, Is.Null);
        }

        [UnityTest]
        public IEnumerator PauseSettings_EscapeKeepsRunPausedAndCloseRestoresFocus()
        {
            yield return LoadScene("MainMenu");
            var bootstrap = GameFoundationBootstrap.Instance;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.playButton.onClick.Invoke();
            yield return WaitForScene("Game");

            var pause = Object.FindAnyObjectByType<PauseMenuController>();
            virtualKeyboard = InputSystem.AddDevice<Keyboard>();
            var keyboardState = new KeyboardState();
            keyboardState.Press(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;
            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Paused));

            pause.OnSettingsClicked();
            var settings = pause.settingsMenu;
            Assert.That(settings.gameObject.activeSelf, Is.True);
            keyboardState.Release(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            keyboardState.Press(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;

            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Paused));
            Assert.That(settings.gameObject.activeSelf, Is.True);

            settings.Close();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(pause.defaultSelection.gameObject));

            keyboardState.Release(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            keyboardState.Press(Key.Escape);
            InputSystem.QueueStateEvent(virtualKeyboard, keyboardState);
            yield return null;
            yield return null;
            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Playing));
        }

        [UnityTest]
        public IEnumerator MainMenuSettings_CloseRestoresKeyboardFocus()
        {
            yield return LoadScene("MainMenu");
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.OnSettingsClicked();
            Assert.That(menu.settingsMenu.gameObject.activeSelf, Is.True);

            menu.settingsMenu.Close();
            yield return null;

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(menu.defaultSelection.gameObject));
        }

        [UnityTest]
        public IEnumerator ResultSettings_CloseRestoresKeyboardFocus()
        {
            yield return LoadScene("MainMenu");
            var bootstrap = GameFoundationBootstrap.Instance;
            Object.FindAnyObjectByType<MainMenuController>().playButton.onClick.Invoke();
            yield return WaitForScene("Game");
            bootstrap.Flow.FinishRun();
            yield return WaitForResult(bootstrap);
            var result = Object.FindAnyObjectByType<ResultMenuController>();
            result.OnSettingsClicked();
            Assert.That(result.settingsMenu.gameObject.activeSelf, Is.True);

            result.settingsMenu.Close();
            yield return null;

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(result.defaultSelection.gameObject));
        }

        [UnityTest]
        public IEnumerator FoundationScenes_HaveOneAudioListener()
        {
            foreach (var sceneName in new[] { "MainMenu", "Game", "Result" })
            {
                yield return LoadScene(sceneName);
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(1), sceneName + " should have exactly one audio listener.");
            }
        }

        [UnityTest]
        public IEnumerator ScoreFinishRestart_FlowsThroughGameAndResult()
        {
            yield return LoadScene("MainMenu");

            var bootstrap = GameFoundationBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null);
            // The best score persists on this machine, so expect max(saved best, 42) rather than exactly 42.
            var config = bootstrap.Flow.Config;
            var bestBefore = bootstrap.SettingsStore.LoadBestScore(config.bestScoreKey, config.bestScoreDefault, config.scoreDirection);
            var expectedBest = config.scoreDirection == ScoreDirection.HigherIsBetter ? Mathf.Max(bestBefore, 42) : Mathf.Min(bestBefore, 42);
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            menu.playButton.onClick.Invoke();
            yield return WaitForScene("Game");

            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(bootstrap.Flow.CurrentRun.Score, Is.EqualTo(0));
            bootstrap.Flow.AddScore(42);
            bootstrap.Flow.Tick(65f);
            bootstrap.Flow.FinishRun();
            yield return WaitForResult(bootstrap);

            var result = Object.FindAnyObjectByType<ResultMenuController>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.scoreLabel.text, Is.EqualTo("42"));
            Assert.That(result.bestScoreLabel.text, Is.EqualTo(expectedBest.ToString()));
            Assert.That(result.timeLabel.text, Is.EqualTo("01:05"));
            if (config.resultDisplay == ResultDisplay.Popup)
            {
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Game"));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
            }

            result.playAgainButton.onClick.Invoke();
            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(bootstrap.Flow.CurrentRun.Score, Is.EqualTo(0));
            Assert.That(bootstrap.Flow.CurrentRun.ElapsedSeconds, Is.EqualTo(0f));
            yield return WaitForScene("Game");
            Assert.That(bootstrap.Flow.State, Is.EqualTo(GameState.Playing));
            Assert.That(bootstrap.Flow.CurrentRun.Score, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SceneLoad_KeepsOneBootstrap()
        {
            yield return LoadScene("MainMenu");
            var bootstrap = GameFoundationBootstrap.Instance;
            Assert.That(bootstrap, Is.Not.Null);

            yield return LoadScene("Game");
            AssertOneBootstrap(bootstrap);
            yield return LoadScene("Result");
            AssertOneBootstrap(bootstrap);
            yield return LoadScene("MainMenu");
            AssertOneBootstrap(bootstrap);
        }

        static IEnumerator LoadScene(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName);
            Assert.That(operation, Is.Not.Null, "Scene must be available in Build Settings: " + sceneName);
            if (operation != null)
                while (!operation.isDone) yield return null;
            yield return null;
        }

        /// <summary>Result scene (Scene mode) or the in-game result popup (Popup mode, after its delay).</summary>
        static IEnumerator WaitForResult(GameFoundationBootstrap bootstrap)
        {
            if (bootstrap.Flow.Config.resultDisplay != ResultDisplay.Popup)
            {
                yield return WaitForScene("Result");
                yield break;
            }
            var giveUpAt = Time.realtimeSinceStartup + bootstrap.Flow.Config.resultPopupDelay + 5f;
            while (!bootstrap.Flow.IsResultShown)
            {
                if (Time.realtimeSinceStartup > giveUpAt) Assert.Fail("Timed out waiting for the result popup.");
                yield return null;
            }
            yield return null;
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            for (var frame = 0; frame < 120; frame++)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Timed out waiting for scene " + sceneName);
        }

        static void AssertOneBootstrap(GameFoundationBootstrap expectedBootstrap)
        {
            var instances = Object.FindObjectsByType<GameFoundationBootstrap>(FindObjectsSortMode.None);
            Assert.That(instances.Length, Is.EqualTo(1));
            Assert.That(instances[0], Is.SameAs(expectedBootstrap));
            Assert.That(GameFoundationBootstrap.Instance, Is.SameAs(instances[0]));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FreedomJam.Foundation;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace FreedomJam.Foundation.Tests
{
    public sealed class InputBindingServiceTests
    {
        static string serializedActions;
        string preferencePrefix;
        List<InputActionAsset> assets;

        [SetUp]
        public void SetUp()
        {
            preferencePrefix = "FoundationInputTests." + Guid.NewGuid() + ".";
            assets = new List<InputActionAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.PlayerPrefs.DeleteKey(preferencePrefix + "settings");
            UnityEngine.PlayerPrefs.Save();
            foreach (var asset in assets)
                UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void RebindableBindings_IncludePlayerAndSystemOnly()
        {
            var asset = CreateActions();
            var service = new InputBindingService(asset, new SettingsStore(preferencePrefix));

            var bindings = service.GetRebindableBindings();

            Assert.That(bindings.Any(binding => binding.ActionMap == "Player" && binding.ActionName == "Move" && binding.DisplayName == "Up"), Is.True);
            Assert.That(bindings.Any(binding => binding.ActionMap == "Player" && binding.ActionName == "Attack" && binding.ControlScheme == "Gamepad"), Is.True);
            Assert.That(bindings.Any(binding => binding.ActionMap == "System" && binding.ActionName == "Pause"), Is.True);
            Assert.That(bindings.Any(binding => binding.ActionMap == "UI"), Is.False);
        }

        [Test]
        public void CompositeOverride_RoundTripsAndResetRestoresDefault()
        {
            var store = new SettingsStore(preferencePrefix);
            var asset = CreateActions();
            var service = new InputBindingService(asset, store);
            var move = asset.FindAction("Player/Move");
            move.ApplyBindingOverride(1, "<Keyboard>/upArrow");

            service.SaveOverrides();
            var restoredAsset = CreateActions();
            var restoredService = new InputBindingService(restoredAsset, store);
            restoredService.LoadOverrides();

            Assert.That(restoredAsset.FindAction("Player/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/upArrow"));
            restoredService.ResetOverrides();
            Assert.That(restoredAsset.FindAction("Player/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void SavedOverrides_LoadAfterServiceRecreation()
        {
            var store = new SettingsStore(preferencePrefix);
            store.SaveSettings(new GameSettings { MusicVolume = 0.25f });
            var firstAsset = CreateActions();
            var firstService = new InputBindingService(firstAsset, store);
            firstAsset.FindAction("Player/Attack").ApplyBindingOverride(0, "<Keyboard>/enter");
            firstAsset.FindAction("UI/Submit").ApplyBindingOverride(0, "<Keyboard>/space");
            firstService.SaveOverrides();

            var secondAsset = CreateActions();
            new InputBindingService(secondAsset, store).LoadOverrides();

            Assert.That(secondAsset.FindAction("Player/Attack").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(secondAsset.FindAction("UI/Submit").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/enter"));
            Assert.That(store.LoadSettings(new GameSettings()).MusicVolume, Is.EqualTo(0.25f));
        }

        [Test]
        public void GameplayState_EnablesExpectedMaps()
        {
            var asset = CreateActions();
            var controller = new GameInputController();
            controller.Initialize(asset);

            controller.SetGameplayState(GameState.Playing);
            Assert.That(asset.FindActionMap("Player").enabled, Is.True);
            Assert.That(asset.FindActionMap("System").enabled, Is.True);
            Assert.That(asset.FindActionMap("UI").enabled, Is.False);

            controller.SetGameplayState(GameState.Paused);
            Assert.That(asset.FindActionMap("Player").enabled, Is.False);
            Assert.That(asset.FindActionMap("System").enabled, Is.True);

            controller.SetGameplayState(GameState.Menu);
            Assert.That(asset.FindActionMap("System").enabled, Is.False);
        }

        InputActionAsset CreateActions()
        {
            if (serializedActions == null)
            {
                var template = BuildActions();
                serializedActions = template.ToJson();
                UnityEngine.Object.DestroyImmediate(template);
            }

            var asset = InputActionAsset.FromJson(serializedActions);
            assets.Add(asset);
            return asset;
        }

        static InputActionAsset BuildActions()
        {
            var asset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
            var player = asset.AddActionMap("Player");
            var move = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", "Keyboard&Mouse")
                .With("Down", "<Keyboard>/s", "Keyboard&Mouse")
                .With("Left", "<Keyboard>/a", "Keyboard&Mouse")
                .With("Right", "<Keyboard>/d", "Keyboard&Mouse");
            var attack = player.AddAction("Attack", InputActionType.Button);
            attack.AddBinding("<Keyboard>/space").WithGroup("Keyboard&Mouse");
            attack.AddBinding("<Gamepad>/buttonSouth").WithGroup("Gamepad");

            var system = asset.AddActionMap("System");
            var pause = system.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape").WithGroup("Keyboard&Mouse");
            pause.AddBinding("<Gamepad>/start").WithGroup("Gamepad");

            var ui = asset.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button).AddBinding("<Keyboard>/enter").WithGroup("Keyboard&Mouse");
            return asset;
        }
    }
}

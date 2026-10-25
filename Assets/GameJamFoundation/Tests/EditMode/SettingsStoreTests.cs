using System;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Foundation.Tests
{
    public class SettingsStoreTests
    {
        string prefix; SettingsStore store;
        [SetUp] public void SetUp() { prefix = "FreedomJam.Test." + Guid.NewGuid() + "."; store = new SettingsStore(prefix); }
        [TearDown] public void TearDown() { PlayerPrefs.DeleteKey(prefix + "settings"); PlayerPrefs.DeleteKey(prefix + "best.test"); }
        [Test] public void Settings_RoundTripPersistsAllValues()
        {
            store.SaveSettings(new GameSettings { MasterVolume=.3f, MusicVolume=.4f, SfxVolume=.7f, MasterMuted=true, MusicMuted=true, SfxMuted=true, BindingOverridesJson="bindings" });
            var loaded = new SettingsStore(prefix).LoadSettings(new GameSettings());
            Assert.That(loaded.MasterVolume, Is.EqualTo(.3f)); Assert.That(loaded.MusicVolume, Is.EqualTo(.4f)); Assert.That(loaded.SfxVolume, Is.EqualTo(.7f));
            Assert.That(loaded.MasterMuted && loaded.MusicMuted && loaded.SfxMuted, Is.True); Assert.That(loaded.BindingOverridesJson, Is.EqualTo("bindings"));
        }
        [Test] public void InvalidOrMissingData_ReturnsDefaults()
        {
            var defaults = new GameSettings { MusicVolume=.4f };
            Assert.That(store.LoadSettings(defaults).MusicVolume, Is.EqualTo(.4f));
            PlayerPrefs.SetString(prefix+"settings", "not json");
            Assert.That(store.LoadSettings(defaults).MusicVolume, Is.EqualTo(.4f));
            PlayerPrefs.SetString(prefix+"settings", "{\"MasterVolume\":9}");
            var result = store.LoadSettings(defaults);
            Assert.That(result.MasterVolume, Is.EqualTo(1)); Assert.That(result.MusicVolume, Is.EqualTo(.4f));
        }
        [TestCase(ScoreDirection.HigherIsBetter, 12, 15, 8, 15)]
        [TestCase(ScoreDirection.LowerIsBetter, 12, 8, 15, 8)]
        public void BestScore_UsesConfiguredDirectionAndDefault(ScoreDirection direction, int first, int better, int worse, int expected)
        {
            Assert.That(store.LoadBestScore("test", 0, direction), Is.Zero);
            Assert.That(store.TryRecordBestScore("test", first, 0, direction), Is.True);
            Assert.That(store.TryRecordBestScore("test", better, 0, direction), Is.True);
            Assert.That(store.TryRecordBestScore("test", worse, 0, direction), Is.False);
            Assert.That(store.LoadBestScore("test", 0, direction), Is.EqualTo(expected));
        }
    }
}

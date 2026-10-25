using System.Collections.Generic;
using FreedomJam.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace FreedomJam.Foundation.Tests
{
    public sealed class AudioServiceTests
    {
        readonly List<AudioService> services = new List<AudioService>();

        [TearDown]
        public void TearDown()
        {
            foreach (var service in services) service.Dispose();
            services.Clear();
        }

        [Test]
        public void VolumeConversion_ClampsAndMapsMuteFloor()
        {
            Assert.That(AudioService.NormalizedToDecibels(-0.5f), Is.EqualTo(AudioService.MuteFloorDecibels));
            Assert.That(AudioService.NormalizedToDecibels(0f), Is.EqualTo(AudioService.MuteFloorDecibels));
            Assert.That(AudioService.NormalizedToDecibels(1f), Is.EqualTo(0f));
            Assert.That(AudioService.NormalizedToDecibels(2f), Is.EqualTo(0f));
        }

        [Test]
        public void Mute_DoesNotOverwriteSelectedLevel()
        {
            var mixer = LoadMixer();
            var service = CreateService(mixer, new GameSettings { MusicVolume = 0.4f });

            service.SetMuted(AudioBus.Music, true);

            Assert.That(service.IsMuted(AudioBus.Music), Is.True);
            Assert.That(service.GetVolume(AudioBus.Music), Is.EqualTo(0.4f).Within(0.001f));

            service.SetMuted(AudioBus.Music, false);

            Assert.That(service.IsMuted(AudioBus.Music), Is.False);
            Assert.That(service.GetVolume(AudioBus.Music), Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void BusParameter_UsesConfiguredMixerName()
        {
            var mixer = LoadMixer();
            AssertParameterIsExposed(mixer, AudioBus.Master, "MasterVolume");
            AssertParameterIsExposed(mixer, AudioBus.Music, "MusicVolume");
            AssertParameterIsExposed(mixer, AudioBus.SFX, "SfxVolume");
        }

        AudioService CreateService(AudioMixer mixer, GameSettings settings)
        {
            var service = new AudioService();
            services.Add(service);
            service.Initialize(mixer, settings);
            return service;
        }

        static AudioMixer LoadMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/GameJamFoundation/Audio/GameJamAudioMixer.mixer");
            Assert.That(mixer, Is.Not.Null, "The foundation AudioMixer asset should be imported.");
            return mixer;
        }

        static void AssertParameterIsExposed(AudioMixer mixer, AudioBus bus, string expected)
        {
            var parameterName = AudioService.GetParameterName(bus);
            float value;
            Assert.That(parameterName, Is.EqualTo(expected));
            Assert.That(mixer.GetFloat(parameterName, out value), Is.True, parameterName + " should be exposed in the mixer.");
        }
    }
}

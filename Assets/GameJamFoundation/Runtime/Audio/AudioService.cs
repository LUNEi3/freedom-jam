using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FreedomJam.Foundation
{
    public enum AudioBus
    {
        Master,
        Music,
        SFX
    }

    public sealed class AudioService : IDisposable
    {
        public const float MuteFloorDecibels = -80f;

        readonly float[] levels = { 1f, 1f, 1f };
        readonly bool[] muted = { false, false, false };
        AudioMixer mixer;
        GameObject sourceRoot;
        AudioSource musicSource;
        AudioSource sfxSource;

        public void Initialize(AudioMixer mixer, GameSettings settings)
        {
            if (mixer == null) throw new ArgumentNullException(nameof(mixer));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this.mixer = mixer;
            var safeSettings = settings.Copy();
            safeSettings.Sanitize(new GameSettings());
            levels[(int)AudioBus.Master] = safeSettings.MasterVolume;
            levels[(int)AudioBus.Music] = safeSettings.MusicVolume;
            levels[(int)AudioBus.SFX] = safeSettings.SfxVolume;
            muted[(int)AudioBus.Master] = safeSettings.MasterMuted;
            muted[(int)AudioBus.Music] = safeSettings.MusicMuted;
            muted[(int)AudioBus.SFX] = safeSettings.SfxMuted;

            EnsureAudioSources(mixer);
            ApplyBus(AudioBus.Master);
            ApplyBus(AudioBus.Music);
            ApplyBus(AudioBus.SFX);
        }

        public void SetVolume(AudioBus bus, float normalized)
        {
            var index = GetBusIndex(bus);
            levels[index] = SanitizeLevel(normalized);
            ApplyBus(bus);
        }

        public void SetMuted(AudioBus bus, bool isMuted)
        {
            var index = GetBusIndex(bus);
            muted[index] = isMuted;
            ApplyBus(bus);
        }

        public float GetVolume(AudioBus bus) => levels[GetBusIndex(bus)];

        public bool IsMuted(AudioBus bus) => muted[GetBusIndex(bus)];

        public void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f)
        {
            EnsureInitialized();
            if (clip == null) return;

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = SanitizeLevel(volume);
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource == null) return;
            musicSource.Stop();
            musicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            EnsureInitialized();
            if (clip == null) return;
            var previousPitch = sfxSource.pitch;
            sfxSource.pitch = SanitizePitch(pitch);
            sfxSource.PlayOneShot(clip, SanitizeLevel(volume));
            sfxSource.pitch = previousPitch;
        }

        public static string GetParameterName(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Master: return "MasterVolume";
                case AudioBus.Music: return "MusicVolume";
                case AudioBus.SFX: return "SfxVolume";
                default: throw new ArgumentOutOfRangeException(nameof(bus), bus, "Unknown audio bus.");
            }
        }

        public static float NormalizedToDecibels(float normalized)
        {
            if (float.IsNaN(normalized) || float.IsNegativeInfinity(normalized)) normalized = 0f;
            else if (float.IsPositiveInfinity(normalized)) normalized = 1f;
            normalized = Mathf.Clamp01(normalized);
            if (normalized <= 0f) return MuteFloorDecibels;
            return Mathf.Max(MuteFloorDecibels, 20f * Mathf.Log10(normalized));
        }

        public void Dispose()
        {
            if (sourceRoot == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(sourceRoot);
            else UnityEngine.Object.DestroyImmediate(sourceRoot);
            sourceRoot = null;
            musicSource = null;
            sfxSource = null;
            mixer = null;
        }

        void EnsureAudioSources(AudioMixer audioMixer)
        {
            if (sourceRoot != null)
            {
                if (musicSource != null) musicSource.outputAudioMixerGroup = FindGroup(audioMixer, "Music");
                if (sfxSource != null) sfxSource.outputAudioMixerGroup = FindGroup(audioMixer, "SFX");
                return;
            }

            sourceRoot = new GameObject("GameJamFoundation Audio");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(sourceRoot);

            musicSource = sourceRoot.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.outputAudioMixerGroup = FindGroup(audioMixer, "Music");

            sfxSource = sourceRoot.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.outputAudioMixerGroup = FindGroup(audioMixer, "SFX");
        }

        AudioMixerGroup FindGroup(AudioMixer audioMixer, string groupName)
        {
            var groups = audioMixer.FindMatchingGroups("Master/" + groupName);
            if (groups == null || groups.Length == 0)
                throw new InvalidOperationException("The audio mixer must contain a Master/" + groupName + " group.");
            return groups[0];
        }

        void ApplyBus(AudioBus bus)
        {
            var index = GetBusIndex(bus);
            var decibels = muted[index] ? MuteFloorDecibels : NormalizedToDecibels(levels[index]);
            if (mixer == null || !Application.isPlaying) return;
            if (!mixer.SetFloat(GetParameterName(bus), decibels))
                throw new InvalidOperationException("The audio mixer does not expose the " + GetParameterName(bus) + " parameter.");
        }

        void EnsureInitialized()
        {
            if (mixer == null) throw new InvalidOperationException("Initialize the audio service before use.");
        }

        static int GetBusIndex(AudioBus bus)
        {
            var index = (int)bus;
            if (index < 0 || index >= 3) throw new ArgumentOutOfRangeException(nameof(bus), bus, "Unknown audio bus.");
            return index;
        }

        static float SanitizeLevel(float value)
        {
            if (float.IsNaN(value) || float.IsNegativeInfinity(value)) return 0f;
            if (float.IsPositiveInfinity(value)) return 1f;
            return Mathf.Clamp01(value);
        }

        static float SanitizePitch(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Mathf.Clamp(value, 0.1f, 3f);
        }
    }
}

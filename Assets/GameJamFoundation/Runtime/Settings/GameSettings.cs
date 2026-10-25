using System;
using UnityEngine;

namespace FreedomJam.Foundation
{
    [Serializable]
    public sealed class GameSettings
    {
        public float MasterVolume = 1, MusicVolume = 1, SfxVolume = 1;
        public bool MasterMuted, MusicMuted, SfxMuted;
        public string BindingOverridesJson = "";
        public GameSettings Copy() => (GameSettings)MemberwiseClone();
        public void Sanitize(GameSettings defaults)
        {
            MasterVolume = Normalize(MasterVolume, defaults.MasterVolume);
            MusicVolume = Normalize(MusicVolume, defaults.MusicVolume);
            SfxVolume = Normalize(SfxVolume, defaults.SfxVolume);
            BindingOverridesJson = BindingOverridesJson ?? "";
        }
        static float Normalize(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }
}

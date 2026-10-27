using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>What the ability bar shows for one ability. Level 0 = not unlocked yet.</summary>
    public readonly struct AbilityStatus
    {
        public readonly string Name;
        public readonly string Key;
        public readonly int Level;
        public readonly float CooldownRemaining;
        public readonly float Cooldown;

        public AbilityStatus(string name, string key, int level, float cooldownRemaining, float cooldown)
        {
            Name = name;
            Key = key;
            Level = level;
            CooldownRemaining = Mathf.Max(0f, cooldownRemaining);
            Cooldown = Mathf.Max(0f, cooldown);
        }

        public bool IsLocked => Level <= 0;
        public bool IsReady => !IsLocked && CooldownRemaining <= 0f;
        /// <summary>1 right after use, falling to 0 when ready again.</summary>
        public float CooldownFraction => Cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / Cooldown) : 0f;

        /// <summary>Seconds left as shown on the slot: one decimal under 10 s ("3.2"), whole seconds above ("12"), empty when ready.</summary>
        public static string FormatSeconds(float remaining)
        {
            if (!(remaining > 0f)) return string.Empty;
            return remaining < 10f ? remaining.ToString("0.0") : Mathf.CeilToInt(remaining).ToString();
        }
    }

    /// <summary>Anything with player abilities reports them here so the ability bar can show them. New abilities just append more.</summary>
    public interface IAbilityStatusSource
    {
        void AppendAbilityStatus(List<AbilityStatus> abilities);
    }
}

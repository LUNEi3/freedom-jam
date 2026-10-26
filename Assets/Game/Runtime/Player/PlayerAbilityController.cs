using System;
using System.Collections.Generic;
using FreedomJam.Game.Enemy;
using FreedomJam.Game.Pickups;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Game.Player
{
    /// <summary>Input, configuration, and upgrade state for the player's active abilities.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerAbilityController : MonoBehaviour, ILevelUpChoiceSource
    {
        [Serializable]
        public sealed class DashLevelSettings
        {
            [Min(0f)] public float distance = 3f;
            [Min(0.01f)] public float speed = 12f;
            [Min(0f)] public float cooldown = 10f;
        }

        [Serializable]
        public sealed class SuckLevelSettings
        {
            [Min(0f)] public float range = 7f;
            [Range(1f, 360f)] public float coneAngle = 100f;
            [Min(0f)] public float pullDuration = 0.6f;
            [Min(0f)] public float pullSpeed = 14f;
            [Min(0f)] public float cooldown = 10f;
        }

        [Header("Dash levels (entry 1 unlocks Dash)")]
        [SerializeField] List<DashLevelSettings> dashLevels = new List<DashLevelSettings>
        {
            new DashLevelSettings(),
            new DashLevelSettings { distance = 4f, speed = 12f, cooldown = 9f }
        };

        [Header("Suck levels (entry 1 unlocks Suck)")]
        [SerializeField] List<SuckLevelSettings> suckLevels = new List<SuckLevelSettings>
        {
            new SuckLevelSettings(),
            new SuckLevelSettings { range = 8.5f, coneAngle = 100f, pullDuration = 0.6f, pullSpeed = 14f, cooldown = 9f }
        };

        [Header("Size scaling (1 + (size ÷ starting size − 1) × strength)")]
        [Tooltip("How much Dash distance grows with player size. 0 = never, 1 = same rate as size.")]
        [SerializeField] float dashDistanceSizeStrength = 1f;
        [Tooltip("How much Suck range grows with player size. 0 = never, 1 = same rate as size.")]
        [SerializeField] float suckRangeSizeStrength = 1f;

        PlayerCharacter player;
        float dashReadyAt;
        float suckReadyAt;

        public int DashLevel { get; private set; }
        public int SuckLevel { get; private set; }
        /// <summary>Includes size scaling.</summary>
        public float DashDistance => CurrentDash != null ? Mathf.Max(0f, CurrentDash.distance) * SizeMultiplier(dashDistanceSizeStrength) : 0f;
        public float DashSpeed => CurrentDash != null ? Mathf.Max(0.01f, CurrentDash.speed) : 0f;
        public float DashCooldown => CurrentDash != null ? Mathf.Max(0f, CurrentDash.cooldown) : 0f;
        public float DashCooldownRemaining => Mathf.Max(0f, dashReadyAt - Time.time);
        /// <summary>Includes size scaling.</summary>
        public float SuckDistance => CurrentSuck != null ? Mathf.Max(0f, CurrentSuck.range) * SizeMultiplier(suckRangeSizeStrength) : 0f;
        public float SuckConeAngle => CurrentSuck != null ? Mathf.Clamp(CurrentSuck.coneAngle, 1f, 360f) : 100f;
        public float SuckCooldown => CurrentSuck != null ? Mathf.Max(0f, CurrentSuck.cooldown) : 0f;
        public float SuckCooldownRemaining => Mathf.Max(0f, suckReadyAt - Time.time);
        public bool CanUpgradeDash => GetLevel(dashLevels, DashLevel + 1) != null;
        public bool CanUpgradeSuck => GetLevel(suckLevels, SuckLevel + 1) != null;

        DashLevelSettings CurrentDash => GetLevel(dashLevels, DashLevel);
        SuckLevelSettings CurrentSuck => GetLevel(suckLevels, SuckLevel);

        void Awake() => player = GetComponent<PlayerCharacter>();

        void Update()
        {
            if (player == null || !player.IsAlive || Time.timeScale <= 0f || Keyboard.current == null) return;

            if (Keyboard.current.jKey.wasPressedThisFrame) ActivateDash();
            if (Keyboard.current.kKey.wasPressedThisFrame) ActivateSuck();
        }

        public bool TryUpgradeDash()
        {
            if (!CanUpgradeDash) return false;
            DashLevel++;
            return true;
        }

        public bool TryUpgradeSuck()
        {
            if (!CanUpgradeSuck) return false;
            SuckLevel++;
            return true;
        }

        public void AppendAvailableChoices(List<LevelUpChoice> choices)
        {
            if (choices == null) return;
            if (CanUpgradeDash)
            {
                var settings = dashLevels[DashLevel];
                choices.Add(new LevelUpChoice("dash", "Dash", DescribeDash(settings, DashLevel + 1)));
            }
            if (CanUpgradeSuck)
            {
                var settings = suckLevels[SuckLevel];
                choices.Add(new LevelUpChoice("suck", "Suck", DescribeSuck(settings, SuckLevel + 1)));
            }
        }

        public bool TryApplyChoice(string id)
        {
            switch (id)
            {
                case "dash": return TryUpgradeDash();
                case "suck": return TryUpgradeSuck();
                default: return false;
            }
        }

        void ActivateDash()
        {
            var settings = CurrentDash;
            if (settings == null || DashCooldownRemaining > 0f) return;
            if (!player.TryDashForward(DashDistance, settings.speed)) return;
            dashReadyAt = Time.time + Mathf.Max(0f, settings.cooldown);
        }

        void ActivateSuck()
        {
            var settings = CurrentSuck;
            if (settings == null || SuckCooldownRemaining > 0f) return;
            suckReadyAt = Time.time + Mathf.Max(0f, settings.cooldown);

            var origin = player.transform.position;
            var forward = player.transform.forward;
            var range = SuckDistance;
            var angle = Mathf.Clamp(settings.coneAngle, 1f, 360f);
            var duration = Mathf.Max(0f, settings.pullDuration);
            var speed = Mathf.Max(0f, settings.pullSpeed);

            var enemies = FindObjectsByType<EnemyCharacter>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (!ConeMath.IsInCone(origin, forward, enemy.transform.position, range,
                        angle, Mathf.Max(0f, enemy.BodySize * 0.5f))) continue;

                enemy.ApplySuction(player, speed, duration);
            }

            var orbs = FindObjectsByType<XpOrb>();
            foreach (var orb in orbs)
            {
                if (orb == null || orb.Xp <= 0) continue;
                if (!ConeMath.IsInCone(origin, forward, orb.transform.position, range, angle, 0.2f)) continue;

                orb.StartSuction(speed);
            }
        }

        float SizeMultiplier(float strength) => player != null ? SizeScaling.Multiplier(player.SizeRatio, strength) : 1f;

        static T GetLevel<T>(List<T> levels, int level) where T : class =>
            levels != null && level > 0 && level <= levels.Count ? levels[level - 1] : null;

        static string DescribeDash(DashLevelSettings settings, int level) =>
            $"Unlock/upgrade to level {level}\nDistance {settings.distance:0.##} m - Speed {settings.speed:0.##} m/s - Cooldown {settings.cooldown:0.#} s";

        static string DescribeSuck(SuckLevelSettings settings, int level) =>
            $"Unlock/upgrade to level {level}\nRange {settings.range:0.##} m - Cone {settings.coneAngle:0} deg - Pull {settings.pullSpeed:0.##} m/s for {settings.pullDuration:0.##} s - Cooldown {settings.cooldown:0.#} s";
    }
}

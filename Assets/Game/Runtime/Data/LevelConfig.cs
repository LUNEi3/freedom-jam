using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>XP per level and stat multiplier caps per level (MVP §5, §6). Values are placeholders until design sets them.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Level Config")]
    public sealed class LevelConfig : ScriptableObject, IXpCurve, IStatCaps, IStatGrowth
    {
        [Serializable]
        sealed class StatGrowth
        {
            // First string field = the element's name in the Inspector list. Filled in by OnValidate.
            [SerializeField, HideInInspector] string label;
            [SerializeField] StatType stat = StatType.MaxHP;
            [Tooltip("Added per level gained. 0.05 = +5 % per level (level 5 → ×1.20). For MouthTickInterval, bigger = bites more often.")]
            [SerializeField] float perLevel = 0.05f;

            public StatType Stat => stat;
            public float PerLevel => perLevel;
            public void SetLabel(string value) => label = value;
        }

        [Serializable]
        struct StatCap
        {
            [SerializeField] StatType stat;
            [Tooltip("X = level, Y = highest multiplier allowed (1 = no bonus).")]
            [SerializeField] AnimationCurve capByLevel;

            public StatType Stat => stat;
            public AnimationCurve CapByLevel => capByLevel;
        }

        [Tooltip("X = level, Y = XP needed to reach the next level.")]
        [SerializeField] AnimationCurve xpToNextLevel = AnimationCurve.Linear(1f, 5f, 20f, 100f);

        [Tooltip("Cap used for any stat not listed below. X = level, Y = highest multiplier.")]
        [SerializeField] AnimationCurve defaultCapByLevel = AnimationCurve.Linear(1f, 1.5f, 20f, 5f);

        [Tooltip("Optional per-stat caps that override the default.")]
        [SerializeField] List<StatCap> statCaps = new();

        [Header("Stat growth per level")]
        [Tooltip("Stats that grow automatically on every level-up. Multiplier = 1 + perLevel × (level − 1). Empty = no growth.")]
        [SerializeField] List<StatGrowth> statGrowth = new();

        public int XpToNextLevel(int level) =>
            Mathf.Max(1, Mathf.RoundToInt(Evaluate(xpToNextLevel, level, 1f)));

        public float GetCap(StatType stat, int level)
        {
            foreach (var cap in statCaps)
            {
                if (cap.Stat == stat && HasKeys(cap.CapByLevel)) return cap.CapByLevel.Evaluate(level);
            }
            return Evaluate(defaultCapByLevel, level, float.PositiveInfinity);
        }

        /// <summary>First matching entry wins.</summary>
        public float GetLevelMultiplier(StatType stat, int level)
        {
            foreach (var growth in statGrowth)
                if (growth != null && growth.Stat == stat) return GrowthMultiplier(growth.PerLevel, level);
            return 1f;
        }

        public static float GrowthMultiplier(float perLevel, int level)
        {
            if (float.IsNaN(perLevel) || float.IsInfinity(perLevel)) return 1f;
            return Mathf.Max(PlayerStats.MinMultiplier, 1f + perLevel * (Mathf.Max(1, level) - 1));
        }

        void OnValidate()
        {
            var seen = new HashSet<StatType>();
            foreach (var growth in statGrowth)
            {
                if (growth == null) continue;
                growth.SetLabel(!seen.Add(growth.Stat)
                    ? $"{growth.Stat}  [WARNING: listed twice, this entry is ignored]"
                    : $"{growth.Stat}  {growth.PerLevel * 100f:+0.##;-0.##}% per level (level 10 → ×{GrowthMultiplier(growth.PerLevel, 10):0.##})");
            }
        }

        static float Evaluate(AnimationCurve curve, int level, float fallback) =>
            HasKeys(curve) ? curve.Evaluate(level) : fallback;

        static bool HasKeys(AnimationCurve curve) => curve != null && curve.length > 0;
    }
}

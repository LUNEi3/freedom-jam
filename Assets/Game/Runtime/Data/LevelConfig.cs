using System;
using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game
{
    /// <summary>XP per level and stat multiplier caps per level (MVP §5, §6). Values are placeholders until design sets them.</summary>
    [CreateAssetMenu(menuName = "Freedom Jam/Level Config")]
    public sealed class LevelConfig : ScriptableObject, IXpCurve, IStatCaps
    {
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

        static float Evaluate(AnimationCurve curve, int level, float fallback) =>
            HasKeys(curve) ? curve.Evaluate(level) : fallback;

        static bool HasKeys(AnimationCurve curve) => curve != null && curve.length > 0;
    }
}

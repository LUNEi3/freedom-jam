using System;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game.Sandbox
{
    /// <summary>Test-only readout of player HP, XP, level and stat multipliers, with a few cheat buttons.</summary>
    public sealed class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] PlayerCharacter player;

        static readonly StatType[] Stats = (StatType[])Enum.GetValues(typeof(StatType));

        void OnGUI()
        {
            if (player == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 340, 460), GUI.skin.box);
            GUILayout.Label("<b>Player sandbox</b>   A / D to steer", Rich);
            GUILayout.Label($"HP {player.CurrentHP:0.#} / {player.MaxHP:0.#}   {(player.IsAlive ? "alive" : "DEAD")}");

            var xp = player.Experience;
            if (xp != null) GUILayout.Label($"Level {xp.Level}   XP {xp.CurrentXp} / {xp.XpToNext}");

            var stats = player.Stats;
            if (stats != null)
            {
                foreach (var stat in Stats)
                    GUILayout.Label($"{stat,-18} {stats.Get(stat),7:0.##}  ×{stats.GetMultiplier(stat):0.##}  (bonus {stats.GetBonus(stat):+0.##;-0.##;0})");
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 XP")) GameplayEvents.RaiseXpCollected(10);
            if (GUILayout.Button("Hurt 1")) player.TakeDamage(new DamageInfo(1f, DamageType.Normal, null));
            if (GUILayout.Button("Kill")) player.TakeDamage(new DamageInfo(9999f, DamageType.Normal, null));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        GUIStyle rich;
        GUIStyle Rich => rich ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}

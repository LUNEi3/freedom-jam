using System;
using FreedomJam.Game.Enemy;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game.PlayerSandbox
{
    /// <summary>
    /// Test-only readout of player HP, XP, level and stat multipliers, with a few cheat buttons.
    /// With a spawner assigned (integration sandbox) it also shows run time, enemies alive and eaten / killed counts.
    /// </summary>
    public sealed class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] PlayerCharacter player;
        [Tooltip("Optional. Set in the integration sandbox.")]
        [SerializeField] EnemySpawner spawner;

        static readonly StatType[] Stats = (StatType[])Enum.GetValues(typeof(StatType));

        int eaten;
        int killed;
        GUIStyle rich;
        GUIStyle Rich => rich ??= new GUIStyle(GUI.skin.label) { richText = true };

        void OnEnable()
        {
            GameplayEvents.CreatureEaten += OnEaten;
            GameplayEvents.CreatureKilled += OnKilled;
        }

        void OnDisable()
        {
            GameplayEvents.CreatureEaten -= OnEaten;
            GameplayEvents.CreatureKilled -= OnKilled;
        }

        void OnEaten(CreatureData _) => eaten++;
        void OnKilled(CreatureData _, Vector3 __) => killed++;

        void OnGUI()
        {
            if (player == null || Time.timeScale <= 0f) return;

            GUILayout.BeginArea(new Rect(10, 112, 360, 520), GUI.skin.box);
            GUILayout.Label("<b>Player sandbox</b>   A / D to steer", Rich);
            GUILayout.Label($"HP {player.CurrentHP:0.#} / {player.MaxHP:0.#}   {(player.IsAlive ? "alive" : "DEAD")}");

            var xp = player.Experience;
            if (xp != null) GUILayout.Label($"Level {xp.Level}   XP {xp.CurrentXp} / {xp.XpToNext}");
            if (spawner != null) GUILayout.Label($"Time {spawner.ElapsedSeconds:0.0}s   Enemies alive {spawner.AliveCount}");
            GUILayout.Label($"Eaten {eaten}   Killed {killed}");

            var stats = player.Stats;
            if (stats != null)
            {
                foreach (var stat in Stats)
                    GUILayout.Label($"{stat,-18} {stats.Get(stat),7:0.##}  ×{stats.GetMultiplier(stat):0.##}  (bonus {stats.GetBonus(stat):+0.##;-0.##;0})");
            }

            var abilities = player.GetComponent<PlayerAbilityController>();
            if (abilities != null)
            {
                var dashState = abilities.DashLevel == 0 ? "LOCKED" : $"{abilities.DashDistance:0.##}m / {abilities.DashSpeed:0.##}m/s, CD {abilities.DashCooldownRemaining:0.0}s";
                var suckState = abilities.SuckLevel == 0 ? "LOCKED" : $"{abilities.SuckDistance:0.##}m, CD {abilities.SuckCooldownRemaining:0.0}s";
                GUILayout.Label($"J Dash Lv {abilities.DashLevel}: {dashState}");
                GUILayout.Label($"K Suck Lv {abilities.SuckLevel}: {suckState}");
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 XP")) GameplayEvents.RaiseXpCollected(10);
            if (GUILayout.Button("Hurt 1")) player.TakeDamage(new DamageInfo(1f, DamageType.Normal, null));
            if (GUILayout.Button("Kill")) player.TakeDamage(new DamageInfo(9999f, DamageType.Normal, null));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}

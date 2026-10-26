using System.Text;
using FreedomJam.Game.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Game.Sandbox
{
    /// <summary>On-screen readout and test keys for the Enemy sandbox (F = toggle forward movement, K = kill outside mouth, R = revive).</summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        [SerializeField] SandboxPlayer player;
        [SerializeField] EnemySpawner spawner;

        readonly StringBuilder text = new();
        GUIStyle style;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || player == null) return;
            if (keyboard.fKey.wasPressedThisFrame) player.MoveForward = !player.MoveForward;
            if (keyboard.kKey.wasPressedThisFrame) player.KillEnemiesOutsideMouth();
            if (keyboard.rKey.wasPressedThisFrame && !player.IsAlive) player.ResetCharacter();
        }

        void OnGUI()
        {
            if (player == null) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, richText = true, padding = new RectOffset(12, 12, 10, 10) };

            text.Clear();
            text.AppendLine("<b>ENEMY SANDBOX</b>");
            text.AppendLine(player.IsAlive ? $"HP: {player.CurrentHP:0.#} / {player.MaxHP:0.#}" : "<color=#ff6666>DEAD — press R to revive</color>");
            if (spawner != null)
            {
                text.AppendLine($"Time: {spawner.ElapsedSeconds:0.0}s");
                text.AppendLine($"Enemies alive: {spawner.AliveCount}");
            }
            text.AppendLine($"Moving: {(player.MoveForward ? "on" : "off")}");
            text.AppendLine($"Eaten: {player.EatenCount}   Killed: {player.KilledCount}");
            text.AppendLine($"XP: {player.TotalXp}");
            text.Append("Bonuses:");
            if (player.Bonuses.Count == 0) text.Append(" none");
            foreach (var bonus in player.Bonuses) text.Append($"  {bonus.Key} +{bonus.Value * 100f:0}%");
            text.AppendLine();
            text.Append("<size=13>A / D steer · F toggle moving · K kill all outside mouth · R revive</size>");

            GUI.Box(new Rect(12, 12, 460, 220), text.ToString(), style);
        }
    }
}

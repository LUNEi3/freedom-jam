namespace FreedomJam.Game
{
    /// <summary>Where enemies, spawners and pickups find the player without referencing Player/ code.</summary>
    public static class GameplayContext
    {
        /// <summary>Null when no player is in the scene. Also check Player.Character.IsAlive.</summary>
        public static IPlayer Player { get; private set; }

        public static void RegisterPlayer(IPlayer player) => Player = player;
        public static void UnregisterPlayer(IPlayer player) { if (Player == player) Player = null; }
    }
}

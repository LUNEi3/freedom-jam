namespace FreedomJam.Game
{
    public enum DamageType { Normal, Fire }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        /// <summary>Who dealt it; may be null (e.g. hazards).</summary>
        public readonly Character Source;

        public DamageInfo(float amount, DamageType type, Character source)
        {
            Amount = amount; Type = type; Source = source;
        }
    }
}

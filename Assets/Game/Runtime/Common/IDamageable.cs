namespace FreedomJam.Game
{
    /// <summary>All damage goes through this. Find it with collider.GetComponentInParent&lt;IDamageable&gt;().</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}

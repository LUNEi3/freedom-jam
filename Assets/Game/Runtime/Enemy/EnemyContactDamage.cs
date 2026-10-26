using UnityEngine;

namespace FreedomJam.Game.Enemy
{
    /// <summary>
    /// Damages the player while touching it, at most once per contactDamageInterval (MVP REQ-ENEMY-3).
    /// Uses body distance rather than trigger events, because enemies stop exactly at the player's surface.
    /// </summary>
    [RequireComponent(typeof(EnemyCharacter))]
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        EnemyCharacter enemy;
        float nextHitTime;

        void Awake() => enemy = GetComponent<EnemyCharacter>();

        // Pooled enemies are re-enabled on reuse, so each life starts ready to hit.
        void OnEnable() => nextHitTime = 0f;

        void Update()
        {
            if (Time.deltaTime <= 0f || !enemy.IsAlive || enemy.Creature == null || Time.time < nextHitTime) return;
            if (!enemy.IsTouchingPlayer()) return;

            GameplayContext.Player.Character.TakeDamage(new DamageInfo(enemy.ContactDamage, DamageType.Normal, enemy));
            nextHitTime = Time.time + enemy.Creature.contactDamageInterval;
        }
    }
}

using UnityEngine;

namespace FreedomJam.Game.PlayerSandbox
{
    /// <summary>
    /// Test-only target for the player sandbox. Stands still, and on death fakes the enemy track's
    /// eat-vs-kill rule so stats and XP can be tested before real enemies exist. Respawns after a delay.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DummyTarget : Character
    {
        [SerializeField] CreatureData creature;
        [SerializeField] float respawnDelay = 2f;

        Renderer[] renderers;
        Collider[] colliders;

        protected override CharacterData Data => creature;

        void Awake()
        {
            GetComponent<Rigidbody>().isKinematic = true;
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
        }

        void OnDisable() => CancelInvoke();

        protected override void Tick(float deltaTime) { }

        protected override void OnDied(DamageInfo killingBlow)
        {
            var player = GameplayContext.Player;
            if (player != null && player.IsInMouth(transform.position))
            {
                Debug.Log($"{name} eaten.", this);
                GameplayEvents.RaiseCreatureEaten(creature);
            }
            else
            {
                Debug.Log($"{name} killed outside the mouth.", this);
                GameplayEvents.RaiseCreatureKilled(creature, transform.position);
            }
            SetVisible(false);
            Invoke(nameof(Respawn), respawnDelay);
        }

        void Respawn()
        {
            SetVisible(true);
            ResetCharacter();
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }
    }
}

using UnityEngine;

namespace FreedomJam.Game.Pickups
{
    /// <summary>
    /// Placed by hand at a fixed spot on the map. Touching it heals the player, then it hides and comes back after
    /// Respawn Seconds (0 = one use only). Ignored while the player is at full HP, so it is never wasted.
    /// </summary>
    public sealed class HpOrb : PickupOrb
    {
        [Tooltip("HP restored on pickup (never above max HP).")]
        [SerializeField, Min(0f)] float healAmount = 3f;
        [Tooltip("Seconds until it reappears after pickup. 0 = one use only.")]
        [SerializeField, Min(0f)] float respawnSeconds = 20f;
        [Tooltip("Shown while the orb can be picked up. Defaults to the orb's own renderers.")]
        [SerializeField] GameObject visual;

        Vector3 home;
        bool homeSet;
        float respawnIn;

        public bool IsAvailable { get; private set; } = true;

        void Awake() => RememberHome();

        void RememberHome()
        {
            if (homeSet) return;
            home = transform.position;
            homeSet = true;
        }

        protected override void OnStep(float deltaTime)
        {
            if (IsAvailable || respawnSeconds <= 0f || deltaTime <= 0f) return;
            respawnIn -= deltaTime;
            if (respawnIn <= 0f) SetAvailable(true);
        }

        protected override bool CanCollect(IPlayer player) =>
            IsAvailable && player.Character.CurrentHP < player.Character.MaxHP;

        protected override void Collect(IPlayer player)
        {
            RememberHome();
            player.Character.Heal(healAmount);
            transform.position = home;
            respawnIn = respawnSeconds;
            SetAvailable(false);
        }

        void SetAvailable(bool available)
        {
            IsAvailable = available;
            if (visual != null) visual.SetActive(available);
            else foreach (var orbRenderer in GetComponentsInChildren<Renderer>(true)) orbRenderer.enabled = available;
        }
    }
}

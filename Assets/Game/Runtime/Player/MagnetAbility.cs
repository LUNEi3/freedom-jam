using System.Collections.Generic;
using FreedomJam.Game.Pickups;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>One-time passive upgrade that continuously attracts all XP orbs.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class MagnetAbility : MonoBehaviour, ILevelUpChoiceSource
    {
        [SerializeField, Min(0.01f)] float pullSpeed = 18f;
        [SerializeField, Min(0.02f)] float scanInterval = 0.2f;

        PlayerCharacter player;
        bool unlocked;
        float nextScanAt;

        public bool IsUnlocked => unlocked;

        void Awake() => player = GetComponent<PlayerCharacter>();

        void Update()
        {
            if (!unlocked || player == null || !player.IsAlive || Time.unscaledTime < nextScanAt) return;
            nextScanAt = Time.unscaledTime + Mathf.Max(0.02f, scanInterval);
            foreach (var orb in FindObjectsByType<XpOrb>())
                if (orb != null && orb.isActiveAndEnabled && orb.Xp > 0)
                    orb.StartSuction(pullSpeed);
        }

        public void AppendAvailableChoices(List<LevelUpChoice> choices)
        {
            if (choices != null && !unlocked)
                choices.Add(new LevelUpChoice("magnet", "Magnet", $"Passively pull all XP orbs toward you at {pullSpeed:0.##} m/s."));
        }

        public bool TryApplyChoice(string id)
        {
            if (id != "magnet" || unlocked) return false;
            unlocked = true;
            nextScanAt = 0f;
            return true;
        }
    }
}

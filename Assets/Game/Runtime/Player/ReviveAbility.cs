using System.Collections;
using System.Collections.Generic;
using FreedomJam.Game.CameraRig;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>One-time upgrade that prevents one lethal hit and restores the player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class ReviveAbility : MonoBehaviour, ILevelUpChoiceSource
    {
        [SerializeField, Min(0f)] float freezeDuration = 0.8f;
        [SerializeField, Min(0.01f)] float protectionDuration = 1f;
        [SerializeField, Range(0.01f, 1f)] float restoredMaxHpRatio = 0.5f;
        [SerializeField, Min(1f)] float moveSpeedMultiplier = 1.5f;
        [SerializeField] Color reviveFlashColor = new(0.2f, 1f, 0.3f, 1f);
        [SerializeField, Min(0f)] float shakeDuration = 0.32f;
        [SerializeField, Min(0f)] float shakeMagnitude = 0.45f;
        [SerializeField, Min(0f)] float shakeFrequency = 30f;
        [SerializeField] GameplayTimeScaleCoordinator timeScaleCoordinator;

        PlayerCharacter player;
        PlayerAudioFeedback audioFeedback;
        ICharacterFlashFeedback visualFeedback;
        bool unlocked;
        bool used;
        bool pending;
        int freezeRequestId;
        float fallbackTimeScale = 1f;
        bool ownsFallbackTimeScale;
        Coroutine reviveRoutine;

        public bool BlocksDamage => pending;
        public bool IsUnlocked => unlocked;
        public bool IsUsed => used;

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            audioFeedback = GetComponent<PlayerAudioFeedback>();
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is ICharacterFlashFeedback feedback)
                {
                    visualFeedback = feedback;
                    break;
                }
            if (timeScaleCoordinator == null) timeScaleCoordinator = GetComponent<GameplayTimeScaleCoordinator>();
        }

        public void AppendAvailableChoices(List<LevelUpChoice> choices)
        {
            if (choices != null && !unlocked)
                choices.Add(new LevelUpChoice("revive", "Revive",
                    $"Prevent one lethal hit. Restore {Mathf.Clamp01(restoredMaxHpRatio):P0} HP, then gain {moveSpeedMultiplier:0.##}x speed and invincibility for {protectionDuration:0.##} s."));
        }

        public bool TryApplyChoice(string id)
        {
            if (id != "revive" || unlocked) return false;
            unlocked = true;
            return true;
        }

        public bool TryPreventLethalDamage(DamageInfo _)
        {
            if (!unlocked || used || pending || player == null) return false;
            used = true;
            pending = true;
            if (timeScaleCoordinator != null)
                freezeRequestId = timeScaleCoordinator.Acquire(0f);
            else
            {
                fallbackTimeScale = Time.timeScale;
                ownsFallbackTimeScale = true;
                Time.timeScale = 0f;
            }
            reviveRoutine = StartCoroutine(ReviveRoutine());
            return true;
        }

        IEnumerator ReviveRoutine()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, freezeDuration));

            if (timeScaleCoordinator != null && freezeRequestId > 0)
                timeScaleCoordinator.Release(freezeRequestId);
            else if (ownsFallbackTimeScale)
                Time.timeScale = fallbackTimeScale;
            freezeRequestId = 0;
            ownsFallbackTimeScale = false;

            if (player != null)
            {
                player.Heal(player.MaxHP * Mathf.Clamp01(restoredMaxHpRatio));
                player.SetReviveProtection(true, moveSpeedMultiplier);
            }
            pending = false;
            if (audioFeedback != null) audioFeedback.PlayRevive();
            if (visualFeedback != null) visualFeedback.PlayFlash(reviveFlashColor, protectionDuration);
            ShakeCamera();

            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, protectionDuration));
            if (player != null) player.SetReviveProtection(false);
            reviveRoutine = null;
        }

        void ShakeCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var shake = camera.GetComponent<CameraShake>() ?? camera.gameObject.AddComponent<CameraShake>();
            shake.Shake(shakeDuration, shakeMagnitude, shakeFrequency);
        }

        void OnDisable()
        {
            if (reviveRoutine != null)
            {
                StopCoroutine(reviveRoutine);
                reviveRoutine = null;
            }
            if (pending && player != null && player.IsAlive)
                player.Heal(player.MaxHP * Mathf.Clamp01(restoredMaxHpRatio));
            if (freezeRequestId > 0 && timeScaleCoordinator != null)
                timeScaleCoordinator.Release(freezeRequestId);
            else if (ownsFallbackTimeScale)
                Time.timeScale = fallbackTimeScale;
            freezeRequestId = 0;
            ownsFallbackTimeScale = false;
            pending = false;
            if (player != null) player.SetReviveProtection(false);
        }
    }
}

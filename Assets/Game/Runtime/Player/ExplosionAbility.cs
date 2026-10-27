using System;
using System.Collections;
using System.Collections.Generic;
using FreedomJam.Game.CameraRig;
using FreedomJam.Game.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Game.Player
{
    /// <summary>Level-based area attack with a short slow-motion windup.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class ExplosionAbility : MonoBehaviour, ILevelUpChoiceSource
    {
        [Serializable]
        public sealed class ExplosionLevelSettings
        {
            [Min(0f)] public float radius = 4f;
            [Min(0f)] public float damage = 25f;
            [Min(0f)] public float cooldown = 12f;
            [Range(0.01f, 1f)] public float slowMotionScale = 0.1f;
            [Min(0f)] public float slowMotionDuration = 0.7f;
            [Min(0f)] public float hitDelayPerMeter = 0.08f;
        }

        [SerializeField] List<ExplosionLevelSettings> levels = new()
        {
            new ExplosionLevelSettings(),
            new ExplosionLevelSettings { radius = 5f, damage = 35f, cooldown = 10f }
        };
        [SerializeField] ParticleSystem explosionVfx;
        [SerializeField] GameplayTimeScaleCoordinator timeScaleCoordinator;
        [SerializeField, Min(0f)] float shakeDuration = 0.3f;
        [SerializeField, Min(0f)] float shakeMagnitude = 0.42f;
        [SerializeField, Min(0f)] float shakeFrequency = 30f;

        PlayerCharacter player;
        PlayerAudioFeedback audioFeedback;
        int level;
        float readyAt;
        bool exploding;
        int slowMotionRequestId;
        float fallbackTimeScale = 1f;
        bool ownsFallbackTimeScale;
        Coroutine explosionRoutine;

        public int Level => level;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool CanUpgrade => GetSettings(level + 1) != null;

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            audioFeedback = GetComponent<PlayerAudioFeedback>();
            if (timeScaleCoordinator == null) timeScaleCoordinator = GetComponent<GameplayTimeScaleCoordinator>();
        }

        void Update()
        {
            if (player == null || !player.IsAlive || level <= 0 || exploding || CooldownRemaining > 0f ||
                Time.timeScale <= 0f || Keyboard.current == null) return;
            if (Keyboard.current.lKey.wasPressedThisFrame) Activate();
        }

        public void AppendAvailableChoices(List<LevelUpChoice> choices)
        {
            if (choices == null || !CanUpgrade) return;
            var settings = GetSettings(level + 1);
            choices.Add(new LevelUpChoice("explosion", "Explosion", Describe(settings, level + 1)));
        }

        public bool TryApplyChoice(string id)
        {
            if (id != "explosion" || !CanUpgrade) return false;
            level++;
            return true;
        }

        void Activate()
        {
            var settings = GetSettings(level);
            if (settings == null) return;
            readyAt = Time.time + Mathf.Max(0f, settings.cooldown);
            exploding = true;
            if (audioFeedback != null) audioFeedback.PlayExplosionCharge();
            if (explosionVfx != null)
            {
                var effectScale = Mathf.Max(0f, settings.radius) / 10f;
                explosionVfx.transform.localScale = Vector3.one * effectScale;
                explosionVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                explosionVfx.Play(true);
            }

            if (timeScaleCoordinator != null)
                slowMotionRequestId = timeScaleCoordinator.Acquire(settings.slowMotionScale);
            else
            {
                fallbackTimeScale = Time.timeScale;
                ownsFallbackTimeScale = true;
                Time.timeScale = Mathf.Clamp(settings.slowMotionScale, 0.01f, 1f);
            }
            explosionRoutine = StartCoroutine(DetonateAfterSlowMotion(settings));
        }

        IEnumerator DetonateAfterSlowMotion(ExplosionLevelSettings settings)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, settings.slowMotionDuration));
            if (timeScaleCoordinator != null && slowMotionRequestId > 0)
                timeScaleCoordinator.Release(slowMotionRequestId);
            else if (ownsFallbackTimeScale)
                Time.timeScale = fallbackTimeScale;
            slowMotionRequestId = 0;
            ownsFallbackTimeScale = false;

            if (audioFeedback != null) audioFeedback.PlayExplosion();
            ShakeCamera();

            var radius = Mathf.Max(0f, settings.radius);
            var radiusSquared = radius * radius;
            var enemies = FindObjectsByType<EnemyCharacter>();
            var impacts = new List<EnemyImpact>(enemies.Length);
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive) continue;
                var distance = Vector3.Distance(enemy.transform.position, player.transform.position);
                if (distance * distance > radiusSquared) continue;
                impacts.Add(new EnemyImpact(enemy, distance));
            }

            impacts.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var elapsedDelay = 0f;
            var playedEnemyHit = false;
            var delayPerMeter = Mathf.Max(0f, settings.hitDelayPerMeter);
            foreach (var impact in impacts)
            {
                var targetDelay = impact.Distance * delayPerMeter;
                var waitDuration = targetDelay - elapsedDelay;
                if (waitDuration > 0f) yield return new WaitForSeconds(waitDuration);
                elapsedDelay = targetDelay;

                var enemy = impact.Enemy;
                if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive) continue;
                enemy.TakeDamage(new DamageInfo(settings.damage, DamageType.Normal, player));
                if (!playedEnemyHit && audioFeedback != null)
                {
                    audioFeedback.PlayEnemyHit();
                    playedEnemyHit = true;
                }
            }

            exploding = false;
            explosionRoutine = null;
        }

        sealed class EnemyImpact
        {
            public readonly EnemyCharacter Enemy;
            public readonly float Distance;

            public EnemyImpact(EnemyCharacter enemy, float distance)
            {
                Enemy = enemy;
                Distance = distance;
            }
        }

        void ShakeCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var shake = camera.GetComponent<CameraShake>() ?? camera.gameObject.AddComponent<CameraShake>();
            shake.Shake(shakeDuration, shakeMagnitude, shakeFrequency);
        }

        ExplosionLevelSettings GetSettings(int index) =>
            levels != null && index > 0 && index <= levels.Count ? levels[index - 1] : null;

        static string Describe(ExplosionLevelSettings settings, int targetLevel) =>
            $"Unlock/upgrade to level {targetLevel}\nRadius {settings.radius:0.##} m - Damage {settings.damage:0.##} - Cooldown {settings.cooldown:0.#} s - Slow {settings.slowMotionScale:P0} for {settings.slowMotionDuration:0.##} s";

        void OnDisable()
        {
            if (explosionRoutine != null)
            {
                StopCoroutine(explosionRoutine);
                explosionRoutine = null;
            }
            if (slowMotionRequestId > 0 && timeScaleCoordinator != null)
                timeScaleCoordinator.Release(slowMotionRequestId);
            else if (ownsFallbackTimeScale)
                Time.timeScale = fallbackTimeScale;
            slowMotionRequestId = 0;
            ownsFallbackTimeScale = false;
            exploding = false;
        }
    }
}

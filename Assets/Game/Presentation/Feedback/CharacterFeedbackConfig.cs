using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    [CreateAssetMenu(menuName = "Freedom Jam/Character Feedback Config")]
    public sealed class CharacterFeedbackConfig : ScriptableObject
    {
        [Header("Health Bars")]
        public Color playerHealthColor = new(0.2f, 0.85f, 0.3f);
        public Color enemyHealthColor = new(0.9f, 0.16f, 0.12f);
        public Vector2 playerBarSize = new(250f, 28f);
        public Vector2 enemyBarSize = new(1.5f, 0.14f);
        public Vector2 playerBarPadding = new(18f, 18f);
        public float enemyBarHeadOffset = 0.18f;

        [Header("Damage Numbers")]
        public Color damageNumberColor = Color.white;
        public float damageNumberLifetime = 0.8f;
        public float damageNumberRise = 0.8f;
        public float damageNumberMinSize = 28f;
        public float damageNumberMaxSize = 48f;
        public float damageNumberScalePerPoint = 2f;
        public float damageNumberSpread = 0.25f;

        [Header("Character Visuals")]
        public Color damageFlashColor = Color.red;
        public float damageFlashDuration = 0.12f;
        public float invincibilityCyclesPerSecond = 10f;
        [Range(0.05f, 1f)] public float minimumInvincibilityAlpha = 0.25f;

        public float SafeNumberLifetime => Mathf.Max(0.05f, damageNumberLifetime);
        public float SafeFlashDuration => Mathf.Max(0f, damageFlashDuration);
        public float SafeBlinkFrequency => Mathf.Max(0f, invincibilityCyclesPerSecond);
    }
}

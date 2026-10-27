using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Objective
{
    /// <summary>Shows the spaceship's message in the middle of the screen for a few seconds when it appears.</summary>
    public sealed class ObjectiveBanner : MonoBehaviour
    {
        [SerializeField] LostSpaceship spaceship;
        [SerializeField] Text label;
        [Tooltip("Seconds the message stays on screen (unscaled, so it still counts down if the game is paused).")]
        [SerializeField, Min(0f)] float showSeconds = 4f;

        float hideAt;

        void Awake() => SetVisible(false);

        void OnEnable() { if (spaceship != null) spaceship.Appeared += OnAppeared; }
        void OnDisable() { if (spaceship != null) spaceship.Appeared -= OnAppeared; }

        void Update()
        {
            if (label != null && label.enabled && Time.unscaledTime >= hideAt) SetVisible(false);
        }

        void OnAppeared(LostSpaceship ship)
        {
            if (label == null) return;
            label.text = ship.AppearMessage;
            hideAt = Time.unscaledTime + showSeconds;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            if (label != null) label.enabled = visible;
        }
    }
}

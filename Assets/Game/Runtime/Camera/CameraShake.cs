using UnityEngine;

namespace FreedomJam.Game.CameraRig
{
    /// <summary>Configurable screen-space camera shake sampled by FixedAngleFollowCamera.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] float duration = 0.22f;
        [SerializeField, Min(0f)] float magnitude = 0.28f;
        [SerializeField, Min(0f)] float frequency = 28f;

        float remaining;
        float strength;
        float activeDuration;
        float activeFrequency;
        float phase;

        public void Shake() => Shake(duration, magnitude, frequency);

        public void Shake(float shakeDuration, float shakeMagnitude, float shakeFrequency)
        {
            if (remaining <= 0f)
            {
                activeDuration = Mathf.Max(0f, shakeDuration);
                remaining = activeDuration;
                strength = Mathf.Max(0f, shakeMagnitude);
            }
            else
            {
                activeDuration = Mathf.Max(activeDuration, Mathf.Max(0f, shakeDuration));
                remaining = Mathf.Max(remaining, Mathf.Max(0f, shakeDuration));
                strength = Mathf.Max(strength, Mathf.Max(0f, shakeMagnitude));
            }
            activeFrequency = Mathf.Max(0f, shakeFrequency);
            phase = Random.value * 100f;
        }

        internal Vector3 SampleOffset(float deltaTime)
        {
            if (remaining <= 0f || strength <= 0f) return Vector3.zero;
            remaining = Mathf.Max(0f, remaining - deltaTime);
            var envelope = activeDuration > 0f ? remaining / activeDuration : 0f;
            phase += deltaTime * activeFrequency;
            var x = Mathf.PerlinNoise(phase, 0.37f) * 2f - 1f;
            var y = Mathf.PerlinNoise(0.73f, phase) * 2f - 1f;
            return transform.right * (x * strength * envelope) + transform.up * (y * strength * envelope);
        }
    }
}

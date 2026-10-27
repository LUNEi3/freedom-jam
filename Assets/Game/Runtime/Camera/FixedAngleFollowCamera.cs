using UnityEngine;

namespace FreedomJam.Game.CameraRig
{
    /// <summary>Isometric-style camera: fixed angle, follows the player. Put it on the Camera object.</summary>
    public sealed class FixedAngleFollowCamera : MonoBehaviour
    {
        [Tooltip("Leave empty to follow whoever is registered in GameplayContext.")]
        [SerializeField] Transform target;
        [Tooltip("Degrees looking down. 90 = straight down.")]
        [SerializeField] float pitch = 50f;
        [Tooltip("Degrees around the vertical axis. 45 = classic isometric.")]
        [SerializeField] float yaw = 45f;
        [SerializeField] float distance = 25f;
        [Tooltip("How fast the camera catches up. 0 = snap instantly.")]
        [SerializeField] float followSharpness = 10f;
        CameraShake shake;
        Vector3 followPosition;
        bool hasFollowPosition;

        public Transform Target { get => target; set => target = value; }

        void LateUpdate()
        {
            if (shake == null) shake = GetComponent<CameraShake>();
            var followed = target;
            if (followed == null && GameplayContext.Player != null) followed = GameplayContext.Player.Character.transform;
            if (followed == null) return;

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desired = followed.position - rotation * Vector3.forward * distance;
            if (!hasFollowPosition)
            {
                followPosition = transform.position;
                hasFollowPosition = true;
            }
            followPosition = followSharpness <= 0f
                ? desired
                : Vector3.Lerp(followPosition, desired, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            var position = followPosition;
            if (shake != null) position += shake.SampleOffset(Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}

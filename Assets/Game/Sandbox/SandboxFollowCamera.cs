using UnityEngine;

namespace FreedomJam.Game.Sandbox
{
    /// <summary>Fixed-angle follow camera for the sandbox. The real camera is Track A's.</summary>
    public sealed class SandboxFollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0f, 20f, -12f);

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.LookAt(target.position);
        }
    }
}

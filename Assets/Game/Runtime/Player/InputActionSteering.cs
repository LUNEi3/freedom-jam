using FreedomJam.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreedomJam.Game.Player
{
    /// <summary>
    /// Steering from the X axis of Player/Move (A / D, left stick).
    /// The foundation enables the Player map only while Playing; when there is no bootstrap
    /// (scene opened directly for testing) this component enables the action itself.
    /// </summary>
    public sealed class InputActionSteering : MonoBehaviour, ISteeringInput
    {
        [Tooltip("Player/Move from InputSystem_Actions.")]
        [SerializeField] InputActionReference move;

        bool enabledBySelf;

        public float Steer => move != null && move.action != null
            ? Mathf.Clamp(move.action.ReadValue<Vector2>().x, -1f, 1f)
            : 0f;

        void OnEnable()
        {
            if (move == null || move.action == null)
            {
                Debug.LogError("InputActionSteering needs the Player/Move action.", this);
                return;
            }
            if (GameFoundationBootstrap.Instance == null && !move.action.enabled)
            {
                move.action.Enable();
                enabledBySelf = true;
            }
        }

        void OnDisable()
        {
            if (enabledBySelf && move != null && move.action != null) move.action.Disable();
            enabledBySelf = false;
        }
    }
}

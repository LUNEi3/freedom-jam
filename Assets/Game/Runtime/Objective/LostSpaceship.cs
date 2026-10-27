using System;
using FreedomJam.Foundation;
using UnityEngine;
using UnityEngine.Serialization;

namespace FreedomJam.Game.Objective
{
    /// <summary>
    /// End-of-run goal. Visible on the map from the start, but only usable from Activate At Seconds of run time:
    /// reaching it after that wins the run (Won). Place it at its fixed spot; there is deliberately no direction arrow.
    /// Move the root (this object), not its children: the reach zone and beam are centred on the root.
    /// </summary>
    [SelectionBase]
    public sealed class LostSpaceship : MonoBehaviour
    {
        [Tooltip("Run time in seconds when the ship becomes usable (90 = with 0:30 left on a 2:00 countdown).")]
        [FormerlySerializedAs("appearAtSeconds")]
        [SerializeField, Min(0f)] float activateAtSeconds = 90f;
        [Tooltip("The player wins when its body is this close to the ship's centre (once active).")]
        [SerializeField, Min(0f)] float reachRadius = 2f;
        [Tooltip("Shown to the player when the ship becomes usable.")]
        [FormerlySerializedAs("appearMessage")]
        [SerializeField, TextArea] string readyMessage = "The lost spaceship is ready!\nReach it to escape!";
        [Tooltip("Optional effect (e.g. a beam) switched on when the ship becomes usable.")]
        [SerializeField] GameObject readyEffect;

        float ownClock;

        public bool IsActive { get; private set; }
        public bool IsReached { get; private set; }
        public string ReadyMessage => readyMessage;

        /// <summary>The ship just became usable; the message is for the on-screen banner.</summary>
        public event Action<LostSpaceship> Activated;
        public event Action<LostSpaceship> Reached;

        void Start()
        {
            if (!IsActive && readyEffect != null) readyEffect.SetActive(false);
            WarnIfModelMovedAwayFromRoot();
        }

        // The win check uses this object's position; a child model dragged away from it looks reachable but isn't.
        void WarnIfModelMovedAwayFromRoot()
        {
            foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (readyEffect != null && childRenderer.transform.IsChildOf(readyEffect.transform)) continue;
                var offset = childRenderer.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= reachRadius) continue;
                Debug.LogWarning($"LostSpaceship: '{childRenderer.name}' is {offset.magnitude:0.#} units from the ship's centre, outside its reach radius. " +
                                 "Move the LostSpaceship object itself, not its Visual child, or the player can't win by touching the model.", this);
                return;
            }
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            var run = CurrentRun();
            ownClock += Time.deltaTime;
            Step(run != null ? run.ElapsedSeconds : ownClock);
        }

        /// <summary>One frame of objective logic at this run time. Called by Update; public so EditMode tests can drive it.</summary>
        public void Step(float runSeconds)
        {
            if (IsReached) return;
            if (!IsActive)
            {
                if (runSeconds < activateAtSeconds) return;
                IsActive = true;
                if (readyEffect != null) readyEffect.SetActive(true);
                Activated?.Invoke(this);
            }

            var player = GameplayContext.Player;
            if (player == null || player.Character == null || !player.Character.IsAlive) return;
            var offset = player.Character.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > reachRadius + player.Character.BodySize * 0.5f) return;

            IsReached = true;
            Reached?.Invoke(this);
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Flow != null) bootstrap.Flow.FinishRun(RunOutcome.Won);
        }

        static GameRunData CurrentRun()
        {
            var bootstrap = GameFoundationBootstrap.Instance;
            return bootstrap != null && bootstrap.Flow != null ? bootstrap.Flow.CurrentRun : null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, reachRadius);
        }
    }
}

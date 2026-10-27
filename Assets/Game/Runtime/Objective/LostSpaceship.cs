using System;
using FreedomJam.Foundation;
using UnityEngine;

namespace FreedomJam.Game.Objective
{
    /// <summary>
    /// End-of-run goal: hidden until Appear At Seconds of run time, then the player must reach it before the
    /// countdown runs out. Reaching it wins the run; time running out is TimeUp, dying is Lost.
    /// Place it at its fixed spot on the map; there is deliberately no direction arrow.
    /// </summary>
    public sealed class LostSpaceship : MonoBehaviour
    {
        [Tooltip("Run time in seconds when the ship appears (90 = with 0:30 left on a 2:00 run).")]
        [SerializeField, Min(0f)] float appearAtSeconds = 90f;
        [Tooltip("The player wins when its body is this close to the ship's centre.")]
        [SerializeField, Min(0f)] float reachRadius = 2f;
        [Tooltip("Shown to the player when the ship appears.")]
        [SerializeField, TextArea] string appearMessage = "The lost spaceship has appeared!\nFind it to win!";
        [Tooltip("The ship's model, hidden until it appears.")]
        [SerializeField] GameObject visual;

        float ownClock;

        public bool HasAppeared { get; private set; }
        public bool IsReached { get; private set; }
        public string AppearMessage => appearMessage;

        /// <summary>The ship just appeared; the message is for the on-screen banner.</summary>
        public event Action<LostSpaceship> Appeared;
        public event Action<LostSpaceship> Reached;

        void Start()
        {
            if (!HasAppeared && visual != null) visual.SetActive(false);
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
            if (!HasAppeared)
            {
                if (runSeconds < appearAtSeconds) return;
                HasAppeared = true;
                if (visual != null) visual.SetActive(true);
                Appeared?.Invoke(this);
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

using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Foundation
{
    public sealed class GameHudController : MonoBehaviour
    {
        public Text scoreLabel;
        public Text timeLabel;

        GameFlowController flow;

        public void Initialize(GameFlowController flow)
        {
            if (this.flow != null)
            {
                this.flow.RunUpdated -= OnRunUpdated;
                this.flow.StateChanged -= OnStateChanged;
            }
            this.flow = flow;
            if (this.flow != null)
            {
                this.flow.RunUpdated += OnRunUpdated;
                this.flow.StateChanged += OnStateChanged;
            }
            Render(this.flow != null ? this.flow.CurrentRun : null);
        }

        void Start()
        {
            if (flow == null && GameFoundationBootstrap.Instance != null)
                Initialize(GameFoundationBootstrap.Instance.Flow);
        }

        void OnEnable()
        {
            if (flow == null) return;
            flow.RunUpdated += OnRunUpdated;
            flow.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            if (flow == null) return;
            flow.RunUpdated -= OnRunUpdated;
            flow.StateChanged -= OnStateChanged;
        }

        void OnDestroy()
        {
            if (flow == null) return;
            flow.RunUpdated -= OnRunUpdated;
            flow.StateChanged -= OnStateChanged;
        }

        void OnRunUpdated(GameRunData run) => Render(run);

        void OnStateChanged(GameState _) => Render(flow != null ? flow.CurrentRun : null);

        void Render(GameRunData run)
        {
            if (run == null)
            {
                if (scoreLabel != null) scoreLabel.text = "0";
                if (timeLabel != null) timeLabel.text = "00:00";
                return;
            }

            if (scoreLabel != null) scoreLabel.text = run.Score.ToString();
            if (timeLabel != null) timeLabel.text = FormatTime(run.DisplaySeconds);
        }

        static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        }
    }
}

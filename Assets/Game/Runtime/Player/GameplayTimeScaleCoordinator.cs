using System.Collections.Generic;
using FreedomJam.Foundation;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Composes temporary gameplay pause and slow-motion requests owned by player systems.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayTimeScaleCoordinator : MonoBehaviour, IGameplayTimeScaleOverride
    {
        readonly Dictionary<int, float> requests = new();
        int nextRequestId = 1;
        float baselineScale = 1f;
        bool hasBaseline;
        int flowPauseRequestId;

        public float CurrentScale => Time.timeScale;

        public void SetFlowPaused(bool paused)
        {
            if (paused)
            {
                if (flowPauseRequestId <= 0) flowPauseRequestId = Acquire(0f);
                return;
            }
            if (flowPauseRequestId <= 0) return;
            Release(flowPauseRequestId);
            flowPauseRequestId = 0;
        }

        public int Acquire(float scale)
        {
            if (requests.Count == 0)
            {
                baselineScale = Time.timeScale;
                hasBaseline = true;
            }

            var id = nextRequestId++;
            if (nextRequestId <= 0) nextRequestId = 1;
            while (requests.ContainsKey(id))
            {
                id = nextRequestId++;
                if (nextRequestId <= 0) nextRequestId = 1;
            }

            requests[id] = Mathf.Clamp(scale, 0f, 1f);
            Apply();
            return id;
        }

        public void Release(int requestId)
        {
            if (requestId <= 0 || !requests.Remove(requestId)) return;
            if (requests.Count == 0)
            {
                if (hasBaseline) Time.timeScale = baselineScale;
                hasBaseline = false;
                return;
            }
            Apply();
        }

        void OnDisable()
        {
            requests.Clear();
            flowPauseRequestId = 0;
            if (hasBaseline) Time.timeScale = baselineScale;
            hasBaseline = false;
        }

        void Apply()
        {
            var effectiveScale = hasBaseline ? baselineScale : Time.timeScale;
            foreach (var requestedScale in requests.Values)
                effectiveScale = Mathf.Min(effectiveScale, requestedScale);
            Time.timeScale = effectiveScale;
        }
    }
}

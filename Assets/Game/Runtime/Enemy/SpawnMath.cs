using System.Collections.Generic;
using UnityEngine;

namespace FreedomJam.Game.Enemy
{
    /// <summary>Pure spawn helpers, kept out of MonoBehaviours so they are EditMode-testable.</summary>
    public static class SpawnMath
    {
        /// <summary>Point on a flat (XZ) ring around center. angle01 in [0, 1] is one full turn.</summary>
        public static Vector3 RingPoint(Vector3 center, float radius, float angle01)
        {
            var angle = angle01 * Mathf.PI * 2f;
            return new Vector3(center.x + Mathf.Cos(angle) * radius, center.y, center.z + Mathf.Sin(angle) * radius);
        }

        /// <summary>Spawner clock: follows the run timer when there is a run, otherwise counts on its own (e.g. a sandbox).</summary>
        public static float NextElapsedSeconds(float elapsed, float deltaTime, float? runElapsed) =>
            runElapsed ?? elapsed + deltaTime;

        /// <summary>
        /// Index of the phase active at time: the latest start ≤ time, or the earliest phase if time is before all of them.
        /// Phases need not be sorted; null entries are skipped. -1 if there are no phases.
        /// </summary>
        public static int FindPhase(IReadOnlyList<EnemySpawner.SpawnPhase> phases, float time)
        {
            int active = -1, earliest = -1;
            for (var i = 0; i < phases.Count; i++)
            {
                if (phases[i] == null) continue;
                var start = phases[i].startTime;
                if (earliest < 0 || start < phases[earliest].startTime) earliest = i;
                if (start <= time && (active < 0 || start >= phases[active].startTime)) active = i;
            }
            return active >= 0 ? active : earliest;
        }

        /// <summary>Phase active at time, or null if there are no phases.</summary>
        public static EnemySpawner.SpawnPhase PhaseAt(IReadOnlyList<EnemySpawner.SpawnPhase> phases, float time)
        {
            var index = FindPhase(phases, time);
            return index >= 0 ? phases[index] : null;
        }

        /// <summary>Start of the next phase after this one, or +∞ for the last phase.</summary>
        public static float PhaseEnd(IReadOnlyList<EnemySpawner.SpawnPhase> phases, int index)
        {
            var start = phases[index].startTime;
            var end = float.PositiveInfinity;
            foreach (var phase in phases)
                if (phase != null && phase.startTime > start && phase.startTime < end) end = phase.startTime;
            return end;
        }

        /// <summary>Max enemies alive at time, ramped within the active phase (the last phase over lastPhaseRampSeconds).</summary>
        public static int MaxAliveAt(IReadOnlyList<EnemySpawner.SpawnPhase> phases, float lastPhaseRampSeconds, float time)
        {
            var index = FindPhase(phases, time);
            if (index < 0) return 0;

            var phase = phases[index];
            var end = PhaseEnd(phases, index);
            if (float.IsPositiveInfinity(end)) end = phase.startTime + lastPhaseRampSeconds;
            return RampedMaxAlive(phase.maxAliveStart, phase.maxAliveEnd, phase.startTime, end, time);
        }

        /// <summary>Max alive rising smoothly from startValue at phaseStart to endValue at phaseEnd, then holding.</summary>
        public static int RampedMaxAlive(int startValue, int endValue, float phaseStart, float phaseEnd, float time)
        {
            var progress = phaseEnd > phaseStart ? Mathf.InverseLerp(phaseStart, phaseEnd, time) : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(startValue, endValue, progress)));
        }

        /// <summary>Index chosen by roll01 in [0, 1] among positive weights, or -1 if none is positive.</summary>
        public static int PickWeighted(IReadOnlyList<float> weights, float roll01)
        {
            var total = 0f;
            var last = -1;
            for (var i = 0; i < weights.Count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                total += weights[i];
                last = i;
            }
            if (last < 0) return -1;

            var remaining = Mathf.Clamp01(roll01) * total;
            for (var i = 0; i < weights.Count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                remaining -= weights[i];
                if (remaining < 0f) return i;
            }
            return last;
        }
    }
}

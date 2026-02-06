using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// DangerHeuristics: Computes a normalized danger level (0-1) from Core simulation metrics.
    /// Danger increases based on:
    /// - Proximity to world height limit (grid overflow threshold)
    /// - Water level near forbidden threshold
    /// - Wind gust imminent
    /// - Pieces falling / decision pressure
    /// </summary>
    public class DangerHeuristics
    {
        private const float HeightThresholdCritical = 0.8f;  // Danger ramps up at 80% of max height
        private const float WaterThresholdCritical = 0.85f;  // Water danger at 85% of limit
        private const float WindWindness = 15f;              // Ticks before gust = max danger
        private const float PressureThreshold = 0.5f;        // Danger threshold for active piece presence

        /// <summary>
        /// Calculates danger level (0-1) from simulation and level state.
        /// </summary>
        public static float CalculateDangerLevel(Simulation simulation, Level level)
        {
            if (simulation == null || level == null)
                return 0f;

            var state = simulation.State;
            float danger = 0f;

            // Height-based danger: proximity to world limit
            // TODO: Replace with actual grid height limit from level
            const float maxGridHeight = 20f;
            float currentHeight = state.Grid != null ? (float)state.Grid.Height : 0f;
            float heightRatio = currentHeight / maxGridHeight;
            if (heightRatio > HeightThresholdCritical)
            {
                danger += (heightRatio - HeightThresholdCritical) / (1f - HeightThresholdCritical);
            }

            // Water-based danger: water level near forbidden zone
            // TODO: Count water voxels and compare to level forbidden threshold
            // For now, use placeholder heuristic: danger if many pieces locked (water likely)
            if (state.PiecesLocked > 10)
            {
                danger += Mathf.Min(1f, (state.PiecesLocked - 10f) / 20f);
            }

            // Wind danger: proximity to next gust
            // TODO: Hook into Core wind scheduler when available
            // Placeholder: assume gust every 60 ticks
            int ticksUntilGust = 60 - (state.TicksElapsed % 60);
            if (ticksUntilGust < WindWindness)
            {
                danger += 1f - (ticksUntilGust / WindWindness);
            }

            // Active piece pressure: danger if falling piece present
            if (state.ActivePiece != null)
            {
                danger += PressureThreshold;
            }

            // Clamp to 0-1 range
            return Mathf.Clamp01(danger);
        }
    }
}

using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;

namespace Floodline.Client
{
    /// <summary>
    /// Main HUD coordinator: manages all UI displays (gravity, objectives, next/hold, score, wind).
    /// Attached to GameLoader or a HUD root Canvas.
    /// Updates all UI panels each frame based on current simulation state.
    /// Respects per-level feature gating (e.g., hide wind UI if no wind).
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [SerializeField]
        private GravityDisplay gravityDisplay;

        [SerializeField]
        private ObjectivesDisplay objectivesDisplay;

        [SerializeField]
        private NextHoldDisplay nextHoldDisplay;

        [SerializeField]
        private ScoreStarsDisplay scoreStarsDisplay;

        [SerializeField]
        private WindGustDisplay windGustDisplay;

        private Simulation simulation;
        private Level level;

        public void Initialize(Simulation sim, Level lvl)
        {
            simulation = sim;
            level = lvl;

            // Validate required components are present
            if (gravityDisplay == null)
                Debug.LogWarning("[HUD] GravityDisplay not assigned");
            if (objectivesDisplay == null)
                Debug.LogWarning("[HUD] ObjectivesDisplay not assigned");
            if (nextHoldDisplay == null)
                Debug.LogWarning("[HUD] NextHoldDisplay not assigned");
            if (scoreStarsDisplay == null)
                Debug.LogWarning("[HUD] ScoreStarsDisplay not assigned");
            if (windGustDisplay == null)
                Debug.LogWarning("[HUD] WindGustDisplay not assigned");

            // Perform initial update
            UpdateAllDisplays();
        }

        private void LateUpdate()
        {
            // Update HUD every frame after simulation tick
            if (simulation != null && simulation.Status == SimulationStatus.Playing)
            {
                UpdateAllDisplays();
            }
        }

        private void UpdateAllDisplays()
        {
            if (simulation == null || level == null)
                return;

            if (gravityDisplay != null)
                gravityDisplay.UpdateGravity(simulation.Gravity);

            if (objectivesDisplay != null)
                objectivesDisplay.UpdateObjectives(simulation.Objectives);

            if (nextHoldDisplay != null)
                nextHoldDisplay.UpdateNextHold(simulation, level);

            if (scoreStarsDisplay != null)
                scoreStarsDisplay.UpdateScoreStars(simulation, level);

            if (windGustDisplay != null)
                windGustDisplay.UpdateWindGust(simulation, level);
        }
    }
}

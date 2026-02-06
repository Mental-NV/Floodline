using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// Displays wind gust countdown and direction telegraph.
    /// Only visible if the level has wind/gust constraints.
    /// Shows countdown timer 1+ second(s) before gust triggers.
    /// </summary>
    public class WindGustDisplay : MonoBehaviour
    {
        [SerializeField]
        private GameObject windContainer;

        [SerializeField]
        private TMPro.TextMeshProUGUI windCountdownText;

        [SerializeField]
        private TMPro.TextMeshProUGUI windDirectionText;

        private int lastTicksUntilGust = -1;
        private GravityDirection lastGustDirection = (GravityDirection)(-1);
        private bool lastHasWind = false;

        public void UpdateWindGust(Simulation simulation, Level level)
        {
            if (simulation == null || level == null)
            {
                if (windContainer != null)
                    windContainer.SetActive(false);
                return;
            }

            // Determine if level has wind
            bool hasWind = level.Meta.HasWindGust; // TODO: Read from level.Meta if schema supports it

            if (!hasWind)
            {
                if (lastHasWind)
                {
                    lastHasWind = false;
                    if (windContainer != null)
                        windContainer.SetActive(false);
                }
                return;
            }

            if (!lastHasWind)
            {
                lastHasWind = true;
                if (windContainer != null)
                    windContainer.SetActive(true);
            }

            // Update countdown to next gust
            // TODO: Calculate from simulation state (requires gust scheduler in Core)
            // For now, placeholder: assume gust every 60 ticks
            int ticksUntilGust = 60 - (simulation.State.TicksElapsed % 60);
            
            if (ticksUntilGust != lastTicksUntilGust)
            {
                lastTicksUntilGust = ticksUntilGust;
                if (windCountdownText != null)
                {
                    float secondsUntil = ticksUntilGust / 60f;
                    if (secondsUntil >= 1f)
                        windCountdownText.text = $"Wind in {secondsUntil:F1}s";
                    else
                        windCountdownText.text = "WIND GUST!";
                }
            }

            // Update gust direction telegraph
            // TODO: Read from simulation state or level config
            GravityDirection gustDir = simulation.State.CurrentGravity;
            if (gustDir != lastGustDirection)
            {
                lastGustDirection = gustDir;
                if (windDirectionText != null)
                    windDirectionText.text = $"← {gustDir} →";
            }
        }
    }
}

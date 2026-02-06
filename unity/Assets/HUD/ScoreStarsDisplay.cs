using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// Displays current score and earned stars (1/2/3).
    /// Derived from simulation state and level-defined star thresholds.
    /// </summary>
    public class ScoreStarsDisplay : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TextMeshProUGUI scoreText;

        [SerializeField]
        private TMPro.TextMeshProUGUI starsText;

        private long lastScore = -1;
        private int lastStarCount = -1;

        public void UpdateScoreStars(Simulation simulation, Level level)
        {
            if (simulation == null || level == null)
                return;

            // Display score (if applicable)
            long currentScore = simulation.State.Score;
            if (currentScore != lastScore)
            {
                lastScore = currentScore;
                if (scoreText != null)
                    scoreText.text = $"Score: {currentScore}";
            }

            // Display stars based on simulated achievements
            // For now, derive from pieces locked (rough heuristic; refine with GDD star rules)
            int earnedStars = Mathf.Min(3, Mathf.Max(0, simulation.State.PiecesLocked / 5));
            
            if (earnedStars != lastStarCount)
            {
                lastStarCount = earnedStars;
                if (starsText != null)
                {
                    string starDisplay = "";
                    for (int i = 0; i < earnedStars; i++) starDisplay += "★";
                    for (int i = earnedStars; i < 3; i++) starDisplay += "☆";
                    starsText.text = starDisplay;
                }
            }
        }
    }
}

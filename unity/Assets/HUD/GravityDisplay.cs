using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// Displays the current gravity direction and magnitude.
    /// Shows a visual arrow + text label indicating which way is "down" in the current gravity.
    /// </summary>
    public class GravityDisplay : MonoBehaviour
    {
        [SerializeField]
        private RectTransform gravityArrowImage;

        [SerializeField]
        private TMPro.TextMeshProUGUI gravityLabelText;

        private static readonly string[] GravityLabels = { "UP", "DOWN", "NORTH", "SOUTH", "EAST", "WEST" };
        private static readonly float[] GravityRotations = { 180f, 0f, 90f, 270f, 0f, 180f };

        private GravityDirection lastGravity = (GravityDirection)(-1);

        public void UpdateGravity(GravityDirection gravity)
        {
            if (gravity == lastGravity)
                return;

            lastGravity = gravity;

            int gravityIdx = (int)gravity;
            if (gravityIdx >= 0 && gravityIdx < GravityLabels.Length)
            {
                if (gravityLabelText != null)
                    gravityLabelText.text = GravityLabels[gravityIdx];

                if (gravityArrowImage != null)
                    gravityArrowImage.rotation = Quaternion.Euler(0, 0, GravityRotations[gravityIdx]);
            }
        }
    }
}

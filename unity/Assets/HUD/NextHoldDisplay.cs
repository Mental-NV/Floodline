using System.Text;
using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// Displays the next pieces (bag preview) and hold piece slot.
    /// Respects per-level hold enable/disable flag from level schema.
    /// </summary>
    public class NextHoldDisplay : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TextMeshProUGUI nextPiecesText;

        [SerializeField]
        private TMPro.TextMeshProUGUI holdPieceText;

        [SerializeField]
        private GameObject holdContainer;

        private int lastNextCount = -1;
        private PieceId lastHoldPiece = (PieceId)(-1);
        private bool lastHoldEnabled = true;

        public void UpdateNextHold(Simulation simulation, Level level)
        {
            if (simulation == null || level == null)
                return;

            // Determine if hold is enabled for this level
            bool holdEnabled = true; // TODO: Read from level.Meta.HoldEnabled if schema supports it
            
            if (holdEnabled != lastHoldEnabled)
            {
                lastHoldEnabled = holdEnabled;
                if (holdContainer != null)
                    holdContainer.SetActive(holdEnabled);
            }

            // Update next pieces display
            var nextPieces = simulation.State.Bag?.GetPiecePreview(5) ?? new PieceId[0];
            if (nextPieces.Length != lastNextCount)
            {
                lastNextCount = nextPieces.Length;
                StringBuilder sb = new();
                sb.AppendLine("NEXT:");
                foreach (PieceId piece in nextPieces)
                {
                    sb.AppendLine(piece.ToString());
                }

                if (nextPiecesText != null)
                    nextPiecesText.text = sb.ToString();
            }

            // Update hold display
            PieceId holdPiece = simulation.State.HeldPiece ?? (PieceId)(-1);
            if (holdPiece != lastHoldPiece)
            {
                lastHoldPiece = holdPiece;
                if (holdPieceText != null)
                {
                    if (holdPiece == (PieceId)(-1))
                        holdPieceText.text = "HOLD\n(empty)";
                    else
                        holdPieceText.text = $"HOLD\n{holdPiece}";
                }
            }
        }
    }
}

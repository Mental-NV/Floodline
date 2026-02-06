using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// SFXTrigger: Monitors simulation state and fires SFX events based on state changes.
    /// Reads resolved metrics each frame and compares to previous state to detect game events.
    /// Never modifies Core state; purely reactive view layer.
    /// </summary>
    public class SFXTrigger : MonoBehaviour
    {
        [SerializeField]
        private AudioManager audioManager;

        private Simulation simulation;
        private Level level;

        // State tracking for change detection
        private int lastPiecesLocked = 0;
        private GravityDirection lastGravity = (GravityDirection)(-1);
        private long lastWaterVoxels = 0;

        public void Initialize(Simulation sim, Level lvl, AudioManager audio)
        {
            simulation = sim;
            level = lvl;
            audioManager = audio;

            if (audioManager == null)
                audioManager = FindObjectOfType<AudioManager>();
        }

        private void LateUpdate()
        {
            if (simulation == null || audioManager == null)
                return;

            DetectAndTriggerSFXEvents();
        }

        private void DetectAndTriggerSFXEvents()
        {
            if (simulation.Status != SimulationStatus.Playing)
                return;

            var state = simulation.State;

            // Piece locked event
            if (state.PiecesLocked > lastPiecesLocked)
            {
                lastPiecesLocked = state.PiecesLocked;
                TriggerSFX(SFXEventType.PieceLock, state.ActivePiece?.Cells[0].Position ?? Vector3.zero);
            }

            // Gravity/tilt change event
            if (simulation.Gravity != lastGravity)
            {
                lastGravity = simulation.Gravity;
                TriggerSFX(SFXEventType.WorldRotate, Vector3.zero);
            }

            // TODO: Implement detection for:
            // - PieceHardDrop, PieceSoftDropTick, PieceRotate
            // - WaterSettle, WaterFlow, DrainTick, DrainRemove
            // - FreezeApply, FreezeThaw
            // - WindGustWhoosh (from Core gust events)
            // These would require additional Core state tracking or event callbacks.
        }

        private void TriggerSFX(SFXEventType eventType, Vector3 position)
        {
            if (audioManager != null)
            {
                var sfxEvent = new SFXEvent(eventType, position, volumeScale: 1f);
                audioManager.PlaySFX(sfxEvent);
            }
        }
    }
}

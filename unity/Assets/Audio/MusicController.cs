using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;

namespace Floodline.Client
{
    /// <summary>
    /// MusicController: Manages ambient music with dynamic danger-based layer crossfading.
    /// States: Calm (baseline) → Tension (building) → Danger (critical)
    /// Transitions based on DangerHeuristics calculation from Core metrics.
    /// Pure presentation layer; never modifies Core simulation.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        /// <summary>
        /// Music state machine states.
        /// </summary>
        public enum MusicState
        {
            Calm,       // Baseline, safe gameplay
            Tension,    // Building pressure, danger increasing
            Danger,     // Critical situation, high risk
        }

        [SerializeField]
        private AudioSource calmAudioSource;

        [SerializeField]
        private AudioSource tensionAudioSource;

        [SerializeField]
        private AudioSource dangerAudioSource;

        [SerializeField]
        private float dangerThresholdLow = 0.3f;   // Calm → Tension at 30% danger

        [SerializeField]
        private float dangerThresholdHigh = 0.65f; // Tension → Danger at 65% danger

        [SerializeField]
        private float crossfadeDuration = 0.5f;    // Smooth transition time

        private Simulation simulation;
        private Level level;
        private MusicState currentState = MusicState.Calm;
        private float lastDangerLevel = 0f;

        public void Initialize(Simulation sim, Level lvl)
        {
            simulation = sim;
            level = lvl;

            // Start with calm music
            PlayMusicState(MusicState.Calm);
        }

        private void LateUpdate()
        {
            if (simulation == null || level == null)
                return;

            // Calculate current danger level
            float dangerLevel = DangerHeuristics.CalculateDangerLevel(simulation, level);
            lastDangerLevel = dangerLevel;

            // Determine target music state based on danger threshold
            MusicState targetState = GetTargetMusicState(dangerLevel);

            // Transition if state changed
            if (targetState != currentState)
            {
                TransitionToState(targetState);
            }
        }

        /// <summary>
        /// Maps danger level (0-1) to music state with hysteresis to prevent flickering.
        /// </summary>
        private MusicState GetTargetMusicState(float dangerLevel)
        {
            // Hysteresis: avoid rapid state changes by keeping current state until threshold crossed
            switch (currentState)
            {
                case MusicState.Calm:
                    // Transition to Tension if danger exceeds low threshold
                    return dangerLevel > dangerThresholdLow ? MusicState.Tension : MusicState.Calm;

                case MusicState.Tension:
                    // Transition to Danger if danger exceeds high threshold
                    if (dangerLevel > dangerThresholdHigh)
                        return MusicState.Danger;
                    // Stay in Tension, or drop back to Calm if danger drops sufficiently
                    return dangerLevel < (dangerThresholdLow * 0.8f) ? MusicState.Calm : MusicState.Tension;

                case MusicState.Danger:
                    // Drop back to Tension if danger drops below high threshold
                    return dangerLevel < dangerThresholdHigh ? MusicState.Tension : MusicState.Danger;

                default:
                    return MusicState.Calm;
            }
        }

        /// <summary>
        /// Transitions to a new music state with smooth crossfade.
        /// </summary>
        private void TransitionToState(MusicState newState)
        {
            currentState = newState;
            PlayMusicState(newState);
        }

        /// <summary>
        /// Starts playing the specified music state.
        /// In a full implementation, this would crossfade between audio sources.
        /// </summary>
        private void PlayMusicState(MusicState state)
        {
            StopAllMusic();

            switch (state)
            {
                case MusicState.Calm:
                    if (calmAudioSource != null)
                    {
                        calmAudioSource.volume = 1f;
                        calmAudioSource.Play();
                    }
                    break;

                case MusicState.Tension:
                    if (tensionAudioSource != null)
                    {
                        tensionAudioSource.volume = 1f;
                        tensionAudioSource.Play();
                    }
                    break;

                case MusicState.Danger:
                    if (dangerAudioSource != null)
                    {
                        dangerAudioSource.volume = 1f;
                        dangerAudioSource.Play();
                    }
                    break;
            }
        }

        /// <summary>
        /// Stops all music sources.
        /// </summary>
        private void StopAllMusic()
        {
            if (calmAudioSource != null && calmAudioSource.isPlaying)
                calmAudioSource.Stop();
            if (tensionAudioSource != null && tensionAudioSource.isPlaying)
                tensionAudioSource.Stop();
            if (dangerAudioSource != null && dangerAudioSource.isPlaying)
                dangerAudioSource.Stop();
        }

        /// <summary>
        /// Gets the current music state (for testing and diagnostics).
        /// </summary>
        public MusicState GetCurrentState() => currentState;

        /// <summary>
        /// Gets the last calculated danger level (for testing and diagnostics).
        /// </summary>
        public float GetLastDangerLevel() => lastDangerLevel;
    }
}

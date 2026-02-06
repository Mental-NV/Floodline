using UnityEngine;
using System.Collections;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// WindGustFeedback: Detects wind gust events and provides screen-space visual feedback.
    /// - Plays wind gust whoosh SFX
    /// - Applies brief screen nudge/shake for player awareness
    /// - Pure presentation layer; no Core modifications
    /// </summary>
    public class WindGustFeedback : MonoBehaviour
    {
        [SerializeField]
        private AudioManager audioManager;

        [SerializeField]
        private RectTransform hudRoot;

        [SerializeField]
        private float nudgeMagnitude = 20f;

        [SerializeField]
        private float nudgeDuration = 0.3f;

        private Simulation simulation;
        private int lastTicksElapsed = 0;
        private bool windGustTriggeredThisTick = false;

        public void Initialize(Simulation sim, AudioManager audio, RectTransform hud = null)
        {
            simulation = sim;
            audioManager = audio;
            hudRoot = hud;

            if (audioManager == null)
                audioManager = FindObjectOfType<AudioManager>();
        }

        private void LateUpdate()
        {
            if (simulation == null)
                return;

            // Detect gust trigger: check if ticks advanced (simulation may have wind on certain ticks)
            // TODO: Hook into actual Core wind event system when available
            // For now, use placeholder gust schedule (every 60 ticks as per WindGustDisplay)
            int currentTicks = simulation.State.TicksElapsed;
            if (currentTicks != lastTicksElapsed)
            {
                lastTicksElapsed = currentTicks;

                // Placeholder: trigger gust on every 60-tick boundary
                if (currentTicks > 0 && currentTicks % 60 == 0)
                {
                    TriggerWindGustFeedback();
                }
            }
        }

        private void TriggerWindGustFeedback()
        {
            // Play wind whoosh SFX
            if (audioManager != null)
            {
                var sfxEvent = new SFXEvent(SFXEventType.WindGustWhoosh, Vector3.zero, volumeScale: 0.8f);
                audioManager.PlaySFX(sfxEvent);
            }

            // Apply screen nudge
            if (hudRoot != null)
            {
                StartCoroutine(ApplyScreenNudge());
            }
        }

        private IEnumerator ApplyScreenNudge()
        {
            Vector2 originalPosition = hudRoot.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < nudgeDuration)
            {
                // Quick horizontal nudge
                float nudgeX = Mathf.Sin(elapsed * Mathf.PI / nudgeDuration) * nudgeMagnitude;
                hudRoot.anchoredPosition = originalPosition + new Vector2(nudgeX, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }

            hudRoot.anchoredPosition = originalPosition;
        }
    }
}

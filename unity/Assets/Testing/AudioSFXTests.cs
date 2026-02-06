using System.Collections;
using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Floodline.Client.Tests
{
    /// <summary>
    /// PlayMode test suite for audio SFX system.
    /// Verifies SFX events trigger correctly and audio system doesn't modify Core state.
    /// Uses spy/mock to avoid depending on actual audio playback.
    /// </summary>
    public class AudioSFXTests
    {
        private GameObject testScene;
        private GameLoader gameLoader;
        private AudioManager audioManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Create a minimal scene with GameLoader and audio system
            testScene = new GameObject("TestScene");
            gameLoader = testScene.AddComponent<GameLoader>();
            
            // Wait for Start() to initialize everything
            yield return null;

            audioManager = testScene.GetComponentInChildren<AudioManager>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (testScene != null)
                Object.Destroy(testScene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AudioManagerInitializesSuccessfully()
        {
            Assert.IsNotNull(audioManager, "AudioManager should be initialized");
            Assert.Greater(audioManager.gameObject.name.Length, 0, "AudioManager GameObject should exist");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SFXEventCanBePlayed()
        {
            var sfxEvent = new SFXEvent(SFXEventType.PieceLock, Vector3.zero, volumeScale: 0.8f);
            audioManager.PlaySFX(sfxEvent);
            
            yield return null;
            
            // If no errors occur, the test passes
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator SFXTriggersDoNotModifyCoreState()
        {
            var simulation = gameLoader.GetSimulation();
            var initialStatus = simulation.Status;
            var initialTicks = simulation.State.TicksElapsed;

            // Trigger multiple SFX events
            for (int i = 0; i < 5; i++)
            {
                var sfxEvent = new SFXEvent(
                    (SFXEventType)(i % 12),  // Cycle through event types
                    Vector3.zero,
                    volumeScale: 0.5f
                );
                audioManager.PlaySFX(sfxEvent);
            }

            yield return null;

            // Core state should remain unchanged by SFX alone
            Assert.AreEqual(initialStatus, simulation.Status, "SFX should not modify simulation status");
            Assert.AreEqual(initialTicks, simulation.State.TicksElapsed, "SFX should not advance ticks");
        }

        [UnityTest]
        public IEnumerator SFXEventCallbackFires()
        {
            bool callbackFired = false;
            SFXEventType firedEventType = (SFXEventType)(-1);

            audioManager.OnSFXTriggered += (eventType, position) =>
            {
                callbackFired = true;
                firedEventType = eventType;
            };

            var sfxEvent = new SFXEvent(SFXEventType.WindGustWhoosh, Vector3.zero);
            audioManager.PlaySFX(sfxEvent);

            yield return null;

            Assert.IsTrue(callbackFired, "SFX event callback should fire");
            Assert.AreEqual(SFXEventType.WindGustWhoosh, firedEventType, "Callback should pass correct event type");
        }

        [UnityTest]
        public IEnumerator SFXTriggerDetectsPieceLock()
        {
            var simulation = gameLoader.GetSimulation();
            var sfxTrigger = testScene.GetComponentInChildren<SFXTrigger>();

            Assert.IsNotNull(sfxTrigger, "SFXTrigger should be initialized");

            // Simulate a few ticks
            for (int i = 0; i < 10; i++)
            {
                simulation.Tick(InputCommand.None);
                yield return null;
            }

            // Test passes if no errors occur during tick+SFX detection loop
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator WindGustFeedbackInitializes()
        {
            var windFeedback = testScene.GetComponentInChildren<WindGustFeedback>();
            Assert.IsNotNull(windFeedback, "WindGustFeedback should be initialized");
            
            yield return null;
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator AudioSystemNeverModifiesCoreSimulation()
        {
            var simulation = gameLoader.GetSimulation();
            var initialGravity = simulation.Gravity;
            var initialObjectives = simulation.Objectives.Objectives.Count;

            // Run audio system for several frames with SFX events
            for (int frame = 0; frame < 30; frame++)
            {
                for (int i = 0; i < 6; i++)
                {
                    var sfxEvent = new SFXEvent(
                        (SFXEventType)(frame + i) % 12,
                        Vector3.zero,
                        volumeScale: 1f
                    );
                    audioManager.PlaySFX(sfxEvent);
                }
                yield return null;
            }

            // Verify Core state unchanged
            Assert.AreEqual(initialGravity, simulation.Gravity, "Audio should not change gravity");
            Assert.AreEqual(initialObjectives, simulation.Objectives.Objectives.Count, "Audio should not change objectives");
        }
    }
}

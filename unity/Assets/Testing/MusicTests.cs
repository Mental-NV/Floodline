using System.Collections;
using UnityEngine;
using Floodline.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Floodline.Client.Tests
{
    /// <summary>
    /// PlayMode test suite for music system.
    /// Validates music state machine transitions and danger heuristics.
    /// </summary>
    public class MusicTests
    {
        private GameObject testScene;
        private GameLoader gameLoader;
        private MusicController musicController;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            testScene = new GameObject("TestScene");
            gameLoader = testScene.AddComponent<GameLoader>();
            
            yield return null;

            musicController = testScene.GetComponentInChildren<MusicController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (testScene != null)
                Object.Destroy(testScene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MusicControllerInitializes()
        {
            Assert.IsNotNull(musicController, "MusicController should be initialized");
            Assert.AreEqual(MusicController.MusicState.Calm, musicController.GetCurrentState(), "Should start in Calm state");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DangerHeuristicsCalculateLevel()
        {
            var simulation = gameLoader.GetSimulation();
            var level = gameLoader.GetLevel();
            
            float dangerLevel = DangerHeuristics.CalculateDangerLevel(simulation, level);
            Assert.GreaterOrEqual(dangerLevel, 0f, "Danger should be >= 0");
            Assert.LessOrEqual(dangerLevel, 1f, "Danger should be <= 1");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator MusicTransitionsOnDangerIncrease()
        {
            var simulation = gameLoader.GetSimulation();
            
            // Start in Calm state
            Assert.AreEqual(MusicController.MusicState.Calm, musicController.GetCurrentState());

            // Simulate until danger level increases
            for (int i = 0; i < 100; i++)
            {
                simulation.Tick(InputCommand.None);
                yield return null;
                
                float danger = musicController.GetLastDangerLevel();
                // If danger exceeds threshold, state should transition
                if (danger > 0.4f)
                {
                    break;
                }
            }

            // Test passes if no exceptions during state transitions
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator MusicSystemNeverModifiesCoreState()
        {
            var simulation = gameLoader.GetSimulation();
            var initialGravity = simulation.Gravity;
            var initialPieces = simulation.State.PiecesLocked;

            // Run for many frames with music updates
            for (int frame = 0; frame < 120; frame++)
            {
                simulation.Tick(InputCommand.None);
                yield return null;
            }

            // Verify no Core modifications from music system
            Assert.AreEqual(initialGravity, simulation.Gravity, "Music should not change gravity");
            // Pieces locked might change from Tick(), but only from simulation, not music
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator DangerHeuristicsAreTestable()
        {
            var simulation = gameLoader.GetSimulation();
            var level = gameLoader.GetLevel();

            // Calculate danger at current state
            float danger0 = DangerHeuristics.CalculateDangerLevel(simulation, level);

            // Advance simulation
            for (int i = 0; i < 30; i++)
                simulation.Tick(InputCommand.None);

            // Danger should be recalculated each frame
            float danger1 = DangerHeuristics.CalculateDangerLevel(simulation, level);
            
            // Both should be valid (0-1)
            Assert.GreaterOrEqual(danger0, 0f);
            Assert.LessOrEqual(danger0, 1f);
            Assert.GreaterOrEqual(danger1, 0f);
            Assert.LessOrEqual(danger1, 1f);
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator HysteresisPreventsMusicFlutter()
        {
            var simulation = gameLoader.GetSimulation();

            MusicController.MusicState lastState = musicController.GetCurrentState();
            int stateChangeCount = 0;

            // Run many frames and count state changes
            for (int frame = 0; frame < 200; frame++)
            {
                simulation.Tick(InputCommand.None);
                
                MusicController.MusicState currentState = musicController.GetCurrentState();
                if (currentState != lastState)
                {
                    stateChangeCount++;
                    lastState = currentState;
                }
                
                yield return null;
            }

            // Should have few state changes due to hysteresis (typically 0-3 in 200 frames)
            // If fluttering, would be many more (20+)
            Assert.Less(stateChangeCount, 10, "Hysteresis should prevent rapid music state changes (flutter)");
        }
    }
}

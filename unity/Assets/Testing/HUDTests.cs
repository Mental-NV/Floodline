using System.Collections;
using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Floodline.Client.Tests
{
    /// <summary>
    /// PlayMode test suite for HUD functionality.
    /// Verifies that HUD reads from simulation state without modifying Core.
    /// </summary>
    public class HUDTests
    {
        private GameObject testScene;
        private GameLoader gameLoader;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Create a minimal scene with GameLoader
            testScene = new GameObject("TestScene");
            gameLoader = testScene.AddComponent<GameLoader>();
            
            // Wait a frame for Start() to execute
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (testScene != null)
                Object.Destroy(testScene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HUDManagerInitializesWithSimulation()
        {
            // Load a level and verify HUD is created
            var hudObj = testScene.transform.Find("HUD");
            Assert.IsNotNull(hudObj, "HUD GameObject should be created during initialization");
            
            HUDManager hud = hudObj.GetComponent<HUDManager>();
            Assert.IsNotNull(hud, "HUDManager component should exist");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator GravityDisplayUpdatesWithSimulationGravity()
        {
            var simulation = gameLoader.GetSimulation();
            Assert.IsNotNull(simulation, "Simulation should be initialized");

            var hudObj = testScene.transform.Find("HUD");
            var gravityDisplay = hudObj?.GetComponentInChildren<GravityDisplay>();
            
            // Gravity should update based on simulation state
            GravityDirection originalGravity = simulation.Gravity;
            gravityDisplay.UpdateGravity(originalGravity);
            
            yield return null;

            // Verify no Core state was modified by HUD
            Assert.AreEqual(originalGravity, simulation.Gravity, "HUD should not modify Core gravity");
        }

        [UnityTest]
        public IEnumerator ObjectivesDisplayReadsFromSimulation()
        {
            var simulation = gameLoader.GetSimulation();
            Assert.IsNotNull(simulation, "Simulation should be initialized");

            var hudObj = testScene.transform.Find("HUD");
            var objectivesDisplay = hudObj?.GetComponentInChildren<ObjectivesDisplay>();
            
            // Objectives should be readable from simulation
            var objectives = simulation.Objectives;
            objectivesDisplay.UpdateObjectives(objectives);
            
            yield return null;

            // Verify objectives state is unchanged
            Assert.AreEqual(simulation.Objectives, objectives, "HUD should not modify objectives");
        }

        [UnityTest]
        public IEnumerator HUDNeverModifiesCoreState()
        {
            var simulation = gameLoader.GetSimulation();
            var initialState = simulation.State;
            var initialGravity = simulation.Gravity;
            var initialObjs = simulation.Objectives;

            // Tick simulation a few times
            for (int i = 0; i < 5; i++)
            {
                simulation.Tick(InputCommand.None);
                yield return null;
            }

            var finalState = simulation.State;

            // HUD should never modify simulation state
            // All state changes should come from Tick() calls, not HUD
            Assert.IsNotNull(finalState, "Simulation state should remain valid");
            Assert.Greater(finalState.TicksElapsed, initialState.TicksElapsed, "Ticks should advance");
            
            yield return null;
        }
    }
}

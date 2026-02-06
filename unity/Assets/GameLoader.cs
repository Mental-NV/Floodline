using System;
using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;

namespace Floodline.Client
{
    /// <summary>
    /// Bootstrap loader for a Floodline level.
    /// Loads a level JSON, creates a simulation, and integrates input, camera, and rendering.
    /// Orchestrates the main game loop: Input → Simulation.Tick → Render.
    /// </summary>
    public class GameLoader : MonoBehaviour
    {
        [SerializeField]
        private string levelPath = "levels/minimal_level.json";

        [SerializeField]
        private uint seed = 0;

        private Simulation simulation;
        private Level level;
        private InputManager inputManager;
        private CameraManager cameraManager;
        private GridRenderer gridRenderer;
        private HUDManager hudManager;
        private AudioManager audioManager;
        private SFXTrigger sfxTrigger;
        private WindGustFeedback windGustFeedback;

        private void Start()
        {
            LoadAndInitializeSimulation();
        }

        private void Update()
        {
            if (simulation != null && simulation.Status == SimulationStatus.Playing && inputManager != null)
            {
                // Sample input for this tick, applying DAS/ARR and buffering rules
                var command = inputManager.SampleAndGenerateCommand();

                // Tick the simulation with the generated command
                simulation.Tick(command);

                // Update grid visualization to reflect new state
                if (gridRenderer != null)
                {
                    gridRenderer.UpdateGridVisualization(simulation);
                }

                // Handle camera snap view input (F1-F4 per Input_Feel_v0_2)
                HandleCameraInput();

                if (simulation.Status != SimulationStatus.Playing)
                {
                    LogSimulationResult();
                }
            }
        }

        private void LoadAndInitializeSimulation()
        {
            try
            {
                // Construct the full path to the level file relative to the project root
                string projectRoot = System.IO.Path.Combine(Application.dataPath, "..");
                string fullLevelPath = System.IO.Path.Combine(projectRoot, levelPath);

                // Load the level
                if (!System.IO.File.Exists(fullLevelPath))
                {
                    Debug.LogError($"Level file not found: {fullLevelPath}");
                    return;
                }

                string levelJson = System.IO.File.ReadAllText(fullLevelPath);
                level = LevelLoader.Load(levelJson, levelPath);

                // Create a deterministic PRNG seeded from the command-line arg or default
                var prng = new Pcg32(seed);

                // Create the simulation
                simulation = new Simulation(level, prng);

                // Initialize input management
                inputManager = gameObject.AddComponent<InputManager>();
                inputManager.InputSource = gameObject.AddComponent<DeviceInputSource>();

                // Initialize camera management
                cameraManager = gameObject.AddComponent<CameraManager>();

                // Initialize grid rendering
                var gridRenderObj = new GameObject("GridRenderer");
                gridRenderObj.transform.parent = transform;
                gridRenderer = gridRenderObj.AddComponent<GridRenderer>();

                // Initialize HUD management
                var hudObj = new GameObject("HUD");
                hudObj.transform.parent = transform;
                hudManager = hudObj.AddComponent<HUDManager>();

                // Initialize audio management
                var audioObj = new GameObject("AudioManager");
                audioObj.transform.parent = transform;
                audioManager = audioObj.AddComponent<AudioManager>();

                // Initialize SFX triggering
                var sfxObj = new GameObject("SFXTrigger");
                sfxObj.transform.parent = transform;
                sfxTrigger = sfxObj.AddComponent<SFXTrigger>();

                // Initialize wind gust feedback
                var windObj = new GameObject("WindGustFeedback");
                windObj.transform.parent = transform;
                windGustFeedback = windObj.AddComponent<WindGustFeedback>();

                // Perform initialization on all systems
                gridRenderer.UpdateGridVisualization(simulation);
                hudManager.Initialize(simulation, level);
                sfxTrigger.Initialize(simulation, level, audioManager);
                windGustFeedback.Initialize(simulation, audioManager, hudManager.GetHUDRoot());

                Debug.Log($"Simulation initialized: level={level.Meta.Id}, seed={seed}, status={simulation.Status}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load simulation: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void HandleCameraInput()
        {
            if (cameraManager == null)
                return;

            // F1-F4: Camera snap views (NE, NW, SE, SW per Input_Feel_v0_2 §7.2)
            if (Input.GetKeyDown(KeyCode.F1))
                cameraManager.SnapTo(CameraManager.SnapView.NE);
            else if (Input.GetKeyDown(KeyCode.F2))
                cameraManager.SnapTo(CameraManager.SnapView.NW);
            else if (Input.GetKeyDown(KeyCode.F3))
                cameraManager.SnapTo(CameraManager.SnapView.SE);
            else if (Input.GetKeyDown(KeyCode.F4))
                cameraManager.SnapTo(CameraManager.SnapView.SW);
        }

        /// <summary>
        /// Public accessor for the simulation (used by other systems like Renderer)
        /// </summary>
        public Simulation GetSimulation() => simulation;

        private void LogSimulationResult()
        {
            Debug.Log($"Simulation finished: status={simulation.Status}");
            if (!string.IsNullOrEmpty(simulation.Status.ToString()))
            {
                var state = simulation.State;
                Debug.Log($"  Final objective status: {(state.Objective?.Status.ToString() ?? "none")}");
            }
        }
    }
}


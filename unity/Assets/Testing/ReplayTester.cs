using System;
using System.IO;
using UnityEngine;
using Floodline.Core.Levels;
using Floodline.Core.Replay;

namespace Floodline.Client
{
    /// <summary>
    /// Batch mode runner for replay parity testing.
    /// Executed by CI in headless mode to verify determinism against CLI.
    /// 
    /// Usage: Unity -batchmode -nographics -quit -executeMethod ReplayTester.ExecuteReplay 
    ///                --replay-file <path> --level-file <path>
    /// </summary>
    public static class ReplayTester
    {
        private const int SuccessExitCode = 0;
        private const int InvalidArgsExitCode = 2;
        private const int FailureExitCode = 1;

        /// <summary>
        /// Entry point for batch mode replay execution.
        /// Parses command-line args, loads replay and level, executes replay, outputs hash.
        /// </summary>
        public static void ExecuteReplay()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                
                string replayPath = null;
                string levelPath = null;
                string outputPath = null;

                // Parse command-line arguments
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--replay-file" && i + 1 < args.Length)
                    {
                        replayPath = args[i + 1];
                        i++;
                    }
                    else if (args[i] == "--level-file" && i + 1 < args.Length)
                    {
                        levelPath = args[i + 1];
                        i++;
                    }
                    else if (args[i] == "--output-file" && i + 1 < args.Length)
                    {
                        outputPath = args[i + 1];
                        i++;
                    }
                }

                if (string.IsNullOrWhiteSpace(replayPath) || string.IsNullOrWhiteSpace(levelPath))
                {
                    WriteError("Missing required arguments: --replay-file and --level-file");
                    Environment.Exit(InvalidArgsExitCode);
                    return;
                }

                if (!File.Exists(replayPath))
                {
                    WriteError($"Replay file not found: {replayPath}");
                    Environment.Exit(FailureExitCode);
                    return;
                }

                if (!File.Exists(levelPath))
                {
                    WriteError($"Level file not found: {levelPath}");
                    Environment.Exit(FailureExitCode);
                    return;
                }

                // Load level
                string levelJson = File.ReadAllText(levelPath);
                Level level = LevelLoader.Load(levelJson, levelPath);

                // Load replay
                string replayJson = File.ReadAllText(replayPath);
                ReplayFile replay = ReplaySerializer.Deserialize(replayJson);

                // Validate replay metadata
                ValidateReplay(replay, level);

                // Execute replay
                var runner = new ReplayRunner(replay, level);
                runner.ExecuteReplay();

                // Output result
                string hash = runner.ComputeDeterminismHash();
                string result = $"DeterminismHash: {hash}";

                WriteOutput(result);

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    File.WriteAllText(outputPath, result);
                }

                Environment.Exit(SuccessExitCode);
            }
            catch (Exception ex)
            {
                WriteError($"Replay execution failed: {ex.Message}\n{ex.StackTrace}");
                Environment.Exit(FailureExitCode);
            }
        }

        private static void ValidateReplay(ReplayFile replay, Level level)
        {
            const int TickRate = 60;

            if (!string.Equals(replay.Meta.ReplayVersion, ReplayFormat.ReplayVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Replay version mismatch: expected {ReplayFormat.ReplayVersion}.");
            }

            if (!string.Equals(replay.Meta.RulesVersion, RulesVersion.Current, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Rules version mismatch: expected {RulesVersion.Current}.");
            }

            if (!string.Equals(replay.Meta.InputEncoding, ReplayFormat.InputEncoding, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Input encoding mismatch: expected {ReplayFormat.InputEncoding}.");
            }

            if (replay.Meta.TickRate != TickRate)
            {
                throw new InvalidOperationException($"Replay tick rate mismatch: expected {TickRate}.");
            }

            if (!string.Equals(replay.Meta.LevelId, level.Meta.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Replay level id does not match the provided level.");
            }

            string levelJson = File.ReadAllText(FindInputFileRelativeToProject("levels", level.Meta.Id + ".json"));
            string levelHash = Floodline.Core.Levels.LevelHash.Compute(levelJson);
            if (!string.Equals(replay.Meta.LevelHash, levelHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Replay level hash does not match the provided level.");
            }
        }

        private static string FindInputFileRelativeToProject(string relativePath, string filename)
        {
            string projectRoot = Path.Combine(Application.dataPath, "..");
            return Path.Combine(projectRoot, relativePath, filename);
        }

        private static void WriteOutput(string message)
        {
            Debug.Log(message);
        }

        private static void WriteError(string message)
        {
            Debug.LogError(message);
        }
    }
}

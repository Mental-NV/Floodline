using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Replay;

namespace Floodline.Client
{
    /// <summary>
    /// Loads and executes a replay deterministically, computing a determinism hash for parity testing.
    /// Used by CI to verify Unity client maintains simulation determinism vs CLI.
    /// </summary>
    public class ReplayRunner
    {
        private Simulation simulation;
        private readonly List<InputCommand> commands;
        private readonly ReplayFile replayFile;
        private int ticksExecuted;

        public ReplayRunner(ReplayFile replay, Level level)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            replayFile = replay;
            commands = BuildReplayCommands(replay);

            // Create simulation with the seed from the replay
            var prng = new Floodline.Core.Random.Pcg32((ulong)replay.Meta.Seed);
            simulation = new Simulation(level, prng);
            ticksExecuted = 0;
        }

        /// <summary>
        /// Executes the entire replay deterministically.
        /// </summary>
        public void ExecuteReplay()
        {
            for (int tick = 0; tick < commands.Count; tick++)
            {
                if (simulation.State.Status != SimulationStatus.InProgress)
                {
                    break;
                }

                InputCommand command = commands[tick];
                simulation.Tick(command);
                ticksExecuted++;
            }
        }

        /// <summary>
        /// Computes the determinism hash of the simulation state.
        /// This hash should match the CLI output for parity testing.
        /// </summary>
        public string ComputeDeterminismHash()
        {
            return simulation.ComputeDeterminismHash();
        }

        /// <summary>
        /// Gets the final simulation status.
        /// </summary>
        public SimulationStatus GetStatus()
        {
            return simulation.State.Status;
        }

        /// <summary>
        /// Gets the number of ticks executed.
        /// </summary>
        public int GetTicksExecuted()
        {
            return ticksExecuted;
        }

        /// <summary>
        /// Gets the replay metadata.
        /// </summary>
        public ReplayMeta GetMetadata()
        {
            return replayFile.Meta;
        }

        private static List<InputCommand> BuildReplayCommands(ReplayFile replay)
        {
            List<InputCommand> commands = new(replay.Inputs.Count);
            int expectedTick = 0;

            foreach (ReplayInput input in replay.Inputs)
            {
                if (input.Tick != expectedTick)
                {
                    throw new ArgumentException("Replay inputs must be contiguous starting at tick 0.");
                }

                commands.Add(input.Command);
                expectedTick++;
            }

            return commands;
        }
    }
}

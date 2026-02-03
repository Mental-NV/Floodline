using System;
using System.Collections.Generic;
using System.IO;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;

namespace Floodline.Cli;

public static class CliApp
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || HasHelpFlag(args))
        {
            PrintUsage(output);
            return 0;
        }

        string? levelPath = null;
        string? inputsPath = null;
        int? ticks = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--level":
                case "-l":
                    if (!TryReadValue(args, ref i, out levelPath))
                    {
                        return Fail(error, "Missing value for --level.");
                    }

                    break;
                case "--inputs":
                case "-i":
                    if (!TryReadValue(args, ref i, out inputsPath))
                    {
                        return Fail(error, "Missing value for --inputs.");
                    }

                    break;
                case "--ticks":
                case "-t":
                    if (!TryReadValue(args, ref i, out string? ticksValue))
                    {
                        return Fail(error, "Missing value for --ticks.");
                    }

                    if (!int.TryParse(ticksValue, out int parsedTicks) || parsedTicks < 0)
                    {
                        return Fail(error, "Ticks must be a non-negative integer.");
                    }

                    ticks = parsedTicks;
                    break;
                default:
                    return Fail(error, $"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(levelPath))
        {
            return Fail(error, "Missing required --level <path> argument.");
        }

        if (!File.Exists(levelPath))
        {
            return Fail(error, $"Level file not found: {levelPath}");
        }

        Level level;
        try
        {
            string json = File.ReadAllText(levelPath);
            level = LevelLoader.Load(json);
        }
        catch (Exception ex)
        {
            return Fail(error, $"Failed to load level: {ex.Message}");
        }

        List<InputCommand> commands = [];
        if (!string.IsNullOrWhiteSpace(inputsPath))
        {
            if (!File.Exists(inputsPath))
            {
                return Fail(error, $"Input script not found: {inputsPath}");
            }

            try
            {
                foreach (string line in File.ReadAllLines(inputsPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    if (!Enum.TryParse(trimmed, ignoreCase: true, out InputCommand command))
                    {
                        return Fail(error, $"Unknown input command '{trimmed}'.");
                    }

                    commands.Add(command);
                }
            }
            catch (Exception ex)
            {
                return Fail(error, $"Failed to read input script: {ex.Message}");
            }
        }

        int ticksToRun = ticks ?? commands.Count;
        if (ticksToRun < commands.Count)
        {
            return Fail(error, "--ticks is smaller than the number of input commands.");
        }

        Simulation simulation = new(level, new Pcg32(level.Meta.Seed));

        for (int tick = 0; tick < ticksToRun; tick++)
        {
            if (simulation.State.Status != SimulationStatus.InProgress)
            {
                break;
            }

            InputCommand command = tick < commands.Count ? commands[tick] : InputCommand.None;
            simulation.Tick(command);
        }

        WriteSummary(output, simulation);
        return 0;
    }

    private static void WriteSummary(TextWriter output, Simulation simulation)
    {
        output.WriteLine($"Status: {simulation.State.Status}");
        output.WriteLine($"TicksElapsed: {simulation.State.TicksElapsed}");
        output.WriteLine($"PiecesLocked: {simulation.State.PiecesLocked}");
        output.WriteLine($"Gravity: {simulation.Gravity}");

        if (simulation.Objectives.Objectives.Count == 0)
        {
            output.WriteLine("Objectives: (none)");
        }
        else
        {
            output.WriteLine("Objectives:");
            foreach (ObjectiveProgress objective in simulation.Objectives.Objectives)
            {
                output.WriteLine(
                    $"- {objective.Type}: {objective.Current}/{objective.Target} (Completed={objective.Completed})");
            }
        }

        output.WriteLine("DeterminismHash: TODO");
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("Floodline CLI");
        output.WriteLine("Usage:");
        output.WriteLine("  Floodline.Cli --level <path> [--inputs <path>] [--ticks <count>]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --level, -l     Path to level JSON file.");
        output.WriteLine("  --inputs, -i    Path to input script (one command per line).");
        output.WriteLine("  --ticks, -t     Total ticks to simulate (defaults to number of input commands).");
        output.WriteLine("  --help, -h      Show this help.");
    }

    private static bool HasHelpFlag(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg is "--help" or "-h" or "/?")
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        int next = index + 1;
        if (next >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[next];
        index = next;
        return true;
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        error.WriteLine("Use --help to see usage.");
        return 2;
    }
}

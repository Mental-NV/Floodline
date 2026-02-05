using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Floodline.Core.Replay;
using Floodline.Cli.Validation;

namespace Floodline.Cli;

public static class CliApp
{
    private const int TickRate = 60;

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
        string? campaignPath = null;
        string? recordPath = null;
        string? replayPath = null;
        string? solutionPath = null;
        int? ticks = null;
        bool validateOnly = false;
        bool validateCampaign = false;
        bool requireWin = false;

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
                case "--campaign":
                    if (!TryReadValue(args, ref i, out campaignPath))
                    {
                        return Fail(error, "Missing value for --campaign.");
                    }

                    break;
                case "--record":
                    if (!TryReadValue(args, ref i, out recordPath))
                    {
                        return Fail(error, "Missing value for --record.");
                    }

                    break;
                case "--replay":
                    if (!TryReadValue(args, ref i, out replayPath))
                    {
                        return Fail(error, "Missing value for --replay.");
                    }

                    break;
                case "--solution":
                    requireWin = true;
                    if (TryReadOptionalValue(args, ref i, out string? solutionValue))
                    {
                        solutionPath = solutionValue;
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
                case "--validate":
                    validateOnly = true;
                    break;
                case "--validate-campaign":
                case "--validate-levels":
                    validateCampaign = true;
                    break;
                default:
                    return Fail(error, $"Unknown argument '{arg}'.");
            }
        }

        if (validateCampaign)
        {
            if (validateOnly)
            {
                return Fail(error, "--validate-campaign cannot be combined with --validate.");
            }

            if (!string.IsNullOrWhiteSpace(levelPath) ||
                !string.IsNullOrWhiteSpace(inputsPath) ||
                !string.IsNullOrWhiteSpace(recordPath) ||
                !string.IsNullOrWhiteSpace(replayPath) ||
                requireWin ||
                ticks.HasValue)
            {
                return Fail(error, "--validate-campaign cannot be combined with --level, --inputs, --ticks, --record, --replay, or --solution.");
            }

            LevelValidationResult validation = CampaignValidator.ValidateFile(campaignPath);
            if (!validation.IsValid)
            {
                foreach (LevelValidationError validationError in validation.Errors)
                {
                    error.WriteLine($"{validationError.FilePath}:{validationError.JsonPointer} [{validationError.RuleId}] {validationError.Message}");
                }

                return 2;
            }

            output.WriteLine("Campaign validation OK");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(levelPath))
        {
            return Fail(error, "Missing required --level <path> argument.");
        }

        if (!File.Exists(levelPath))
        {
            return Fail(error, $"Level file not found: {levelPath}");
        }

        bool usingSolution = requireWin;
        bool usingReplay = usingSolution || !string.IsNullOrWhiteSpace(replayPath);

        if (usingSolution && !string.IsNullOrWhiteSpace(replayPath))
        {
            return Fail(error, "--solution cannot be combined with --replay.");
        }

        if (!string.IsNullOrWhiteSpace(recordPath) && usingReplay)
        {
            return Fail(error, "Choose either --record or --replay/--solution, not both.");
        }

        if (usingReplay && !string.IsNullOrWhiteSpace(inputsPath))
        {
            return Fail(error, "--inputs cannot be used with --replay/--solution (replay uses recorded inputs).");
        }

        if (usingReplay && ticks.HasValue)
        {
            return Fail(error, "--ticks cannot be used with --replay/--solution (replay uses recorded ticks).");
        }

        if (validateOnly)
        {
            if (!string.IsNullOrWhiteSpace(inputsPath) ||
                !string.IsNullOrWhiteSpace(recordPath) ||
                !string.IsNullOrWhiteSpace(replayPath) ||
                requireWin ||
                ticks.HasValue)
            {
                return Fail(error, "--validate cannot be combined with --inputs, --ticks, --record, --replay, or --solution.");
            }

            LevelValidationResult validation = LevelValidator.ValidateFile(levelPath);
            if (!validation.IsValid)
            {
                foreach (LevelValidationError validationError in validation.Errors)
                {
                    error.WriteLine($"{validationError.FilePath}:{validationError.JsonPointer} [{validationError.RuleId}] {validationError.Message}");
                }

                return 2;
            }

            output.WriteLine("Validation OK");
            return 0;
        }

        Level level;
        string levelJson;
        string levelHash;
        try
        {
            levelJson = File.ReadAllText(levelPath);
            level = LevelLoader.Load(levelJson);
            levelHash = LevelHash.Compute(levelJson);
        }
        catch (Exception ex)
        {
            return Fail(error, $"Failed to load level: {ex.Message}");
        }

        string? resolvedReplayPath = replayPath;
        if (requireWin)
        {
            resolvedReplayPath = solutionPath;
            if (string.IsNullOrWhiteSpace(resolvedReplayPath))
            {
                resolvedReplayPath = Path.Combine("levels", "solutions", $"{level.Meta.Id}.replay.json");
            }
        }

        List<InputCommand> commands;
        int ticksToRun;
        int seed;
        if (!string.IsNullOrWhiteSpace(resolvedReplayPath))
        {
            if (!File.Exists(resolvedReplayPath))
            {
                return Fail(error, requireWin
                    ? $"Solution replay not found: {resolvedReplayPath}"
                    : $"Replay file not found: {resolvedReplayPath}");
            }

            ReplayFile replay;
            try
            {
                string replayJson = File.ReadAllText(resolvedReplayPath);
                replay = ReplaySerializer.Deserialize(replayJson);
            }
            catch (Exception ex)
            {
                return Fail(error, $"Failed to read replay: {ex.Message}");
            }

            if (!string.Equals(replay.Meta.ReplayVersion, ReplayFormat.ReplayVersion, StringComparison.Ordinal))
            {
                return Fail(error, $"Replay version mismatch: expected {ReplayFormat.ReplayVersion}.");
            }

            if (!string.Equals(replay.Meta.RulesVersion, RulesVersion.Current, StringComparison.Ordinal))
            {
                return Fail(error, $"Rules version mismatch: expected {RulesVersion.Current}.");
            }

            if (!string.Equals(replay.Meta.InputEncoding, ReplayFormat.InputEncoding, StringComparison.Ordinal))
            {
                return Fail(error, $"Input encoding mismatch: expected {ReplayFormat.InputEncoding}.");
            }

            if (replay.Meta.TickRate != TickRate)
            {
                return Fail(error, $"Replay tick rate mismatch: expected {TickRate}.");
            }

            if (replay.Meta.Seed < 0)
            {
                return Fail(error, "Replay seed must be non-negative.");
            }

            if (!string.Equals(replay.Meta.LevelId, level.Meta.Id, StringComparison.Ordinal))
            {
                return Fail(error, "Replay level id does not match the provided level.");
            }

            if (!string.Equals(replay.Meta.LevelHash, levelHash, StringComparison.Ordinal))
            {
                return Fail(error, "Replay level hash does not match the provided level.");
            }

            try
            {
                commands = BuildReplayCommands(replay);
            }
            catch (Exception ex)
            {
                return Fail(error, $"Replay inputs invalid: {ex.Message}");
            }

            ticksToRun = commands.Count;
            seed = replay.Meta.Seed;
        }
        else
        {
            commands = [];
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

            ticksToRun = ticks ?? commands.Count;
            if (ticksToRun < commands.Count)
            {
                return Fail(error, "--ticks is smaller than the number of input commands.");
            }

            seed = (int)level.Meta.Seed;
        }

        Simulation simulation = new(level, new Pcg32((ulong)seed));
        int ticksExecuted = 0;

        for (int tick = 0; tick < ticksToRun; tick++)
        {
            if (simulation.State.Status != SimulationStatus.InProgress)
            {
                break;
            }

            InputCommand command = tick < commands.Count ? commands[tick] : InputCommand.None;
            simulation.Tick(command);
            ticksExecuted++;
        }

        if (!string.IsNullOrWhiteSpace(recordPath))
        {
            try
            {
                List<ReplayInput> recordedInputs = new(ticksExecuted);
                for (int tick = 0; tick < ticksExecuted; tick++)
                {
                    InputCommand command = tick < commands.Count ? commands[tick] : InputCommand.None;
                    recordedInputs.Add(new ReplayInput(tick, command));
                }

                ReplayMeta meta = new(
                    ReplayFormat.ReplayVersion,
                    RulesVersion.Current,
                    level.Meta.Id,
                    levelHash,
                    seed,
                    TickRate,
                    RuntimeInformation.OSDescription,
                    ReplayFormat.InputEncoding);

                ReplayFile replayFile = new(meta, recordedInputs);
                string replayJson = ReplaySerializer.Serialize(replayFile);
                File.WriteAllText(recordPath, replayJson);
            }
            catch (Exception ex)
            {
                return Fail(error, $"Failed to write replay: {ex.Message}");
            }
        }

        if (requireWin && simulation.State.Status != SimulationStatus.Won)
        {
            return Fail(error, $"Solution did not win: status {simulation.State.Status} after {ticksExecuted} ticks.");
        }

        WriteSummary(output, simulation);
        return 0;
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

        output.WriteLine($"DeterminismHash: {simulation.ComputeDeterminismHash()}");
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("Floodline CLI");
        output.WriteLine("Usage:");
        output.WriteLine("  Floodline.Cli --level <path> [--inputs <path>] [--ticks <count>] [--record <path>]");
        output.WriteLine("  Floodline.Cli --level <path> --replay <path>");
        output.WriteLine("  Floodline.Cli --level <path> --solution [path]");
        output.WriteLine("  Floodline.Cli --validate-campaign [--campaign <path>]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --level, -l     Path to level JSON file.");
        output.WriteLine("  --inputs, -i    Path to input script (one command per line).");
        output.WriteLine("  --campaign      Path to campaign JSON file (defaults to levels/campaign.v0.2.0.json).");
        output.WriteLine("  --record        Write a replay file for the run.");
        output.WriteLine("  --replay        Replay from a replay file (ignores --inputs/--ticks).");
        output.WriteLine("  --solution      Run a replay solution (defaults to levels/solutions/{levelId}.replay.json).");
        output.WriteLine("  --ticks, -t     Total ticks to simulate (defaults to number of input commands).");
        output.WriteLine("  --validate      Validate the level JSON and exit.");
        output.WriteLine("  --validate-campaign  Validate campaign JSON and referenced levels.");
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

    private static bool TryReadOptionalValue(string[] args, ref int index, out string? value)
    {
        int next = index + 1;
        if (next >= args.Length)
        {
            value = null;
            return false;
        }

        string candidate = args[next];
        if (candidate.Length > 0 && candidate[0] == '-')
        {
            value = null;
            return false;
        }

        value = candidate;
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

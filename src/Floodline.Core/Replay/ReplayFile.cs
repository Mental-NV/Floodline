using System.Collections.Generic;
using Floodline.Core.Movement;

namespace Floodline.Core.Replay;

/// <summary>
/// Replay format constants for v0.1.
/// </summary>
public static class ReplayFormat
{
    public const string ReplayVersion = "0.1.2";
    public const string InputEncoding = "command-v2";
}

/// <summary>
/// Replay metadata header.
/// </summary>
public sealed record ReplayMeta(
    string ReplayVersion,
    string RulesVersion,
    string LevelId,
    string LevelHash,
    int Seed,
    int TickRate,
    string Platform,
    string InputEncoding
);

/// <summary>
/// Single per-tick input command.
/// </summary>
public readonly record struct ReplayInput(int Tick, InputCommand Command);

/// <summary>
/// Replay file contents (header + per-tick inputs).
/// </summary>
public sealed record ReplayFile(ReplayMeta Meta, IReadOnlyList<ReplayInput> Inputs);

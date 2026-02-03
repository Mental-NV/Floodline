namespace Floodline.Core.Levels;

/// <summary>
/// Data for a single voxel in the initial grid state.
/// </summary>
public record VoxelData(
    Int3 Pos,
    OccupancyType Type,
    string? MaterialId = null,
    DrainConfig? Drain = null
);

/// <summary>
/// Defines the drain scope for water removal.
/// </summary>
public enum DrainScope
{
    /// <summary>
    /// Only the drain cell itself.
    /// </summary>
    Self,

    /// <summary>
    /// The six orthogonal adjacent neighbors.
    /// </summary>
    Adj6,

    /// <summary>
    /// All 26 adjacent neighbors (including diagonals).
    /// </summary>
    Adj26
}

/// <summary>
/// Configuration for a drain tile.
/// </summary>
/// <param name="RatePerResolve">Number of water units removed per resolve.</param>
/// <param name="Scope">The drain scope.</param>
public sealed record DrainConfig(int RatePerResolve, DrainScope Scope)
{
    /// <summary>
    /// Default drain configuration (rate 1, scope SELF).
    /// </summary>
    public static DrainConfig Default { get; } = new(1, DrainScope.Self);
}

/// <summary>
/// Configuration for world rotation constraints.
/// </summary>
public record RotationConfig(
    int? MaxRotations = null,
    int? TiltBudget = null,
    int? CooldownTicks = null,
    string[]? AllowedDirections = null,
    RotationAxis[]? AllowedPieceRotationAxes = null
);

/// <summary>
/// Configuration for the piece bag.
/// </summary>
public record BagConfig(
    string Type,
    string[]? Sequence = null,
    Dictionary<string, int>? Weights = null
);

/// <summary>
/// Generic configuration for an objective.
/// </summary>
public record ObjectiveConfig(
    string Type,
    Dictionary<string, object> Params
);

/// <summary>
/// Generic configuration for a hazard (e.g., Wind).
/// </summary>
public record HazardConfig(
    string Type,
    Dictionary<string, object> Params
);

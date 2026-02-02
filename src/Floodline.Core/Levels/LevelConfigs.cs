namespace Floodline.Core.Levels;

/// <summary>
/// Data for a single voxel in the initial grid state.
/// </summary>
public record VoxelData(
    Int3 Pos,
    OccupancyType Type,
    string? MaterialId = null
);

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

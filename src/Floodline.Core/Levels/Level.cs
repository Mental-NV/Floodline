namespace Floodline.Core.Levels;

/// <summary>
/// Canonical level definition.
/// </summary>
public record Level(
    LevelMeta Meta,
    Int3 Bounds,
    List<VoxelData> InitialVoxels,
    List<ObjectiveConfig> Objectives,
    RotationConfig Rotation,
    BagConfig Bag,
    List<HazardConfig> Hazards,
    AbilitiesConfig? Abilities = null,
    ConstraintsConfig? Constraints = null
);

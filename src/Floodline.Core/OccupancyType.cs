namespace Floodline.Core;

/// <summary>
/// Defines the canonical occupancy types for a grid cell as per Simulation Rules §2.2.
/// </summary>
public enum OccupancyType
{
    /// <summary>
    /// Cell is empty.
    /// </summary>
    Empty = 0,

    /// <summary>
    /// Cell contains a movable solid block.
    /// </summary>
    Solid = 1,

    /// <summary>
    /// Cell contains an immovable wall.
    /// </summary>
    Wall = 2,

    /// <summary>
    /// Cell contains immovable bedrock.
    /// </summary>
    Bedrock = 3,

    /// <summary>
    /// Cell contains one unit of water.
    /// </summary>
    Water = 4,

    /// <summary>
    /// Cell contains frozen water (ice).
    /// </summary>
    Ice = 5,

    /// <summary>
    /// Cell contains a porous material (passable for water, non-occupiable by water).
    /// </summary>
    Porous = 6,

    /// <summary>
    /// Cell contains a drain.
    /// </summary>
    Drain = 7
}

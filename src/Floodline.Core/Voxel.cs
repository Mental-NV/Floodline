namespace Floodline.Core;

/// <summary>
/// Represents the contents of a single grid cell.
/// </summary>
/// <param name="Type">The occupancy type of the cell.</param>
/// <param name="MaterialId">Optional material identifier for solids and porous cells.</param>
/// <param name="Anchored">Whether the voxel is anchored (immovable during settling).</param>
public record Voxel(OccupancyType Type, string? MaterialId = null, bool Anchored = false)
{
    /// <summary>
    /// Gets a static instance of an empty voxel.
    /// </summary>
    public static readonly Voxel Empty = new(OccupancyType.Empty);

    /// <summary>
    /// Gets a static instance of a water voxel.
    /// </summary>
    public static readonly Voxel Water = new(OccupancyType.Water);
}

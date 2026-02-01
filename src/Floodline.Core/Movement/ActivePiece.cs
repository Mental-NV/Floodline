namespace Floodline.Core.Movement;

/// <summary>
/// Represents the active falling piece with its current state and movement logic.
/// Implements collision detection per Simulation_Rules_v0_2.md §4.
/// </summary>
/// <param name="piece">The oriented piece definition.</param>
/// <param name="origin">The initial origin position.</param>
public sealed class ActivePiece(OrientedPiece piece, Int3 origin)
{
    /// <summary>
    /// Gets the oriented piece definition (shape and voxels).
    /// </summary>
    public OrientedPiece Piece { get; } = piece ?? throw new ArgumentNullException(nameof(piece));

    /// <summary>
    /// Gets or sets the origin position of the piece in grid coordinates.
    /// </summary>
    public Int3 Origin { get; private set; } = origin;

    /// <summary>
    /// Gets the absolute world positions of all voxels in the piece.
    /// </summary>
    /// <returns>A list of absolute grid positions.</returns>
    public IReadOnlyList<Int3> GetWorldPositions()
    {
        List<Int3> positions = new(Piece.Voxels.Count);
        foreach (Int3 voxel in Piece.Voxels)
        {
            positions.Add(Origin + voxel);
        }
        return positions;
    }

    /// <summary>
    /// Attempts to translate the piece by the specified delta.
    /// Per Simulation_Rules_v0_2.md §4.2: move is valid if all destination cells are in bounds and are EMPTY or WATER.
    /// </summary>
    /// <param name="delta">The translation delta.</param>
    /// <param name="grid">The grid to check collision against.</param>
    /// <returns>True if the translation succeeded; otherwise, false.</returns>
    public bool TryTranslate(Int3 delta, Grid grid)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        Int3 newOrigin = Origin + delta;
        if (IsValidPlacement(newOrigin, Piece.Voxels, grid))
        {
            Origin = newOrigin;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the piece can advance one cell in the gravity direction.
    /// Per Simulation_Rules_v0_2.md §4.3: piece locks when it cannot advance due to collision or out-of-bounds.
    /// </summary>
    /// <param name="grid">The grid to check collision against.</param>
    /// <param name="gravity">The current gravity direction.</param>
    /// <returns>True if the piece can advance; otherwise, false (lock condition).</returns>
    public bool CanAdvance(Grid grid, GravityDirection gravity)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        Int3 gravityVector = GravityTable.GetVector(gravity);
        Int3 newOrigin = Origin + gravityVector;
        return IsValidPlacement(newOrigin, Piece.Voxels, grid);
    }

    /// <summary>
    /// Checks if a placement is valid per Simulation_Rules_v0_2.md §4.2.
    /// A move is valid if all occupied destination cells are in bounds and are EMPTY or WATER.
    /// </summary>
    /// <param name="origin">The origin position to test.</param>
    /// <param name="voxels">The voxel offsets relative to origin.</param>
    /// <param name="grid">The grid to check against.</param>
    /// <returns>True if the placement is valid; otherwise, false.</returns>
    private static bool IsValidPlacement(Int3 origin, IReadOnlyList<Int3> voxels, Grid grid)
    {
        foreach (Int3 voxel in voxels)
        {
            Int3 worldPos = origin + voxel;

            // Check bounds
            if (!grid.IsInBounds(worldPos))
            {
                return false;
            }

            // Check occupancy - per §4.2, EMPTY and WATER are passable
            Voxel cell = grid.GetVoxel(worldPos);
            if (cell.Type is not (OccupancyType.Empty or OccupancyType.Water))
            {
                // Collision with SOLID, WALL, BEDROCK, ICE, DRAIN, or POROUS
                return false;
            }
        }

        return true;
    }
}

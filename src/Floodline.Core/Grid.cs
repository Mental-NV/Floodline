namespace Floodline.Core;

/// <summary>
/// Represents the 3D simulation grid.
/// </summary>
public sealed class Grid
{
    private readonly Voxel[,,] _cells;

    /// <summary>
    /// Gets the dimensions of the grid.
    /// </summary>
    public Int3 Size { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Grid"/> class with specified dimensions.
    /// </summary>
    /// <param name="size">The grid dimensions (X, Y, Z).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if any dimension is less than or equal to zero.</exception>
    public Grid(Int3 size)
    {
        if (size.X <= 0 || size.Y <= 0 || size.Z <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Grid dimensions must be positive.");
        }

        Size = size;
        _cells = new Voxel[size.X, size.Y, size.Z];

        // Initialize with Empty voxels
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                for (int z = 0; z < size.Z; z++)
                {
                    _cells[x, y, z] = Voxel.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Checks if a position is within the grid bounds.
    /// </summary>
    /// <param name="pos">The position to check.</param>
    /// <returns>True if within bounds; otherwise, false.</returns>
    public bool IsInBounds(Int3 pos)
    {
        return pos.X >= 0 && pos.X < Size.X &&
               pos.Y >= 0 && pos.Y < Size.Y &&
               pos.Z >= 0 && pos.Z < Size.Z;
    }

    /// <summary>
    /// Attempts to get the voxel at the specified position.
    /// </summary>
    /// <param name="pos">The position to query.</param>
    /// <returns>The voxel at the position.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if pos is out of bounds.</exception>
    public Voxel GetVoxel(Int3 pos)
    {
        return IsInBounds(pos)
            ? _cells[pos.X, pos.Y, pos.Z]
            : throw new System.ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of grid bounds {Size}.");
    }

    /// <summary>
    /// Sets the voxel at the specified position.
    /// </summary>
    /// <param name="pos">The position to update.</param>
    /// <param name="voxel">The new voxel content.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if pos is out of bounds.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if voxel is null.</exception>
    public void SetVoxel(Int3 pos, Voxel voxel)
    {
        if (!IsInBounds(pos))
        {
            throw new System.ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of grid bounds {Size}.");
        }

        _cells[pos.X, pos.Y, pos.Z] = voxel ?? throw new System.ArgumentNullException(nameof(voxel));
    }

    /// <summary>
    /// Safely attempts to get the voxel at the specified position.
    /// </summary>
    /// <param name="pos">The position to query.</param>
    /// <param name="voxel">The voxel at the position, or Voxel.Empty if out of bounds.</param>
    /// <returns>True if within bounds; otherwise, false.</returns>
    public bool TryGetVoxel(Int3 pos, out Voxel voxel)
    {
        if (IsInBounds(pos))
        {
            voxel = _cells[pos.X, pos.Y, pos.Z];
            return true;
        }

        voxel = Voxel.Empty;
        return false;
    }
}

using System.Collections.Generic;
using Floodline.Core.Levels;

namespace Floodline.Core;

/// <summary>
/// Applies drain removal rules deterministically.
/// </summary>
public static class DrainSolver
{
    private static readonly Int3[] Adj6Offsets =
    [
        new Int3(1, 0, 0),
        new Int3(-1, 0, 0),
        new Int3(0, 1, 0),
        new Int3(0, -1, 0),
        new Int3(0, 0, 1),
        new Int3(0, 0, -1)
    ];

    private static readonly Int3[] Adj26Offsets = BuildAdj26Offsets();

    /// <summary>
    /// Applies drains on the grid and returns the number of water units removed.
    /// </summary>
    /// <param name="grid">The grid to update.</param>
    /// <param name="gravity">The current gravity direction.</param>
    /// <param name="drainConfigs">Drain configuration lookup by position.</param>
    /// <returns>The total number of water units removed.</returns>
    public static int Apply(Grid grid, GravityDirection gravity, IReadOnlyDictionary<Int3, DrainConfig>? drainConfigs)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        List<DrainInstance> drains = [];
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Int3 pos = new(x, y, z);
                    Voxel voxel = grid.GetVoxel(pos);
                    if (voxel.Type != OccupancyType.Drain)
                    {
                        continue;
                    }

                    DrainConfig config = DrainConfig.Default;
                    if (drainConfigs != null && drainConfigs.TryGetValue(pos, out DrainConfig? custom))
                    {
                        config = custom;
                    }

                    int elev = DeterministicOrdering.GetGravElev(pos, gravity);
                    TieCoord tie = DeterministicOrdering.GetTieCoord(pos, gravity);
                    drains.Add(new DrainInstance(pos, config, elev, tie));
                }
            }
        }

        drains.Sort((a, b) =>
        {
            int elevComp = a.Elevation.CompareTo(b.Elevation);
            return elevComp != 0 ? elevComp : a.Tie.CompareTo(b.Tie);
        });

        int removed = 0;
        foreach (DrainInstance drain in drains)
        {
            if (drain.Config.RatePerResolve <= 0)
            {
                continue;
            }

            List<WaterCandidate> candidates = CollectWaterCandidates(grid, gravity, drain.Pos, drain.Config.Scope);
            if (candidates.Count == 0)
            {
                continue;
            }

            candidates.Sort((a, b) =>
            {
                int elevComp = a.Elevation.CompareTo(b.Elevation);
                return elevComp != 0 ? elevComp : a.Tie.CompareTo(b.Tie);
            });

            int limit = drain.Config.RatePerResolve;
            if (limit > candidates.Count)
            {
                limit = candidates.Count;
            }

            for (int i = 0; i < limit; i++)
            {
                grid.SetVoxel(candidates[i].Pos, Voxel.Empty);
                removed++;
            }
        }

        return removed;
    }

    private static List<WaterCandidate> CollectWaterCandidates(
        Grid grid,
        GravityDirection gravity,
        Int3 drainPos,
        DrainScope scope)
    {
        List<WaterCandidate> candidates = [];

        if (scope == DrainScope.Self)
        {
            AddIfWater(grid, gravity, drainPos, candidates);
            return candidates;
        }

        Int3[] offsets = scope == DrainScope.Adj6 ? Adj6Offsets : Adj26Offsets;
        foreach (Int3 offset in offsets)
        {
            Int3 pos = drainPos + offset;
            AddIfWater(grid, gravity, pos, candidates);
        }

        return candidates;
    }

    private static void AddIfWater(Grid grid, GravityDirection gravity, Int3 pos, List<WaterCandidate> candidates)
    {
        if (!grid.IsInBounds(pos))
        {
            return;
        }

        Voxel voxel = grid.GetVoxel(pos);
        if (voxel.Type != OccupancyType.Water)
        {
            return;
        }

        int elev = DeterministicOrdering.GetGravElev(pos, gravity);
        TieCoord tie = DeterministicOrdering.GetTieCoord(pos, gravity);
        candidates.Add(new WaterCandidate(pos, elev, tie));
    }

    private static Int3[] BuildAdj26Offsets()
    {
        List<Int3> offsets = [];
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    offsets.Add(new Int3(x, y, z));
                }
            }
        }

        return [.. offsets];
    }

    private readonly record struct DrainInstance(Int3 Pos, DrainConfig Config, int Elevation, TieCoord Tie);

    private readonly record struct WaterCandidate(Int3 Pos, int Elevation, TieCoord Tie);
}

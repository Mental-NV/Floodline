using System;
using System.Collections.Generic;

namespace Floodline.Core;

/// <summary>
/// Tracks temporary ice durations created by freeze actions.
/// </summary>
public sealed class IceTracker
{
    private readonly Dictionary<Int3, int> _timers = [];

    /// <summary>
    /// Represents an ice timer snapshot entry.
    /// </summary>
    internal readonly record struct IceTimerSnapshot(Int3 Position, int Remaining);

    /// <summary>
    /// Gets the number of active ice timers.
    /// </summary>
    public int ActiveCount => _timers.Count;

    /// <summary>
    /// Converts targeted water cells to ice and starts their timers.
    /// </summary>
    /// <param name="grid">The grid to update.</param>
    /// <param name="targets">Target positions to freeze.</param>
    /// <param name="durationResolves">Duration in resolves.</param>
    /// <returns>The number of cells frozen.</returns>
    public int ApplyFreeze(Grid grid, IReadOnlyList<Int3> targets, int durationResolves)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (targets is null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        if (durationResolves <= 0)
        {
            return 0;
        }

        int frozen = 0;
        foreach (Int3 target in targets)
        {
            if (!grid.IsInBounds(target))
            {
                continue;
            }

            Voxel voxel = grid.GetVoxel(target);
            if (voxel.Type != OccupancyType.Water)
            {
                continue;
            }

            grid.SetVoxel(target, new Voxel(OccupancyType.Ice, null));

            int existing = _timers.TryGetValue(target, out int current) ? current : 0;
            _timers[target] = existing > durationResolves ? existing : durationResolves;
            frozen++;
        }

        return frozen;
    }

    /// <summary>
    /// Advances all ice timers by one resolve and thaws any expired ice cells.
    /// </summary>
    /// <param name="grid">The grid to update.</param>
    /// <returns>Positions that thawed this resolve.</returns>
    public IReadOnlyList<Int3> AdvanceResolve(Grid grid)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (_timers.Count == 0)
        {
            return [];
        }

        List<Int3> thawed = [];
        List<Int3> toRemove = [];
        List<(Int3 Pos, int Remaining)> updates = [];

        foreach (KeyValuePair<Int3, int> entry in _timers)
        {
            Int3 pos = entry.Key;
            int remaining = entry.Value - 1;

            if (!grid.IsInBounds(pos) || grid.GetVoxel(pos).Type != OccupancyType.Ice)
            {
                toRemove.Add(pos);
                continue;
            }

            if (remaining <= 0)
            {
                toRemove.Add(pos);
                thawed.Add(pos);
            }
            else
            {
                updates.Add((pos, remaining));
            }
        }

        foreach ((Int3 pos, int remaining) in updates)
        {
            _timers[pos] = remaining;
        }

        foreach (Int3 pos in toRemove)
        {
            _timers.Remove(pos);
        }

        foreach (Int3 pos in thawed)
        {
            grid.SetVoxel(pos, Voxel.Water);
        }

        return thawed;
    }

    internal IReadOnlyList<IceTimerSnapshot> GetTimersSnapshot()
    {
        if (_timers.Count == 0)
        {
            return [];
        }

        List<IceTimerSnapshot> snapshot = [];
        foreach (KeyValuePair<Int3, int> entry in _timers)
        {
            snapshot.Add(new IceTimerSnapshot(entry.Key, entry.Value));
        }

        snapshot.Sort((left, right) => ComparePositions(left.Position, right.Position));
        return snapshot;
    }

    private static int ComparePositions(Int3 left, Int3 right)
    {
        int x = left.X.CompareTo(right.X);
        if (x != 0)
        {
            return x;
        }

        int y = left.Y.CompareTo(right.Y);
        if (y != 0)
        {
            return y;
        }

        return left.Z.CompareTo(right.Z);
    }
}

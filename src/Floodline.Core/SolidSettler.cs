using System.Collections.Generic;

namespace Floodline.Core;

public sealed record SolidSettleResult(IReadOnlyList<Int3> DisplacedWater);

public static class SolidSettler
{
    private static readonly Int3[] NeighborOffsets =
    [
        new Int3(1, 0, 0),
        new Int3(-1, 0, 0),
        new Int3(0, 1, 0),
        new Int3(0, -1, 0),
        new Int3(0, 0, 1),
        new Int3(0, 0, -1)
    ];

    public static SolidSettleResult Settle(Grid grid, GravityDirection gravity)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        List<Int3> displacedWater = [];
        Int3 gravityVector = GravityTable.GetVector(gravity);

        int iteration = 0;
        int maxIterations = grid.Size.X * grid.Size.Y * grid.Size.Z;
        bool moved;

        do
        {
            moved = false;
            List<SolidComponent> components = BuildComponents(grid, gravity, gravityVector);
            components.Sort(CompareComponents);

            foreach (SolidComponent component in components)
            {
                if (component.IsSupported)
                {
                    continue;
                }

                int dropDistance = ComputeDropDistance(component, grid, gravityVector);
                if (dropDistance <= 0)
                {
                    continue;
                }

                MoveComponent(component, grid, gravityVector * dropDistance, displacedWater);
                moved = true;
            }

            iteration++;
        }
        while (moved && iteration < maxIterations);

        return new SolidSettleResult(displacedWater);
    }

    private static List<SolidComponent> BuildComponents(Grid grid, GravityDirection gravity, Int3 gravityVector)
    {
        List<SolidComponent> components = [];
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;
        bool[,,] visited = new bool[sizeX, sizeY, sizeZ];

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (visited[x, y, z])
                    {
                        continue;
                    }

                    Int3 pos = new(x, y, z);
                    Voxel voxel = grid.GetVoxel(pos);
                    if (!IsMovableSolid(voxel.Type))
                    {
                        visited[x, y, z] = true;
                        continue;
                    }

                    SolidComponent component = BuildComponentFrom(grid, pos, visited, gravity, gravityVector);
                    components.Add(component);
                }
            }
        }

        return components;
    }

    private static SolidComponent BuildComponentFrom(Grid grid, Int3 start, bool[,,] visited, GravityDirection gravity, Int3 gravityVector)
    {
        Queue<Int3> queue = new();
        List<Int3> cells = [];
        HashSet<Int3> cellSet = [];

        queue.Enqueue(start);
        visited[start.X, start.Y, start.Z] = true;

        int minElev = int.MaxValue;
        TieCoord minTieCoord = new(int.MaxValue, int.MaxValue, int.MaxValue);
        bool supported = false;

        while (queue.Count > 0)
        {
            Int3 current = queue.Dequeue();
            cells.Add(current);
            cellSet.Add(current);

            int elev = DeterministicOrdering.GetGravElev(current, gravity);
            if (elev < minElev)
            {
                minElev = elev;
            }

            TieCoord tieCoord = DeterministicOrdering.GetTieCoord(current, gravity);
            if (tieCoord < minTieCoord)
            {
                minTieCoord = tieCoord;
            }

            if (!supported && IsCellSupported(grid, current, gravityVector))
            {
                supported = true;
            }

            if (!supported && HasAdjacentImmovableSupport(grid, current))
            {
                supported = true;
            }

            foreach (Int3 offset in NeighborOffsets)
            {
                Int3 next = current + offset;
                if (!grid.IsInBounds(next))
                {
                    continue;
                }

                if (visited[next.X, next.Y, next.Z])
                {
                    continue;
                }

                Voxel neighbor = grid.GetVoxel(next);
                if (!IsMovableSolid(neighbor.Type))
                {
                    visited[next.X, next.Y, next.Z] = true;
                    continue;
                }

                visited[next.X, next.Y, next.Z] = true;
                queue.Enqueue(next);
            }
        }

        return new SolidComponent(cells, cellSet, minElev, minTieCoord, supported);
    }

    private static bool IsCellSupported(Grid grid, Int3 cell, Int3 gravityVector)
    {
        Int3 supportPos = cell + gravityVector;
        if (!grid.TryGetVoxel(supportPos, out Voxel supportVoxel))
        {
            return false;
        }

        return IsSupportVoxel(supportVoxel.Type);
    }

    private static bool HasAdjacentImmovableSupport(Grid grid, Int3 cell)
    {
        foreach (Int3 offset in NeighborOffsets)
        {
            Int3 adjacent = cell + offset;
            if (!grid.TryGetVoxel(adjacent, out Voxel voxel))
            {
                continue;
            }

            if (IsImmovableSupport(voxel.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static int ComputeDropDistance(SolidComponent component, Grid grid, Int3 gravityVector)
    {
        int distance = 0;

        while (true)
        {
            int candidate = distance + 1;
            bool canMove = true;

            foreach (Int3 cell in component.Cells)
            {
                Int3 target = cell + (gravityVector * candidate);
                if (!grid.IsInBounds(target))
                {
                    canMove = false;
                    break;
                }

                Voxel targetVoxel = grid.GetVoxel(target);
                if (IsBlockingVoxel(targetVoxel.Type) && !component.CellSet.Contains(target))
                {
                    canMove = false;
                    break;
                }
            }

            if (!canMove)
            {
                return distance;
            }

            distance = candidate;
        }
    }

    private static void MoveComponent(SolidComponent component, Grid grid, Int3 delta, List<Int3> displacedWater)
    {
        List<(Int3 Target, Voxel Voxel)> moves = new(component.Cells.Count);

        foreach (Int3 cell in component.Cells)
        {
            Voxel voxel = grid.GetVoxel(cell);
            moves.Add((cell + delta, voxel));
        }

        foreach (Int3 cell in component.Cells)
        {
            grid.SetVoxel(cell, Voxel.Empty);
        }

        foreach ((Int3 Target, Voxel Voxel) move in moves)
        {
            Voxel targetVoxel = grid.GetVoxel(move.Target);
            if (targetVoxel.Type == OccupancyType.Water)
            {
                displacedWater.Add(move.Target);
            }

            grid.SetVoxel(move.Target, move.Voxel);
        }
    }

    private static int CompareComponents(SolidComponent left, SolidComponent right)
    {
        int elevComp = left.MinElev.CompareTo(right.MinElev);
        return elevComp != 0 ? elevComp : left.MinTieCoord.CompareTo(right.MinTieCoord);
    }

    private static bool IsMovableSolid(OccupancyType type) =>
        type is OccupancyType.Solid or OccupancyType.Porous;

    private static bool IsSupportVoxel(OccupancyType type) =>
        type is OccupancyType.Solid or OccupancyType.Wall or OccupancyType.Bedrock or OccupancyType.Ice
            or OccupancyType.Drain or OccupancyType.Porous;

    private static bool IsImmovableSupport(OccupancyType type) =>
        type is OccupancyType.Wall or OccupancyType.Bedrock or OccupancyType.Ice or OccupancyType.Drain;

    private static bool IsBlockingVoxel(OccupancyType type) =>
        type is OccupancyType.Solid or OccupancyType.Wall or OccupancyType.Bedrock or OccupancyType.Ice
            or OccupancyType.Drain or OccupancyType.Porous;

    private sealed record SolidComponent(
        IReadOnlyList<Int3> Cells,
        HashSet<Int3> CellSet,
        int MinElev,
        TieCoord MinTieCoord,
        bool IsSupported);
}

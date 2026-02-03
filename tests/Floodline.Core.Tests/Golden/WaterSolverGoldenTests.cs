using System;
using System.Collections.Generic;
using Floodline.Core;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class WaterSolverGoldenTests
{
    [Fact]
    public void Golden_BasinNoSpill()
    {
        Grid grid = new(new Int3(4, 2, 1));
        grid.SetVoxel(new Int3(1, 0, 0), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);

        Int3[] displaced = [];
        _ = WaterSolver.Settle(grid, GravityDirection.Down, displaced);

        GoldenAssert.Matches("water/basin_no_spill", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_BasinSpill()
    {
        Grid grid = new(new Int3(4, 2, 1));
        grid.SetVoxel(new Int3(1, 0, 0), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);

        Int3[] displaced = [new Int3(0, 0, 0)];
        _ = WaterSolver.Settle(grid, GravityDirection.Down, displaced);

        GoldenAssert.Matches("water/basin_spill", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_BlockedCells()
    {
        Grid grid = new(new Int3(3, 2, 1));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);

        Int3[] displaced = [new Int3(0, 0, 0)];
        HashSet<Int3> blocked = [new Int3(1, 0, 0)];

        _ = WaterSolver.Settle(grid, GravityDirection.Down, displaced, blocked);

        GoldenAssert.Matches("water/blocked_cells", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_DisplacedSources()
    {
        Grid grid = new(new Int3(2, 2, 1));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);

        Int3[] displaced = [new Int3(0, 0, 0), new Int3(1, 0, 0)];
        WaterSettleResult result = WaterSolver.Settle(grid, GravityDirection.Down, displaced);

        Assert.Equal(3, result.TotalUnits);
        GoldenAssert.Matches("water/displaced_sources", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_Overflow()
    {
        Grid grid = new(new Int3(2, 1, 1));
        grid.SetVoxel(new Int3(1, 0, 0), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);

        Int3[] displaced = [new Int3(0, 0, 0)];
        WaterSettleResult result = WaterSolver.Settle(grid, GravityDirection.Down, displaced);

        Assert.Equal(1, result.OverflowUnits);
        GoldenAssert.Matches("water/overflow", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Settle_NullDisplacedSources_Throws()
    {
        Grid grid = new(new Int3(1, 1, 1));

        _ = Assert.Throws<ArgumentNullException>(() => WaterSolver.Settle(grid, GravityDirection.Down, null!));
    }
}

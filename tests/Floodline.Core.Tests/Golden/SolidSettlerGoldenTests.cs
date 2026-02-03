using System.Collections.Generic;
using Floodline.Core;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class SolidSettlerGoldenTests
{
    [Fact]
    public void Golden_SingleDrop()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid));

        _ = SolidSettler.Settle(grid, GravityDirection.Down);

        GoldenAssert.Matches("solids/single_drop", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_AdjacentWallSupport()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(0, 1, 1), new Voxel(OccupancyType.Wall));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid));

        _ = SolidSettler.Settle(grid, GravityDirection.Down);

        GoldenAssert.Matches("solids/adjacent_wall_support", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_TwoComponentOrder()
    {
        Grid grid = new(new Int3(3, 4, 3));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid));
        grid.SetVoxel(new Int3(1, 3, 1), new Voxel(OccupancyType.Solid));

        _ = SolidSettler.Settle(grid, GravityDirection.Down);

        GoldenAssert.Matches("solids/two_component_order", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_MultiVoxelComponentDrop()
    {
        Grid grid = new(new Int3(4, 4, 4));
        grid.SetVoxel(new Int3(1, 3, 1), new Voxel(OccupancyType.Solid));
        grid.SetVoxel(new Int3(2, 3, 1), new Voxel(OccupancyType.Solid));

        _ = SolidSettler.Settle(grid, GravityDirection.Down);

        GoldenAssert.Matches("solids/multi_voxel_drop", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void Golden_WaterDisplacement()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(0, 0, 0), Voxel.Water);
        grid.SetVoxel(new Int3(1, 1, 1), Voxel.Water);
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Contains(new Int3(1, 1, 1), result.DisplacedWater);
        GoldenAssert.Matches("solids/water_displacement", SnapshotWriter.Write(grid, GravityDirection.Down));
    }

    [Fact]
    public void TrySettle_BlockedCell_Rejects()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid));

        HashSet<Int3> blockedCells = [new Int3(1, 1, 1)];

        bool settled = SolidSettler.TrySettle(grid, GravityDirection.Down, blockedCells, out SolidSettleResult result);

        Assert.False(settled);
        Assert.Empty(result.DisplacedWater);
    }
}

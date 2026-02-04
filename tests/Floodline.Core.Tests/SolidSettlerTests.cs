using Floodline.Core;
using Xunit;

namespace Floodline.Core.Tests;

public class SolidSettlerTests
{
    [Fact]
    public void Settle_Drops_Unsupported_Component_Until_Supported()
    {
        Grid grid = new(new Int3(3, 4, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Empty(result.DisplacedWater);
        Assert.Equal(OccupancyType.Solid, grid.GetVoxel(new Int3(1, 1, 1)).Type);
        Assert.Equal(OccupancyType.Empty, grid.GetVoxel(new Int3(1, 2, 1)).Type);
    }

    [Fact]
    public void Settle_Leaves_Supported_Component()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Empty(result.DisplacedWater);
        Assert.Equal(OccupancyType.Solid, grid.GetVoxel(new Int3(1, 1, 1)).Type);
    }

    [Fact]
    public void Settle_Uses_Immovable_Adjacency_For_Support()
    {
        Grid grid = new(new Int3(3, 3, 3));
        grid.SetVoxel(new Int3(0, 1, 1), new Voxel(OccupancyType.Wall));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Empty(result.DisplacedWater);
        Assert.Equal(OccupancyType.Solid, grid.GetVoxel(new Int3(1, 1, 1)).Type);
    }

    [Fact]
    public void Settle_Records_Displaced_Water()
    {
        Grid grid = new(new Int3(3, 4, 3));
        grid.SetVoxel(new Int3(1, 0, 1), new Voxel(OccupancyType.Bedrock));
        grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Water));
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Single(result.DisplacedWater);
        Assert.Contains(new Int3(1, 1, 1), result.DisplacedWater);
        Assert.Equal(OccupancyType.Solid, grid.GetVoxel(new Int3(1, 1, 1)).Type);
    }

    [Fact]
    public void Settle_Does_Not_Move_Anchored_Solid()
    {
        Grid grid = new(new Int3(3, 4, 3));
        grid.SetVoxel(new Int3(1, 2, 1), new Voxel(OccupancyType.Solid, "STANDARD", Anchored: true));

        SolidSettleResult result = SolidSettler.Settle(grid, GravityDirection.Down);

        Assert.Empty(result.DisplacedWater);
        Voxel anchored = grid.GetVoxel(new Int3(1, 2, 1));
        Assert.Equal(OccupancyType.Solid, anchored.Type);
        Assert.True(anchored.Anchored);
        Assert.Equal(OccupancyType.Empty, grid.GetVoxel(new Int3(1, 1, 1)).Type);
    }
}

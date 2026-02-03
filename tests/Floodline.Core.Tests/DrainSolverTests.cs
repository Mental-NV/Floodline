using System.Collections.Generic;
using Floodline.Core;
using Floodline.Core.Levels;
using Xunit;

namespace Floodline.Core.Tests;

public class DrainSolverTests
{
    [Fact]
    public void DrainSolver_Removes_Water_In_Deterministic_Order()
    {
        // Arrange
        Grid grid = new(new Int3(3, 3, 3));
        Int3 drainPos = new(1, 1, 1);
        Int3 first = new(1, 1, 0);
        Int3 second = new(1, 1, 2);

        grid.SetVoxel(drainPos, new Voxel(OccupancyType.Drain));
        grid.SetVoxel(first, Voxel.Water);
        grid.SetVoxel(second, Voxel.Water);

        Dictionary<Int3, DrainConfig> configs = new()
        {
            [drainPos] = new DrainConfig(1, DrainScope.Adj6)
        };

        // Act
        int removed = DrainSolver.Apply(grid, GravityDirection.Down, configs);

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(OccupancyType.Water, grid.GetVoxel(first).Type);
        Assert.Equal(OccupancyType.Empty, grid.GetVoxel(second).Type);
    }
}

using System.Collections.Generic;
using Floodline.Core;
using Xunit;

namespace Floodline.Core.Tests;

public class IceTrackerTests
{
    [Fact]
    public void IceTracker_Freezes_And_Thaws_Water()
    {
        // Arrange
        Grid grid = new(new Int3(3, 3, 3));
        Int3 waterPos = new(1, 0, 1);
        grid.SetVoxel(waterPos, Voxel.Water);

        IceTracker tracker = new();

        // Act
        int frozen = tracker.ApplyFreeze(grid, [waterPos], 2);
        IReadOnlyList<Int3> thawedAfterFirst = tracker.AdvanceResolve(grid);
        IReadOnlyList<Int3> thawedAfterSecond = tracker.AdvanceResolve(grid);

        // Assert
        Assert.Equal(1, frozen);
        Assert.Empty(thawedAfterFirst);
        Assert.Single(thawedAfterSecond);
        Assert.Equal(OccupancyType.Water, grid.GetVoxel(waterPos).Type);
    }
}

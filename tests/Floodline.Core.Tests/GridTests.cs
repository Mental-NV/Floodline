namespace Floodline.Core.Tests;

public class GridTests
{
    [Fact]
    public void GridConstructorSetsSizeAndInitializesEmpty()
    {
        // Arrange
        Int3 size = new(10, 20, 10);

        // Act
        Grid grid = new(size);

        // Assert
        Assert.Equal(size, grid.Size);
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                for (int z = 0; z < size.Z; z++)
                {
                    Assert.Equal(OccupancyType.Empty, grid.GetVoxel(new Int3(x, y, z)).Type);
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(-1, 10, 10)]
    public void GridConstructorThrowsOnInvalidSize(int x, int y, int z) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(new Int3(x, y, z)));

    [Fact]
    public void GridGetSetVoxelWorksInBounds()
    {
        // Arrange
        Grid grid = new(new Int3(5, 5, 5));
        Int3 pos = new(2, 3, 4);
        Voxel voxel = new(OccupancyType.Solid, "Standard");

        // Act
        grid.SetVoxel(pos, voxel);
        Voxel retrieved = grid.GetVoxel(pos);

        // Assert
        Assert.Equal(voxel, retrieved);
        Assert.Equal(OccupancyType.Solid, retrieved.Type);
        Assert.Equal("Standard", retrieved.MaterialId);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(5, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 5)]
    public void GridGetSetVoxelThrowsOutOfBounds(int x, int y, int z)
    {
        // Arrange
        Grid grid = new(new Int3(5, 5, 5));
        Int3 pos = new(x, y, z);

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetVoxel(pos));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetVoxel(pos, Voxel.Water));
    }

    [Fact]
    public void GridTryGetVoxelReturnsCorrectStatus()
    {
        // Arrange
        Grid grid = new(new Int3(5, 5, 5));
        Int3 inPos = new(2, 2, 2);
        Int3 outPos = new(10, 10, 10);
        grid.SetVoxel(inPos, Voxel.Water);

        // Act
        bool inResult = grid.TryGetVoxel(inPos, out Voxel inVoxel);
        bool outResult = grid.TryGetVoxel(outPos, out Voxel outVoxel);

        // Assert
        Assert.True(inResult);
        Assert.Equal(Voxel.Water, inVoxel);
        Assert.False(outResult);
        Assert.Equal(Voxel.Empty, outVoxel);
    }

    [Fact]
    public void VoxelEqualityWorksAsExpected()
    {
        // Arrange
        Voxel v1 = new(OccupancyType.Solid, "Concrete");
        Voxel v2 = new(OccupancyType.Solid, "Concrete");
        Voxel v3 = new(OccupancyType.Solid, "Wood");
        Voxel v4 = new(OccupancyType.Water);

        // Assert
        Assert.Equal(v1, v2);
        Assert.NotEqual(v1, v3);
        Assert.NotEqual(v1, v4);
    }
}

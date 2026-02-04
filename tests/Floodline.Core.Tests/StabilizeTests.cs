using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class StabilizeTests
{
    [Fact]
    public void Stabilize_Anchors_On_Lock_And_Expires()
    {
        Level level = new(
            new("test_id", "Test Title", "0.2.0", 12345U),
            new(10, 20, 10),
            [],
            [],
            new RotationConfig(),
            new("FIXED_SEQUENCE", ["I4"], null),
            [],
            new AbilitiesConfig(StabilizeCharges: 1)
        );

        Simulation sim = new(level, new Pcg32(1));
        int voxelCount = sim.ActivePiece!.Piece.Voxels.Count;

        sim.Tick(InputCommand.Stabilize);
        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(voxelCount, CountAnchoredSolids(sim.Grid));

        sim.Tick(InputCommand.RotateWorldForward);
        Assert.Equal(voxelCount, CountAnchoredSolids(sim.Grid));

        sim.Tick(InputCommand.RotateWorldBack);
        Assert.Equal(0, CountAnchoredSolids(sim.Grid));
    }

    [Fact]
    public void Reinforced_Material_Anchors_Permanently()
    {
        Level level = new(
            new("test_id", "Test Title", "0.2.0", 12345U),
            new(10, 20, 10),
            [],
            [],
            new RotationConfig(),
            new("FIXED_SEQUENCE", ["I4:REINFORCED"], null),
            []
        );

        Simulation sim = new(level, new Pcg32(1));
        int voxelCount = sim.ActivePiece!.Piece.Voxels.Count;

        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(voxelCount, CountAnchoredSolids(sim.Grid));

        sim.Tick(InputCommand.RotateWorldForward);
        sim.Tick(InputCommand.RotateWorldBack);

        Assert.Equal(voxelCount, CountAnchoredSolids(sim.Grid));
    }

    private static int CountAnchoredSolids(Grid grid)
    {
        int count = 0;
        for (int x = 0; x < grid.Size.X; x++)
        {
            for (int y = 0; y < grid.Size.Y; y++)
            {
                for (int z = 0; z < grid.Size.Z; z++)
                {
                    Voxel voxel = grid.GetVoxel(new Int3(x, y, z));
                    if (voxel.Type == OccupancyType.Solid && voxel.Anchored)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }
}

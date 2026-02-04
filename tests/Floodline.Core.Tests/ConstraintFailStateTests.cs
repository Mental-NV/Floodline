using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class ConstraintFailStateTests
{
    private static Level CreateLevel(ConstraintsConfig constraints, List<VoxelData>? voxels = null) =>
        new(
            new("test_id", "Test Title", "0.2.0", 12345U),
            new(10, 20, 10),
            voxels ?? [],
            [],
            new RotationConfig(),
            new("FIXED_SEQUENCE", ["I4"], null),
            [],
            Abilities: null,
            Constraints: constraints
        );

    [Fact]
    public void Simulation_Loses_When_Weight_Exceeded()
    {
        Level level = CreateLevel(new ConstraintsConfig(MaxMass: 1));
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(SimulationStatus.Lost, sim.State.Status);
    }

    [Fact]
    public void Simulation_Loses_When_Water_Forbidden()
    {
        List<VoxelData> initial =
        [
            new VoxelData(new Int3(1, 0, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(0, 1, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(2, 1, 1), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 0), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 2), OccupancyType.Bedrock),
            new VoxelData(new Int3(1, 1, 1), OccupancyType.Water)
        ];
        Level level = CreateLevel(new ConstraintsConfig(WaterForbiddenWorldHeightMin: 1), initial);
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(SimulationStatus.Lost, sim.State.Status);
    }

    [Fact]
    public void Simulation_Loses_When_Resting_On_Water()
    {
        Level level = CreateLevel(new ConstraintsConfig(NoRestingOnWater: true));
        Simulation sim = new(level, new Pcg32(1));

        sim.Grid.SetVoxel(new Int3(1, 1, 1), new Voxel(OccupancyType.Solid, "STANDARD", Anchored: true));
        sim.Grid.SetVoxel(new Int3(1, 0, 1), Voxel.Water);
        sim.Grid.SetVoxel(new Int3(0, 0, 1), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(2, 0, 1), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(1, 0, 0), new Voxel(OccupancyType.Bedrock));
        sim.Grid.SetVoxel(new Int3(1, 0, 2), new Voxel(OccupancyType.Bedrock));

        sim.Tick(InputCommand.HardDrop);

        Assert.Equal(SimulationStatus.Lost, sim.State.Status);
    }
}

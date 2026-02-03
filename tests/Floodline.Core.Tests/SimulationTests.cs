using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class SimulationTests
{
    private static Level CreateTestLevel() =>
        new(
            new("test_id", "Test Title", 1, 12345U),
            new(10, 20, 10),
            [],
            [],
            new(),
            new("FIXED_SEQUENCE", ["I4"], null),
            []
        );

    [Fact]
    public void Simulation_Initializes_With_Piece()
    {
        // Arrange
        var level = CreateTestLevel();
        var random = new Pcg32(12345);

        // Act
        var sim = new Simulation(level, random);

        // Assert
        Assert.NotNull(sim.ActivePiece);
        Assert.Equal(SimulationStatus.InProgress, sim.State.Status);
    }

    [Fact]
    public void Simulation_Tick_Increments_Time()
    {
        // Arrange
        var sim = new Simulation(CreateTestLevel(), new Pcg32(1));

        // Act
        sim.Tick(InputCommand.None);

        // Assert
        Assert.Equal(1, sim.State.TicksElapsed);
    }

    [Fact]
    public void Simulation_HardDrop_Locks_Piece_And_Respawns()
    {
        // Arrange
        var sim = new Simulation(CreateTestLevel(), new Pcg32(1));
        var initialOrigin = sim.ActivePiece!.Origin;

        // Act
        sim.Tick(InputCommand.HardDrop);

        // Assert
        Assert.Equal(1, sim.State.PiecesLocked);
        Assert.NotNull(sim.ActivePiece);
        // New piece should be at spawn height, while hard dropped piece should be below it
        Assert.True(sim.Grid.GetVoxel(new Int3(initialOrigin.X, 0, initialOrigin.Z)).Type == OccupancyType.Solid);
    }

    [Fact]
    public void Simulation_WorldRotation_Triggers_TiltResolve_Without_Merging_ActivePiece()
    {
        // Arrange
        var sim = new Simulation(CreateTestLevel(), new Pcg32(1));

        // Act
        sim.Tick(InputCommand.RotateWorldForward);

        // Assert
        Assert.Equal(SimulationStatus.InProgress, sim.State.Status);
        Assert.NotNull(sim.ActivePiece);
        Assert.Equal(0, sim.State.PiecesLocked);

        // Tilt resolve should not merge the active piece into the grid.
        var positions = sim.ActivePiece!.GetWorldPositions();
        foreach (var pos in positions)
        {
            Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(pos).Type);
        }
    }
}

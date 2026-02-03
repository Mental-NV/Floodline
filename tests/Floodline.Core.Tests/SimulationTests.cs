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

    [Fact]
    public void Simulation_WorldRotation_Rejected_When_TiltResolve_Would_Collide_With_ActivePiece()
    {
        // Arrange
        Level level = new(
            new("test_id", "Test Title", 1, 12345U),
            new(6, 6, 6),
            [],
            [],
            new(),
            new("FIXED_SEQUENCE", ["I4"], null),
            []
        );
        Simulation sim = new(level, new FixedRandom(0));
        ActivePiece activePiece = sim.ActivePiece!;
        IReadOnlyList<Int3> positions = activePiece.GetWorldPositions();

        bool found = false;
        Int3 activePos = default;
        foreach (Int3 pos in positions)
        {
            if (pos.Z + 1 < sim.Grid.Size.Z)
            {
                activePos = pos;
                found = true;
                break;
            }
        }

        Assert.True(found);

        Int3 solidPos = new(activePos.X, activePos.Y, activePos.Z + 1);
        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(solidPos).Type);
        sim.Grid.SetVoxel(solidPos, new Voxel(OccupancyType.Solid, null));

        // Act
        sim.Tick(InputCommand.RotateWorldForward);

        // Assert
        Assert.Equal(GravityDirection.Down, sim.Gravity);
        Assert.Equal(OccupancyType.Solid, sim.Grid.GetVoxel(solidPos).Type);
        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(activePos).Type);
    }

    [Fact]
    public void Simulation_WorldRotation_Rejected_Rolls_Back_Partial_Settle()
    {
        // Arrange
        Level level = new(
            new("test_id", "Test Title", 1, 12345U),
            new(6, 6, 6),
            [],
            [],
            new(),
            new("FIXED_SEQUENCE", ["I4"], null),
            []
        );
        Simulation sim = new(level, new FixedRandom(0));
        ActivePiece activePiece = sim.ActivePiece!;

        Int3 blockedCell = default;
        bool foundBlocked = false;
        foreach (Int3 pos in activePiece.GetWorldPositions())
        {
            if (pos.Z < sim.Grid.Size.Z - 1 && (!foundBlocked || pos.Z > blockedCell.Z))
            {
                blockedCell = pos;
                foundBlocked = true;
            }
        }

        Assert.True(foundBlocked);

        Int3 firstComponentPos = new(blockedCell.X, blockedCell.Y, blockedCell.Z - 2);
        Int3 secondComponentPos = new(blockedCell.X, blockedCell.Y, blockedCell.Z + 1);
        Assert.True(sim.Grid.IsInBounds(firstComponentPos));
        Assert.True(sim.Grid.IsInBounds(secondComponentPos));
        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(firstComponentPos).Type);
        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(secondComponentPos).Type);

        sim.Grid.SetVoxel(firstComponentPos, new Voxel(OccupancyType.Solid, null));
        sim.Grid.SetVoxel(secondComponentPos, new Voxel(OccupancyType.Solid, null));

        // Act
        sim.Tick(InputCommand.RotateWorldForward);

        // Assert
        Assert.Equal(GravityDirection.Down, sim.Gravity);
        Assert.Equal(OccupancyType.Solid, sim.Grid.GetVoxel(firstComponentPos).Type);
        Assert.Equal(OccupancyType.Solid, sim.Grid.GetVoxel(secondComponentPos).Type);
        Assert.Equal(OccupancyType.Empty, sim.Grid.GetVoxel(new Int3(blockedCell.X, blockedCell.Y, 0)).Type);
    }

    private sealed class FixedRandom(int value) : IRandom
    {
        private readonly int _value = value;

        public uint Nextuint() => (uint)_value;

        public int NextInt(int max) => Clamp(_value, 0, max - 1);

        public int NextInt(int min, int max) => Clamp(_value, min, max - 1);

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}

using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class LockDelayTests
{
    private static Level CreateTestLevel() =>
        new(
            new("test_id", "Test Title", "0.2.0", 12345U),
            new(20, 20, 20),
            [],
            [],
            new(),
            new("FIXED_SEQUENCE", ["I4"], null),
            []
        );

    private static Simulation CreateSimulationWithGroundPlane()
    {
        Simulation sim = new(CreateTestLevel(), new Pcg32(1));
        ActivePiece piece = sim.ActivePiece!;
        int supportY = piece.Origin.Y - 1;
        Assert.True(supportY >= 0);

        for (int x = 0; x < sim.Grid.Size.X; x++)
        {
            for (int z = 0; z < sim.Grid.Size.Z; z++)
            {
                sim.Grid.SetVoxel(new Int3(x, supportY, z), new Voxel(OccupancyType.Bedrock));
            }
        }

        return sim;
    }

    private static void TickNTimes(Simulation sim, InputCommand command, int count)
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            sim.Tick(command);
        }
    }

    [Fact]
    public void LockDelay_LocksAfterDelayWhileGrounded()
    {
        Simulation sim = CreateSimulationWithGroundPlane();
        int delay = Constants.LockDelayTicks;

        TickNTimes(sim, InputCommand.None, delay - 1);

        Assert.Equal(0, sim.State.PiecesLocked);

        sim.Tick(InputCommand.None);

        Assert.Equal(1, sim.State.PiecesLocked);
    }

    [Fact]
    public void LockDelay_Resets_When_Ungrounded()
    {
        Simulation sim = CreateSimulationWithGroundPlane();
        int delay = Constants.LockDelayTicks;

        TickNTimes(sim, InputCommand.None, delay - 1);

        sim.Tick(InputCommand.RotateWorldForward);
        sim.Tick(InputCommand.RotateWorldBack);

        Assert.Equal(0, sim.State.PiecesLocked);

        TickNTimes(sim, InputCommand.None, delay - 2);

        Assert.Equal(0, sim.State.PiecesLocked);

        sim.Tick(InputCommand.None);

        Assert.Equal(1, sim.State.PiecesLocked);
    }

    [Fact]
    public void LockDelay_Resets_Capped_At_Max()
    {
        Simulation sim = CreateSimulationWithGroundPlane();
        int delay = Constants.LockDelayTicks;
        int maxResets = Constants.MaxLockResets;

        TickNTimes(sim, InputCommand.None, delay - 1);

        for (int i = 0; i < maxResets; i++)
        {
            sim.Tick(InputCommand.RotateWorldForward);
            sim.Tick(InputCommand.RotateWorldBack);

            Assert.Equal(0, sim.State.PiecesLocked);

            if (i < maxResets - 1)
            {
                TickNTimes(sim, InputCommand.None, delay - 2);
            }
        }

        TickNTimes(sim, InputCommand.None, delay - 2);

        Assert.Equal(0, sim.State.PiecesLocked);

        sim.Tick(InputCommand.RotateWorldForward);
        sim.Tick(InputCommand.RotateWorldBack);

        Assert.Equal(1, sim.State.PiecesLocked);
    }
}

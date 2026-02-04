using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests.Golden;

public class HoldGoldenTests
{
    [Fact]
    public void Golden_Hold_Swap()
    {
        Simulation sim = new(CreateHoldLevel(["I4", "O2", "L3"]), new Pcg32(1));

        sim.Tick(InputCommand.Hold);

        GoldenAssert.Matches(
            "hold/swap",
            SnapshotWriter.Write(sim.Grid, sim.Gravity, sim.State, activePiece: sim.ActivePiece));
    }

    [Fact]
    public void Golden_Hold_OncePerDrop()
    {
        Simulation sim = new(CreateHoldLevel(["I4", "O2", "L3"]), new Pcg32(2));

        sim.Tick(InputCommand.Hold);
        sim.Tick(InputCommand.Hold);

        GoldenAssert.Matches(
            "hold/once_per_drop",
            SnapshotWriter.Write(sim.Grid, sim.Gravity, sim.State, activePiece: sim.ActivePiece));
    }

    [Fact]
    public void Golden_Hold_Resets_Orientation()
    {
        Simulation sim = new(CreateHoldLevel(["L3", "O2", "I4"]), new Pcg32(3));

        sim.Tick(InputCommand.RotatePieceYawCW);
        sim.Tick(InputCommand.Hold);
        sim.Tick(InputCommand.HardDrop);
        sim.Tick(InputCommand.Hold);

        GoldenAssert.Matches(
            "hold/rotation_reset",
            SnapshotWriter.Write(sim.Grid, sim.Gravity, sim.State, activePiece: sim.ActivePiece));
    }

    [Fact]
    public void Golden_Hold_With_Rotation_And_SoftDrop()
    {
        Simulation sim = new(CreateHoldLevel(["L3", "I4", "O2"]), new Pcg32(4));

        sim.Tick(InputCommand.Hold);
        sim.Tick(InputCommand.RotatePieceYawCW);
        sim.Tick(InputCommand.SoftDrop);
        sim.Tick(InputCommand.HardDrop);

        GoldenAssert.Matches(
            "hold/rotate_softdrop",
            SnapshotWriter.Write(sim.Grid, sim.Gravity, sim.State, activePiece: sim.ActivePiece));
    }

    private static Level CreateHoldLevel(string[] sequence) =>
        new(
            new LevelMeta("hold-golden", "Hold Golden", "0.2.0", 1U),
            new Int3(6, 12, 6),
            [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", sequence, null),
            [],
            new AbilitiesConfig(HoldEnabled: true, StabilizeCharges: 0),
            new ConstraintsConfig());
}

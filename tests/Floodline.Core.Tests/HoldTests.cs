using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class HoldTests
{
    [Fact]
    public void Hold_Swaps_And_Is_Once_Per_Drop()
    {
        Simulation sim = new(CreateHoldLevel(), new Pcg32(1));

        Assert.Equal(PieceId.I4, sim.ActivePiece!.Piece.Id);

        sim.Tick(InputCommand.Hold);
        Assert.Equal(PieceId.O2, sim.ActivePiece!.Piece.Id);

        sim.Tick(InputCommand.Hold);
        Assert.Equal(PieceId.O2, sim.ActivePiece!.Piece.Id);

        sim.Tick(InputCommand.HardDrop);
        Assert.Equal(PieceId.L3, sim.ActivePiece!.Piece.Id);

        sim.Tick(InputCommand.Hold);
        Assert.Equal(PieceId.I4, sim.ActivePiece!.Piece.Id);
    }

    [Fact]
    public void Hold_Resets_Orientation_To_Spawn()
    {
        Level level = CreateHoldLevel(["L3", "O2", "I4"]);
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.RotatePieceYawCW);
        int rotatedIndex = sim.ActivePiece!.Piece.OrientationIndex;
        Assert.NotEqual(0, rotatedIndex);

        sim.Tick(InputCommand.Hold);
        sim.Tick(InputCommand.HardDrop);

        sim.Tick(InputCommand.Hold);
        Assert.Equal(0, sim.ActivePiece!.Piece.OrientationIndex);
    }

    private static Level CreateHoldLevel(string[]? sequence = null) =>
        new(
            new LevelMeta("hold-test", "Hold Test", "0.2.0", 1U),
            new Int3(6, 12, 6),
            [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", sequence ?? ["I4", "O2", "L3"], null),
            [],
            new AbilitiesConfig(HoldEnabled: true, StabilizeCharges: 0),
            new ConstraintsConfig());
}

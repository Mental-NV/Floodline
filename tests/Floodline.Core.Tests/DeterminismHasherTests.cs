using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class DeterminismHasherTests
{
    [Fact]
    public void Hash_IsStable_ForSameInputs()
    {
        Level level = CreateTestLevel();
        Simulation first = new(level, new Pcg32(12345));
        Simulation second = new(level, new Pcg32(12345));

        InputCommand[] commands =
        [
            InputCommand.None,
            InputCommand.MoveLeft,
            InputCommand.RotatePieceYawCW,
            InputCommand.HardDrop,
            InputCommand.None
        ];

        foreach (InputCommand command in commands)
        {
            first.Tick(command);
            second.Tick(command);
        }

        string hashA = first.ComputeDeterminismHash();
        string hashB = second.ComputeDeterminismHash();

        Assert.Equal(hashA, hashB);
    }

    private static Level CreateTestLevel() =>
        new(
            new LevelMeta("test_id", "Test Level", 1, 12345U),
            new Int3(6, 8, 6),
            [],
            [],
            new RotationConfig(),
            new BagConfig("FIXED_SEQUENCE", ["I4"], null),
            []);
}

using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class RotationConstraintTests
{
    private static Level CreateLevel(RotationConfig rotation) =>
        new(
            new("test_id", "Test Title", "0.2.0", 12345U),
            new(10, 20, 10),
            [],
            [],
            rotation,
            new("FIXED_SEQUENCE", ["I4"], null),
            []
        );

    [Fact]
    public void RotationRejected_When_Direction_Disallowed()
    {
        Level level = CreateLevel(new RotationConfig(AllowedDirections: ["DOWN"]));
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.RotateWorldForward);

        Assert.Equal(GravityDirection.Down, sim.Gravity);
    }

    [Fact]
    public void RotationRespects_Cooldown_And_MaxRotations()
    {
        Level level = CreateLevel(new RotationConfig(MaxRotations: 2, CooldownTicks: 1));
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.RotateWorldForward);
        Assert.Equal(GravityDirection.North, sim.Gravity);

        sim.Tick(InputCommand.RotateWorldBack);
        Assert.Equal(GravityDirection.North, sim.Gravity);

        sim.Tick(InputCommand.RotateWorldBack);
        Assert.Equal(GravityDirection.Down, sim.Gravity);

        sim.Tick(InputCommand.RotateWorldForward);
        Assert.Equal(GravityDirection.Down, sim.Gravity);
    }

    [Fact]
    public void RotationRejected_When_TiltBudget_Exceeded()
    {
        Level level = CreateLevel(new RotationConfig(TiltBudget: 1));
        Simulation sim = new(level, new Pcg32(1));

        sim.Tick(InputCommand.RotateWorldForward);
        Assert.Equal(GravityDirection.North, sim.Gravity);

        sim.Tick(InputCommand.RotateWorldBack);
        Assert.Equal(GravityDirection.North, sim.Gravity);
    }
}

using Floodline.Core.Levels;
using Floodline.Core.Movement;

namespace Floodline.Core.Tests.Movement;

public class MovementConstraintTests
{
    private readonly Grid _grid;

    public MovementConstraintTests()
    {
        _grid = new Grid(new Int3(10, 20, 10));
    }

    [Fact]
    public void DefaultConstraints_AllowYaw_RejectPitchAndRoll()
    {
        // Default RotationConfig should allow only Yaw.
        var controller = new MovementController(_grid);
        PieceDefinition i3 = PieceLibrary.Get(PieceId.I3);
        OrientedPiece piece = new(i3.Id, i3.UniqueOrientations[0], 0);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 5, 5));

        // Yaw allowed
        Assert.True(controller.ProcessInput(InputCommand.RotatePieceYawCW).Accepted);
        Assert.True(controller.ProcessInput(InputCommand.RotatePieceYawCCW).Accepted);

        // Pitch rejected
        Assert.False(controller.ProcessInput(InputCommand.RotatePiecePitchCW).Accepted);
        Assert.False(controller.ProcessInput(InputCommand.RotatePiecePitchCCW).Accepted);

        // Roll rejected
        Assert.False(controller.ProcessInput(InputCommand.RotatePieceRollCW).Accepted);
        Assert.False(controller.ProcessInput(InputCommand.RotatePieceRollCCW).Accepted);
    }

    [Fact]
    public void ExplicitConstraints_AllowSpecifiedAxes()
    {
        // Config allowing Pitch and Roll but NOT Yaw.
        var config = new RotationConfig
        {
            AllowedPieceRotationAxes = [RotationAxis.Pitch, RotationAxis.Roll]
        };
        var controller = new MovementController(_grid, config);
        PieceDefinition i3 = PieceLibrary.Get(PieceId.I3);
        OrientedPiece piece = new(i3.Id, i3.UniqueOrientations[0], 0);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 5, 5));

        // Yaw rejected
        Assert.False(controller.ProcessInput(InputCommand.RotatePieceYawCW).Accepted);

        // Pitch allowed
        Assert.True(controller.ProcessInput(InputCommand.RotatePiecePitchCW).Accepted);

        // Roll allowed
        Assert.True(controller.ProcessInput(InputCommand.RotatePieceRollCW).Accepted);
    }

    [Fact]
    public void EmptyConstraints_AllowAllAxes()
    {
        // Empty array means no restrictions (all allowed).
        var config = new RotationConfig
        {
            AllowedPieceRotationAxes = []
        };
        var controller = new MovementController(_grid, config);
        PieceDefinition i3 = PieceLibrary.Get(PieceId.I3);
        OrientedPiece piece = new(i3.Id, i3.UniqueOrientations[0], 0);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 5, 5));

        Assert.True(controller.ProcessInput(InputCommand.RotatePieceYawCW).Accepted);
        Assert.True(controller.ProcessInput(InputCommand.RotatePiecePitchCW).Accepted);
        Assert.True(controller.ProcessInput(InputCommand.RotatePieceRollCW).Accepted);
    }
}

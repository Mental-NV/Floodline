using Floodline.Core;
using Floodline.Core.Movement;

namespace Floodline.Core.Tests.Movement;

public class WorldRotationTests
{
    [Fact]
    public void WorldRotationUpdatesGravityCorrectly()
    {
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);

        // Default is Down
        Assert.Equal(GravityDirection.Down, controller.Gravity);

        // Tilt Forward (PitchCW) -> Gravity becomes North (0,0,-1)
        controller.ResolveWorldRotation(WorldRotationDirection.TiltForward);
        Assert.Equal(GravityDirection.North, controller.Gravity);

        // Reset gravity for next check
        controller.SetGravity(GravityDirection.Down);

        // Tilt Back (PitchCCW) -> Gravity becomes South (0,0,1)
        controller.ResolveWorldRotation(WorldRotationDirection.TiltBack);
        Assert.Equal(GravityDirection.South, controller.Gravity);
    }

    [Fact]
    public void WorldRotationTransformsActivePiece()
    {
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);

        // I piece (long bar)
        PieceDefinition def = PieceLibrary.Get(PieceId.I4);
        OrientedPiece piece = new(PieceId.I4, def.UniqueOrientations[0], 0);
        Int3 origin = new(5, 10, 5);
        controller.CurrentPiece = new ActivePiece(piece, origin);

        // Initial orientation
        System.Collections.Generic.IReadOnlyList<Int3> initialVoxels = controller.CurrentPiece.Piece.Voxels;

        // Tilt Right (RollCCW)
        controller.ResolveWorldRotation(WorldRotationDirection.TiltRight);

        // Piece orientation should change
        Assert.NotEqual(initialVoxels, controller.CurrentPiece.Piece.Voxels);
    }
}

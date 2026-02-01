using Floodline.Core.Movement;

namespace Floodline.Core.Tests.Movement;

/// <summary>
/// Tests for active piece movement and collision detection.
/// Grid coordinate convention: Forward/North = -Z, Back/South = +Z, Right = +X, Left = -X.
/// </summary>
public sealed class MovementTests
{
    [Fact]
    public void ActivePiece_GetWorldPositions_ReturnsAbsolutePositions()
    {
        // Arrange
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        OrientedPiece piece = new(PieceId.I4, voxels, 0);
        Int3 origin = new(5, 10, 3);
        ActivePiece activePiece = new(piece, origin);

        // Act
        IReadOnlyList<Int3> worldPositions = activePiece.GetWorldPositions();

        // Assert
        Assert.Equal(3, worldPositions.Count);
        Assert.Contains(new Int3(5, 10, 3), worldPositions);
        Assert.Contains(new Int3(6, 10, 3), worldPositions);
        Assert.Contains(new Int3(5, 11, 3), worldPositions);
    }

    [Fact]
    public void ActivePiece_TryTranslate_ValidPlacement_ReturnsTrue()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.True(result);
        Assert.Equal(new Int3(6, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_TryTranslate_OutOfBounds_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(9, 10, 5));

        // Act - try to move right, which would put voxel at x=11 (out of bounds)
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(9, 10, 5), activePiece.Origin); // Origin unchanged
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithSolid_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Solid, "STANDARD"));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act - try to move right into solid
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(5, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_TryTranslate_IntoWater_ReturnsTrue()
    {
        // Arrange - per Simulation_Rules_v0_2.md §4.2, WATER is passable
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), Voxel.Water);

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act - move into water cell
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.True(result);
        Assert.Equal(new Int3(6, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithWall_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Wall));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithBedrock_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Bedrock));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithIce_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Ice));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithDrain_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Drain));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithPorous_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Porous, "STANDARD"));

        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(5, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_TryTranslate_CollidesWithSolid_OnNonOriginVoxel_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        // Block the *second voxel* destination after MoveRight
        grid.SetVoxel(new Int3(6, 10, 5), new Voxel(OccupancyType.Solid, "STANDARD"));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        // Start at x=4 so origin moves to x=5 (empty), but second voxel moves to x=6 (solid)
        ActivePiece activePiece = new(piece, new Int3(4, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(4, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_TryTranslate_OutOfBounds_OnNonOriginVoxel_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);

        // Origin chosen so origin voxel stats in-bounds after move (x=8 -> x=9), but the second voxel goes OOB (x=9 -> x=10).
        ActivePiece activePiece = new(piece, new Int3(8, 10, 5));

        // Act
        bool result = activePiece.TryTranslate(new Int3(1, 0, 0), grid);

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(8, 10, 5), activePiece.Origin);
    }

    [Fact]
    public void ActivePiece_CanAdvanceInGravity_EmptyBelow_ReturnsTrue()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.CanAdvance(grid, GravityDirection.Down);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ActivePiece_CanAdvanceInGravity_SolidBelow_ReturnsFalse()
    {
        // Arrange - per §4.3, piece locks when it cannot advance
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(5, 9, 5), new Voxel(OccupancyType.Solid, "STANDARD"));

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.CanAdvance(grid, GravityDirection.Down);

        // Assert - lock condition detected
        Assert.False(result);
    }

    [Fact]
    public void ActivePiece_CanAdvanceInGravity_WaterBelow_ReturnsTrue()
    {
        // Arrange - per §4.4, piece can enter water cells
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(5, 9, 5), Voxel.Water);

        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 10, 5));

        // Act
        bool result = activePiece.CanAdvance(grid, GravityDirection.Down);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ActivePiece_CanAdvanceInGravity_OutOfBounds_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        List<Int3> voxels = [new(0, 0, 0), new(1, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        ActivePiece activePiece = new(piece, new Int3(5, 0, 5));

        // Act - at y=0, moving down would go to y=-1 (out of bounds)
        bool result = activePiece.CanAdvance(grid, GravityDirection.Down);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MovementController_ProcessInput_MoveLeft_Success()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveLeft);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.Equal(new Int3(4, 10, 5), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ProcessInput_MoveRight_Success()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveRight);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.Equal(new Int3(6, 10, 5), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ProcessInput_MoveForward_Success()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveForward);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.Equal(new Int3(5, 10, 4), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ProcessInput_MoveBack_Success()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveBack);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.Equal(new Int3(5, 10, 6), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ApplyGravityStep_MovesDownWhenClear()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        bool result = controller.ApplyGravityStep();

        // Assert
        Assert.True(result);
        Assert.Equal(new Int3(5, 9, 5), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ApplyGravityStep_BlockedBySolid_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(5, 9, 5), new Voxel(OccupancyType.Solid, "STANDARD"));

        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        bool result = controller.ApplyGravityStep();

        // Assert
        Assert.False(result);
        Assert.Equal(new Int3(5, 10, 5), controller.CurrentPiece.Origin); // No movement
    }

    [Fact]
    public void MovementController_ProcessInput_SoftDrop_AppliesGravityStep()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.SoftDrop);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.Equal(new Int3(5, 9, 5), controller.CurrentPiece.Origin);
    }

    [Fact]
    public void MovementController_ProcessInput_HardDrop_MovesToBottom()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(5, 2, 5), new Voxel(OccupancyType.Solid, "STANDARD"));

        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.HardDrop);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Moved);
        Assert.True(result.LockRequested);
        Assert.Equal(new Int3(5, 3, 5), controller.CurrentPiece.Origin); // Stopped at y=3 (above solid at y=2)
    }

    [Fact]
    public void MovementController_ProcessInput_NoPiece_ReturnsFalse()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        MovementController controller = new(grid);

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveLeft);

        // Assert
        Assert.False(result.Accepted);
    }

    [Fact]
    public void MovementController_ProcessInput_MoveLeft_Blocked_ReturnsAcceptedButNotMoved()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(4, 10, 5), new Voxel(OccupancyType.Solid, "STANDARD")); // Block left movement

        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.MoveLeft);

        // Assert
        Assert.True(result.Accepted);
        Assert.False(result.Moved);
        Assert.False(result.LockRequested);
        Assert.Equal(new Int3(5, 10, 5), controller.CurrentPiece.Origin); // Did not move
    }

    [Fact]
    public void MovementController_ProcessInput_HardDrop_Grounded_RequestsLock()
    {
        // Arrange
        Grid grid = new(new Int3(10, 20, 10));
        grid.SetVoxel(new Int3(5, 9, 5), new Voxel(OccupancyType.Solid, "STANDARD")); // Block gravity (grounded)

        MovementController controller = new(grid);
        List<Int3> voxels = [new(0, 0, 0)];
        OrientedPiece piece = new(PieceId.O2, voxels, 0);
        controller.SetGravity(GravityDirection.Down);
        controller.CurrentPiece = new ActivePiece(piece, new Int3(5, 10, 5));

        // Act
        InputApplyResult result = controller.ProcessInput(InputCommand.HardDrop);

        // Assert
        Assert.True(result.Accepted);
        Assert.False(result.Moved); // Already grounded, so no movement
        Assert.True(result.LockRequested); // BUT, lock is requested
    }
}

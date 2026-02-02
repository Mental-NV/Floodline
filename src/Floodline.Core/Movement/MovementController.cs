using System.Linq;
using Floodline.Core.Levels;

namespace Floodline.Core.Movement;

/// <summary>
/// Processes per-tick input commands and applies gravity to the active piece.
/// Implements movement logic per Input_Feel_v0_2.md §2.
/// </summary>
/// <param name="grid">The grid.</param>
/// <param name="rotationConfig">The rotation configuration for the level.</param>
public sealed class MovementController(Grid grid, RotationConfig? rotationConfig = null)
{
    private static readonly RotationAxis[] DefaultAllowedAxes = [RotationAxis.Yaw];

    /// <summary>
    /// Gets or sets the current active piece.
    /// </summary>
    public ActivePiece? CurrentPiece { get; set; }

    /// <summary>
    /// Gets the grid.
    /// </summary>
    public Grid Grid { get; } = grid ?? throw new ArgumentNullException(nameof(grid));

    /// <summary>
    /// Gets the rotation configuration.
    /// </summary>
    public RotationConfig RotationConfig { get; } = rotationConfig ?? new RotationConfig();

    /// <summary>
    /// Gets the current world gravity direction.
    /// Default is Down (-Y).
    /// </summary>
    public GravityDirection Gravity { get; private set; } = GravityDirection.Down;

    /// <summary>
    /// Sets the current world gravity direction.
    /// </summary>
    /// <param name="gravity">The new gravity direction.</param>
    public void SetGravity(GravityDirection gravity) => Gravity = gravity;

    private bool IsRotationAxisAllowed(RotationAxis axis)
    {
        RotationAxis[] allowed = RotationConfig.AllowedPieceRotationAxes ?? DefaultAllowedAxes;
        return allowed.Length == 0 || allowed.Contains(axis);
    }

    /// <summary>
    /// Processes a single input command for the current tick.
    /// Per Input_Feel_v0_2.md §2, commands are applied in canonical order.
    /// </summary>
    /// <param name="command">The input command to process.</param>
    /// <returns>A result describing the outcome of the command.</returns>
    public InputApplyResult ProcessInput(InputCommand command) =>
        CurrentPiece is null
            ? new InputApplyResult(Accepted: false, Moved: false, LockRequested: false)
            : command switch
            {
                InputCommand.MoveLeft =>
                    ResultFromMove(CurrentPiece.TryTranslate(new Int3(-1, 0, 0), Grid)),

                InputCommand.MoveRight =>
                    ResultFromMove(CurrentPiece.TryTranslate(new Int3(1, 0, 0), Grid)),

                InputCommand.MoveForward =>
                    ResultFromMove(CurrentPiece.TryTranslate(new Int3(0, 0, -1), Grid)),

                InputCommand.MoveBack =>
                    ResultFromMove(CurrentPiece.TryTranslate(new Int3(0, 0, 1), Grid)),

                InputCommand.SoftDrop =>
                    ResultFromMove(ApplyGravityStep()),

                InputCommand.HardDrop =>
                    ApplyHardDropResult(),

                InputCommand.RotatePieceYawCW =>
                    IsRotationAxisAllowed(RotationAxis.Yaw)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.YawCW, Grid))
                        : Reject(),

                InputCommand.RotatePieceYawCCW =>
                    IsRotationAxisAllowed(RotationAxis.Yaw)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.YawCCW, Grid))
                        : Reject(),

                InputCommand.RotatePiecePitchCW =>
                    IsRotationAxisAllowed(RotationAxis.Pitch)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.PitchCW, Grid))
                        : Reject(),

                InputCommand.RotatePiecePitchCCW =>
                    IsRotationAxisAllowed(RotationAxis.Pitch)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.PitchCCW, Grid))
                        : Reject(),

                InputCommand.RotatePieceRollCW =>
                    IsRotationAxisAllowed(RotationAxis.Roll)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.RollCW, Grid))
                        : Reject(),

                InputCommand.RotatePieceRollCCW =>
                    IsRotationAxisAllowed(RotationAxis.Roll)
                        ? ResultFromMove(CurrentPiece.AttemptRotation(Matrix3x3.RollCCW, Grid))
                        : Reject(),

                InputCommand.RotateWorldForward =>
                    ResultFromMove(ResolveWorldRotation(WorldRotationDirection.TiltForward)),

                InputCommand.RotateWorldBack =>
                    ResultFromMove(ResolveWorldRotation(WorldRotationDirection.TiltBack)),

                InputCommand.RotateWorldLeft =>
                    ResultFromMove(ResolveWorldRotation(WorldRotationDirection.TiltLeft)),

                InputCommand.RotateWorldRight =>
                    ResultFromMove(ResolveWorldRotation(WorldRotationDirection.TiltRight)),

                InputCommand.None =>
                    new InputApplyResult(Accepted: true, Moved: false, LockRequested: false),

                _ =>
                    new InputApplyResult(Accepted: false, Moved: false, LockRequested: false)
            };

    /// <summary>
    /// Resolves a world rotation.
    /// Updates gravity and transforms the active piece's orientation.
    /// </summary>
    /// <param name="direction">The tilt direction.</param>
    /// <returns>True if the rotation was accepted and applied; otherwise, false.</returns>
    public bool ResolveWorldRotation(WorldRotationDirection direction)
    {
        // 1. Get rotation matrix
        Matrix3x3 matrix = GravityTable.GetMatrix(direction);

        // 2. Compute new gravity
        GravityDirection newGravity = GravityTable.GetRotatedGravity(Gravity, matrix);

        // 3. Check level constraints (future work: cooldown, budget)
        // For now, assume all directions in {Down, North, South, East, West} are valid
        // per Simulation_Rules §2.3.

        // 4. Update active piece if it exists
        if (CurrentPiece is not null)
        {
            // Per Simulation_Rules §3.2, rotation is rejected if active piece would collide
            // In world rotation, the piece voxels transform, but they should stay at the same
            // board positions relative to the piece origin.
            // Wait, the piece orientation must change so it follows the world.
            // If the world rotates PitchCW, the piece must rotate PitchCW relative to its local origin.

            if (!CurrentPiece.AttemptRotation(matrix, Grid))
            {
                // Partial implementation: rotation rejected if piece can't fit in new orientation
                return false;
            }
        }

        // 5. Apply new gravity
        Gravity = newGravity;

        // Note: Tilt Resolve for settled world (solids + water) will be handled in Resolve Phase (FL-0109+)
        return true;
    }

    private static InputApplyResult ResultFromMove(bool moved) =>
        new(Accepted: true, Moved: moved, LockRequested: false);

    private static InputApplyResult Reject() =>
        new(Accepted: false, Moved: false, LockRequested: false);

    private InputApplyResult ApplyHardDropResult()
    {
        bool movedAtLeastOnce = false;
        while (ApplyGravityStep())
        {
            movedAtLeastOnce = true;
        }

        // Hard drop semantics: accepted => lock requested even if it didn't move collision-wise
        return new InputApplyResult(Accepted: true, Moved: movedAtLeastOnce, LockRequested: true);
    }

    /// <summary>
    /// Applies a single gravity step to the active piece.
    /// Moves the piece one cell in the gravity direction if possible.
    /// </summary>
    /// <returns>True if the piece moved; false if blocked (lock condition).</returns>
    public bool ApplyGravityStep()
    {
        if (CurrentPiece is null)
        {
            return false;
        }

        Int3 gravityVector = GravityTable.GetVector(Gravity);
        return CurrentPiece.TryTranslate(gravityVector, Grid);
    }


}

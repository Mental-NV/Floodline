namespace Floodline.Core.Movement;

/// <summary>
/// Processes per-tick input commands and applies gravity to the active piece.
/// Implements movement logic per Input_Feel_v0_2.md §2.
/// </summary>
/// <param name="grid">The grid.</param>
public sealed class MovementController(Grid grid)
{
    /// <summary>
    /// Gets or sets the current active piece.
    /// </summary>
    public ActivePiece? CurrentPiece { get; set; }

    /// <summary>
    /// Gets the grid.
    /// </summary>
    public Grid Grid { get; } = grid ?? throw new ArgumentNullException(nameof(grid));

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

                InputCommand.RotatePiece =>
                    new InputApplyResult(Accepted: false, Moved: false, LockRequested: false), // Placeholder for FL-0107

                InputCommand.RotateWorld =>
                    new InputApplyResult(Accepted: false, Moved: false, LockRequested: false), // Placeholder for FL-0108

                InputCommand.None =>
                    new InputApplyResult(Accepted: true, Moved: false, LockRequested: false),

                _ =>
                    new InputApplyResult(Accepted: false, Moved: false, LockRequested: false)
            };

    private static InputApplyResult ResultFromMove(bool moved) =>
        new(Accepted: true, Moved: moved, LockRequested: false);

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

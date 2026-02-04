namespace Floodline.Core.Movement;

/// <summary>
/// Defines per-tick input commands for the active piece.
/// Commands are processed in canonical order per Input_Feel_v0_2.md §2.
/// </summary>
public enum InputCommand
{
    /// <summary>
    /// No input this tick.
    /// </summary>
    None = 0,

    /// <summary>
    /// Move piece left (negative X direction in world space).
    /// </summary>
    MoveLeft,

    /// <summary>
    /// Move piece right (positive X direction in world space).
    /// </summary>
    MoveRight,

    /// <summary>
    /// Move piece forward/north (negative Z direction in world space).
    /// </summary>
    MoveForward,

    /// <summary>
    /// Move piece back/south (positive Z direction in world space).
    /// </summary>
    MoveBack,

    /// <summary>
    /// Hold the current piece (once per drop).
    /// </summary>
    Hold,

    /// <summary>
    /// Rotate the piece Yaw CW (around the vertical Y axis).
    /// </summary>
    RotatePieceYawCW,

    /// <summary>
    /// Rotate the piece Yaw CCW (around the vertical Y axis).
    /// </summary>
    RotatePieceYawCCW,

    /// <summary>
    /// Rotate the piece Pitch CW (around the horizontal X axis).
    /// </summary>
    RotatePiecePitchCW,

    /// <summary>
    /// Rotate the piece Pitch CCW (around the horizontal X axis).
    /// </summary>
    RotatePiecePitchCCW,

    /// <summary>
    /// Rotate the piece Roll CW (around the depth Z axis).
    /// </summary>
    RotatePieceRollCW,

    /// <summary>
    /// Rotate the piece Roll CCW (around the depth Z axis).
    /// </summary>
    RotatePieceRollCCW,

    /// <summary>
    /// Soft drop - accelerate gravity stepping to 1 cell per tick.
    /// </summary>
    SoftDrop,

    /// <summary>
    /// Hard drop - immediately place and lock the piece.
    /// </summary>
    HardDrop,

    /// <summary>
    /// Rotate the world Forward (Tilt Forward).
    /// </summary>
    RotateWorldForward,

    /// <summary>
    /// Rotate the world Back (Tilt Back).
    /// </summary>
    RotateWorldBack,

    /// <summary>
    /// Rotate the world Left (Tilt Left).
    /// </summary>
    RotateWorldLeft,

    /// <summary>
    /// Rotate the world Right (Tilt Right).
    /// </summary>
    RotateWorldRight
}

/// <summary>
/// Defines world rotation directions (placeholder for FL-0108).
/// </summary>
public enum WorldRotationDirection
{
    /// <summary>
    /// Tilt forward.
    /// </summary>
    TiltForward,

    /// <summary>
    /// Tilt back.
    /// </summary>
    TiltBack,

    /// <summary>
    /// Tilt left.
    /// </summary>
    TiltLeft,

    /// <summary>
    /// Tilt right.
    /// </summary>
    TiltRight
}

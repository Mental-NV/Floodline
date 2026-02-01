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
    /// Rotate the piece locally (implementation in FL-0107).
    /// </summary>
    RotatePiece,

    /// <summary>
    /// Soft drop - accelerate gravity stepping to 1 cell per tick.
    /// </summary>
    SoftDrop,

    /// <summary>
    /// Hard drop - immediately place and lock the piece.
    /// </summary>
    HardDrop,

    /// <summary>
    /// Rotate the world (snap 90°) - implementation in FL-0108.
    /// </summary>
    RotateWorld
}

/// <summary>
/// Defines local piece rotation axes (placeholder for FL-0107).
/// </summary>
public enum PieceRotationAxis
{
    /// <summary>
    /// Yaw rotation (around vertical axis).
    /// </summary>
    Yaw,

    /// <summary>
    /// Pitch rotation (around horizontal axis).
    /// </summary>
    Pitch,

    /// <summary>
    /// Roll rotation (around depth axis).
    /// </summary>
    Roll
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

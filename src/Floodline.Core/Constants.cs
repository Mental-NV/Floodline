namespace Floodline.Core;

/// <summary>
/// Canonical constants for the deterministic simulation.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The fixed tick rate for the simulation (60 Hz).
    /// All durations in level data and replay must be in integer ticks based on this rate.
    /// </summary>
    public const int TickHz = 60;

    /// <summary>
    /// Natural gravity cadence: one cell per N ticks.
    /// </summary>
    public const int GravityTicksPerStep = 2;

    /// <summary>
    /// Lock delay in ticks after the piece becomes grounded.
    /// </summary>
    public const int LockDelayTicks = 12;

    /// <summary>
    /// Maximum number of lock delay resets per piece.
    /// </summary>
    public const int MaxLockResets = 4;
}

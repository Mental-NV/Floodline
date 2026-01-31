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
}

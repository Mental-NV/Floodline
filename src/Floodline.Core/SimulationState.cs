namespace Floodline.Core;

/// <summary>
/// Status of the current simulation.
/// </summary>
public enum SimulationStatus
{
    /// <summary>
    /// Game is currently being played.
    /// </summary>
    InProgress,

    /// <summary>
    /// Player has met all primary objectives.
    /// </summary>
    Won,

    /// <summary>
    /// Player has hit a fail state.
    /// </summary>
    Lost
}

/// <summary>
/// Represents the high-level state of a simulation run.
/// </summary>
/// <param name="Status">The current status.</param>
/// <param name="TicksElapsed">Total ticks since level start.</param>
/// <param name="PiecesLocked">Number of pieces that have locked into the grid.</param>
public record SimulationState(
    SimulationStatus Status,
    long TicksElapsed,
    int PiecesLocked
);

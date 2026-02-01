namespace Floodline.Core.Movement;

/// <summary>
/// Result of applying an input command to the movement controller.
/// </summary>
/// <param name="Accepted">Whether the command was accepted/handled by the controller.</param>
/// <param name="Moved">Whether the command resulted in any movement/translation.</param>
/// <param name="LockRequested">Whether the command requests an immediate lock (e.g. HardDrop).</param>
public readonly record struct InputApplyResult(
    bool Accepted,
    bool Moved,
    bool LockRequested
);

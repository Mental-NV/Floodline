namespace Floodline.Core.Random;

/// <summary>
/// Exposes deterministic PRNG state for hashing and replay validation.
/// </summary>
public interface IRandomState
{
    /// <summary>
    /// Gets the current internal state.
    /// </summary>
    ulong State { get; }
}

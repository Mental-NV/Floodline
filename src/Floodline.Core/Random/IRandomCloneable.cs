namespace Floodline.Core.Random;

/// <summary>
/// Provides a deterministic clone of a random generator.
/// </summary>
public interface IRandomCloneable
{
    /// <summary>
    /// Creates a clone with identical internal state.
    /// </summary>
    IRandom Clone();
}

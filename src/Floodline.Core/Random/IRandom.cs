namespace Floodline.Core.Random;

/// <summary>
/// Interface for a deterministic random number generator.
/// </summary>
public interface IRandom
{
    /// <summary>
    /// Returns the next random unsigned integer.
    /// </summary>
    uint Nextuint();

    /// <summary>
    /// Returns a non-negative random integer less than the specified maximum.
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A non-negative integer less than max.</returns>
    int NextInt(int max);

    /// <summary>
    /// Returns a random integer within the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer between min (inclusive) and max (exclusive).</returns>
    int NextInt(int min, int max);
}

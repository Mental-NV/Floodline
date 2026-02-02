namespace Floodline.Core.Random;

/// <summary>
/// Extension methods for IRandom.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Returns a random element from the specified collection.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="random">The random generator.</param>
    /// <param name="collection">The collection to choose from.</param>
    /// <returns>A random element from the collection.</returns>
    public static T NextChoice<T>(this IRandom random, IReadOnlyList<T> collection)
    {
        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (collection is null || collection.Count == 0)
        {
            throw new ArgumentException("Collection must not be null or empty.", nameof(collection));
        }

        int index = random.NextInt(collection.Count);
        return collection[index];
    }
}

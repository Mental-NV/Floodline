namespace Floodline.Core.Random;

/// <summary>
/// A minimal implementation of the PCG-XSH-RR generator.
/// This generator is strictly deterministic based on the provided seed.
/// See: https://www.pcg-random.org/
/// </summary>
public sealed class Pcg32 : IRandom, IRandomState
{
    // State for PCG-XSH-RR
    public ulong State { get; private set; }
    // We use a fixed increment for simplicity as we only need a single stream.
    // If we needed multiple streams we would make this configurable (must be odd).
    private const ulong Inc = 1442695040888963407UL;

    /// <summary>
    /// Initializes a new instance of Pcg32 with the given seed.
    /// </summary>
    /// <param name="seed">The seed value.</param>
    public Pcg32(ulong seed)
    {
        State = 0;
        Step();
        State += seed;
        Step();
    }

    private void Step() => State = (State * 6364136223846793005UL) + Inc;

    /// <inheritdoc/>
    public uint Nextuint()
    {
        ulong oldState = State;
        Step();
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rot = (int)(oldState >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <inheritdoc/>
    public int NextInt(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than 0");
        }

        // Simple rejection sampling to avoid bias
        uint threshold = (uint)((0x100000000UL - (ulong)max) % (ulong)max);

        while (true)
        {
            uint r = Nextuint();
            if (r >= threshold)
            {
                return (int)(r % (uint)max);
            }
        }
    }

    /// <inheritdoc/>
    public int NextInt(int min, int max)
    {
        if (max <= min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than min");
        }

        // Use uint to allow ranges larger than int.MaxValue (e.g. min=int.MinValue, max=int.MaxValue)
        uint range = (uint)max - (uint)min;
        uint threshold = (uint)((0x100000000UL - range) % range);

        while (true)
        {
            uint r = Nextuint();
            if (r >= threshold)
            {
                return (int)((uint)min + (r % range));
            }
        }
    }
}

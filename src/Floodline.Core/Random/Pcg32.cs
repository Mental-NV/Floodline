namespace Floodline.Core.Random;

/// <summary>
/// A minimal implementation of the PCG-XSH-RR generator.
/// This generator is strictly deterministic based on the provided seed.
/// See: https://www.pcg-random.org/
/// </summary>
public sealed class Pcg32 : IRandom
{
    // State for PCG-XSH-RR
    private ulong _state;
    // We use a fixed increment for simplicity as we only need a single stream.
    // If we needed multiple streams we would make this configurable (must be odd).
    private const ulong Inc = 1442695040888963407UL;

    /// <summary>
    /// Initializes a new instance of Pcg32 with the given seed.
    /// </summary>
    /// <param name="seed">The seed value.</param>
    public Pcg32(ulong seed)
    {
        _state = 0;
        Step();
        _state += seed;
        Step();
    }

    private void Step()
    {
        _state = (_state * 6364136223846793005UL) + Inc;
    }

    /// <inheritdoc/>
    public uint Nextuint()
    {
        ulong oldState = _state;
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
            throw new System.ArgumentOutOfRangeException(nameof(max), "Max must be greater than 0");
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
        return max <= min
            ? throw new System.ArgumentOutOfRangeException(nameof(max), "Max must be greater than min")
            : min + NextInt(max - min);
    }
}

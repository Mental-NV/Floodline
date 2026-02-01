using Floodline.Core.Random;

namespace Floodline.Core.Tests.Random;

public class Pcg32Tests
{
    [Fact]
    public void TickRateIsCanonical() => Assert.Equal(60, Constants.TickHz);

    [Fact]
    public void ConstructionIsDeterministic()
    {
        Pcg32 rng1 = new(12345);
        Pcg32 rng2 = new(12345);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.Nextuint(), rng2.Nextuint());
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentSequences()
    {
        Pcg32 rng1 = new(12345);
        Pcg32 rng2 = new(67890);

        bool allEqual = true;
        for (int i = 0; i < 100; i++)
        {
            if (rng1.Nextuint() != rng2.Nextuint())
            {
                allEqual = false;
                break;
            }
        }
        Assert.False(allEqual, "Different seeds should produce different sequences");
    }

    [Fact]
    public void NextIntMaxRespectsBounds()
    {
        Pcg32 rng = new(1);
        int max = 10;
        for (int i = 0; i < 1000; i++)
        {
            int val = rng.NextInt(max);
            Assert.True(val >= 0 && val < max, $"Value {val} out of bounds [0, {max})");
        }
    }

    [Fact]
    public void NextIntMinMaxRespectsBounds()
    {
        Pcg32 rng = new(1);
        int min = 5;
        int max = 15;
        for (int i = 0; i < 1000; i++)
        {
            int val = rng.NextInt(min, max);
            Assert.True(val >= min && val < max, $"Value {val} out of bounds [{min}, {max})");
        }
    }

    [Fact]
    public void NextIntMinMaxHandlesFullRange()
    {
        Pcg32 rng = new(1);
        // Range covers full 32-bit space: int.MinValue to int.MaxValue
        // range = 2^32 - 1, which fits in uint.
        // We chose constraints such that it runs multiple times without crashing.
        int min = int.MinValue;
        int max = int.MaxValue;

        for (int i = 0; i < 100; i++)
        {
            int val = rng.NextInt(min, max);
            Assert.True(val >= min && val < max, $"Value {val} out of bounds [{min}, {max})");
        }
    }

    [Fact]
    public void SequenceIsStableForSpecificSeed()
    {
        // Regression test: ensure the sequence doesn't change if implementation details change
        Pcg32 rng = new(42);

        // These values are captured from the canonical implementation
        Assert.Equal(3270867926u, rng.Nextuint());
        Assert.Equal(1795671209u, rng.Nextuint());
        Assert.Equal(1924641435u, rng.Nextuint());
        Assert.Equal(1143034755u, rng.Nextuint());
    }
}

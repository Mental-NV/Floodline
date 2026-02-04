using System;
using Floodline.Core;
using Floodline.Core.Levels;
using Floodline.Core.Random;
using Xunit;

namespace Floodline.Core.Tests;

public class BagTests
{
    [Fact]
    public void PieceBag_FixedSequence_Wraps_Deterministically()
    {
        PieceBag bag = new(
            new BagConfig("FIXED_SEQUENCE", ["I4", "O2"], null),
            new Pcg32(1));

        Assert.Equal(PieceId.I4, bag.DrawNext().PieceId);
        Assert.Equal(PieceId.O2, bag.DrawNext().PieceId);
        Assert.Equal(PieceId.I4, bag.DrawNext().PieceId);
    }

    [Fact]
    public void PieceBag_Weighted_IsStable_WithSeed()
    {
        BagConfig config = new("WEIGHTED", null, new()
        {
            ["I4"] = 1,
            ["O2"] = 3
        });

        PieceBag first = new(config, new Pcg32(42));
        PieceBag second = new(config, new Pcg32(42));

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(first.DrawNext(), second.DrawNext());
        }
    }

    [Fact]
    public void PieceBag_Parses_Material_Tokens()
    {
        PieceBag bag = new(
            new BagConfig("FIXED_SEQUENCE", ["I4:HEAVY"], null),
            new Pcg32(5));

        BagEntry entry = bag.DrawNext();

        Assert.Equal(PieceId.I4, entry.PieceId);
        Assert.Equal("HEAVY", entry.MaterialId);
    }

    [Fact]
    public void PieceBag_Peek_Does_Not_Advance_FixedSequence()
    {
        PieceBag bag = new(
            new BagConfig("FIXED_SEQUENCE", ["I4", "O2", "L3"], null),
            new Pcg32(1));

        BagEntry[] preview = [.. bag.PeekNext(2)];
        BagEntry first = bag.DrawNext();
        BagEntry second = bag.DrawNext();

        Assert.Equal(preview[0], first);
        Assert.Equal(preview[1], second);
    }

    [Fact]
    public void PieceBag_Peek_Does_Not_Advance_Weighted()
    {
        BagConfig config = new("WEIGHTED", null, new()
        {
            ["I4"] = 2,
            ["O2"] = 1
        });

        PieceBag bag = new(config, new Pcg32(7));
        BagEntry[] preview = [.. bag.PeekNext(3)];

        Assert.Equal(preview[0], bag.DrawNext());
        Assert.Equal(preview[1], bag.DrawNext());
        Assert.Equal(preview[2], bag.DrawNext());
    }

    [Fact]
    public void PieceBag_Invalid_Token_Throws()
    {
        BagConfig config = new("FIXED_SEQUENCE", ["NOT_A_PIECE"], null);

        Assert.Throws<ArgumentException>(() => new PieceBag(config, new Pcg32(1)));
    }
}

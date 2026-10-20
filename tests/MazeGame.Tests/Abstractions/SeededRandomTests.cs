using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Abstractions;

namespace MazeGame.Tests.Abstractions;

/// <summary>
/// Verifies that <see cref="SeededRandom"/> is reproducible, stays in range, and shuffles
/// without losing or duplicating items.
/// </summary>
public sealed class SeededRandomTests
{
    private const int SampleSeed = 12345;

    [Fact]
    public void NextInt_WithSameSeed_ProducesSameSequence()
    {
        var first = new SeededRandom(SampleSeed);
        var second = new SeededRandom(SampleSeed);

        for (var draw = 0; draw < 100; draw++)
        {
            Assert.Equal(first.NextInt(1000), second.NextInt(1000));
        }
    }

    [Fact]
    public void NextInt_WithDifferentSeeds_ProducesDifferentSequence()
    {
        var first = new SeededRandom(1);
        var second = new SeededRandom(2);

        var firstSequence = Enumerable.Range(0, 20).Select(_ => first.NextInt(1000)).ToArray();
        var secondSequence = Enumerable.Range(0, 20).Select(_ => second.NextInt(1000)).ToArray();

        Assert.NotEqual(firstSequence, secondSequence);
    }

    [Fact]
    public void NextInt_StaysWithinRequestedRange()
    {
        var random = new SeededRandom(SampleSeed);

        for (var draw = 0; draw < 1000; draw++)
        {
            var value = random.NextInt(7);

            Assert.InRange(value, 0, 6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextInt_WithNonPositiveBound_Throws(int maxExclusive)
    {
        var random = new SeededRandom(SampleSeed);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(maxExclusive));
    }

    [Fact]
    public void Shuffle_WithSameSeed_ProducesSameOrder()
    {
        var firstItems = Enumerable.Range(0, 25).ToList();
        var secondItems = Enumerable.Range(0, 25).ToList();

        new SeededRandom(SampleSeed).Shuffle(firstItems);
        new SeededRandom(SampleSeed).Shuffle(secondItems);

        Assert.Equal(firstItems, secondItems);
    }

    [Fact]
    public void Shuffle_KeepsEveryItemExactlyOnce()
    {
        var items = Enumerable.Range(0, 50).ToList();
        var original = items.ToArray();

        new SeededRandom(SampleSeed).Shuffle(items);

        Assert.Equal(original.Length, items.Count);
        Assert.Equal(original.OrderBy(value => value), items.OrderBy(value => value));
    }

    [Fact]
    public void Shuffle_WithEmptyList_DoesNothing()
    {
        var items = new List<int>();

        new SeededRandom(SampleSeed).Shuffle(items);

        Assert.Empty(items);
    }

    [Fact]
    public void Shuffle_WithNull_Throws()
    {
        var random = new SeededRandom(SampleSeed);

        Assert.Throws<ArgumentNullException>(() => random.Shuffle<int>(null!));
    }
}

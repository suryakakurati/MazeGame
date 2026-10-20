using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Behavioural tests for <see cref="DisjointSet"/>: set counting, union semantics,
/// path compression and input validation.
/// </summary>
public sealed class DisjointSetTests
{
    [Fact]
    public void Constructor_WithElements_StartsWithOneSetPerElement()
    {
        var sets = new DisjointSet(5);

        Assert.Equal(5, sets.Count);
    }

    [Fact]
    public void Constructor_WithZeroElements_IsEmptyButUsable()
    {
        var sets = new DisjointSet(0);

        Assert.Equal(0, sets.Count);
    }

    [Fact]
    public void Constructor_WithNegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisjointSet(-1));
    }

    [Fact]
    public void Find_OnFreshSet_ReturnsElementItself()
    {
        var sets = new DisjointSet(3);

        Assert.Equal(0, sets.Find(0));
        Assert.Equal(1, sets.Find(1));
        Assert.Equal(2, sets.Find(2));
    }

    [Fact]
    public void Find_AfterUnion_ReturnsSameRootForBothElements()
    {
        var sets = new DisjointSet(4);

        sets.Union(0, 1);

        Assert.Equal(sets.Find(0), sets.Find(1));
        Assert.NotEqual(sets.Find(0), sets.Find(2));
    }

    [Fact]
    public void Union_OfTwoSeparateSets_ReturnsTrueAndReducesCount()
    {
        var sets = new DisjointSet(4);

        var merged = sets.Union(0, 1);

        Assert.True(merged);
        Assert.Equal(3, sets.Count);
    }

    [Fact]
    public void Union_OfAlreadyConnectedElements_ReturnsFalseAndKeepsCount()
    {
        var sets = new DisjointSet(3);
        sets.Union(0, 1);
        sets.Union(1, 2);
        var countBefore = sets.Count;

        var merged = sets.Union(0, 2);

        Assert.False(merged);
        Assert.Equal(countBefore, sets.Count);
    }

    [Fact]
    public void IsConnected_BeforeUnion_IsFalse()
    {
        var sets = new DisjointSet(2);

        Assert.False(sets.IsConnected(0, 1));
    }

    [Fact]
    public void IsConnected_AfterUnion_IsSymmetric()
    {
        var sets = new DisjointSet(3);
        sets.Union(2, 0);

        Assert.True(sets.IsConnected(2, 0));
        Assert.True(sets.IsConnected(0, 2));
        Assert.False(sets.IsConnected(0, 1));
    }

    [Fact]
    public void Union_AllElements_LeavesExactlyOneSet()
    {
        var sets = new DisjointSet(8);

        for (var element = 1; element < 8; element++)
        {
            sets.Union(element - 1, element);
        }

        Assert.Equal(1, sets.Count);

        var root = sets.Find(0);
        for (var element = 1; element < 8; element++)
        {
            Assert.Equal(root, sets.Find(element));
        }
    }

    [Fact]
    public void Find_AfterRepeatedLookups_KeepsRootStable()
    {
        var sets = new DisjointSet(6);
        sets.Union(0, 1);
        sets.Union(1, 2);
        sets.Union(3, 4);
        var root = sets.Find(2);

        // Calling Find repeatedly must never change which set an element belongs to.
        for (var lookup = 0; lookup < 10; lookup++)
        {
            Assert.Equal(root, sets.Find(0));
            Assert.Equal(root, sets.Find(1));
            Assert.Equal(root, sets.Find(2));
        }

        Assert.Equal(3, sets.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Find_WithElementOutsideRange_Throws(int element)
    {
        var sets = new DisjointSet(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => sets.Find(element));
    }

    [Fact]
    public void Union_WithSecondElementOutsideRange_Throws()
    {
        var sets = new DisjointSet(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => sets.Union(0, 3));
    }

    [Fact]
    public void IsConnected_OnEmptySet_Throws()
    {
        var sets = new DisjointSet(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => sets.IsConnected(0, 0));
    }
}

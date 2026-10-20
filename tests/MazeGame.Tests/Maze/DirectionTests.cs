using System;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies the two facts the rest of the maze code relies on: opposite directions point
/// back, and every direction produces the matching row/column step.
/// </summary>
public sealed class DirectionTests
{
    [Theory]
    [InlineData(Direction.North, Direction.South)]
    [InlineData(Direction.East, Direction.West)]
    [InlineData(Direction.South, Direction.North)]
    [InlineData(Direction.West, Direction.East)]
    public void Opposite_ReturnsReverseDirection(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Opposite());
    }

    [Fact]
    public void Opposite_IsItsOwnInverse()
    {
        foreach (var direction in Enum.GetValues<Direction>())
        {
            Assert.Equal(direction, direction.Opposite().Opposite());
        }
    }

    [Theory]
    [InlineData(Direction.North, -1, 0)]
    [InlineData(Direction.East, 0, 1)]
    [InlineData(Direction.South, 1, 0)]
    [InlineData(Direction.West, 0, -1)]
    public void Delta_ReturnsUnitStepForDirection(Direction direction, int expectedRow, int expectedColumn)
    {
        var (row, column) = direction.Delta();

        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedColumn, column);
    }

    [Fact]
    public void Opposite_WithUndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Direction)99).Opposite());
    }

    [Fact]
    public void Delta_WithUndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Direction)99).Delta());
    }
}

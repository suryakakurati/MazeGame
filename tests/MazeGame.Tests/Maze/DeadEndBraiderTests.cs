using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies the dead-end braid: it opens one extra wall at the requested share of dead ends,
/// leaves the maze connected, never opens the outer boundary, and cannot invent new traps.
/// </summary>
/// <remarks>
/// The counts matter as much as the safety properties. Braiding is what decides how often a
/// chase ends in a corner, so a test that only checked "it does not throw" would miss the whole
/// point of the class.
/// </remarks>
public sealed class DeadEndBraiderTests
{
    private const int Size = 8;

    [Fact]
    public void Generate_WithFullBraid_LeavesNoDeadEnds()
    {
        var maze = Generate(seed: 3, percent: 100);

        Assert.Equal(0, DeadEndCount(maze));
    }

    [Fact]
    public void Generate_WithFullBraid_OpensAtMostOneWallPerDeadEnd()
    {
        // One opening can braid two neighbouring dead ends at once, so the number of new
        // passages lies between half the dead ends and all of them. More than that would mean
        // the braid is adding holes the requested share never asked for; fewer would mean it
        // failed to braid everything.
        var perfect = Generate(seed: 4, percent: 0);
        var braided = Generate(seed: 4, percent: 100);
        var deadEnds = DeadEndCount(perfect);

        Assert.InRange(braided.CountPassages() - perfect.CountPassages(), (deadEnds + 1) / 2, deadEnds);
    }

    [Fact]
    public void Generate_WithHalfTheDeadEnds_LeavesAboutHalfOfThem()
    {
        var perfect = Generate(seed: 5, percent: 0);
        var braided = Generate(seed: 5, percent: 50);

        var before = DeadEndCount(perfect);
        var after = DeadEndCount(braided);

        Assert.True(after < before, "braiding must reduce the number of dead ends");
        Assert.True(after > 0, "half a braid must leave some traps behind");
        Assert.InRange(after, (int)(before * 0.3), (int)(before * 0.7));
    }

    [Fact]
    public void Generate_WithMoreBraiding_LeavesFewerDeadEnds()
    {
        var few = Generate(seed: 6, percent: 25);
        var many = Generate(seed: 6, percent: 75);

        Assert.True(DeadEndCount(many) < DeadEndCount(few));
    }

    [Fact]
    public void Generate_WithZeroPercent_LeavesAPerfectMaze()
    {
        var maze = Generate(seed: 7, percent: 0);

        Assert.Equal((Size * Size) - 1, maze.CountPassages());
    }

    [Fact]
    public void Generate_KeepsEveryCellReachable()
    {
        var maze = Generate(seed: 8, percent: 100);

        Assert.Equal(maze.CellCount, ReachableCount(maze));
    }

    [Fact]
    public void Generate_NeverOpensTheOuterBoundary()
    {
        var maze = Generate(seed: 9, percent: 100);

        for (var column = 0; column < maze.Columns; column++)
        {
            Assert.True(maze.HasWall(0, column, Direction.North));
            Assert.True(maze.HasWall(maze.Rows - 1, column, Direction.South));
        }

        for (var row = 0; row < maze.Rows; row++)
        {
            Assert.True(maze.HasWall(row, 0, Direction.West));
            Assert.True(maze.HasWall(row, maze.Columns - 1, Direction.East));
        }
    }

    [Fact]
    public void Generate_WithTheSameSeed_ProducesTheSameMaze()
    {
        var first = Generate(seed: 10, percent: 60);
        var second = Generate(seed: 10, percent: 60);

        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                Assert.Equal(first.GetCell(row, column), second.GetCell(row, column));
            }
        }
    }

    [Fact]
    public void Generate_OnAOneRowCorridor_SkipsDeadEndsWithNoInteriorWall()
    {
        // The ends of a straight corridor are dead ends whose three walls are all on the
        // boundary. There is nothing to open, so the braid must leave the corridor alone
        // instead of throwing or punching a hole out of the maze.
        var generator = new DeadEndBraider(new MazeGenerator(new SeededRandom(11)), new SeededRandom(11), percent: 100);

        var maze = generator.Generate(new MazeOptions(1, 8, 11));

        Assert.Equal(7, maze.CountPassages());
        Assert.True(maze.HasWall(0, 0, Direction.West));
        Assert.True(maze.HasWall(0, 7, Direction.East));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_WithPercentOutsideZeroToHundred_Throws(int percent)
    {
        var inner = new MazeGenerator(new SeededRandom(1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DeadEndBraider(inner, new SeededRandom(1), percent));
    }

    [Fact]
    public void Constructor_WithNullInnerGenerator_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DeadEndBraider(null!, new SeededRandom(1), percent: 50));
    }

    private static Grid Generate(int seed, int percent) =>
        new DeadEndBraider(new MazeGenerator(new SeededRandom(seed)), new SeededRandom(seed), percent)
            .Generate(new MazeOptions(Size, Size, seed));

    /// <summary>Counts cells with exactly one opening.</summary>
    private static int DeadEndCount(Grid maze)
    {
        var deadEnds = 0;

        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (maze.GetCell(row, column).WallCount == 3)
                {
                    deadEnds++;
                }
            }
        }

        return deadEnds;
    }

    /// <summary>
    /// Counts the cells reachable from (0, 0) through open passages, using breadth-first search.
    /// </summary>
    private static int ReachableCount(Grid maze)
    {
        var seen = new HashSet<Cell> { maze.GetCell(0, 0) };
        var frontier = new Queue<Cell>();
        frontier.Enqueue(maze.GetCell(0, 0));

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();

            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbour = maze.GetCell(cell.Row + rowDelta, cell.Column + columnDelta);

                if (seen.Add(neighbour))
                {
                    frontier.Enqueue(neighbour);
                }
            }
        }

        return seen.Count;
    }
}

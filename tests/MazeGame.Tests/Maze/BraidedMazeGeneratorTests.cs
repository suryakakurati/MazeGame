using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies that braiding adds exactly the requested number of loops, never breaks the maze
/// and never opens the outer boundary.
/// </summary>
public sealed class BraidedMazeGeneratorTests
{
    private const int Size = 8;

    [Fact]
    public void Generate_WithExtraPassages_AddsOnePassagePerLoop()
    {
        // A perfect maze of N cells has N - 1 passages, and each removed interior wall adds
        // exactly one cycle, so the count is a precise check of the braid.
        var maze = Generator(seed: 3, extraPassages: 6).Generate(Options(seed: 3));

        Assert.Equal((Size * Size) - 1 + 6, maze.CountPassages());
    }

    [Fact]
    public void Generate_WithZeroExtraPassages_LeavesAPerfectMaze()
    {
        var maze = Generator(seed: 4, extraPassages: 0).Generate(Options(seed: 4));

        Assert.Equal((Size * Size) - 1, maze.CountPassages());
    }

    [Fact]
    public void Generate_WithExtraPassages_KeepsEveryCellReachable()
    {
        var maze = Generator(seed: 5, extraPassages: 8).Generate(Options(seed: 5));

        Assert.Equal(maze.CellCount, ReachableCount(maze));
    }

    [Fact]
    public void Generate_WithMoreLoopsThanWalls_RemovesEveryInteriorWallOnce()
    {
        // 4x4: 4 * 3 horizontal interior walls plus 3 * 4 vertical ones = 24.
        var generator = new BraidedMazeGenerator(
            new MazeGenerator(new SeededRandom(9)),
            new SeededRandom(9),
            extraPassages: 1000);

        var maze = generator.Generate(new MazeOptions(4, 4, 9));

        Assert.Equal((4 * 3) + (3 * 4), maze.CountPassages());
    }

    [Fact]
    public void Generate_NeverOpensTheOuterBoundary()
    {
        var maze = Generator(seed: 11, extraPassages: 20).Generate(Options(seed: 11));

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
        var first = Generator(seed: 21, extraPassages: 5).Generate(Options(seed: 21));
        var second = Generator(seed: 21, extraPassages: 5).Generate(Options(seed: 21));

        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                Assert.Equal(first.GetCell(row, column), second.GetCell(row, column));
            }
        }
    }

    [Fact]
    public void Generate_WithMoreLoops_LeavesFewerWallsStanding()
    {
        var few = Generator(seed: 31, extraPassages: 2).Generate(Options(seed: 31));
        var many = Generator(seed: 31, extraPassages: 12).Generate(Options(seed: 31));

        Assert.True(many.CountPassages() > few.CountPassages());
    }

    [Fact]
    public void Constructor_WithNegativeExtraPassages_Throws()
    {
        var inner = new MazeGenerator(new SeededRandom(1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BraidedMazeGenerator(inner, new SeededRandom(1), extraPassages: -1));
    }

    private static BraidedMazeGenerator Generator(int seed, int extraPassages) =>
        new(new MazeGenerator(new SeededRandom(seed)), new SeededRandom(seed), extraPassages);

    private static MazeOptions Options(int seed) => new(Size, Size, seed);

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

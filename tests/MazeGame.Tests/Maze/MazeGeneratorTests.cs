using System;
using System.Collections.Generic;
using System.Text;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies the three invariants that define a perfect maze — every cell is reachable,
/// there are exactly N - 1 passages, and no wall on the outer boundary is opened — plus
/// dimension handling and seed reproducibility.
/// </summary>
public sealed class MazeGeneratorTests
{
    private const int Rows = 8;
    private const int Columns = 8;
    private const int SeedCount = 50;

    [Fact]
    public void Generate_OneByOneMaze_HasNoPassages()
    {
        var grid = Generate(rows: 1, columns: 1, seed: 7);

        Assert.Equal(1, grid.CellCount);
        Assert.Equal(0, grid.CountPassages());
        Assert.Equal(4, grid.GetCell(0, 0).WallCount);
    }

    [Fact]
    public void Generate_SingleRowMaze_IsACorridor()
    {
        var grid = Generate(rows: 1, columns: 5, seed: 3);

        Assert.Equal(4, grid.CountPassages());
        Assert.Equal(5, CountReachableCells(grid));
    }

    [Fact]
    public void Generate_SingleColumnMaze_IsACorridor()
    {
        var grid = Generate(rows: 5, columns: 1, seed: 3);

        Assert.Equal(4, grid.CountPassages());
        Assert.Equal(5, CountReachableCells(grid));
    }

    [Fact]
    public void Generate_AcrossManySeeds_ConnectsEveryCell()
    {
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var grid = Generate(Rows, Columns, seed);

            Assert.Equal(grid.CellCount, CountReachableCells(grid));
        }
    }

    [Fact]
    public void Generate_AcrossManySeeds_HasExactlyCellCountMinusOnePassages()
    {
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var grid = Generate(Rows, Columns, seed);

            Assert.Equal(grid.CellCount - 1, grid.CountPassages());
        }
    }

    [Fact]
    public void Generate_AcrossManySeeds_KeepsOuterBoundaryClosed()
    {
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var grid = Generate(Rows, Columns, seed);

            for (var row = 0; row < grid.Rows; row++)
            {
                Assert.True(grid.HasWall(row, 0, Direction.West));
                Assert.True(grid.HasWall(row, grid.Columns - 1, Direction.East));
            }

            for (var column = 0; column < grid.Columns; column++)
            {
                Assert.True(grid.HasWall(0, column, Direction.North));
                Assert.True(grid.HasWall(grid.Rows - 1, column, Direction.South));
            }
        }
    }

    [Fact]
    public void Generate_WithSameSeed_ProducesIdenticalMaze()
    {
        var first = Describe(Generate(Rows, Columns, seed: 4242));
        var second = Describe(Generate(Rows, Columns, seed: 4242));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_WithDifferentSeeds_ProducesDifferentMazes()
    {
        var first = Describe(Generate(Rows, Columns, seed: 1));
        var second = Describe(Generate(Rows, Columns, seed: 2));

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void Generate_WithInvalidSize_Throws(int rows, int columns)
    {
        var generator = CreateGenerator(seed: 5);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(new MazeOptions(rows, columns, Seed: 5)));
    }

    [Fact]
    public void Constructor_WithNullRandom_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MazeGenerator(null!));
    }

    [Fact]
    public void Generate_WithNonShufflingRandom_StillProducesAPerfectMaze()
    {
        // Proves the generator's correctness comes from the union-find invariant, not from
        // a lucky shuffle order: even with the insertion order untouched the result must
        // be connected, acyclic and have N - 1 passages.
        var generator = new MazeGenerator(new FixedRandom(0));

        var grid = generator.Generate(new MazeOptions(Rows, Columns, Seed: null));

        Assert.Equal(grid.CellCount, CountReachableCells(grid));
        Assert.Equal(grid.CellCount - 1, grid.CountPassages());
    }

    private static Grid Generate(int rows, int columns, int seed) =>
        CreateGenerator(seed).Generate(new MazeOptions(rows, columns, seed));

    private static MazeGenerator CreateGenerator(int seed) => new(new SeededRandom(seed));

    /// <summary>
    /// Breadth-first search over passages, counting how many cells are reachable from
    /// (0, 0). A perfect maze must reach all of them.
    /// </summary>
    private static int CountReachableCells(Grid grid)
    {
        var visited = new bool[grid.Rows, grid.Columns];
        var queue = new Queue<(int Row, int Column)>();
        queue.Enqueue((0, 0));
        visited[0, 0] = true;
        var count = 0;

        while (queue.Count > 0)
        {
            var (row, column) = queue.Dequeue();
            count++;

            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (grid.HasWall(row, column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbourRow = row + rowDelta;
                var neighbourColumn = column + columnDelta;

                if (visited[neighbourRow, neighbourColumn])
                {
                    continue;
                }

                visited[neighbourRow, neighbourColumn] = true;
                queue.Enqueue((neighbourRow, neighbourColumn));
            }
        }

        return count;
    }

    /// <summary>
    /// Turns a maze into a string so two mazes can be compared exactly. Each cell
    /// contributes one character per direction: '1' for a standing wall, '0' for a passage.
    /// </summary>
    private static string Describe(Grid grid)
    {
        var description = new StringBuilder();

        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                foreach (var direction in Enum.GetValues<Direction>())
                {
                    description.Append(grid.HasWall(row, column, direction) ? '1' : '0');
                }
            }
        }

        return description.ToString();
    }
}

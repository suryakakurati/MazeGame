using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Pathfinding;

/// <summary>
/// Verifies <see cref="DijkstraPathfinder"/>: it finds the cheapest route rather than merely
/// a route, never crosses a wall, prefers cheap cells over expensive ones, still reaches a
/// goal when the only route is expensive, and returns nothing when there is genuinely no
/// route.
/// </summary>
public sealed class DijkstraPathfinderTests
{
    private static readonly DijkstraPathfinder Pathfinder = new();

    [Fact]
    public void FindPath_WhenStartEqualsGoal_ReturnsOnlyStart()
    {
        var grid = BuildCorridor(length: 5);
        var start = grid.GetCell(0, 2);

        var path = Pathfinder.FindPath(grid, start, start, UniformCost);

        Assert.Single(path);
        Assert.Equal(start, path[0]);
    }

    [Fact]
    public void FindPath_AlongCorridor_ReturnsEveryCellInOrder()
    {
        var grid = BuildCorridor(length: 5);
        var start = grid.GetCell(0, 0);
        var goal = grid.GetCell(0, 4);

        var path = Pathfinder.FindPath(grid, start, goal, UniformCost);

        Assert.Equal(5, path.Count);
        for (var column = 0; column < 5; column++)
        {
            Assert.Equal(grid.GetCell(0, column), path[column]);
        }
    }

    [Fact]
    public void FindPath_WithOneRoute_ReturnsThatRoute()
    {
        // 2x2: (0,0) - (1,0) - (1,1) is the only way to reach the bottom right.
        var grid = new Grid(2, 2);
        grid.RemoveWall(0, 0, Direction.South);
        grid.RemoveWall(1, 0, Direction.East);

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(1, 1), UniformCost);

        Assert.Equal(3, path.Count);
        Assert.Equal(grid.GetCell(0, 0), path[0]);
        Assert.Equal(grid.GetCell(1, 0), path[1]);
        Assert.Equal(grid.GetCell(1, 1), path[2]);
    }

    [Fact]
    public void FindPath_WhenGoalIsUnreachable_ReturnsEmpty()
    {
        var grid = new Grid(1, 3);

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 2), UniformCost);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_WithTwoRoutes_PrefersTheShorterOne()
    {
        var grid = BuildTwoRouteGrid();

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 2), UniformCost);

        Assert.Equal(3, path.Count);
        Assert.Equal(grid.GetCell(0, 1), path[1]);
    }

    [Fact]
    public void FindPath_WhenShortRouteIsExpensive_TakesTheCheaperDetour()
    {
        var grid = BuildTwoRouteGrid();

        // Crossing (0,1) now costs 25, while the four-cell detour costs 1 per cell.
        static int Weights(Cell cell) => cell.Row == 0 && cell.Column == 1 ? 25 : 1;

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 2), Weights);

        Assert.Equal(5, path.Count);
        Assert.DoesNotContain(grid.GetCell(0, 1), path);
        Assert.Equal(grid.GetCell(0, 0), path[0]);
        Assert.Equal(grid.GetCell(0, 2), path[4]);
    }

    [Fact]
    public void FindPath_WhenOnlyRouteIsExpensive_StillReturnsIt()
    {
        // The expensive cell can never act as a wall: the enemy must always have a route.
        var grid = BuildCorridor(length: 3);

        static int Weights(Cell cell) => cell.Column == 1 ? 25 : 1;

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 2), Weights);

        Assert.Equal(3, path.Count);
        Assert.Equal(grid.GetCell(0, 1), path[1]);
    }

    [Fact]
    public void FindPath_OnGeneratedMaze_StartsAtStartAndEndsAtGoal()
    {
        var grid = new MazeGenerator(new SeededRandom(seed: 99))
            .Generate(new MazeOptions(Rows: 10, Columns: 10, Seed: 99));
        var start = grid.GetCell(0, 0);
        var goal = grid.GetCell(9, 9);

        var path = Pathfinder.FindPath(grid, start, goal, UniformCost);

        Assert.NotEmpty(path);
        Assert.Equal(start, path[0]);
        Assert.Equal(goal, path[path.Count - 1]);
    }

    [Fact]
    public void FindPath_OnGeneratedMaze_NeverMovesThroughAWall()
    {
        var grid = new MazeGenerator(new SeededRandom(seed: 5))
            .Generate(new MazeOptions(Rows: 10, Columns: 10, Seed: 5));

        var path = Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(9, 9), UniformCost);

        AssertValidRoute(grid, path);
    }

    [Fact]
    public void FindPath_OnGeneratedMazes_MatchesBreadthFirstShortestDistance()
    {
        // Independent reference implementation: with every weight equal to 1, Dijkstra must
        // match a plain breadth-first search, which proves optimality rather than "a route
        // exists".
        for (var seed = 0; seed < 10; seed++)
        {
            var grid = new MazeGenerator(new SeededRandom(seed))
                .Generate(new MazeOptions(Rows: 10, Columns: 10, Seed: seed));
            var start = grid.GetCell(0, 0);
            var goal = grid.GetCell(9, 9);

            var path = Pathfinder.FindPath(grid, start, goal, UniformCost);
            var expectedCells = BreadthFirstCellCount(grid, start, goal);

            Assert.Equal(expectedCells, path.Count);
        }
    }

    [Fact]
    public void FindPath_CalledTwice_ReturnsTheSameRoute()
    {
        var grid = BuildTwoRouteGrid();
        var start = grid.GetCell(0, 0);
        var goal = grid.GetCell(0, 2);

        var first = Pathfinder.FindPath(grid, start, goal, UniformCost);
        var second = Pathfinder.FindPath(grid, start, goal, UniformCost);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FindPath_DoesNotChangeTheMaze()
    {
        var grid = new MazeGenerator(new SeededRandom(seed: 11))
            .Generate(new MazeOptions(Rows: 8, Columns: 8, Seed: 11));
        var passagesBefore = grid.CountPassages();

        Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(7, 7), UniformCost);

        Assert.Equal(passagesBefore, grid.CountPassages());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void FindPath_WithCostBelowOne_Throws(int weight)
    {
        var grid = BuildCorridor(length: 3);

        Assert.Throws<ArgumentException>(
            () => Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 2), _ => weight));
    }

    [Fact]
    public void FindPath_WithNullGrid_Throws()
    {
        var grid = BuildCorridor(length: 2);

        Assert.Throws<ArgumentNullException>(
            () => Pathfinder.FindPath(null!, grid.GetCell(0, 0), grid.GetCell(0, 1), UniformCost));
    }

    [Fact]
    public void FindPath_WithNullCost_Throws()
    {
        var grid = BuildCorridor(length: 2);

        Assert.Throws<ArgumentNullException>(
            () => Pathfinder.FindPath(grid, grid.GetCell(0, 0), grid.GetCell(0, 1), null!));
    }

    private static int UniformCost(Cell cell) => 1;

    /// <summary>
    /// Builds a single-row corridor of <paramref name="length"/> cells by removing every
    /// east wall between neighbours.
    /// </summary>
    private static Grid BuildCorridor(int length)
    {
        var grid = new Grid(1, length);

        for (var column = 0; column < length - 1; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
        }

        return grid;
    }

    /// <summary>
    /// Builds a 2x3 grid with two routes from (0,0) to (0,2): a short top route of three
    /// cells and a long bottom detour of five.
    /// </summary>
    private static Grid BuildTwoRouteGrid()
    {
        var grid = new Grid(2, 3);

        grid.RemoveWall(0, 0, Direction.East);
        grid.RemoveWall(0, 1, Direction.East);

        grid.RemoveWall(0, 0, Direction.South);
        grid.RemoveWall(1, 0, Direction.East);
        grid.RemoveWall(1, 1, Direction.East);
        grid.RemoveWall(1, 2, Direction.North);

        return grid;
    }

    /// <summary>
    /// Asserts that consecutive cells in <paramref name="path"/> are neighbours with no wall
    /// between them, so the route cannot have jumped over a wall.
    /// </summary>
    private static void AssertValidRoute(Grid grid, IReadOnlyList<Cell> path)
    {
        Assert.NotEmpty(path);

        for (var position = 1; position < path.Count; position++)
        {
            var from = path[position - 1];
            var to = path[position];
            var rowDelta = to.Row - from.Row;
            var columnDelta = to.Column - from.Column;

            Assert.Equal(1, Math.Abs(rowDelta) + Math.Abs(columnDelta));

            var direction = rowDelta switch
            {
                -1 => Direction.North,
                1 => Direction.South,
                _ => columnDelta == 1 ? Direction.East : Direction.West,
            };

            Assert.False(grid.HasWall(from.Row, from.Column, direction));
        }
    }

    /// <summary>
    /// Counts the cells on the shortest unweighted route using breadth-first search, or
    /// returns -1 when <paramref name="goal"/> cannot be reached. Used as an independent
    /// reference for Dijkstra's answer.
    /// </summary>
    private static int BreadthFirstCellCount(Grid grid, Cell start, Cell goal)
    {
        var visited = new bool[grid.Rows, grid.Columns];
        var queue = new Queue<(int Row, int Column, int CellCount)>();
        queue.Enqueue((start.Row, start.Column, 1));
        visited[start.Row, start.Column] = true;

        while (queue.Count > 0)
        {
            var (row, column, cellCount) = queue.Dequeue();

            if (row == goal.Row && column == goal.Column)
            {
                return cellCount;
            }

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
                queue.Enqueue((neighbourRow, neighbourColumn, cellCount + 1));
            }
        }

        return -1;
    }
}

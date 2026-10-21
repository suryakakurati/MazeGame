using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies floor outlines: a shaped maze keeps its rock closed, stays connected, and is
/// reproducible from a seed.
/// </summary>
/// <remarks>
/// Everything downstream assumes the property these tests pin down: the cells outside the shape are
/// rock nobody can enter, and the cells inside it are exactly as connected as a rectangular maze.
/// A shape that broke either one would strand keys or leave the exit behind a wall.
/// </remarks>
public sealed class MazeShapeTests
{
    private const int Seed = 31;

    private static readonly ShapeKind[] Shaped = [ShapeKind.Blob, ShapeKind.Ellipse];

    [Fact]
    public void ARectangle_KeepsTheWholeGrid()
    {
        var shape = MazeShape.Create(ShapeKind.Rect, 15, 15, new SeededRandom(Seed));

        Assert.Equal(225, shape.OpenCount);
    }

    [Theory]
    [InlineData(ShapeKind.Blob)]
    [InlineData(ShapeKind.Ellipse)]
    public void AShape_CutsTheCornersButKeepsMostOfTheFloor(ShapeKind kind)
    {
        var shape = MazeShape.Create(kind, 15, 15, new SeededRandom(Seed));

        // Enough floor to hold a maze with keys and hunters, little enough that the silhouette is
        // visibly not a rectangle.
        Assert.InRange(shape.OpenCount, 120, 210);
        Assert.False(shape.IsOpen(0, 0) && shape.IsOpen(0, 14) && shape.IsOpen(14, 0) && shape.IsOpen(14, 14));
    }

    [Theory]
    [InlineData(ShapeKind.Blob)]
    [InlineData(ShapeKind.Ellipse)]
    public void AShape_IsOnePiece(ShapeKind kind)
    {
        var shape = MazeShape.Create(kind, 15, 15, new SeededRandom(Seed));
        var reached = Flood(shape);

        Assert.Equal(shape.OpenCount, reached);
    }

    [Fact]
    public void AShape_IsReproducibleFromItsSeed()
    {
        var first = MazeShape.Create(ShapeKind.Blob, 15, 15, new SeededRandom(Seed));
        var second = MazeShape.Create(ShapeKind.Blob, 15, 15, new SeededRandom(Seed));

        for (var row = 0; row < 15; row++)
        {
            for (var column = 0; column < 15; column++)
            {
                Assert.Equal(first.IsOpen(row, column), second.IsOpen(row, column));
            }
        }
    }

    [Theory]
    [InlineData(ShapeKind.Blob)]
    [InlineData(ShapeKind.Ellipse)]
    public void AShapedMaze_LeavesTheRockClosedAndConnectsEveryFloorCell(ShapeKind kind)
    {
        var shape = MazeShape.Create(kind, 15, 15, new SeededRandom(Seed));
        var maze = new MazeGenerator(new SeededRandom(Seed))
            .Generate(new MazeOptions(15, 15, Seed) { Shape = shape });

        var floors = 0;

        for (var row = 0; row < 15; row++)
        {
            for (var column = 0; column < 15; column++)
            {
                var cell = maze.GetCell(row, column);

                if (maze.IsSolid(row, column))
                {
                    // A solid cell is rock: all four of its walls are still standing.
                    Assert.Equal(4, cell.WallCount);
                    continue;
                }

                floors++;

                // A floor cell may not have a passage into the rock around it.
                foreach (var direction in Directions.All)
                {
                    if (maze.HasWall(row, column, direction))
                    {
                        continue;
                    }

                    var (rowDelta, columnDelta) = direction.Delta();
                    Assert.False(maze.IsSolid(row + rowDelta, column + columnDelta));
                }
            }
        }

        Assert.Equal(shape.OpenCount, floors);

        // Every floor cell is reachable from the first one, so keys and the exit can never be
        // stranded behind the rock.
        var reached = OpenCellsReached(maze);
        Assert.Equal(floors, reached);
    }

    [Fact]
    public void AShapedMaze_HasOnePassageForEveryFloorCellLessOne()
    {
        var shape = MazeShape.Create(ShapeKind.Blob, 15, 15, new SeededRandom(Seed));
        var maze = new MazeGenerator(new SeededRandom(Seed))
            .Generate(new MazeOptions(15, 15, Seed) { Shape = shape });

        // A perfect maze on the cells that exist: no passage is spent on rock.
        Assert.Equal(maze.OpenCellCount - 1, maze.CountPassages());
    }

    /// <summary>Counts the open cells reachable from the first one, walking through passages.</summary>
    private static int OpenCellsReached(Grid maze)
    {
        var seen = new HashSet<Cell>();
        var pending = new Stack<Cell>();
        var start = FirstOpen(maze);

        seen.Add(start);
        pending.Push(start);

        while (pending.Count > 0)
        {
            var cell = pending.Pop();

            foreach (var direction in Directions.All)
            {
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbour = maze.GetCell(cell.Row + rowDelta, cell.Column + columnDelta);

                if (seen.Add(neighbour))
                {
                    pending.Push(neighbour);
                }
            }
        }

        return seen.Count;
    }

    /// <summary>Counts the shape's cells reachable from the first one, ignoring walls (there are none yet).</summary>
    private static int Flood(MazeShape shape)
    {
        var seen = new HashSet<(int Row, int Column)>();
        var pending = new Stack<(int Row, int Column)>();
        var (startRow, startColumn) = FirstOpenCell(shape);

        seen.Add((startRow, startColumn));
        var start = (startRow, startColumn);
        pending.Push(start);

        while (pending.Count > 0)
        {
            var (row, column) = pending.Pop();

            foreach (var (rowDelta, columnDelta) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nextRow = row + rowDelta;
                var nextColumn = column + columnDelta;

                if (nextRow < 0 || nextRow >= shape.Rows || nextColumn < 0 || nextColumn >= shape.Columns ||
                    !shape.IsOpen(nextRow, nextColumn) || !seen.Add((nextRow, nextColumn)))
                {
                    continue;
                }

                pending.Push((nextRow, nextColumn));
            }
        }

        return seen.Count;
    }

    private static (int Row, int Column) FirstOpenCell(MazeShape shape)
    {
        for (var row = 0; row < shape.Rows; row++)
        {
            for (var column = 0; column < shape.Columns; column++)
            {
                if (shape.IsOpen(row, column))
                {
                    return (row, column);
                }
            }
        }

        throw new InvalidOperationException("The shape has no floor cells.");
    }

    private static Cell FirstOpen(Grid maze)
    {
        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (!maze.IsSolid(row, column))
                {
                    return maze.GetCell(row, column);
                }
            }
        }

        throw new InvalidOperationException("The maze has no floor cells.");
    }

    /// <summary>The four directions, for tests that have to walk a maze.</summary>
    private static class Directions
    {
        public static readonly Direction[] All =
            [Direction.North, Direction.East, Direction.South, Direction.West];
    }
}

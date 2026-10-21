using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Maze;

/// <summary>What outline a floor's maze is carved into.</summary>
public enum ShapeKind
{
    /// <summary>The whole rectangle: every cell of the grid is part of the floor.</summary>
    Rect,

    /// <summary>A ragged cave, grown outward from a cell near the middle.</summary>
    Blob,

    /// <summary>An oval arena, with its corners cut away.</summary>
    Ellipse,
}

/// <summary>
/// The set of cells a floor is allowed to use — the maze's outline.
/// </summary>
/// <remarks>
/// <para>
/// A maze on a grid is a rectangle by default, which is the least interesting thing a cave can be:
/// every floor has the same silhouette, so the only thing that changes between them is the pattern
/// of walls inside it. A shape closes some cells as solid rock (see <see cref="Grid.IsSolid"/>)
/// <i>before</i> the maze is generated, so the generator only ever connects cells that are part of
/// the floor and the walls never open into the rock around it.
/// </para>
/// <para>
/// Shapes are grown rather than drawn from a formula wherever possible, and the mask is then reduced
/// to its largest connected piece. That is the one guarantee the rest of the game needs: a shaped
/// floor is exactly as connected as a rectangular one, so every key and the exit stay reachable.
/// </para>
/// <para>
/// Randomness comes from the injected <see cref="IRandom"/>, so the same seed grows the same cave.
/// </para>
/// </remarks>
public sealed class MazeShape
{
    /// <summary>How much of the rectangle a grown shape aims to keep.</summary>
    private const double BlobFill = 0.66;

    /// <summary>How far the blob's size may wander from <see cref="BlobFill"/>.</summary>
    private const double BlobFillJitter = 0.08;

    /// <summary>How much of each axis an ellipse reaches.</summary>
    private const double EllipseReach = 0.47;

    /// <summary>How far the oval's edge may wander, in normalised radius.</summary>
    private const double EllipseRoughness = 0.09;

    private readonly bool[] _open;

    private MazeShape(ShapeKind kind, int rows, int columns, bool[] open)
    {
        Kind = kind;
        Rows = rows;
        Columns = columns;
        _open = open;
        OpenCount = CountOpen(open);
    }

    /// <summary>Gets the outline this shape was built as.</summary>
    public ShapeKind Kind { get; }

    /// <summary>Gets the number of rows the shape was built for.</summary>
    public int Rows { get; }

    /// <summary>Gets the number of columns the shape was built for.</summary>
    public int Columns { get; }

    /// <summary>Gets how many cells are part of the floor.</summary>
    public int OpenCount { get; }

    /// <summary>Determines whether a cell belongs to the floor.</summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns><see langword="true"/> when the cell is walkable floor rather than rock.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the shape's grid.
    /// </exception>
    public bool IsOpen(int row, int column)
    {
        if (row < 0 || row >= Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Row must be between 0 and {Rows - 1}.");
        }

        if (column < 0 || column >= Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"Column must be between 0 and {Columns - 1}.");
        }

        return _open[(row * Columns) + column];
    }

    /// <summary>
    /// Grows the shape of <paramref name="kind"/> over a <paramref name="rows"/> by
    /// <paramref name="columns"/> grid.
    /// </summary>
    /// <param name="kind">The outline to grow.</param>
    /// <param name="rows">The grid's height in cells. At least 1.</param>
    /// <param name="columns">The grid's width in cells. At least 1.</param>
    /// <param name="random">The source of randomness, so a seed reproduces the same cave.</param>
    /// <returns>A connected shape covering most of the grid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the grid has fewer than one row or column, or the kind is unknown.
    /// </exception>
    public static MazeShape Create(ShapeKind kind, int rows, int columns, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "A shape needs at least one row.");
        }

        if (columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A shape needs at least one column.");
        }

        var open = kind switch
        {
            ShapeKind.Rect => Rect(rows, columns),
            ShapeKind.Blob => Grow(rows, columns, random),
            ShapeKind.Ellipse => Oval(rows, columns, random),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shape."),
        };

        // A jittered edge or a half-grown blob can pinch off a corner. Keeping only the largest
        // piece is what makes "shaped" mean the same thing as "connected" for everything downstream.
        open = LargestPiece(open, rows, columns);

        return new MazeShape(kind, rows, columns, open);
    }

    /// <summary>Closes every cell of <paramref name="grid"/> that is not part of the floor.</summary>
    /// <param name="grid">The grid to shape, before any maze is generated in it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="grid"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the grid's size does not match the shape's.
    /// </exception>
    internal void ApplyTo(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.Rows != Rows || grid.Columns != Columns)
        {
            throw new ArgumentException(
                $"The shape is {Rows}x{Columns} but the grid is {grid.Rows}x{grid.Columns}.",
                nameof(grid));
        }

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (!IsOpen(row, column))
                {
                    grid.CloseCell(row, column);
                }
            }
        }
    }

    private static bool[] Rect(int rows, int columns)
    {
        var open = new bool[rows * columns];
        Array.Fill(open, true);
        return open;
    }

    /// <summary>Grows a ragged blob outward from a cell near the middle of the grid.</summary>
    private static bool[] Grow(int rows, int columns, IRandom random)
    {
        var open = new bool[rows * columns];
        var target = Math.Max(
            1,
            (int)Math.Round(
                rows * columns * (BlobFill + ((Fraction(random) * 2 - 1) * BlobFillJitter))));

        var startRow = rows / 2;
        var startColumn = columns / 2;
        open[(startRow * columns) + startColumn] = true;

        var frontier = new List<(int Row, int Column)>();
        AddNeighbours(frontier, open, rows, columns, startRow, startColumn);

        var count = 1;

        while (count < target && frontier.Count > 0)
        {
            var index = random.NextInt(frontier.Count);
            var (row, column) = frontier[index];
            frontier[index] = frontier[^1];
            frontier.RemoveAt(frontier.Count - 1);

            var cell = (row * columns) + column;

            if (open[cell])
            {
                continue;
            }

            open[cell] = true;
            count++;
            AddNeighbours(frontier, open, rows, columns, row, column);
        }

        return open;
    }

    /// <summary>Queues every neighbour of a cell that is still rock.</summary>
    private static void AddNeighbours(
        List<(int Row, int Column)> frontier,
        bool[] open,
        int rows,
        int columns,
        int row,
        int column)
    {
        if (row > 0 && !open[((row - 1) * columns) + column])
        {
            frontier.Add((row - 1, column));
        }

        if (row + 1 < rows && !open[((row + 1) * columns) + column])
        {
            frontier.Add((row + 1, column));
        }

        if (column > 0 && !open[(row * columns) + column - 1])
        {
            frontier.Add((row, column - 1));
        }

        if (column + 1 < columns && !open[(row * columns) + column + 1])
        {
            frontier.Add((row, column + 1));
        }
    }

    /// <summary>Carves an oval, with an edge that wanders rather than following the formula.</summary>
    private static bool[] Oval(int rows, int columns, IRandom random)
    {
        var open = new bool[rows * columns];
        var centreRow = (rows - 1) / 2.0;
        var centreColumn = (columns - 1) / 2.0;
        var radiusRow = Math.Max(1.0, rows * EllipseReach);
        var radiusColumn = Math.Max(1.0, columns * EllipseReach);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var vertical = (row - centreRow) / radiusRow;
                var horizontal = (column - centreColumn) / radiusColumn;
                var distance = (vertical * vertical) + (horizontal * horizontal);
                var rough = ((Fraction(random) * 2) - 1) * EllipseRoughness;

                open[(row * columns) + column] = distance <= 1.0 + rough;
            }
        }

        // A tiny grid can have its middle jittered out; the centre cell is always floor.
        open[(((int)centreRow) * columns) + (int)centreColumn] = true;

        return open;
    }

    /// <summary>
    /// Returns a fractional value in [0, 1) from the injected generator.
    /// </summary>
    /// <remarks>
    /// <see cref="IRandom"/> deliberately exposes only integers and shuffles, so a shape takes its
    /// fractions from a thousand-step integer draw. That is far finer than any edge needs, and it
    /// keeps the interface every fake in the test suite implements down to two members.
    /// </remarks>
    private static double Fraction(IRandom random) => random.NextInt(1000) / 1000.0;

    /// <summary>Keeps the largest four-connected piece of a mask and closes everything else.</summary>
    private static bool[] LargestPiece(bool[] open, int rows, int columns)
    {
        var seen = new bool[open.Length];
        var best = new List<int>();
        var pending = new Stack<int>();

        for (var start = 0; start < open.Length; start++)
        {
            if (!open[start] || seen[start])
            {
                continue;
            }

            var piece = new List<int>();
            seen[start] = true;
            pending.Push(start);

            while (pending.Count > 0)
            {
                var cell = pending.Pop();
                piece.Add(cell);

                var row = cell / columns;
                var column = cell % columns;

                foreach (var (rowDelta, columnDelta) in Steps.Cardinals)
                {
                    var neighbourRow = row + rowDelta;
                    var neighbourColumn = column + columnDelta;

                    if (neighbourRow < 0 || neighbourRow >= rows ||
                        neighbourColumn < 0 || neighbourColumn >= columns)
                    {
                        continue;
                    }

                    var neighbour = (neighbourRow * columns) + neighbourColumn;

                    if (!open[neighbour] || seen[neighbour])
                    {
                        continue;
                    }

                    seen[neighbour] = true;
                    pending.Push(neighbour);
                }
            }

            if (piece.Count > best.Count)
            {
                best = piece;
            }
        }

        var result = new bool[open.Length];

        foreach (var cell in best)
        {
            result[cell] = true;
        }

        return result;
    }

    private static int CountOpen(bool[] open)
    {
        var count = 0;

        foreach (var cell in open)
        {
            if (cell)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>A tiny holder for the four neighbour steps, so the flood fill allocates nothing per cell.</summary>
    private static class Steps
    {
        /// <summary>The four cardinal steps.</summary>
        public static readonly (int Row, int Column)[] Cardinals = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    }
}

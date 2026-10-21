using System;

namespace MazeGame.Core.Maze;

/// <summary>
/// One square of the maze: its position plus which of its four walls are still standing.
/// </summary>
/// <remarks>
/// <para>
/// A cell is an immutable value. Removing a wall therefore produces a new
/// <see cref="Cell"/> rather than mutating the existing one; <see cref="Grid"/> swaps the
/// new value into place. That makes walls impossible to change by accident from outside
/// the grid, and keeps equality simple (two cells are equal when they describe exactly
/// the same square with the same walls).
/// </para>
/// <para>
/// The four walls are packed into one byte, one bit per <see cref="Direction"/>, because
/// a maze can contain thousands of cells and a byte per cell is a significant saving over
/// four booleans. Only the four defined directions are meaningful here: callers should go
/// through <see cref="Grid"/>, which validates its inputs.
/// </para>
/// <para>
/// Cells are only constructed by <see cref="Grid"/>, which owns the coordinate system and
/// validates positions, so there is no public constructor to get wrong.
/// </para>
/// </remarks>
public readonly record struct Cell
{
    // All four wall bits set: the state of every cell in a brand new grid.
    private const byte AllWalls = (byte)(Direction.North | Direction.East | Direction.South | Direction.West);

    private readonly byte _walls;

    private Cell(int row, int column, byte walls)
    {
        Row = row;
        Column = column;
        _walls = walls;
    }

    /// <summary>
    /// Creates a cell at the given position with all four walls intact.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    internal Cell(int row, int column)
        : this(row, column, AllWalls)
    {
    }

    /// <summary>
    /// Gets the row index of this cell. Row 0 is the top row.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the column index of this cell. Column 0 is the leftmost column.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the number of walls still standing on this cell.
    /// </summary>
    /// <value>A number from 0 to 4.</value>
    public int WallCount
    {
        get
        {
            var count = 0;
            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (HasWall(direction))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Determines whether the wall on <paramref name="direction"/> of this cell is
    /// still standing.
    /// </summary>
    /// <param name="direction">The side of the cell to test.</param>
    /// <returns><see langword="true"/> when the wall has not been removed.</returns>
    public bool HasWall(Direction direction) => (_walls & (byte)direction) != 0;

    /// <summary>
    /// Returns a copy of this cell with one wall removed. Removing a wall that is
    /// already gone returns an equal cell.
    /// </summary>
    /// <param name="direction">The side whose wall should be removed.</param>
    /// <returns>A cell at the same position without that wall.</returns>
    internal Cell WithoutWall(Direction direction) =>
        new(Row, Column, (byte)(_walls & ~(byte)direction));

    /// <summary>
    /// Returns a copy of this cell with one wall restored. Adding a wall that is already
    /// standing returns an equal cell.
    /// </summary>
    /// <param name="direction">The side whose wall should be restored.</param>
    /// <returns>A cell at the same position with that wall standing.</returns>
    internal Cell WithWall(Direction direction) =>
        new(Row, Column, (byte)(_walls | (byte)direction));
}

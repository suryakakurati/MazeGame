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
/// the grid.
/// </para>
/// <para>
/// Equality is by position only: two cells are the same cell when they sit at the same
/// row and column, whatever walls they currently carry. Walls move during a floor (Fracture,
/// Regrowth, a secret room being carved), and everything that holds a cell — the player, a key,
/// the arch, an Echo, a barrier — holds the copy it was given. If equality included the walls,
/// moving a wall at one of those cells would quietly make the stored copy stop matching the
/// cell the rest of the game reads, and a key could become uncollectable or an arch impossible
/// to enter. Comparing by position keeps every reference valid for the whole floor.
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
    /// Determines whether <paramref name="other"/> is the same square of the maze.
    /// </summary>
    /// <param name="other">The cell to compare with.</param>
    /// <returns>
    /// <see langword="true"/> when both cells share a row and a column, regardless of the walls
    /// they currently describe.
    /// </returns>
    /// <remarks>
    /// The walls are deliberately not part of equality: a wall may be opened or closed after a
    /// cell has been stored (see the type's remarks), and every consumer wants the position, not
    /// a snapshot of the walls.
    /// </remarks>
    public bool Equals(Cell other) => Row == other.Row && Column == other.Column;

    /// <summary>
    /// Returns a hash code based on the cell's position, matching <see cref="Equals(Cell)"/>.
    /// </summary>
    /// <returns>A hash consistent with positional equality.</returns>
    public override int GetHashCode() => HashCode.Combine(Row, Column);

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

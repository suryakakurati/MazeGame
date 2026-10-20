using System;

namespace MazeGame.Core.Maze;

/// <summary>
/// A fixed-size rectangular grid of <see cref="Cell"/>s that owns the maze's walls.
/// </summary>
/// <remarks>
/// <para>
/// Every cell starts fully walled in, so a new grid is a set of isolated rooms. The maze
/// generator removes walls between neighbours (leaving the outer boundary intact) until
/// every cell is connected.
/// </para>
/// <para>
/// A wall is shared by the two cells either side of it, so <see cref="RemoveWall"/>
/// always updates both cells. Keeping that pairing in one place is what prevents the
/// classic bug where a passage is open from one side but not the other.
/// </para>
/// <para>
/// Cells are addressed by <c>(row, column)</c> with row 0 at the top and column 0 on the
/// left. Every public member validates its coordinates and throws rather than returning a
/// silent default, so mistakes surface immediately.
/// </para>
/// </remarks>
public sealed class Grid
{
    private readonly Cell[,] _cells;

    /// <summary>
    /// Creates a grid of <paramref name="rows"/> x <paramref name="columns"/> cells, all
    /// fully walled.
    /// </summary>
    /// <param name="rows">Number of rows. Must be at least 1.</param>
    /// <param name="columns">Number of columns. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rows"/> or <paramref name="columns"/> is below 1.
    /// </exception>
    public Grid(int rows, int columns)
    {
        if (rows < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rows),
                rows,
                "A maze needs at least one row.");
        }

        if (columns < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                columns,
                "A maze needs at least one column.");
        }

        Rows = rows;
        Columns = columns;
        _cells = new Cell[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                _cells[row, column] = new Cell(row, column);
            }
        }
    }

    /// <summary>
    /// Gets the number of rows in the grid.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns in the grid.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// Gets the total number of cells, that is <see cref="Rows"/> multiplied by
    /// <see cref="Columns"/>.
    /// </summary>
    public int CellCount => Rows * Columns;

    /// <summary>
    /// Returns the cell at the given position.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns>A copy of that cell. Walls are changed through
    /// <see cref="RemoveWall"/>, not on the copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid.
    /// </exception>
    public Cell GetCell(int row, int column)
    {
        ValidatePosition(row, column);
        return _cells[row, column];
    }

    /// <summary>
    /// Determines whether the wall on <paramref name="direction"/> of the cell at
    /// <paramref name="row"/>, <paramref name="column"/> is still standing.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <param name="direction">The side of the cell to test.</param>
    /// <returns><see langword="true"/> when the wall has not been removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid or the direction is unknown.
    /// </exception>
    public bool HasWall(int row, int column, Direction direction)
    {
        ValidateDirection(direction);
        ValidatePosition(row, column);
        return _cells[row, column].HasWall(direction);
    }

    /// <summary>
    /// Removes the wall between the cell at <paramref name="row"/>,
    /// <paramref name="column"/> and its neighbour in <paramref name="direction"/>,
    /// creating a passage. Both cells are updated so the passage is open from each side.
    /// The outer boundary cannot be opened this way: the neighbour must exist.
    /// </summary>
    /// <param name="row">Zero-based row index of the cell.</param>
    /// <param name="column">Zero-based column index of the cell.</param>
    /// <param name="direction">The side whose wall should be removed.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the cell, its neighbour in that direction, or the direction itself is
    /// outside the grid.
    /// </exception>
    /// <remarks>
    /// Removing the same wall twice is harmless and changes nothing.
    /// </remarks>
    public void RemoveWall(int row, int column, Direction direction)
    {
        ValidateDirection(direction);
        ValidatePosition(row, column);

        var (rowDelta, columnDelta) = direction.Delta();
        var neighbourRow = row + rowDelta;
        var neighbourColumn = column + columnDelta;
        ValidatePosition(neighbourRow, neighbourColumn);

        _cells[row, column] = _cells[row, column].WithoutWall(direction);
        _cells[neighbourRow, neighbourColumn] =
            _cells[neighbourRow, neighbourColumn].WithoutWall(direction.Opposite());
    }

    /// <summary>
    /// Converts a cell position into the single integer index used by flat structures
    /// such as <see cref="DisjointSet"/>: <c>row * Columns + column</c>, counting rows
    /// first.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns>The cell's index in the range <c>0 .. CellCount - 1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid.
    /// </exception>
    public int GetIndex(int row, int column)
    {
        ValidatePosition(row, column);
        return (row * Columns) + column;
    }

    /// <summary>
    /// Counts the passages in the grid, that is the walls that have been removed between
    /// two neighbours. A perfect maze of N cells has exactly N - 1 passages.
    /// </summary>
    /// <returns>The number of removed interior walls.</returns>
    public int CountPassages()
    {
        var passages = 0;

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                // Count only south and east from each cell. Each passage then has exactly
                // one cell that counts it, so no passage is counted twice.
                if (row + 1 < Rows && !_cells[row, column].HasWall(Direction.South))
                {
                    passages++;
                }

                if (column + 1 < Columns && !_cells[row, column].HasWall(Direction.East))
                {
                    passages++;
                }
            }
        }

        return passages;
    }

    /// <summary>
    /// Throws when <paramref name="row"/> or <paramref name="column"/> is outside the
    /// grid. Every public member funnels through here so the error message is consistent.
    /// </summary>
    private void ValidatePosition(int row, int column)
    {
        if (row < 0 || row >= Rows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(row),
                row,
                $"Row must be between 0 and {Rows - 1}.");
        }

        if (column < 0 || column >= Columns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(column),
                column,
                $"Column must be between 0 and {Columns - 1}.");
        }
    }

    /// <summary>
    /// Rejects values that are not one of the four defined directions.
    /// </summary>
    private static void ValidateDirection(Direction direction)
    {
        if (direction is not (Direction.North or Direction.East or Direction.South or Direction.West))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Direction must be one of North, East, South or West.");
        }
    }
}

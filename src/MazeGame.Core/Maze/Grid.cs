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
    private readonly bool[,] _solid;

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
        _solid = new bool[rows, columns];

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

        // Rock that no cell lives in cannot be walked into: a wall between a floor cell and a solid
        // one is the maze's edge, and every decorator that opens walls works through this method.
        if (_solid[row, column] || _solid[neighbourRow, neighbourColumn])
        {
            return;
        }

        _cells[row, column] = _cells[row, column].WithoutWall(direction);
        _cells[neighbourRow, neighbourColumn] =
            _cells[neighbourRow, neighbourColumn].WithoutWall(direction.Opposite());
    }

    /// <summary>
    /// Restores the wall between the cell at <paramref name="row"/>, <paramref name="column"/> and
    /// its neighbour in <paramref name="direction"/>, closing a passage. Both cells are updated, so
    /// the wall is standing from either side.
    /// </summary>
    /// <param name="row">Zero-based row index of the cell.</param>
    /// <param name="column">Zero-based column index of the cell.</param>
    /// <param name="direction">The side whose wall should stand.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the cell, its neighbour in that direction, or the direction itself is
    /// outside the grid.
    /// </exception>
    /// <remarks>
    /// Adding a wall that already stands is harmless and changes nothing, exactly as removing a
    /// missing wall is. Rock is left alone: a wall between a floor cell and a solid one is the
    /// maze's edge, and conditions such as Fracture only close passages they opened first.
    /// </remarks>
    public void AddWall(int row, int column, Direction direction)
    {
        ValidateDirection(direction);
        ValidatePosition(row, column);

        var (rowDelta, columnDelta) = direction.Delta();
        var neighbourRow = row + rowDelta;
        var neighbourColumn = column + columnDelta;
        ValidatePosition(neighbourRow, neighbourColumn);

        if (_solid[row, column] || _solid[neighbourRow, neighbourColumn])
        {
            return;
        }

        _cells[row, column] = _cells[row, column].WithWall(direction);
        _cells[neighbourRow, neighbourColumn] =
            _cells[neighbourRow, neighbourColumn].WithWall(direction.Opposite());
    }

    /// <summary>
    /// Returns the directions in which the cell at <paramref name="row"/>, <paramref name="column"/>
    /// has an open passage. A solid cell has none.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns>The open sides, in north, east, south, west order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is outside the grid.</exception>
    public IReadOnlyList<Direction> OpenDirections(int row, int column)
    {
        ValidatePosition(row, column);

        if (_solid[row, column])
        {
            return Array.Empty<Direction>();
        }

        var open = new List<Direction>(4);

        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            if (!HasWall(row, column, direction))
            {
                open.Add(direction);
            }
        }

        return open;
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
    /// Determines whether a cell is solid rock: outside the floor's shape, so never walkable.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns><see langword="true"/> when no cell lives there.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid.
    /// </exception>
    /// <remarks>
    /// A solid cell keeps all four of its walls forever, because <see cref="RemoveWall"/> refuses
    /// any wall that has one on either side. That is what lets a floor be an oval or a blob: the
    /// cells outside the shape are simply rock, and no passage can ever be opened into them.
    /// </remarks>
    public bool IsSolid(int row, int column)
    {
        ValidatePosition(row, column);
        return _solid[row, column];
    }

    /// <summary>Gets how many cells are solid rock rather than floor.</summary>
    public int SolidCount
    {
        get
        {
            var count = 0;

            foreach (var solid in _solid)
            {
                if (solid)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Gets how many cells belong to the floor's shape.</summary>
    public int OpenCellCount => CellCount - SolidCount;

    /// <summary>
    /// Marks a cell as solid rock. Called by a <see cref="MazeShape"/> before generation.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid.
    /// </exception>
    internal void CloseCell(int row, int column)
    {
        ValidatePosition(row, column);
        _solid[row, column] = true;
    }

    /// <summary>
    /// Turns a solid cell back into fully walled floor. Called by <see cref="SecretRoomCarver"/>
    /// after generation, and by nobody else.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the position lies outside the grid.
    /// </exception>
    /// <remarks>
    /// Only rock is carved: a cell that is already floor is left exactly as it is. That is the one
    /// guarantee the rest of the maze depends on — carving is how a room is cut out of stone, never
    /// how a passage is deleted — and a fresh cell's four walls are what stop the carve opening an
    /// edge that <see cref="RemoveWall"/> would then be spent on by accident.
    /// </remarks>
    internal void CarveCell(int row, int column)
    {
        ValidatePosition(row, column);

        if (!_solid[row, column])
        {
            return;
        }

        _solid[row, column] = false;
        _cells[row, column] = new Cell(row, column);
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

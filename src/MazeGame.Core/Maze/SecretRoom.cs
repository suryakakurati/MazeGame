using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Maze;

/// <summary>
/// A 3x3 pocket of floor with exactly one entrance: the safe room behind a run's secret door.
/// </summary>
/// <remarks>
/// <para>
/// The room is a place, not a system. It knows its nine cells, the one cell that is its doorway,
/// and the ordinary floor cell outside that doorway; everything else about it (what it pauses,
/// what it sells) belongs to <c>GameState</c> and <c>Campaign</c>.
/// </para>
/// <para>
/// A room is carved from rock that was never part of the maze — either a hole the floor's shape
/// left, or a pocket reserved before generation — so opening it can never reroute, disconnect or
/// delete a corridor. That is the property the tests pin down: the passages among the original
/// cells are unchanged, and the room adds exactly one.
/// </para>
/// </remarks>
public sealed class SecretRoom
{
    /// <summary>Gets the room's side length in cells.</summary>
    public const int Size = 3;

    /// <summary>Gets how many cells a room holds.</summary>
    public const int CellCount = Size * Size;

    private readonly Cell[] _cells;

    /// <summary>
    /// Creates a room from the nine carved cells and its one doorway.
    /// </summary>
    /// <param name="cells">The nine cells, in row-major order.</param>
    /// <param name="doorway">The room cell the entrance is cut from.</param>
    /// <param name="door">The ordinary floor cell outside the entrance.</param>
    /// <param name="center">The middle cell, where a prop or a counter stands.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the room does not hold exactly nine distinct cells, or when the doorway or door
    /// does not belong to it.
    /// </exception>
    internal SecretRoom(IReadOnlyList<Cell> cells, Cell doorway, Cell door, Cell center)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Count != CellCount)
        {
            throw new ArgumentException(
                $"A room is {Size}x{Size} = {CellCount} cells, not {cells.Count}.",
                nameof(cells));
        }

        _cells = new Cell[CellCount];
        var seen = new HashSet<(int Row, int Column)>();

        for (var index = 0; index < CellCount; index++)
        {
            var cell = cells[index];

            if (!seen.Add((cell.Row, cell.Column)))
            {
                throw new ArgumentException("A room's cells are distinct.", nameof(cells));
            }

            _cells[index] = cell;
        }

        if (!Contains(doorway))
        {
            throw new ArgumentException("The doorway is one of the room's own cells.", nameof(doorway));
        }

        if (Contains(door))
        {
            throw new ArgumentException("The door is ordinary floor outside the room.", nameof(door));
        }

        if (!Contains(center))
        {
            throw new ArgumentException("The centre is one of the room's own cells.", nameof(center));
        }

        Doorway = doorway;
        Door = door;
        Center = center;
    }

    /// <summary>Gets the nine cells, in row-major order from the room's top left.</summary>
    public IReadOnlyList<Cell> Cells => new ReadOnlyCollection<Cell>(_cells);

    /// <summary>Gets the room cell the single entrance is cut from.</summary>
    public Cell Doorway { get; }

    /// <summary>
    /// Gets the ordinary floor cell outside the entrance. It is not part of the room: standing
    /// there is standing in the maze, and it is the only way in or out.
    /// </summary>
    public Cell Door { get; }

    /// <summary>Gets the middle cell.</summary>
    public Cell Center { get; }

    /// <summary>Gets the top-left cell of the room's 3x3 block.</summary>
    public Cell TopLeft => _cells[0];

    /// <summary>
    /// Determines whether <paramref name="cell"/> is one of the room's nine cells.
    /// </summary>
    /// <param name="cell">The cell to test.</param>
    /// <returns><see langword="true"/> when the cell lies inside the room.</returns>
    /// <remarks>
    /// The comparison is by position, not by value: a wall may later open or close (a pressure
    /// gate, a Fracture), and a room must not stop recognising its own floor because a wall moved.
    /// </remarks>
    public bool Contains(Cell cell)
    {
        foreach (var roomCell in _cells)
        {
            if (roomCell.Row == cell.Row && roomCell.Column == cell.Column)
            {
                return true;
            }
        }

        return false;
    }
}

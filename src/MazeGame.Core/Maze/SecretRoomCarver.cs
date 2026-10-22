using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Maze;

/// <summary>
/// Finds and carves a floor's secret room: a 3x3 open area with exactly one entrance.
/// </summary>
/// <remarks>
/// <para>
/// The carver never disturbs the maze. It only ever turns rock into floor, so no corridor is
/// rerouted, no passage among the original cells is removed, and every original cell stays
/// reachable — the room is a new dead end hanging off the maze by a single door.
/// </para>
/// <para>
/// Planning happens before generation, because a floor with no rock of its own has to have its
/// pocket reserved first: closing nine cells before Kruskal is what makes a rectangular floor
/// able to hold a room at all (see <see cref="SecretRoomSite"/>). The roll and the site are drawn
/// from the caller's own random stream, so rooms neither shift an existing maze nor change where
/// the player, the arch or the hunters are placed.
/// </para>
/// </remarks>
public static class SecretRoomCarver
{
    /// <summary>How many opening floors always hold a room.</summary>
    /// <remarks>
    /// The first floor teaches the loop, and the safe room is part of that loop: a mechanic that
    /// first appears on a coin flip is a mechanic a new player may never meet. Every floor after
    /// it rolls, so the rooms are a find rather than a fixed schedule.
    /// </remarks>
    public const int TeachingFloors = 1;

    /// <summary>Gets the chance every floor after the teaching floor rolls, in whole percent.</summary>
    /// <remarks>
    /// Half of the floors hold a room, which is about ten in a twenty-floor run. The old curve
    /// was one percent per floor, so floor 17 rolled a room one time in six and a run could reach
    /// the bottom having met the mechanic only once; a room the player never walks into is a
    /// whole system that might as well not exist.
    /// </remarks>
    public const int RoomChancePercent = 50;

    /// <summary>Gets the percentage chance a floor has of holding a room.</summary>
    /// <param name="floorNumber">The floor's number in the run.</param>
    /// <returns>The chance in whole percent: 100 on the first floor, 50 on every floor after it.</returns>
    public static int ChancePercent(int floorNumber) =>
        floorNumber <= 0 ? 0 :
        floorNumber <= TeachingFloors ? 100 :
        RoomChancePercent;

    /// <summary>
    /// Rolls whether <paramref name="floorNumber"/> holds a room at all.
    /// </summary>
    /// <param name="floorNumber">The floor's number, which decides its chance.</param>
    /// <param name="random">The room's own stream.</param>
    /// <returns><see langword="true"/> when this floor rolled a room.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <remarks>
    /// Kept separate from <see cref="Plan"/> so the chance curve can be tested and shown on its
    /// own: the Cartographer's card is exactly this number for the floors below.
    /// </remarks>
    public static bool Rolls(int floorNumber, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.NextInt(100) < ChancePercent(floorNumber);
    }

    /// <summary>
    /// Rolls the floor's chance and picks the site its room would be carved from.
    /// </summary>
    /// <param name="shape">The floor's outline, or <see langword="null"/> for a full rectangle.</param>
    /// <param name="rows">The maze's height in cells.</param>
    /// <param name="columns">The maze's width in cells.</param>
    /// <param name="floorNumber">The floor's number, which decides its chance.</param>
    /// <param name="random">
    /// The room's own stream. Passing the floor's maze stream here would shift the maze whenever a
    /// room was rolled, which is exactly what the separate salt exists to prevent.
    /// </param>
    /// <returns>
    /// The site to carve, or <see langword="null"/> when the roll missed or the floor is too small
    /// to hold a room inside its border.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="rows"/> or <paramref name="columns"/> is below one.
    /// </exception>
    public static SecretRoomSite? Plan(
        MazeShape? shape,
        int rows,
        int columns,
        int floorNumber,
        IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "A maze needs at least one row.");
        }

        if (columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A maze needs at least one column.");
        }

        if (shape is not null && (shape.Rows != rows || shape.Columns != columns))
        {
            throw new ArgumentException(
                $"The shape is {shape.Rows}x{shape.Columns} but the maze is {rows}x{columns}.",
                nameof(shape));
        }

        // A room has to sit one cell in from the border, or its own wall ring would be the maze's
        // outer wall and the ninth cell could be outside the grid entirely.
        if (rows < SecretRoom.Size + 2 || columns < SecretRoom.Size + 2)
        {
            return null;
        }

        if (!Rolls(floorNumber, random))
        {
            return null;
        }

        var sites = new List<SecretRoomSite>(4);

        // Rock first: a room hidden behind the floor's own wall reads better than one punched into
        // the middle of the cave, so a shaped floor prefers what its outline already left over.
        CollectRockSites(shape, rows, columns, sites);

        if (sites.Count == 0)
        {
            CollectPocketSites(shape, rows, columns, sites);
        }

        if (sites.Count == 0)
        {
            return null;
        }

        return sites[random.NextInt(sites.Count)];
    }

    /// <summary>
    /// Closes the nine cells of a reserved site so the maze is generated around them.
    /// </summary>
    /// <param name="site">The site whose pocket must be kept clear of corridors.</param>
    /// <returns>The nine cells, in row-major order, for <see cref="MazeOptions.ReservedCells"/>.</returns>
    public static IReadOnlyList<Cell> ReservedCells(SecretRoomSite site)
    {
        var cells = new Cell[SecretRoom.CellCount];
        var index = 0;

        for (var rowOffset = 0; rowOffset < SecretRoom.Size; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
            {
                cells[index++] = new Cell(site.Row + rowOffset, site.Column + columnOffset);
            }
        }

        return cells;
    }

    /// <summary>
    /// Carves the planned room into an already generated maze.
    /// </summary>
    /// <param name="grid">The generated maze.</param>
    /// <param name="site">The site planned before generation, or <see langword="null"/> for none.</param>
    /// <param name="random">The room's own stream, used to pick the one door.</param>
    /// <returns>The carved room, or <see langword="null"/> when there was nothing to carve.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="grid"/> or <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the site does not fit the grid, which would be a planning bug.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the site's nine cells are not rock. Carving a cell that already has passages
    /// would silently delete them, so this fails loudly rather than damaging the maze.
    /// </exception>
    public static SecretRoom? TryCarve(Grid grid, SecretRoomSite? site, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(random);

        if (site is not { } planned)
        {
            return null;
        }

        if (planned.Row < 0 || planned.Column < 0 ||
            planned.Row + SecretRoom.Size > grid.Rows ||
            planned.Column + SecretRoom.Size > grid.Columns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(site),
                $"A room at ({planned.Row}, {planned.Column}) does not fit a {grid.Rows}x{grid.Columns} maze.");
        }

        for (var rowOffset = 0; rowOffset < SecretRoom.Size; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
            {
                if (!grid.IsSolid(planned.Row + rowOffset, planned.Column + columnOffset))
                {
                    throw new InvalidOperationException(
                        $"The room's cell ({planned.Row + rowOffset}, {planned.Column + columnOffset}) " +
                        "was carved by the maze; a room may only be cut from rock.");
                }
            }
        }

        // 1. Rock becomes floor. All nine first, so the internal walls below join two floor cells.
        for (var rowOffset = 0; rowOffset < SecretRoom.Size; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
            {
                grid.CarveCell(planned.Row + rowOffset, planned.Column + columnOffset);
            }
        }

        // 2. One room, not nine cupboards: open every internal edge (six east, six south).
        for (var offset = 0; offset < SecretRoom.Size; offset++)
        {
            for (var inner = 0; inner < SecretRoom.Size - 1; inner++)
            {
                grid.RemoveWall(planned.Row + offset, planned.Column + inner, Direction.East);
                grid.RemoveWall(planned.Row + inner, planned.Column + offset, Direction.South);
            }
        }

        // 3. Exactly one boundary edge becomes the door; the other eleven stay walls.
        var doorways = new List<(Cell Inside, Direction Direction, Cell Outside)>(4);

        foreach (var (inside, direction, outside) in BoundaryEdges(grid, planned))
        {
            if (!grid.IsSolid(outside.Row, outside.Column))
            {
                doorways.Add((inside, direction, outside));
            }
        }

        if (doorways.Count == 0)
        {
            throw new InvalidOperationException(
                $"The room at ({planned.Row}, {planned.Column}) has no floor beside it, so it could never be entered.");
        }

        var doorIndex = random.NextInt(doorways.Count);

        for (var index = 0; index < doorways.Count; index++)
        {
            var (inside, direction, _) = doorways[index];

            if (index == doorIndex)
            {
                grid.RemoveWall(inside.Row, inside.Column, direction);
            }
            else
            {
                // Already standing for a pocket the generator never touched; the explicit close
                // keeps the single-entrance guarantee true even if a future decorator opens one.
                grid.AddWall(inside.Row, inside.Column, direction);
            }
        }

        var cells = new Cell[SecretRoom.CellCount];
        var next = 0;

        for (var rowOffset = 0; rowOffset < SecretRoom.Size; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
            {
                cells[next++] = grid.GetCell(planned.Row + rowOffset, planned.Column + columnOffset);
            }
        }

        var (doorwayCell, _, doorCell) = doorways[doorIndex];

        return new SecretRoom(
            cells,
            doorwayCell,
            doorCell,
            grid.GetCell(planned.Row + 1, planned.Column + 1));
    }

    /// <summary>Finds 3x3 blocks of solid rock that touch at least one floor cell.</summary>
    private static void CollectRockSites(
        MazeShape? shape,
        int rows,
        int columns,
        List<SecretRoomSite> sites)
    {
        foreach (var (row, column) in Blocks(rows, columns))
        {
            var solid = true;

            for (var rowOffset = 0; rowOffset < SecretRoom.Size && solid; rowOffset++)
            {
                for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
                {
                    if (IsOpen(shape, row + rowOffset, column + columnOffset))
                    {
                        solid = false;
                        break;
                    }
                }
            }

            // Rock that is itself surrounded by rock is an island inside the stone: carving it
            // would make a cupboard with no door. It has to face the floor somewhere.
            if (solid && HasOpenNeighbour(shape, row, column))
            {
                sites.Add(new SecretRoomSite(row, column, Reserved: false));
            }
        }
    }

    /// <summary>
    /// Finds 3x3 blocks of floor that can be closed before generation without splitting the floor.
    /// </summary>
    private static void CollectPocketSites(
        MazeShape? shape,
        int rows,
        int columns,
        List<SecretRoomSite> sites)
    {
        foreach (var (row, column) in Blocks(rows, columns))
        {
            var open = true;

            for (var rowOffset = 0; rowOffset < SecretRoom.Size && open; rowOffset++)
            {
                for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
                {
                    if (!IsOpen(shape, row + rowOffset, column + columnOffset))
                    {
                        open = false;
                        break;
                    }
                }
            }

            if (open && StaysConnected(shape, rows, columns, row, column))
            {
                sites.Add(new SecretRoomSite(row, column, Reserved: true));
            }
        }
    }

    /// <summary>Determines whether any of a block's twelve neighbouring cells is floor.</summary>
    private static bool HasOpenNeighbour(MazeShape? shape, int blockRow, int blockColumn)
    {
        for (var offset = 0; offset < SecretRoom.Size; offset++)
        {
            if (IsOpen(shape, blockRow - 1, blockColumn + offset) ||
                IsOpen(shape, blockRow + SecretRoom.Size, blockColumn + offset) ||
                IsOpen(shape, blockRow + offset, blockColumn - 1) ||
                IsOpen(shape, blockRow + offset, blockColumn + SecretRoom.Size))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests that closing a 3x3 block leaves every remaining floor cell reachable.
    /// </summary>
    /// <remarks>
    /// A blob's waist can be three cells wide: closing a block across it would cut the cave in two
    /// and strand whatever sits beyond. The generator would happily draw the surviving half, and
    /// the room would then be the only way across — so the site is rejected before it is reserved.
    /// </remarks>
    private static bool StaysConnected(
        MazeShape? shape,
        int rows,
        int columns,
        int blockRow,
        int blockColumn)
    {
        var total = 0;
        var start = -1;
        var seen = new bool[rows * columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (!IsOpen(shape, row, column) || InBlock(row, column, blockRow, blockColumn))
                {
                    continue;
                }

                total++;

                if (start < 0)
                {
                    start = (row * columns) + column;
                    seen[start] = true;
                }
            }
        }

        if (start < 0)
        {
            // The floor is nothing but this block: there is no maze left to attach a door to.
            return false;
        }

        var visited = 1;
        var pending = new Stack<int>();
        pending.Push(start);

        while (pending.Count > 0)
        {
            var cell = pending.Pop();
            var row = cell / columns;
            var column = cell % columns;

            for (var index = 0; index < NeighbourDirections.Length; index++)
            {
                var (rowDelta, columnDelta) = NeighbourDirections[index].Delta();
                var neighbourRow = row + rowDelta;
                var neighbourColumn = column + columnDelta;

                if (neighbourRow < 0 || neighbourRow >= rows ||
                    neighbourColumn < 0 || neighbourColumn >= columns)
                {
                    continue;
                }

                var neighbour = (neighbourRow * columns) + neighbourColumn;

                if (seen[neighbour] ||
                    !IsOpen(shape, neighbourRow, neighbourColumn) ||
                    InBlock(neighbourRow, neighbourColumn, blockRow, blockColumn))
                {
                    continue;
                }

                seen[neighbour] = true;
                visited++;
                pending.Push(neighbour);
            }
        }

        return visited == total;
    }

    /// <summary>Yields every 3x3 block that keeps a one-cell border of the grid around it.</summary>
    private static IEnumerable<(int Row, int Column)> Blocks(int rows, int columns)
    {
        for (var row = 1; row + SecretRoom.Size < rows; row++)
        {
            for (var column = 1; column + SecretRoom.Size < columns; column++)
            {
                yield return (row, column);
            }
        }
    }

    /// <summary>
    /// Yields the twelve boundary edges of the block, with the room cell on the inside.
    /// </summary>
    private static IEnumerable<(Cell Inside, Direction Direction, Cell Outside)> BoundaryEdges(
        Grid grid,
        SecretRoomSite site)
    {
        for (var offset = 0; offset < SecretRoom.Size; offset++)
        {
            yield return Edge(grid, site.Row, site.Column + offset, Direction.North);
            yield return Edge(grid, site.Row + SecretRoom.Size - 1, site.Column + offset, Direction.South);
            yield return Edge(grid, site.Row + offset, site.Column, Direction.West);
            yield return Edge(grid, site.Row + offset, site.Column + SecretRoom.Size - 1, Direction.East);
        }
    }

    /// <summary>Builds one boundary edge that a door could be cut from.</summary>
    private static (Cell Inside, Direction Direction, Cell Outside) Edge(
        Grid grid,
        int row,
        int column,
        Direction direction)
    {
        var (rowDelta, columnDelta) = direction.Delta();

        return (
            grid.GetCell(row, column),
            direction,
            grid.GetCell(row + rowDelta, column + columnDelta));
    }

    /// <summary>Determines whether a cell is inside the 3x3 block.</summary>
    private static bool InBlock(int row, int column, int blockRow, int blockColumn) =>
        row >= blockRow && row < blockRow + SecretRoom.Size &&
        column >= blockColumn && column < blockColumn + SecretRoom.Size;

    /// <summary>Determines whether a cell is floor of the shape (every cell, for a rectangle).</summary>
    private static bool IsOpen(MazeShape? shape, int row, int column) =>
        shape is null || shape.IsOpen(row, column);

    private static readonly Direction[] NeighbourDirections =
        [Direction.North, Direction.East, Direction.South, Direction.West];
}

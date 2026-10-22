using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Maze;

/// <summary>
/// Generates a perfect maze with randomised Kruskal's algorithm, built on
/// <see cref="DisjointSet"/>.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm treats each cell as its own set. Every wall between two neighbouring
/// cells is a candidate passage; the candidates are shuffled, then visited once:
/// </para>
/// <list type="number">
///   <item>
///     <description>Ask the <see cref="DisjointSet"/> whether the two cells are already
///     in the same set.</description>
///   </item>
///   <item>
///     <description>If they are not (the union succeeds), remove the wall and merge the
///     sets. If they are (the union fails), the wall would close a loop, so it stays.</description>
///   </item>
/// </list>
/// <para>
/// Because a wall is removed only when it joins two different sets, no cycle can ever be
/// created. Because the loop keeps going until one set remains, the result is connected.
/// Connected with no cycles means exactly one route between any two cells, and exactly
/// <c>cellCount - 1</c> passages. That is what makes it a perfect maze.
/// </para>
/// <para>
/// Randomness comes only from the injected <see cref="IRandom"/>, so the same seed
/// reproduces the same maze every time.
/// </para>
/// </remarks>
public sealed class MazeGenerator : IMazeGenerator
{
    private readonly IRandom _random;

    /// <summary>
    /// Creates a generator that takes all its randomness from
    /// <paramref name="random"/>.
    /// </summary>
    /// <param name="random">The source of randomness; usually a seeded implementation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="random"/> is <see langword="null"/>.
    /// </exception>
    public MazeGenerator(IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the requested maze has fewer than one row or column.
    /// </exception>
    public Grid Generate(MazeOptions options)
    {
        // Validation happens in Grid: it is the single authority on what a legal maze
        // size is, so this class does not repeat the bounds checks.
        var grid = new Grid(options.Rows, options.Columns);

        // The floor's outline is decided before a single wall moves: cells outside it are rock,
        // and every candidate wall below is between two floor cells. Shaping afterwards would
        // carve passages into the rock and could cut the maze in two.
        options.Shape?.ApplyTo(grid);

        // A reserved pocket is closed before the first candidate wall is collected, so the maze is
        // generated around it and no corridor is ever routed through it. Carving the room later is
        // then the only thing that can open those cells.
        if (options.ReservedCells is { } reserved)
        {
            foreach (var cell in reserved)
            {
                grid.CloseCell(cell.Row, cell.Column);
            }
        }

        // One set per cell. The index arithmetic comes from Grid so that the mapping
        // between (row, column) and set index lives in exactly one place.
        var sets = new DisjointSet(grid.CellCount);

        var candidates = CollectCandidateWalls(grid);
        _random.Shuffle(candidates);

        foreach (var wall in candidates)
        {
            var (rowDelta, columnDelta) = wall.Direction.Delta();
            var firstIndex = grid.GetIndex(wall.Row, wall.Column);
            var secondIndex = grid.GetIndex(wall.Row + rowDelta, wall.Column + columnDelta);

            // Union returns false when the cells were already connected, which is exactly
            // the case where removing this wall would create a cycle. Keeping the wall in
            // that case is what makes the maze perfect.
            if (sets.Union(firstIndex, secondIndex))
            {
                grid.RemoveWall(wall.Row, wall.Column, wall.Direction);
            }
        }

        return grid;
    }

    /// <summary>
    /// Lists every wall that could become a passage.
    /// </summary>
    /// <remarks>
    /// Only the south and east walls of each cell are collected. Each of those is shared
    /// with exactly one neighbour, so the list holds every interior wall exactly once and
    /// never includes the outer boundary, which must stay closed. A wall with solid rock on either
    /// side is not a candidate at all: it is the edge of the floor's shape, and opening it would
    /// walk into the rock.
    /// </remarks>
    private static List<CandidateWall> CollectCandidateWalls(Grid grid)
    {
        var candidates = new List<CandidateWall>(
            (grid.Rows * (grid.Columns - 1)) + (grid.Columns * (grid.Rows - 1)));

        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                if (grid.IsSolid(row, column))
                {
                    continue;
                }

                if (row + 1 < grid.Rows && !grid.IsSolid(row + 1, column))
                {
                    candidates.Add(new CandidateWall(row, column, Direction.South));
                }

                if (column + 1 < grid.Columns && !grid.IsSolid(row, column + 1))
                {
                    candidates.Add(new CandidateWall(row, column, Direction.East));
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// A wall that may be knocked down, identified by the cell on its inside and the
    /// direction pointing at it.
    /// </summary>
    private readonly record struct CandidateWall(int Row, int Column, Direction Direction);
}

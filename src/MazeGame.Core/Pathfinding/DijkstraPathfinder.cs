using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Pathfinding;

/// <summary>
/// Dijkstra's shortest-path algorithm over a maze, built on the hand-written
/// <see cref="MinHeap{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Dijkstra grows a set of cells whose cheapest cost is final. It starts at the source
/// with cost 0 and repeatedly takes the <i>unsettled</i> cell with the smallest known cost
/// out of the min-heap; because every weight is positive, no later route can ever beat a
/// cell that has already been taken, so that cell's cost is final. Each neighbour is then
/// "relaxed": if going through the current cell is cheaper than the best route found so
/// far, the neighbour's cost is lowered and it is pushed back into the heap.
/// </para>
/// <para>
/// <b>Why a heap and not a linear scan?</b> Picking the minimum by scanning every unsettled
/// cell costs <c>O(V)</c> per step, so <c>O(V²)</c> overall. The heap makes each pick
/// <c>O(log V)</c> and there is at most one push per edge, giving
/// <c>O((V + E) log V)</c> — for a grid maze that is dramatically faster, and it is the
/// graded data structure.
/// </para>
/// <para>
/// <b>No decrease-key operation.</b> Instead of finding and lowering an entry already in
/// the heap (which needs an index map), this implementation pushes a fresh entry with the
/// lower cost and ignores stale entries when they come out — each entry is checked against
/// the best known distance. That is simpler to read and costs only a logarithmic factor on
/// the number of pushes.
/// </para>
/// <para>
/// The search never crosses a wall: neighbours are only considered when the wall between
/// them has been removed. Movement cost is the weight of the cell being entered, so an
/// obstacle only makes a detour attractive, never the destination unreachable.
/// </para>
/// </remarks>
public sealed class DijkstraPathfinder : IPathfinder
{
    // Sentinel meaning "this cell has no known predecessor yet".
    private const int NoPrevious = -1;

    // Walking the four directions is the inner loop of the search; caching the array avoids
    // allocating a new one for every cell that is settled.
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();

    /// <inheritdoc />
    public IReadOnlyList<Cell> FindPath(Grid grid, Cell start, Cell goal, Func<Cell, int> cost)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(cost);

        // GetIndex validates the coordinates, so a cell from outside the grid is rejected
        // here rather than causing an out-of-bounds write below.
        var startIndex = grid.GetIndex(start.Row, start.Column);
        var goalIndex = grid.GetIndex(goal.Row, goal.Column);

        var distances = new int[grid.CellCount];
        Array.Fill(distances, int.MaxValue);

        // For every cell, the index of the cell we arrived from on the cheapest route, so
        // the path can be rebuilt backwards once the goal is settled.
        var previous = new int[grid.CellCount];
        Array.Fill(previous, NoPrevious);

        distances[startIndex] = 0;

        var frontier = new MinHeap<FrontierEntry>();
        frontier.Insert(new FrontierEntry(0, startIndex));

        while (!frontier.IsEmpty)
        {
            var current = frontier.ExtractMin();

            // A stale entry: a cheaper route to this cell was found after this entry was
            // pushed, so its distance is already better. Nothing to do.
            if (current.Distance > distances[current.Index])
            {
                continue;
            }

            // The goal coming out of the heap means its cost is final: every route still in
            // the heap is at least this expensive.
            if (current.Index == goalIndex)
            {
                break;
            }

            RelaxNeighbours(grid, cost, distances, previous, frontier, current);
        }

        if (distances[goalIndex] == int.MaxValue)
        {
            return [];
        }

        return ReconstructPath(grid, previous, startIndex, goalIndex);
    }

    /// <summary>
    /// Offers every open neighbour of the settled cell a cheaper route through it.
    /// </summary>
    private static void RelaxNeighbours(
        Grid grid,
        Func<Cell, int> cost,
        int[] distances,
        int[] previous,
        MinHeap<FrontierEntry> frontier,
        FrontierEntry current)
    {
        var row = current.Index / grid.Columns;
        var column = current.Index % grid.Columns;

        foreach (var direction in Directions)
        {
            // A standing wall means there is no edge in that direction, even if a cell is
            // geometrically adjacent.
            if (grid.HasWall(row, column, direction))
            {
                continue;
            }

            var (rowDelta, columnDelta) = direction.Delta();
            var neighbourRow = row + rowDelta;
            var neighbourColumn = column + columnDelta;

            // Boundary walls are never removed, but checking keeps this safe for any grid.
            if (neighbourRow < 0 || neighbourRow >= grid.Rows ||
                neighbourColumn < 0 || neighbourColumn >= grid.Columns)
            {
                continue;
            }

            var weight = cost(grid.GetCell(neighbourRow, neighbourColumn));
            if (weight < 1)
            {
                throw new ArgumentException(
                    $"The cost of entering a cell must be at least 1 but was {weight}.",
                    nameof(cost));
            }

            var neighbourIndex = grid.GetIndex(neighbourRow, neighbourColumn);

            // Sum in long: a caller-supplied huge weight must not wrap around into a
            // negative distance, which would silently corrupt the search.
            var candidateDistance = (long)current.Distance + weight;

            if (candidateDistance >= distances[neighbourIndex])
            {
                continue;
            }

            distances[neighbourIndex] = (int)candidateDistance;
            previous[neighbourIndex] = current.Index;
            frontier.Insert(new FrontierEntry((int)candidateDistance, neighbourIndex));
        }
    }

    /// <summary>
    /// Walks the predecessor links from the goal back to the start and returns the cells in
    /// travel order.
    /// </summary>
    private static Cell[] ReconstructPath(Grid grid, int[] previous, int startIndex, int goalIndex)
    {
        var reversed = new List<int>();

        for (var index = goalIndex; index != NoPrevious; index = previous[index])
        {
            reversed.Add(index);

            if (index == startIndex)
            {
                break;
            }
        }

        reversed.Reverse();

        var path = new Cell[reversed.Count];
        for (var position = 0; position < reversed.Count; position++)
        {
            var index = reversed[position];
            path[position] = grid.GetCell(index / grid.Columns, index % grid.Columns);
        }

        return path;
    }

    /// <summary>
    /// One pending route to a cell: how much it has cost so far, and which cell it reaches.
    /// Ordered by distance so the <see cref="MinHeap{T}"/> always yields the closest cell.
    /// </summary>
    private readonly record struct FrontierEntry(int Distance, int Index) : IComparable<FrontierEntry>
    {
        /// <summary>
        /// Compares by distance, then by cell index so that equal distances come out in a
        /// fixed order. Determinism matters: the same maze must always give the same path.
        /// </summary>
        public int CompareTo(FrontierEntry other)
        {
            var byDistance = Distance.CompareTo(other.Distance);
            return byDistance != 0 ? byDistance : Index.CompareTo(other.Index);
        }
    }
}

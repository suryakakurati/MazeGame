using System;
using System.Collections.Generic;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Abstractions;

/// <summary>
/// Finds the cheapest route between two cells of a maze.
/// </summary>
/// <remarks>
/// <para>
/// The pathfinder is intentionally ignorant of <i>why</i> a cell is expensive: it asks the
/// supplied cost function for the weight of each cell it enters. Obstacles, slow floors or
/// hazards are therefore a game-state concept, not a pathfinding concept, and a new kind
/// of weighted terrain needs no change here.
/// </para>
/// <para>
/// Weights must be positive (at least 1) and finite. Nothing in Core is allowed to make a
/// cell impassable, so a path inside a connected maze always exists — which is what stops
/// the enemy from ever being permanently stuck.
/// </para>
/// </remarks>
public interface IPathfinder
{
    /// <summary>
    /// Finds the cheapest path from <paramref name="start"/> to
    /// <paramref name="goal"/>, moving only between cells with no wall between them.
    /// </summary>
    /// <param name="grid">The maze to search.</param>
    /// <param name="start">The cell the route begins at (inclusive in the result).</param>
    /// <param name="goal">The cell the route ends at (inclusive in the result).</param>
    /// <param name="cost">
    /// The weight of <i>entering</i> a cell. The start cell's own weight is not paid.
    /// Must return at least 1; a larger value means the cell is more expensive to cross.
    /// </param>
    /// <returns>
    /// The cells from <paramref name="start"/> to <paramref name="goal"/> in order, both
    /// ends included, or an empty list when no route exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="grid"/> or <paramref name="cost"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a cell lies outside <paramref name="grid"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="cost"/> returns a weight below 1, which would break
    /// Dijkstra's guarantee.
    /// </exception>
    IReadOnlyList<Cell> FindPath(Grid grid, Cell start, Cell goal, Func<Cell, int> cost);
}

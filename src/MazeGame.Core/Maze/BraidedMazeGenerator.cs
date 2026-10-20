using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Maze;

/// <summary>
/// Decorates another <see cref="IMazeGenerator"/> by knocking out a few interior walls after
/// it runs, which turns some corridors into loops ("braiding" the maze).
/// </summary>
/// <remarks>
/// <para>
/// The inner generator still produces the maze, so the graded algorithm (randomised Kruskal)
/// and its perfect-maze guarantees are untouched. This class only removes walls that are
/// still standing, and removing a wall from a perfect maze always adds exactly one cycle: the
/// result stays fully connected, it just stops being a tree.
/// </para>
/// <para>
/// That matters for play, because a perfect maze leaves the killer no alternative route, so a
/// barrier can only cost it time. With a handful of loops, a barrier placed on the short way
/// round makes Dijkstra pick the long way — the skill mechanic gains a second dimension.
/// </para>
/// <para>
/// The count is absolute rather than a fraction so "a few" means a few on a 15x15 maze and on
/// an 8x8 one; asking for more loops than there are interior walls simply removes every
/// remaining interior wall instead of throwing, which keeps debug configs safe.
/// </para>
/// </remarks>
public sealed class BraidedMazeGenerator : IMazeGenerator
{
    private readonly IMazeGenerator _inner;
    private readonly IRandom _random;

    /// <summary>
    /// Wraps <paramref name="inner"/> so that each generated maze additionally loses
    /// <paramref name="extraPassages"/> interior walls.
    /// </summary>
    /// <param name="inner">The generator that builds the perfect maze first.</param>
    /// <param name="random">
    /// Source of randomness used to choose which walls to remove. Pass the same instance the
    /// inner generator uses to keep a whole game reproducible from one seed.
    /// </param>
    /// <param name="extraPassages">
    /// How many interior walls to remove. Zero leaves the maze perfect. Must not be negative.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="inner"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="extraPassages"/> is negative.
    /// </exception>
    public BraidedMazeGenerator(IMazeGenerator inner, IRandom random, int extraPassages)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(random);

        if (extraPassages < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extraPassages),
                extraPassages,
                "The number of extra passages cannot be negative.");
        }

        _inner = inner;
        _random = random;
        ExtraPassages = extraPassages;
    }

    /// <summary>
    /// Gets the number of interior walls this generator tries to remove from each maze.
    /// </summary>
    public int ExtraPassages { get; }

    /// <summary>
    /// Generates a maze with <see cref="ExtraPassages"/> extra loops.
    /// </summary>
    /// <param name="options">The requested size and seed, passed straight to the inner generator.</param>
    /// <returns>
    /// A fully connected grid with at most <see cref="ExtraPassages"/> extra passages beyond a
    /// perfect maze's <c>cellCount - 1</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by the inner generator when <paramref name="options"/> describes fewer than one
    /// row or column.
    /// </exception>
    public Grid Generate(MazeOptions options)
    {
        var maze = _inner.Generate(options);

        if (ExtraPassages == 0)
        {
            return maze;
        }

        var candidates = CollectStandingInteriorWalls(maze);
        _random.Shuffle(candidates);

        var removals = Math.Min(ExtraPassages, candidates.Count);
        for (var index = 0; index < removals; index++)
        {
            var (row, column, direction) = candidates[index];
            maze.RemoveWall(row, column, direction);
        }

        return maze;
    }

    /// <summary>
    /// Lists every interior wall that is still standing, exactly once.
    /// </summary>
    /// <remarks>
    /// Only the east and south sides of each cell are collected, because the west and north
    /// walls of its neighbours are the same walls seen from the other side. The outer boundary
    /// is never a candidate: a neighbour outside the grid is required to remove a wall.
    /// </remarks>
    private static List<(int Row, int Column, Direction Direction)> CollectStandingInteriorWalls(Grid maze)
    {
        var candidates = new List<(int Row, int Column, Direction Direction)>();

        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (column + 1 < maze.Columns && maze.HasWall(row, column, Direction.East))
                {
                    candidates.Add((row, column, Direction.East));
                }

                if (row + 1 < maze.Rows && maze.HasWall(row, column, Direction.South))
                {
                    candidates.Add((row, column, Direction.South));
                }
            }
        }

        return candidates;
    }
}

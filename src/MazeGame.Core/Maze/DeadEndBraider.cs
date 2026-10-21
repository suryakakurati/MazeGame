using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Maze;

/// <summary>
/// Decorates another <see cref="IMazeGenerator"/> by opening a second exit from a share of its
/// dead ends, which is the "braiding" that decides how often a chase becomes a corner trap.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the 15x15 game, a perfect maze has 64 dead ends out of 225 cells, and the
/// random-wall braid leaves 57: a quarter of the map is a trap, so being run into one is the
/// default outcome rather than a mistake. Opening one wall at roughly half of them leaves about
/// 22 while keeping long corridors intact, which changes how the maze <i>plays</i>: a barrier
/// on the killer's route can then send it the long way round instead of merely stalling it.
/// </para>
/// <para>
/// Removing a wall only ever raises the number of openings, so braiding cannot create a new
/// dead end; the count therefore falls monotonically with the share. Both cells of the new
/// passage are updated by <see cref="Grid.RemoveWall"/>, the outer boundary is never touched,
/// and the maze stays fully connected because it was connected before.
/// </para>
/// <para>
/// Dead ends with no interior wall to open (the far ends of a one-row corridor, for example)
/// are skipped rather than counted, so a tiny debug maze degrades to "fewer loops" instead of
/// throwing.
/// </para>
/// </remarks>
public sealed class DeadEndBraider : IMazeGenerator
{
    private const int WallsInADeadEnd = 3;

    private readonly IMazeGenerator _inner;
    private readonly IRandom _random;

    /// <summary>
    /// Wraps <paramref name="inner"/> so that each generated maze opens one extra wall at
    /// <paramref name="percent"/> of its dead ends.
    /// </summary>
    /// <param name="inner">The generator that builds the maze whose dead ends are opened.</param>
    /// <param name="random">
    /// Source of randomness used to choose which dead ends to braid and where they open to.
    /// Pass the same instance the inner generator uses so a whole game stays reproducible from
    /// one seed.
    /// </param>
    /// <param name="percent">
    /// Share of dead ends to braid, from 0 (a perfect maze) to 100 (no dead ends left where the
    /// maze allows it). Must be between 0 and 100 inclusive.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="inner"/> or <paramref name="random"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="percent"/> lies outside 0 to 100.
    /// </exception>
    public DeadEndBraider(IMazeGenerator inner, IRandom random, int percent)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(random);

        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                percent,
                "The braided share must be a percentage between 0 and 100.");
        }

        _inner = inner;
        _random = random;
        Percent = percent;
    }

    /// <summary>
    /// Gets the share of dead ends this generator opens, as a percentage.
    /// </summary>
    public int Percent { get; }

    /// <summary>
    /// Generates a maze with a share of its dead ends braided open.
    /// </summary>
    /// <param name="options">The requested size and seed, passed straight to the inner generator.</param>
    /// <returns>
    /// A fully connected grid with at most <c>cellCount - 1</c> plus one passage per braided
    /// dead end.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by the inner generator when <paramref name="options"/> describes fewer than one
    /// row or column.
    /// </exception>
    public Grid Generate(MazeOptions options)
    {
        var maze = _inner.Generate(options);

        if (Percent == 0)
        {
            return maze;
        }

        var deadEnds = CollectDeadEnds(maze);
        var deadEndSet = new HashSet<Cell>(deadEnds);
        _random.Shuffle(deadEnds);

        // Round rather than truncate so "half" of an odd count opens the nearer number of walls
        // instead of silently biasing every maze towards fewer loops.
        var target = (int)Math.Round(deadEnds.Count * Percent / 100.0, MidpointRounding.AwayFromZero);

        // Two neighbouring dead ends share the wall between them, so one opening braids both.
        // Counting the cells rather than the openings keeps the share honest and stops the loop
        // from punching a second hole into a cell that is no longer a dead end.
        var braided = new HashSet<Cell>();

        foreach (var deadEnd in deadEnds)
        {
            if (braided.Count >= target)
            {
                break;
            }

            if (braided.Contains(deadEnd))
            {
                continue;
            }

            if (OpenOneWall(maze, deadEnd) is not { } neighbour)
            {
                continue;
            }

            braided.Add(deadEnd);

            if (deadEndSet.Contains(neighbour))
            {
                braided.Add(neighbour);
            }
        }

        return maze;
    }

    /// <summary>
    /// Lists every cell with exactly three walls, that is exactly one opening.
    /// </summary>
    /// <remarks>
    /// <see cref="Cell.WallCount"/> already counts the outer boundary as a wall, so this needs
    /// no position arithmetic of its own.
    /// </remarks>
    private static List<Cell> CollectDeadEnds(Grid maze)
    {
        var deadEnds = new List<Cell>();

        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                var cell = maze.GetCell(row, column);
                if (cell.WallCount == WallsInADeadEnd)
                {
                    deadEnds.Add(cell);
                }
            }
        }

        return deadEnds;
    }

    /// <summary>
    /// Opens one still-standing wall of <paramref name="deadEnd"/> that has a neighbour behind
    /// it, chosen at random.
    /// </summary>
    /// <returns>
    /// The cell the new passage leads into, or <see langword="null"/> when every standing wall
    /// is on the outer boundary.
    /// </returns>
    /// <remarks>
    /// The candidates are the three walls a dead end has by definition; the one that is already
    /// open is not among them because it was removed. Shuffling a fresh three-element list keeps
    /// the choice unbiased without a manual random index.
    /// </remarks>
    private Cell? OpenOneWall(Grid maze, Cell deadEnd)
    {
        var candidates = new List<Direction>(WallsInADeadEnd);

        foreach (var direction in Enum.GetValues<Direction>())
        {
            if (!maze.HasWall(deadEnd.Row, deadEnd.Column, direction))
            {
                continue;
            }

            var (rowDelta, columnDelta) = direction.Delta();
            var neighbourRow = deadEnd.Row + rowDelta;
            var neighbourColumn = deadEnd.Column + columnDelta;

            // A wall on the outer boundary has no cell behind it and must stay closed, and so must
            // a wall into solid rock: on a shaped floor that rock is the edge of the cave.
            if (neighbourRow < 0 || neighbourRow >= maze.Rows ||
                neighbourColumn < 0 || neighbourColumn >= maze.Columns ||
                maze.IsSolid(neighbourRow, neighbourColumn))
            {
                continue;
            }

            candidates.Add(direction);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        _random.Shuffle(candidates);
        var chosen = candidates[0];
        maze.RemoveWall(deadEnd.Row, deadEnd.Column, chosen);

        var (chosenRowDelta, chosenColumnDelta) = chosen.Delta();
        return maze.GetCell(deadEnd.Row + chosenRowDelta, deadEnd.Column + chosenColumnDelta);
    }
}

using System.Collections.Generic;

namespace MazeGame.Core.Maze;

/// <summary>
/// Describes the maze a caller wants: its size and, optionally, the seed that makes the
/// result reproducible.
/// </summary>
/// <param name="Rows">Number of rows, at least 1.</param>
/// <param name="Columns">Number of columns, at least 1.</param>
/// <param name="Seed">
/// Seed for the generator, or <see langword="null"/> for a different maze on every run.
/// The same seed always produces the same maze.
/// </param>
/// <remarks>
/// <para>
/// This is a plain request object with no logic, so it can be built from a settings file,
/// a command-line argument or a test without depending on the generator. The values are
/// validated by <see cref="Grid"/> when the maze is actually generated.
/// </para>
/// <para>
/// <see cref="Shape"/> is the outline the maze is carved into. It is optional and defaults to the
/// full rectangle, which is what every caller that does not care about floor silhouettes gets.
/// </para>
/// </remarks>
public readonly record struct MazeOptions(int Rows, int Columns, int? Seed)
{
    /// <summary>
    /// Gets the outline the maze is carved into, or null for the whole rectangle.
    /// </summary>
    /// <remarks>
    /// The shape must have been grown for the same rows and columns; a mismatch is an argument
    /// error rather than a maze that is quietly the wrong size.
    /// </remarks>
    public MazeShape? Shape { get; init; }

    /// <summary>
    /// Gets the cells that must stay solid so a secret room can be carved there afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied after <see cref="Shape"/> and before the first wall is considered, so the pocket is
    /// rock as far as Kruskal is concerned: no corridor is ever routed through it, and the room's
    /// single door is the only passage it will ever have.
    /// </para>
    /// <para>
    /// This is how a rectangular floor gets a room at all — its shape leaves no rock inside, so the
    /// pocket is reserved instead of found. <see langword="null"/> (the default) leaves the maze
    /// exactly as it was before rooms existed.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Cell>? ReservedCells { get; init; }
}

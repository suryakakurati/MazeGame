using System;

namespace MazeGame.Core.Maze;

/// <summary>
/// Helpers for working with <see cref="Direction"/>: the reverse direction and the
/// row/column step that moving in a direction produces.
/// </summary>
/// <remarks>
/// These live in an extension class (rather than on the enum) so the enum stays a plain
/// value type and the logic is unit-testable on its own.
/// </remarks>
public static class DirectionExtensions
{
    /// <summary>
    /// Returns the direction pointing the opposite way.
    /// </summary>
    /// <param name="direction">Any valid <see cref="Direction"/>.</param>
    /// <returns>
    /// <see cref="Direction.North"/> for south, <see cref="Direction.South"/> for north,
    /// and likewise for east/west.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="direction"/> is not one of the four defined values.
    /// </exception>
    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => throw new ArgumentOutOfRangeException(
            nameof(direction),
            direction,
            "Unknown direction."),
    };

    /// <summary>
    /// Returns the row and column step produced by moving one cell in
    /// <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">Any valid <see cref="Direction"/>.</param>
    /// <returns>
    /// A tuple of the row and column deltas, e.g. north is <c>(-1, 0)</c> and east is
    /// <c>(0, 1)</c>. Add them to a cell's coordinates to reach its neighbour.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="direction"/> is not one of the four defined values.
    /// </exception>
    public static (int Row, int Column) Delta(this Direction direction) => direction switch
    {
        Direction.North => (-1, 0),
        Direction.East => (0, 1),
        Direction.South => (1, 0),
        Direction.West => (0, -1),
        _ => throw new ArgumentOutOfRangeException(
            nameof(direction),
            direction,
            "Unknown direction."),
    };
}

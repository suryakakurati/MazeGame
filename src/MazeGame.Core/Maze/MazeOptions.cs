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
/// This is a plain request object with no logic, so it can be built from a settings file,
/// a command-line argument or a test without depending on the generator. The values are
/// validated by <see cref="Grid"/> when the maze is actually generated.
/// </remarks>
public readonly record struct MazeOptions(int Rows, int Columns, int? Seed);

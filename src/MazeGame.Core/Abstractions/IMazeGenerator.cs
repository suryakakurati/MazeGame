using MazeGame.Core.Maze;

namespace MazeGame.Core.Abstractions;

/// <summary>
/// Produces a playable maze from a request.
/// </summary>
/// <remarks>
/// The generator is behind an interface so the algorithm can be swapped (for example for
/// a room-and-corridor style maze) without touching the game loop, and so tests can
/// substitute a small hand-built maze for a generated one.
/// </remarks>
public interface IMazeGenerator
{
    /// <summary>
    /// Generates a new maze.
    /// </summary>
    /// <param name="options">The requested size and seed.</param>
    /// <returns>
    /// A fully connected grid: every cell is reachable from every other. <see cref="MazeGenerator"/>
    /// returns a perfect maze (exactly one route between any two cells); decorators may shape it
    /// afterwards — <see cref="DeadEndBraider"/> opens a second exit at a share of the dead ends
    /// and <see cref="BraidedMazeGenerator"/> adds loops — but must never disconnect it.
    /// </returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="options"/> describes fewer than one row or column.
    /// </exception>
    Grid Generate(MazeOptions options);
}

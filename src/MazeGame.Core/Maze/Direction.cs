namespace MazeGame.Core.Maze;

/// <summary>
/// The four cardinal directions in which a cell can have a wall or a neighbour.
/// </summary>
/// <remarks>
/// The values are powers of two (1, 2, 4, 8) rather than 0-3 so a cell can record all
/// four of its walls in a single bit mask. Only the four names are valid values; code
/// that receives a direction from outside should treat anything else as a bug.
/// </remarks>
public enum Direction
{
    /// <summary>Towards decreasing row indices (up on screen).</summary>
    North = 1,

    /// <summary>Towards increasing column indices (right on screen).</summary>
    East = 2,

    /// <summary>Towards increasing row indices (down on screen).</summary>
    South = 4,

    /// <summary>Towards decreasing column indices (left on screen).</summary>
    West = 8,
}

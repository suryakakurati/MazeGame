using MazeGame.Core.Game;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Geometry of the graphical window: cell size, margins and window size for a maze.
/// </summary>
/// <remarks>
/// The frontend needs the window size before the renderer exists, and the renderer needs the
/// same numbers to place every cell, so they live here rather than being duplicated (or
/// guessed) in both.
/// </remarks>
internal static class RaylibLayout
{
    /// <summary>Side of one maze cell in pixels.</summary>
    public const int CellSize = 36;

    /// <summary>Thickness of a maze wall in pixels.</summary>
    public const int WallThickness = 3;

    /// <summary>Margin around the maze, in pixels.</summary>
    public const int Margin = 24;

    /// <summary>Height of the status strip above the maze, in pixels.</summary>
    public const int HudHeight = 56;

    /// <summary>Height of the control-hint strip below the maze, in pixels.</summary>
    public const int FooterHeight = 40;

    /// <summary>Target frame rate; the game clock uses measured time, so this is only smoothness.</summary>
    public const int TargetFps = 60;

    /// <summary>Window title.</summary>
    public const string Title = "MazeGame";

    /// <summary>Left edge of the maze inside the window.</summary>
    public const int OriginX = Margin;

    /// <summary>Top edge of the maze inside the window.</summary>
    public const int OriginY = HudHeight + Margin;

    /// <summary>
    /// Returns the window size needed to show a maze of <paramref name="config"/>'s size plus
    /// the two text strips.
    /// </summary>
    public static (int Width, int Height) WindowSize(GameConfig config) =>
        ((config.Columns * CellSize) + (2 * Margin),
         (config.Rows * CellSize) + HudHeight + FooterHeight + (2 * Margin));

    /// <summary>Returns the pixel x of a cell's left edge.</summary>
    public static int CellX(int column) => OriginX + (column * CellSize);

    /// <summary>Returns the pixel y of a cell's top edge.</summary>
    public static int CellY(int row) => OriginY + (row * CellSize);
}

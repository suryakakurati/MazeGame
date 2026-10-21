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
    /// <summary>Side of one maze cell in pixels. Large enough to draw a small figure in.</summary>
    public const int CellSize = 40;

    /// <summary>Thickness of a maze wall in pixels.</summary>
    public const int WallThickness = 6;

    /// <summary>Margin around the maze, in pixels.</summary>
    public const int Margin = 28;

    /// <summary>
    /// Height of the status strip above the maze, in pixels. Two rows: the floor identity and the
    /// key/exit chips on top, the five powerup charges underneath.
    /// </summary>
    public const int HudHeight = 76;

    /// <summary>Height of the control-hint strip below the maze, in pixels.</summary>
    public const int FooterHeight = 44;

    /// <summary>Target frame rate; the game clock uses measured time, so this is only smoothness.</summary>
    public const int TargetFps = 60;

    /// <summary>
    /// The screen scale the window is opened at. The canvas is a viewport now, so the window is sized
    /// from it rather than from the floor: four screen pixels per atlas pixel means one maze cell is
    /// 64 px, against 32 px before the camera existed.
    /// </summary>
    public const int PreferredScale = 4;

    /// <summary>The smallest window the player can drag the frame down to.</summary>
    public const int MinWindowWidth = 640;

    /// <summary>The smallest window height, which still shows the canvas at 2x.</summary>
    public const int MinWindowHeight = 560;

    /// <summary>Window title, matching the title screen rather than the repository name.</summary>
    public const string Title = "Bioluminescent Depths";

    /// <summary>Left edge of the maze inside the window.</summary>
    public const int OriginX = Margin;

    /// <summary>Top edge of the maze inside the window.</summary>
    public const int OriginY = HudHeight + Margin;

    /// <summary>
    /// Returns the window size that shows the floor's viewport at <see cref="PreferredScale"/>,
    /// plus the two text strips.
    /// </summary>
    /// <remarks>
    /// A viewport-sized window is what makes the camera worth having: the frame follows the
    /// lantern, so the window no longer has to hold the whole floor. It is resizable, and
    /// <c>PixelLayout.Fit</c> re-derives the integer scale from whatever size the frame is dragged
    /// to, so the same layout serves a laptop and a 4K monitor.
    /// </remarks>
    public static (int Width, int Height) WindowSize(GameConfig config)
    {
        System.ArgumentNullException.ThrowIfNull(config);

        var cells = Pixels.PixelLayout.ViewCells(config);

        return (
            System.Math.Max(
                MinWindowWidth,
                (Pixels.PixelLayout.CanvasWidth(cells) * PreferredScale) + (2 * Margin)),
            System.Math.Max(
                MinWindowHeight,
                (Pixels.PixelLayout.CanvasHeight(cells) * PreferredScale) + HudHeight + FooterHeight + (2 * Margin)));
    }

    /// <summary>Returns the pixel x of a cell's left edge.</summary>
    public static int CellX(int column) => OriginX + (column * CellSize);

    /// <summary>Returns the pixel y of a cell's top edge.</summary>
    public static int CellY(int row) => OriginY + (row * CellSize);
}

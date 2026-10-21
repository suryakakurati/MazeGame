using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Geometry of the virtual canvas the pixel renderer draws into, and of the integer scale that
/// puts it on screen.
/// </summary>
/// <remarks>
/// <para>
/// The canvas is a <b>viewport</b>, not the floor: it shows a fixed number of cells around the
/// player and the camera moves with them, so a 15x15 maze is seen nine cells at a time. A cell is
/// exactly one 16x16 atlas tile, so the maze can only ever be drawn on its own pixel grid.
/// Nothing here scales by a fraction — <see cref="Fit"/> returns an integer scale and a centred
/// offset, and the letterbox is whatever is left.
/// </para>
/// <para>
/// The viewport is sized from the floor's vision radius: the lantern's circle plus
/// <see cref="ViewMargin"/> rings, so there is always darkness between what is lit and the edge of
/// the screen. A card that raises vision therefore zooms the camera out — the same floor seen from
/// further away, not a different canvas rule.
/// </para>
/// </remarks>
public static class PixelLayout
{
    /// <summary>Side of one atlas tile, and therefore of one maze cell on screen.</summary>
    public const int CellSize = 16;

    /// <summary>Margin around the viewport inside the canvas, in atlas pixels.</summary>
    public const int Pad = 8;

    /// <summary>Strip reserved above the viewport inside the canvas, for the pixel HUD.</summary>
    public const int HeaderHeight = 18;

    /// <summary>The largest integer scale the game will ever draw at.</summary>
    public const int MaxScale = 6;

    /// <summary>Cells shown past the lit radius, so the viewport never ends inside the light.</summary>
    public const int ViewMargin = 1;

    /// <summary>The smallest number of cells across the viewport, so a blind floor still shows a room.</summary>
    public const int MinViewCells = 9;

    /// <summary>The largest number of cells across the viewport, so the whole floor is never on screen.</summary>
    public const int MaxViewCells = 13;

    /// <summary>
    /// Returns how many cells across the viewport is for a floor with <paramref name="visionRadius"/>.
    /// </summary>
    /// <param name="visionRadius">The floor's vision radius in steps; zero is allowed.</param>
    /// <returns>An odd number of cells between <see cref="MinViewCells"/> and <see cref="MaxViewCells"/>.</returns>
    /// <remarks>
    /// Odd so the player always sits in the middle cell of an odd grid; clamped at thirteen so a
    /// generous vision card zooms out without turning the maze into a map.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is negative.</exception>
    public static int ViewCells(int visionRadius)
    {
        if (visionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visionRadius), visionRadius, "A vision radius cannot be negative.");
        }

        var cells = (2 * (visionRadius + ViewMargin)) + 1;

        // Round up to an odd number, then clamp: the player must land on the centre cell.
        if ((cells & 1) == 0)
        {
            cells++;
        }

        return Math.Clamp(cells, MinViewCells, MaxViewCells);
    }

    /// <summary>Returns how many cells across the viewport is for <paramref name="config"/>.</summary>
    /// <param name="config">The floor's settings.</param>
    /// <returns>The viewport size in cells.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static int ViewCells(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return ViewCells(config.VisionRadius);
    }

    /// <summary>Returns the canvas width for a viewport <paramref name="viewColumns"/> cells wide.</summary>
    /// <param name="viewColumns">The viewport size in cells.</param>
    /// <returns>The canvas width in atlas pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is not positive.</exception>
    public static int CanvasWidth(int viewColumns)
    {
        if (viewColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewColumns), viewColumns, "A viewport needs at least one column.");
        }

        return (viewColumns * CellSize) + (2 * Pad);
    }

    /// <summary>Returns the canvas height for a viewport <paramref name="viewRows"/> cells tall.</summary>
    /// <param name="viewRows">The viewport size in cells.</param>
    /// <returns>The canvas height in atlas pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is not positive.</exception>
    public static int CanvasHeight(int viewRows)
    {
        if (viewRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewRows), viewRows, "A viewport needs at least one row.");
        }

        return (viewRows * CellSize) + (2 * Pad) + HeaderHeight;
    }

    /// <summary>Returns the canvas x of <paramref name="column"/>'s left edge, seen from a camera.</summary>
    /// <param name="column">The cell's column.</param>
    /// <param name="cameraColumn">The leftmost cell the camera shows.</param>
    /// <returns>The pixel x inside the canvas; negative when the cell is off the left edge.</returns>
    public static int CellX(int column, int cameraColumn) => Pad + ((column - cameraColumn) * CellSize);

    /// <summary>Returns the canvas y of <paramref name="row"/>'s top edge, seen from a camera.</summary>
    /// <param name="row">The cell's row.</param>
    /// <param name="cameraRow">The topmost cell the camera shows.</param>
    /// <returns>The pixel y inside the canvas; negative when the cell is above the viewport.</returns>
    public static int CellY(int row, int cameraRow) => HeaderHeight + Pad + ((row - cameraRow) * CellSize);

    /// <summary>
    /// Works out the integer scale and offset that put a canvas inside a window.
    /// </summary>
    /// <param name="canvasWidth">The canvas width in atlas pixels.</param>
    /// <param name="canvasHeight">The canvas height in atlas pixels.</param>
    /// <param name="windowWidth">The window width in screen pixels.</param>
    /// <param name="windowHeight">The window height in screen pixels.</param>
    /// <param name="topInset">Screen pixels reserved at the top (the status strip).</param>
    /// <param name="bottomInset">Screen pixels reserved at the bottom (the hint strip).</param>
    /// <param name="margin">Screen pixels to keep free on every side.</param>
    /// <returns>The scale and the top-left corner, centred in what is left.</returns>
    /// <remarks>
    /// The scale is an integer by construction: pixel art resampled by 1.5 is blurry, and a
    /// sharpener would only make it look wrong in a different way. When even 1x does not fit the
    /// canvas is still drawn at 1x and clipped by the window rather than squeezed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a size is not positive or an inset is negative.
    /// </exception>
    public static PixelFit Fit(
        int canvasWidth,
        int canvasHeight,
        int windowWidth,
        int windowHeight,
        int topInset = 0,
        int bottomInset = 0,
        int margin = 0)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(canvasWidth),
                $"A canvas needs a positive size, not {canvasWidth}x{canvasHeight}.");
        }

        if (windowWidth <= 0 || windowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowWidth),
                $"A window needs a positive size, not {windowWidth}x{windowHeight}.");
        }

        if (topInset < 0 || bottomInset < 0 || margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), margin, "Insets and margins cannot be negative.");
        }

        var availableWidth = Math.Max(1, windowWidth - (2 * margin));
        var availableHeight = Math.Max(1, windowHeight - topInset - bottomInset - (2 * margin));

        var scale = Math.Min(availableWidth / canvasWidth, availableHeight / canvasHeight);
        scale = Math.Clamp(scale, 1, MaxScale);

        var width = canvasWidth * scale;
        var height = canvasHeight * scale;

        var x = (windowWidth - width) / 2;
        var y = topInset + margin + Math.Max(0, (availableHeight - height) / 2);

        return new PixelFit(scale, x, y, width, height);
    }

    /// <summary>Where a scaled canvas sits in the window.</summary>
    /// <param name="Scale">The integer scale factor, at least 1.</param>
    /// <param name="X">The left edge of the canvas in window pixels.</param>
    /// <param name="Y">The top edge of the canvas in window pixels.</param>
    /// <param name="Width">The drawn width in window pixels.</param>
    /// <param name="Height">The drawn height in window pixels.</param>
    public readonly record struct PixelFit(int Scale, int X, int Y, int Width, int Height);
}

/// <summary>
/// The cell the canvas's top-left corner sits on: the camera that follows the player.
/// </summary>
/// <remarks>
/// Kept as two integers rather than a pixel offset so the world can only ever be drawn on the tile
/// grid — the camera snaps one cell at a time, like the figure it follows. When a floor is smaller
/// than the viewport the camera centres the floor instead of clamping to zero, so a small maze does
/// not hug the top-left corner of the screen.
/// </remarks>
public readonly record struct PixelCamera(int Column, int Row)
{
    /// <summary>Works out the camera that centres <paramref name="focus"/> in the viewport.</summary>
    /// <param name="mazeRows">The floor's height in cells.</param>
    /// <param name="mazeColumns">The floor's width in cells.</param>
    /// <param name="viewRows">The viewport height in cells.</param>
    /// <param name="viewColumns">The viewport width in cells.</param>
    /// <param name="focus">The cell to centre, usually the player.</param>
    /// <returns>The camera's top-left cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is not positive.</exception>
    public static PixelCamera For(
        int mazeRows,
        int mazeColumns,
        int viewRows,
        int viewColumns,
        Cell focus)
    {
        if (mazeRows <= 0 || mazeColumns <= 0 || viewRows <= 0 || viewColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewRows), "A camera needs a positive maze and viewport.");
        }

        return new PixelCamera(
            Axis(mazeColumns, viewColumns, focus.Column),
            Axis(mazeRows, viewRows, focus.Row));
    }

    /// <summary>Returns whether a cell is inside the viewport.</summary>
    /// <param name="cell">The cell to test.</param>
    /// <param name="viewRows">The viewport height in cells.</param>
    /// <param name="viewColumns">The viewport width in cells.</param>
    /// <returns><see langword="true"/> when the cell is on screen.</returns>
    public bool Shows(Cell cell, int viewRows, int viewColumns) =>
        cell.Column >= Column && cell.Column < Column + viewColumns &&
        cell.Row >= Row && cell.Row < Row + viewRows;

    private static int Axis(int maze, int view, int focus)
    {
        if (maze <= view)
        {
            // A floor narrower than the viewport is centred, with darkness on both sides.
            return -(view - maze) / 2;
        }

        return Math.Clamp(focus - (view / 2), 0, maze - view);
    }
}

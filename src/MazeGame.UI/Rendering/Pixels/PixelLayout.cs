using System;
using MazeGame.Core.Game;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Geometry of the virtual canvas the pixel renderer draws into, and of the integer scale that
/// puts it on screen.
/// </summary>
/// <remarks>
/// <para>
/// The canvas is sized in <b>atlas pixels</b>: a cell is exactly one 16x16 tile, so the maze can
/// only ever be drawn on its own pixel grid, and the margins are whole tiles' worth of breathing
/// room. Nothing here scales by a fraction — <see cref="Fit"/> returns an integer scale and a
/// centred offset, and the letterbox is whatever is left.
/// </para>
/// <para>
/// The canvas is per floor size rather than one fixed 336x400, because that fixed size was chosen
/// to fit nineteen-tile floors: computing it from the floor keeps a 15x15 floor from carrying 40
/// px of dead space on every side, and a 19x19 one still fits.
/// </para>
/// </remarks>
public static class PixelLayout
{
    /// <summary>Side of one atlas tile, and therefore of one maze cell on screen.</summary>
    public const int CellSize = 16;

    /// <summary>Margin around the maze inside the canvas, in atlas pixels.</summary>
    public const int Pad = 8;

    /// <summary>Strip reserved above the maze inside the canvas, for the pixel HUD.</summary>
    public const int HeaderHeight = 18;

    /// <summary>The largest integer scale the game will ever draw at.</summary>
    public const int MaxScale = 4;

    /// <summary>Returns the canvas width for a maze <paramref name="columns"/> wide.</summary>
    /// <param name="columns">The floor's width in cells.</param>
    /// <returns>The canvas width in atlas pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="columns"/> is not positive.</exception>
    public static int CanvasWidth(int columns)
    {
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A maze needs at least one column.");
        }

        return (columns * CellSize) + (2 * Pad);
    }

    /// <summary>Returns the canvas height for a maze <paramref name="rows"/> tall.</summary>
    /// <param name="rows">The floor's height in cells.</param>
    /// <returns>The canvas height in atlas pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rows"/> is not positive.</exception>
    public static int CanvasHeight(int rows)
    {
        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "A maze needs at least one row.");
        }

        return (rows * CellSize) + (2 * Pad) + HeaderHeight;
    }

    /// <summary>Returns the canvas width for <paramref name="config"/>'s maze.</summary>
    /// <param name="config">The floor's settings.</param>
    /// <returns>The canvas width in atlas pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static int CanvasWidth(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return CanvasWidth(config.Columns);
    }

    /// <summary>Returns the canvas height for <paramref name="config"/>'s maze.</summary>
    /// <param name="config">The floor's settings.</param>
    /// <returns>The canvas height in atlas pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public static int CanvasHeight(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return CanvasHeight(config.Rows);
    }

    /// <summary>Returns the canvas x of <paramref name="column"/>'s left edge.</summary>
    /// <param name="column">The cell's column.</param>
    /// <returns>The pixel x inside the canvas.</returns>
    public static int CellX(int column) => Pad + (column * CellSize);

    /// <summary>Returns the canvas y of <paramref name="row"/>'s top edge.</summary>
    /// <param name="row">The cell's row.</param>
    /// <returns>The pixel y inside the canvas.</returns>
    public static int CellY(int row) => HeaderHeight + Pad + (row * CellSize);

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

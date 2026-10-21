using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// The tool's drawing surface: a grid of palette indices, 16x16 unless a caller asks otherwise.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is integer-only and has no anti-aliasing, which is the whole point: a pixel is
/// either a palette entry or <see cref="Transparent"/>, and every primitive lands on the grid. That
/// is what makes a tile reproducible from a seed and a theme, byte for byte.
/// </para>
/// <para>
/// The canvas stores indices, not colour, so a theme's palette is applied once at the very end
/// (<see cref="ToRgba"/>). A tile therefore never knows what colour it is, and one drawing routine
/// serves every theme.
/// </para>
/// </remarks>
public sealed class PixelCanvas
{
    /// <summary>The index that means "nothing here"; it always encodes as fully transparent.</summary>
    public const byte Transparent = 0;

    private readonly byte[] _pixels;

    /// <summary>
    /// Creates a canvas of <paramref name="width"/> by <paramref name="height"/> transparent
    /// pixels.
    /// </summary>
    /// <param name="width">The width in pixels; must be positive.</param>
    /// <param name="height">The height in pixels; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either side is not positive.
    /// </exception>
    public PixelCanvas(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A canvas needs a positive width.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A canvas needs a positive height.");
        }

        Width = width;
        Height = height;
        _pixels = new byte[width * height];
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets or sets one pixel, throwing when the coordinate is outside the canvas.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The palette index at that pixel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the coordinate is outside the canvas. A typo in a drawing routine is a bug, not
    /// something to clip away silently; the clipping <see cref="Set"/> exists for shapes.
    /// </exception>
    public byte this[int x, int y]
    {
        get
        {
            if (!Contains(x, y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    $"({x},{y}) is outside a {Width}x{Height} canvas.");
            }

            return _pixels[(y * Width) + x];
        }

        set
        {
            if (!Contains(x, y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    $"({x},{y}) is outside a {Width}x{Height} canvas.");
            }

            _pixels[(y * Width) + x] = value;
        }
    }

    /// <summary>Returns whether a pixel is inside the canvas.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when the coordinate can be written.</returns>
    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Sets a pixel, ignoring coordinates outside the canvas.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="index">The palette index to write.</param>
    public void Set(int x, int y, byte index)
    {
        if (Contains(x, y))
        {
            _pixels[(y * Width) + x] = index;
        }
    }

    /// <summary>Fills every pixel with <paramref name="index"/>.</summary>
    /// <param name="index">The palette index to fill with.</param>
    public void Fill(byte index) => Array.Fill(_pixels, index);

    /// <summary>Fills an axis-aligned rectangle, clipped to the canvas.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width in pixels; zero or less draws nothing.</param>
    /// <param name="height">The height in pixels; zero or less draws nothing.</param>
    /// <param name="index">The palette index to write.</param>
    public void Rect(int x, int y, int width, int height, byte index)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                Set(column, row, index);
            }
        }
    }

    /// <summary>Draws a one-pixel line, following Bresenham's algorithm.</summary>
    /// <param name="x0">The starting column.</param>
    /// <param name="y0">The starting row.</param>
    /// <param name="x1">The ending column.</param>
    /// <param name="y1">The ending row.</param>
    /// <param name="index">The palette index to write.</param>
    public void Line(int x0, int y0, int x1, int y1, byte index)
    {
        var deltaX = Math.Abs(x1 - x0);
        var deltaY = -Math.Abs(y1 - y0);
        var stepX = x0 < x1 ? 1 : -1;
        var stepY = y0 < y1 ? 1 : -1;
        var error = deltaX + deltaY;

        while (true)
        {
            Set(x0, y0, index);

            if (x0 == x1 && y0 == y1)
            {
                return;
            }

            var doubled = 2 * error;

            if (doubled >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }

            if (doubled <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    /// <summary>Fills an axis-aligned ellipse, clipped to the canvas.</summary>
    /// <param name="centreX">The centre column.</param>
    /// <param name="centreY">The centre row.</param>
    /// <param name="radiusX">The horizontal radius in pixels.</param>
    /// <param name="radiusY">The vertical radius in pixels.</param>
    /// <param name="index">The palette index to write.</param>
    public void Ellipse(int centreX, int centreY, int radiusX, int radiusY, byte index)
    {
        if (radiusX < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusX), radiusX, "A radius cannot be negative.");
        }

        if (radiusY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusY), radiusY, "A radius cannot be negative.");
        }

        // Integer test: a pixel is inside when (dx/rx)^2 + (dy/ry)^2 <= 1, scaled by the radii so
        // no floating point is involved and the result cannot vary by platform.
        for (var row = centreY - radiusY; row <= centreY + radiusY; row++)
        {
            for (var column = centreX - radiusX; column <= centreX + radiusX; column++)
            {
                var deltaX = column - centreX;
                var deltaY = row - centreY;
                var normalised = (deltaX * deltaX * radiusY * radiusY) + (deltaY * deltaY * radiusX * radiusX);
                var limit = radiusX * radiusX * radiusY * radiusY;

                if (normalised <= limit)
                {
                    Set(column, row, index);
                }
            }
        }
    }

    /// <summary>Scatters single pixels over the canvas, using <paramref name="random"/>.</summary>
    /// <param name="index">The palette index to write.</param>
    /// <param name="rate">How many pixels, as a fraction of the canvas; between 0 and 1.</param>
    /// <param name="random">The seeded source, so the speckle is reproducible.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rate"/> is not in 0..1.</exception>
    public void Speckle(byte index, double rate, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!(rate >= 0) || rate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A speckle rate is a fraction between 0 and 1.");
        }

        var count = (int)Math.Round(Width * Height * rate);

        for (var speck = 0; speck < count; speck++)
        {
            Set(random.NextInt(Width), random.NextInt(Height), index);
        }
    }

    /// <summary>Draws a checkerboard over a rectangle, clipped to the canvas.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="index">The palette index to write.</param>
    /// <param name="parity">The checker parity to keep: 0 or 1.</param>
    public void Dither(int x, int y, int width, int height, byte index, int parity)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                if (((row + column) & 1) == parity)
                {
                    Set(column, row, index);
                }
            }
        }
    }

    /// <summary>Draws horizontal stripes over a rectangle, clipped to the canvas.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="index">The palette index to write.</param>
    /// <param name="step">The gap between stripes; at least 2.</param>
    public void Shade(int x, int y, int width, int height, byte index, int step)
    {
        if (step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "A shade step must be at least 1.");
        }

        for (var row = y; row < y + height; row++)
        {
            if (((row - y) % (step + 1)) == 0)
            {
                Rect(x, row, width, 1, index);
            }
        }
    }

    /// <summary>
    /// Adds a one-pixel outline just inside every edge of the drawn silhouette.
    /// </summary>
    /// <param name="index">The palette index to write.</param>
    /// <remarks>
    /// "Drawn" means any pixel that is not <see cref="Transparent"/>. Reading the whole canvas
    /// first is what keeps the outline from spreading into itself as it is written.
    /// </remarks>
    public void Outline(byte index)
    {
        var source = (byte[])_pixels.Clone();

        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                if (source[(row * Width) + column] == Transparent)
                {
                    continue;
                }

                var onEdge =
                    column == 0 ||
                    row == 0 ||
                    column == Width - 1 ||
                    row == Height - 1 ||
                    source[(row * Width) + column - 1] == Transparent ||
                    source[(row * Width) + column + 1] == Transparent ||
                    source[((row - 1) * Width) + column] == Transparent ||
                    source[((row + 1) * Width) + column] == Transparent;

                if (onEdge)
                {
                    Set(column, row, index);
                }
            }
        }
    }

    /// <summary>Mirrors the canvas left to right, in place.</summary>
    public void Mirror()
    {
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width / 2; column++)
            {
                var left = (row * Width) + column;
                var right = (row * Width) + Width - 1 - column;
                (_pixels[left], _pixels[right]) = (_pixels[right], _pixels[left]);
            }
        }
    }

    /// <summary>Copies another canvas onto this one at an offset.</summary>
    /// <param name="source">The canvas to copy from.</param>
    /// <param name="x">The column to place its left edge at.</param>
    /// <param name="y">The row to place its top edge at.</param>
    /// <param name="skipTransparent">
    /// When true, transparent pixels of the source leave this canvas alone, so a sprite can be
    /// stamped onto a filled tile.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public void Stamp(PixelCanvas source, int x, int y, bool skipTransparent)
    {
        ArgumentNullException.ThrowIfNull(source);

        for (var row = 0; row < source.Height; row++)
        {
            for (var column = 0; column < source.Width; column++)
            {
                var pixel = source[column, row];

                if (skipTransparent && pixel == Transparent)
                {
                    continue;
                }

                Set(x + column, y + row, pixel);
            }
        }
    }

    /// <summary>Counts how many pixels are not <see cref="Transparent"/>.</summary>
    /// <returns>The number of drawn pixels.</returns>
    public int DrawnPixels()
    {
        var count = 0;

        foreach (var pixel in _pixels)
        {
            if (pixel != Transparent)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Expands the indices into 8-bit RGBA bytes, row by row.</summary>
    /// <param name="palette">The palette to resolve indices against.</param>
    /// <returns>A byte array of <c>Width * Height * 4</c> bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="palette"/> is null.</exception>
    public byte[] ToRgba(Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        var bytes = new byte[Width * Height * 4];

        for (var index = 0; index < _pixels.Length; index++)
        {
            var colour = palette[_pixels[index]];
            var offset = index * 4;
            bytes[offset] = colour.Red;
            bytes[offset + 1] = colour.Green;
            bytes[offset + 2] = colour.Blue;
            bytes[offset + 3] = _pixels[index] == Transparent ? (byte)0 : (byte)255;
        }

        return bytes;
    }
}

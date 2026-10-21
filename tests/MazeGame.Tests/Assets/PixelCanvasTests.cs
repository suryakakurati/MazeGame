using System;
using MazeGame.AssetForge;
using MazeGame.Core.Abstractions;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the drawing surface the sprite compiler is built on: clipping, the primitives' exact
/// pixels, and the promise that a seeded canvas is reproducible.
/// </summary>
public sealed class PixelCanvasTests
{
    private const byte Ink = 3;

    [Fact]
    public void ANewCanvas_IsFullyTransparent()
    {
        var canvas = new PixelCanvas(16, 16);

        Assert.Equal(16, canvas.Width);
        Assert.Equal(16, canvas.Height);
        Assert.Equal(0, canvas.DrawnPixels());
        Assert.Equal(PixelCanvas.Transparent, canvas[0, 0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ACanvas_WithNoArea_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelCanvas(size, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelCanvas(16, size));
    }

    [Fact]
    public void TheIndexer_RejectsCoordinatesOutsideTheCanvas()
    {
        var canvas = new PixelCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas[4, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas[0, -1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas[-1, 0] = Ink);
    }

    [Fact]
    public void Set_ClipsInsteadOfThrowing()
    {
        var canvas = new PixelCanvas(4, 4);

        canvas.Set(-3, -3, Ink);
        canvas.Set(10, 10, Ink);

        // Shapes routinely overhang a tile, so Set is the clipping entry point; the indexer is the
        // one that complains.
        Assert.Equal(0, canvas.DrawnPixels());
    }

    [Fact]
    public void Rect_ClipsAtEveryEdge()
    {
        var canvas = new PixelCanvas(4, 4);

        canvas.Rect(-1, -1, 3, 3, Ink);

        Assert.Equal(Ink, canvas[0, 0]);
        Assert.Equal(Ink, canvas[1, 1]);
        Assert.Equal(PixelCanvas.Transparent, canvas[2, 2]);
        Assert.Equal(4, canvas.DrawnPixels());
    }

    [Fact]
    public void Line_HitsBothEndsAndStaysInside()
    {
        var canvas = new PixelCanvas(8, 8);

        canvas.Line(0, 0, 7, 7, Ink);

        Assert.Equal(Ink, canvas[0, 0]);
        Assert.Equal(Ink, canvas[7, 7]);
        Assert.Equal(8, canvas.DrawnPixels());
    }

    [Fact]
    public void Ellipse_IsFilledAndClipped()
    {
        var canvas = new PixelCanvas(8, 8);

        canvas.Ellipse(0, 0, 2, 2, Ink);

        Assert.Equal(Ink, canvas[0, 0]);
        Assert.Equal(Ink, canvas[2, 0]);
        Assert.Equal(Ink, canvas[0, 2]);
        Assert.Equal(PixelCanvas.Transparent, canvas[3, 3]);
    }

    [Fact]
    public void Mirror_SwapsTheCanvasLeftToRight()
    {
        var canvas = new PixelCanvas(4, 1);
        canvas[0, 0] = Ink;

        canvas.Mirror();

        Assert.Equal(Ink, canvas[3, 0]);
        Assert.Equal(PixelCanvas.Transparent, canvas[0, 0]);
    }

    [Fact]
    public void Outline_TracesTheSilhouetteAndLeavesTheInside()
    {
        var canvas = new PixelCanvas(5, 5);
        canvas.Rect(1, 1, 3, 3, Ink);

        canvas.Outline(7);

        Assert.Equal(7, canvas[1, 1]);
        Assert.Equal(7, canvas[2, 1]);
        Assert.Equal(7, canvas[3, 3]);
        Assert.Equal(Ink, canvas[2, 2]);
    }

    [Fact]
    public void Dither_KeepsOneParity()
    {
        var canvas = new PixelCanvas(4, 4);

        canvas.Dither(0, 0, 4, 4, Ink, 0);

        Assert.Equal(Ink, canvas[0, 0]);
        Assert.Equal(PixelCanvas.Transparent, canvas[1, 0]);
        Assert.Equal(8, canvas.DrawnPixels());
    }

    [Fact]
    public void Speckle_IsReproducibleFromItsSeed()
    {
        var first = new PixelCanvas(16, 16);
        var second = new PixelCanvas(16, 16);

        first.Speckle(Ink, 0.25, new SeededRandom(99));
        second.Speckle(Ink, 0.25, new SeededRandom(99));

        // 64 specks land on random pixels, so a few land on each other: the count is a ceiling,
        // not an exact number, but the two canvases must agree pixel for pixel.
        Assert.InRange(first.DrawnPixels(), 40, 64);
        Assert.Equal(Rgba(first), Rgba(second));
    }

    [Fact]
    public void Speckle_RejectsAnImpossibleRate()
    {
        var canvas = new PixelCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Speckle(Ink, 1.5, new SeededRandom(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.Speckle(Ink, -0.1, new SeededRandom(1)));
        Assert.Throws<ArgumentNullException>(() => canvas.Speckle(Ink, 0.5, null!));
    }

    [Fact]
    public void Stamp_CanLeaveTheBackgroundAlone()
    {
        var background = new PixelCanvas(8, 8);
        background.Fill(Ink);

        var sprite = new PixelCanvas(2, 2);
        sprite[0, 0] = 5;

        background.Stamp(sprite, 3, 3, skipTransparent: true);

        Assert.Equal(5, background[3, 3]);
        Assert.Equal(Ink, background[4, 3]);
    }

    [Fact]
    public void ToRgba_MakesTransparentPixelsFullySeeThrough()
    {
        var canvas = new PixelCanvas(2, 1);
        canvas[1, 0] = Ink;

        var palette = Palette.Derive(ThemeCatalog.ById("shallows")!);
        var bytes = canvas.ToRgba(palette);

        Assert.Equal(8, bytes.Length);
        Assert.Equal(0, bytes[3]);
        Assert.Equal(255, bytes[7]);
        Assert.Throws<ArgumentNullException>(() => canvas.ToRgba(null!));
    }

    private static byte[] Rgba(PixelCanvas canvas) =>
        canvas.ToRgba(Palette.Derive(ThemeCatalog.ById("shallows")!));
}

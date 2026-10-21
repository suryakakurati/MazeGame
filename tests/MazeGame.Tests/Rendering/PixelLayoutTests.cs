using System;
using MazeGame.Core.Game;
using MazeGame.UI.Rendering.Pixels;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies the canvas geometry and the integer-scale rule: art is never resampled by a fraction,
/// the canvas always fits between the HUD strips, and a floor of any planned size has a canvas.
/// </summary>
public sealed class PixelLayoutTests
{
    [Theory]
    [InlineData(13, 13)]
    [InlineData(15, 15)]
    [InlineData(19, 19)]
    public void Canvas_HoldsTheWholeMazePlusMargins(int rows, int columns)
    {
        var width = PixelLayout.CanvasWidth(columns);
        var height = PixelLayout.CanvasHeight(rows);

        Assert.True(width >= (columns * PixelLayout.CellSize) + (2 * PixelLayout.Pad));
        Assert.True(height >= (rows * PixelLayout.CellSize) + (2 * PixelLayout.Pad) + PixelLayout.HeaderHeight);

        // The design note's cap: 21 tiles by 21 tiles plus HUD, which is 336 px at 16 px cells.
        // A nineteen-tile floor plus two 8 px margins is 320, so the header may take the rest and
        // no more — one tile is the whole budget for it.
        Assert.True(width <= (21 * PixelLayout.CellSize) + 8, $"A {columns}-column floor needs {width} px.");
        Assert.True(height <= (21 * PixelLayout.CellSize) + 8, $"A {rows}-row floor needs {height} px.");
    }

    [Fact]
    public void Canvas_ComesFromTheConfig()
    {
        var config = new GameConfig { Rows = 15, Columns = 15, KeyCount = 3, BarrierBudget = 3, EnemyStepSeconds = 1, Seed = 1 };

        Assert.Equal(PixelLayout.CanvasWidth(15), PixelLayout.CanvasWidth(config));
        Assert.Equal(PixelLayout.CanvasHeight(15), PixelLayout.CanvasHeight(config));
    }

    [Fact]
    public void Cells_SitOnTheTileGridInsideTheMargins()
    {
        Assert.Equal(PixelLayout.Pad, PixelLayout.CellX(0));
        Assert.Equal(PixelLayout.Pad + PixelLayout.CellSize, PixelLayout.CellX(1));
        Assert.Equal(PixelLayout.HeaderHeight + PixelLayout.Pad, PixelLayout.CellY(0));
    }

    [Theory]
    [InlineData(656, 760)]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    [InlineData(1920, 1080)]
    [InlineData(300, 300)]
    public void Fit_ScalesByAWholeNumberThatFits(int windowWidth, int windowHeight)
    {
        const int Top = 60;
        const int Bottom = 44;
        const int Margin = 12;

        var fit = PixelLayout.Fit(
            PixelLayout.CanvasWidth(15),
            PixelLayout.CanvasHeight(15),
            windowWidth,
            windowHeight,
            Top,
            Bottom,
            Margin);

        Assert.InRange(fit.Scale, 1, PixelLayout.MaxScale);
        Assert.Equal(PixelLayout.CanvasWidth(15) * fit.Scale, fit.Width);
        Assert.Equal(PixelLayout.CanvasHeight(15) * fit.Scale, fit.Height);

        // The drawing must stay inside the window and out of both HUD strips whenever it can.
        if (fit.Width <= windowWidth)
        {
            Assert.InRange(fit.X, 0, windowWidth - fit.Width);
        }

        if (fit.Height <= windowHeight - Top - Bottom)
        {
            Assert.InRange(fit.Y, Top, windowHeight - Bottom - fit.Height);
        }
    }

    [Fact]
    public void Fit_CentresTheCanvasInWhatTheHudLeaves()
    {
        var fit = PixelLayout.Fit(256, 282, 656, 760, topInset: 60, bottomInset: 44, margin: 12);

        var expectedX = (656 - fit.Width) / 2;
        var expectedY = 60 + 12 + ((760 - 60 - 44 - 24 - fit.Height) / 2);

        Assert.Equal(expectedX, fit.X);
        Assert.Equal(expectedY, fit.Y);
    }

    [Fact]
    public void Fit_GrowsToFitAWiderWindow()
    {
        var small = PixelLayout.Fit(256, 282, 600, 700);
        var large = PixelLayout.Fit(256, 282, 1200, 1400);

        Assert.True(large.Scale > small.Scale);
    }

    [Fact]
    public void Fit_NeverShrinksBelowOneToOne()
    {
        var fit = PixelLayout.Fit(256, 282, 100, 100);

        Assert.Equal(1, fit.Scale);
        Assert.Equal(256, fit.Width);
        Assert.Equal(282, fit.Height);
    }

    [Fact]
    public void Fit_RejectsANegativeInset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.Fit(256, 282, 656, 760, topInset: -1));
    }

    [Fact]
    public void Canvas_RejectsAnEmptyMaze()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.CanvasWidth(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.CanvasHeight(0));
    }
}

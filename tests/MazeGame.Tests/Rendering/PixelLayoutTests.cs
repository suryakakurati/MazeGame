using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.UI.Rendering.Pixels;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies the viewport geometry: the canvas is sized from the lantern, the camera keeps the
/// player in the middle and inside the floor, and the screen scale is always a whole number.
/// </summary>
/// <remarks>
/// The camera is the one part of the renderer that can be tested without a window: it is two
/// integers derived from the floor and the player, so "the player is centred" and "the viewport
/// never leaves the maze" are arithmetic rather than pixels.
/// </remarks>
public sealed class PixelLayoutTests
{
    /// <summary>A maze wide enough for every camera case; cells are taken from it by position.</summary>
    private static readonly Grid Maze = new(15, 15);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(9)]
    public void ViewCells_IsOddAndInsideTheDesignLimits(int visionRadius)
    {
        var cells = PixelLayout.ViewCells(visionRadius);

        Assert.Equal(1, cells % 2);
        Assert.InRange(cells, PixelLayout.MinViewCells, PixelLayout.MaxViewCells);
    }

    [Fact]
    public void ViewCells_GrowsWithVisionThenStops()
    {
        var narrow = PixelLayout.ViewCells(3);
        var wide = PixelLayout.ViewCells(5);

        Assert.True(wide > narrow, "More vision must show more cells.");

        // A generous card must not turn the maze into a map: the cap holds.
        Assert.Equal(PixelLayout.MaxViewCells, PixelLayout.ViewCells(20));
    }

    [Fact]
    public void ViewCells_HoldsTheLitCirclePlusAtLeastOneRing()
    {
        for (var radius = 0; radius <= 5; radius++)
        {
            var cells = PixelLayout.ViewCells(radius);

            Assert.True(
                cells >= (2 * radius) + 1,
                $"A radius of {radius} is lit across {(2 * radius) + 1} cells but the viewport is {cells}.");
        }
    }

    private static Cell At(int row, int column) => Maze.GetCell(row, column);

    [Fact]
    public void ViewCells_ComesFromTheConfig()
    {
        var config = new GameConfig { Rows = 15, Columns = 15, KeyCount = 3, EnemyStepSeconds = 1, Seed = 1 };

        Assert.Equal(PixelLayout.ViewCells(config.VisionRadius), PixelLayout.ViewCells(config));
    }

    [Fact]
    public void Canvas_HoldsTheViewportPlusMarginsAndHeader()
    {
        var width = PixelLayout.CanvasWidth(9);
        var height = PixelLayout.CanvasHeight(9);

        Assert.Equal((9 * PixelLayout.CellSize) + (2 * PixelLayout.Pad), width);
        Assert.Equal((9 * PixelLayout.CellSize) + (2 * PixelLayout.Pad) + PixelLayout.HeaderHeight, height);
    }

    [Fact]
    public void Cells_SitOnTheTileGridRelativeToTheCamera()
    {
        Assert.Equal(PixelLayout.Pad, PixelLayout.CellX(0, 0));
        Assert.Equal(PixelLayout.Pad + PixelLayout.CellSize, PixelLayout.CellX(1, 0));
        Assert.Equal(PixelLayout.HeaderHeight + PixelLayout.Pad, PixelLayout.CellY(0, 0));

        // With the camera four cells in, cell four is where cell zero used to be.
        Assert.Equal(PixelLayout.Pad, PixelLayout.CellX(4, 4));
        Assert.Equal(PixelLayout.Pad - (4 * PixelLayout.CellSize), PixelLayout.CellX(0, 4));
    }

    [Fact]
    public void Camera_CentresThePlayerOnAFloorLargerThanTheViewport()
    {
        var camera = PixelCamera.For(15, 15, 9, 9, At(7, 7));

        Assert.Equal(3, camera.Column);
        Assert.Equal(3, camera.Row);

        // The player's cell is the middle cell of a 9-wide viewport.
        Assert.True(camera.Shows(At(7, 7), 9, 9));
        Assert.Equal(PixelLayout.Pad + (4 * PixelLayout.CellSize), PixelLayout.CellX(7, camera.Column));
    }

    [Fact]
    public void Camera_ClampsToTheFloorAtTheEdges()
    {
        var corner = PixelCamera.For(15, 15, 9, 9, At(0, 0));
        var farCorner = PixelCamera.For(15, 15, 9, 9, At(14, 14));

        Assert.Equal(0, corner.Column);
        Assert.Equal(0, corner.Row);
        Assert.Equal(6, farCorner.Column);
        Assert.Equal(6, farCorner.Row);

        // The far edge of the viewport is the floor's far edge, not past it.
        Assert.True(farCorner.Shows(At(14, 14), 9, 9));
        Assert.False(farCorner.Shows(At(14, 4), 9, 9));
    }

    [Fact]
    public void Camera_CentresAFloorNarrowerThanTheViewport()
    {
        var camera = PixelCamera.For(5, 5, 9, 9, At(2, 2));

        Assert.Equal(-2, camera.Column);
        Assert.Equal(-2, camera.Row);
    }

    [Fact]
    public void Camera_KeepsThePlayerOnScreenOnATinyFloor()
    {
        var player = At(1, 1);
        var camera = PixelCamera.For(3, 3, 9, 9, player);

        Assert.True(camera.Shows(player, 9, 9));
    }

    [Theory]
    [InlineData(640, 700)]
    [InlineData(696, 872)]
    [InlineData(1280, 900)]
    [InlineData(1920, 1080)]
    [InlineData(400, 400)]
    public void Fit_ScalesByAWholeNumberThatFits(int windowWidth, int windowHeight)
    {
        const int Top = 60;
        const int Bottom = 44;
        const int Margin = 12;
        var canvasWidth = PixelLayout.CanvasWidth(PixelLayout.ViewCells(3));
        var canvasHeight = PixelLayout.CanvasHeight(PixelLayout.ViewCells(3));

        var fit = PixelLayout.Fit(canvasWidth, canvasHeight, windowWidth, windowHeight, Top, Bottom, Margin);

        Assert.InRange(fit.Scale, 1, PixelLayout.MaxScale);
        Assert.Equal(canvasWidth * fit.Scale, fit.Width);
        Assert.Equal(canvasHeight * fit.Scale, fit.Height);

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
        var fit = PixelLayout.Fit(160, 178, 696, 872, topInset: 60, bottomInset: 44, margin: 12);

        var expectedX = (696 - fit.Width) / 2;
        var expectedY = 60 + 12 + ((872 - 60 - 44 - 24 - fit.Height) / 2);

        Assert.Equal(expectedX, fit.X);
        Assert.Equal(expectedY, fit.Y);
    }

    [Fact]
    public void Fit_GrowsToFitAWiderWindow()
    {
        var small = PixelLayout.Fit(160, 178, 600, 700);
        var large = PixelLayout.Fit(160, 178, 1400, 1500);

        Assert.True(large.Scale > small.Scale);
    }

    [Fact]
    public void Fit_GivesTheShippedWindowAReadableScale()
    {
        var config = new GameConfig { Rows = 15, Columns = 15, KeyCount = 3, EnemyStepSeconds = 1, Seed = 1 };
        var cells = PixelLayout.ViewCells(config.VisionRadius);
        var fit = PixelLayout.Fit(
            PixelLayout.CanvasWidth(cells),
            PixelLayout.CanvasHeight(cells),
            720,
            900,
            topInset: 60,
            bottomInset: 44,
            margin: 12);

        // One maze cell is 16 atlas px, so a scale of 4 draws it 64 px wide: the camera exists to
        // make the tiles bigger, and a window that only manages 2x is not doing its job.
        Assert.True(fit.Scale >= 4, $"The default window only managed {fit.Scale}x.");
    }

    [Fact]
    public void Fit_NeverShrinksBelowOneToOne()
    {
        var fit = PixelLayout.Fit(160, 178, 100, 100);

        Assert.Equal(1, fit.Scale);
        Assert.Equal(160, fit.Width);
        Assert.Equal(178, fit.Height);
    }

    [Fact]
    public void Fit_RejectsANegativeInset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.Fit(160, 178, 696, 872, topInset: -1));
    }

    [Fact]
    public void Canvas_RejectsAnEmptyViewport()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.CanvasWidth(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.CanvasHeight(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelLayout.ViewCells(-1));
    }
}

using System;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Measures whether a wall can be told apart from the floor it is painted over.
/// </summary>
/// <remarks>
/// <para>
/// A wall here is a five-pixel band on top of a floor tile, so it has almost no area to make its
/// case: it reads as a wall only because it is brighter than the floor, has a lit rim along one
/// edge and a near-black foot along the other. When the first shipped palette only stepped the wall
/// body a shade or two above the floor, the maze looked like one flat surface with faint lines on
/// it — the exact complaint this test now stands in front of.
/// </para>
/// <para>
/// The check is on relative luminance rather than on a colour distance, because what a player sees
/// is brightness, and the ratios are the contract: the same numbers hold for every theme, which is
/// why a new palette cannot quietly flatten the walls.
/// </para>
/// </remarks>
public sealed class WallContrastTests
{
    /// <summary>How much brighter than the floor a wall's body must be.</summary>
    /// <remarks>
    /// Three times, not one and a half. At the old ratio the palette was technically separated but
    /// the band still read as a lit line on the path: the floor and the wall sat close enough that
    /// a dropped frame or a dim remembered cell erased the difference, and the maze turned into
    /// guesswork. Walls are the thing the player navigates by, so they get the loudest value in the
    /// palette after the lantern itself.
    /// </remarks>
    private const double MinimumBodyRatio = 3.0;

    /// <summary>How much brighter the lit rim must be than the floor.</summary>
    private const double MinimumRimRatio = 5.0;

    /// <summary>How much darker than the floor the contact shadow must be.</summary>
    private const double MaximumFootRatio = 0.4;

    /// <summary>How much brighter the drawn body rows must be than the floor tile.</summary>
    private const double MinimumDrawnBodyRatio = 2.2;

    [Fact]
    public void EveryShippedFloor_HasAWallThatStandsOffItsFloor()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var palette = Palette.Derive(theme);
            var floor = Luminance(palette[PaletteRole.Floor]);
            var body = Luminance(palette[PaletteRole.Wall]);
            var rim = Luminance(palette[PaletteRole.WallTop]);
            var foot = Luminance(palette[PaletteRole.WallEdge]);

            Assert.True(
                body / floor >= MinimumBodyRatio,
                $"{theme.Id}: the wall body is only {body / floor:0.00}x the floor (need {MinimumBodyRatio}).");

            Assert.True(
                rim / floor >= MinimumRimRatio,
                $"{theme.Id}: the lit rim is only {rim / floor:0.00}x the floor (need {MinimumRimRatio}).");

            Assert.True(
                foot / floor <= MaximumFootRatio,
                $"{theme.Id}: the contact shadow is {foot / floor:0.00}x the floor (need {MaximumFootRatio} or darker).");
        }
    }

    [Fact]
    public void EveryShippedFloor_PaintsABandThatIsBrighterThanItsFloor()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var palette = Palette.Derive(theme);
            var band = WallStrips.Face(theme, WallSide.North);
            var floor = MeanLuminance(TileGrammar.Floor(theme, 0), palette, 0, 16);

            // The body rows only: the outline and the contact shadow are meant to be dark, and a
            // mean over the whole band would let a family bury its rock under texture and still
            // pass, which is how the band became a line in the first place.
            var wall = MeanLuminance(band, palette, 1, WallStrips.Thickness - 2);

            Assert.True(
                wall / floor >= MinimumDrawnBodyRatio,
                $"{theme.Id}: the drawn band is {wall / floor:0.00}x its floor (need {MinimumDrawnBodyRatio}).");
        }
    }

    [Fact]
    public void AWallIsThickEnoughToSee_AndLeavesACorridorFloor()
    {
        // A one-cell corridor has a band on each side. The bands must be thick enough to read as
        // rock and thin enough to leave a path between them, or the maze becomes a solid block of
        // wall with a hint of floor.
        var striped = WallStrips.Thickness * 2;
        var floor = CaveTiles.TileSize - striped;

        Assert.True(WallStrips.Thickness >= 6, $"A {WallStrips.Thickness} px band reads as a line, not a wall.");
        Assert.True(floor >= 4, $"A corridor between two bands has only {floor} px of floor left.");
    }

    [Fact]
    public void EveryShippedFloor_LeavesAShadowLineAlongTheBand()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var palette = Palette.Derive(theme);
            var band = WallStrips.Face(theme, WallSide.North);
            var floor = MeanLuminance(TileGrammar.Floor(theme, 0), palette, 0, 16);

            // The innermost row of the band is the contact shadow. Its job is to draw the edge of
            // the wall, so it must be clearly darker than the floor rather than about the same.
            var foot = MeanLuminance(band, palette, WallStrips.Thickness - 1, WallStrips.Thickness);

            Assert.True(
                foot / floor <= MaximumFootRatio,
                $"{theme.Id}: the shadow line is {foot / floor:0.00}x its floor (need {MaximumFootRatio} or darker).");
        }
    }

    /// <summary>Returns the mean luminance of rows <c>[from, to)</c> of a canvas.</summary>
    private static double MeanLuminance(PixelCanvas canvas, Palette palette, int from, int to)
    {
        var total = 0.0;
        var count = 0;

        for (var row = from; row < to; row++)
        {
            for (var column = 0; column < canvas.Width; column++)
            {
                var index = canvas[column, row];

                if (index == PixelCanvas.Transparent)
                {
                    continue;
                }

                total += Luminance(palette[index]);
                count++;
            }
        }

        return count == 0 ? 0 : total / count;
    }

    /// <summary>Returns a colour's relative luminance, 0 (black) to 1 (white).</summary>
    private static double Luminance(Rgb colour)
    {
        static double Channel(byte value)
        {
            var scaled = value / 255.0;
            return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.Red)) + (0.7152 * Channel(colour.Green)) + (0.0722 * Channel(colour.Blue));
    }
}

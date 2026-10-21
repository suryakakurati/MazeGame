using System;
using System.Collections.Generic;
using MazeGame.AssetForge;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies that the shipped floors are different <b>places</b>, not the same tiles in different
/// colours.
/// </summary>
/// <remarks>
/// The comparison is on palette <i>indices</i>, which are theme-blind: two themes that draw the same
/// shape produce the same index grid whatever their hues are. That is what makes "it is a recolour"
/// a red test rather than a matter of taste. The distinctness check in <c>--check</c> only counts
/// parameters; this compares the pixels those parameters produce.
/// </remarks>
public sealed class ThemeFamilyTests
{
    [Fact]
    public void TheShippedFloors_UseEveryShapeLanguage()
    {
        var families = new HashSet<TileFamily>();

        foreach (var theme in ThemeCatalog.Shipped)
        {
            families.Add(theme.Family);
        }

        // Twenty places from three grammars: the families are reused, the *rooms* are not.
        Assert.Equal(Enum.GetValues<TileFamily>().Length, families.Count);
    }

    [Fact]
    public void TheThemes_LineUpWithTheFloors()
    {
        // The compiler's table and the campaign's table are separate files on purpose (the game
        // must not reference the tool), so the order is the contract that keeps them honest.
        Assert.Equal(FloorCatalog.Shipped.Count, ThemeCatalog.Shipped.Count);

        for (var index = 0; index < FloorCatalog.Shipped.Count; index++)
        {
            Assert.Equal(FloorCatalog.Shipped[index].ThemeId, ThemeCatalog.Shipped[index].Id);
        }
    }

    [Fact]
    public void TheShippedFloors_DifferInShapeNotJustColour()
    {
        var themes = ThemeCatalog.Shipped;

        for (var left = 0; left < themes.Count; left++)
        {
            for (var right = left + 1; right < themes.Count; right++)
            {
                Assert.False(
                    SamePixels(TileGrammar.Floor(themes[left], 0), TileGrammar.Floor(themes[right], 0)),
                    $"{themes[left].Id} and {themes[right].Id} draw the same floor; that is a recolour.");

                Assert.False(
                    SamePixels(TileGrammar.Wall(themes[left]), TileGrammar.Wall(themes[right])),
                    $"{themes[left].Id} and {themes[right].Id} draw the same wall; that is a recolour.");

                Assert.False(
                    SamePixels(TileGrammar.WallFace(themes[left], WallSide.North), TileGrammar.WallFace(themes[right], WallSide.North)),
                    $"{themes[left].Id} and {themes[right].Id} draw the same wall face; that is a recolour.");
            }
        }
    }

    [Fact]
    public void EveryShippedFloor_CompilesTwoDistinctProps()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var first = TileGrammar.Prop(theme, 0);
            var second = TileGrammar.Prop(theme, 1);

            Assert.True(first.DrawnPixels() > 0, $"{theme.Id} prop 0 is empty.");
            Assert.True(second.DrawnPixels() > 0, $"{theme.Id} prop 1 is empty.");
            Assert.False(SamePixels(first, second), $"{theme.Id} compiles the same prop twice.");
        }
    }

    [Fact]
    public void EveryShippedFloor_CompilesThePropsIntoItsAtlas()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tile in SpriteCompiler.Compile(theme).Tiles)
            {
                names.Add(tile.Name);
            }

            for (var prop = 0; prop < TileGrammar.PropCount; prop++)
            {
                Assert.Contains($"prop-{prop}", names);
            }
        }
    }

    [Fact]
    public void AdjacentFloors_DifferInAtLeastTwoStructuralParameters()
    {
        var themes = ThemeCatalog.Shipped;

        for (var index = 1; index < themes.Count; index++)
        {
            var differences = themes[index - 1].StructuralDifferences(themes[index]);

            Assert.True(
                differences >= SpriteCompiler.MinimumStructuralDifferences,
                $"{themes[index - 1].Id} and {themes[index].Id} differ in {differences} structural parameters.");
        }
    }

    [Fact]
    public void Prop_RejectsAnIndexOutsideTheFamilyRange()
    {
        var theme = ThemeCatalog.Shipped[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => TileGrammar.Prop(theme, TileGrammar.PropCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileGrammar.Prop(theme, -1));
    }

    /// <summary>Returns whether two canvases hold the same palette indices, pixel for pixel.</summary>
    private static bool SamePixels(PixelCanvas left, PixelCanvas right)
    {
        if (left.Width != right.Width || left.Height != right.Height)
        {
            return false;
        }

        for (var row = 0; row < left.Height; row++)
        {
            for (var column = 0; column < left.Width; column++)
            {
                if (left[column, row] != right[column, row])
                {
                    return false;
                }
            }
        }

        return true;
    }
}

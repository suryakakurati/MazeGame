using System;
using System.Collections.Generic;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies that each shipped floor has a signature object, an emblem and a wall face of its own,
/// not only a different palette.
/// </summary>
/// <remarks>
/// The existing <see cref="ThemeFamilyTests"/> compares floor and wall tiles. A floor is more than
/// those two tiles: the thing a player remembers is the object in the room and the mark on the
/// wall, so those are compared here across all twenty floors.
/// </remarks>
public sealed class FloorIdentityTests
{
    [Fact]
    public void EveryShippedFloor_DrawsANonEmptySignatureFeature()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var feature = TileGrammar.Feature(theme);

            Assert.True(feature.DrawnPixels() > 0, $"{theme.Id} has an empty signature feature.");
        }
    }

    [Fact]
    public void EveryShippedFloor_CompilesItsFeatureAndEmblemIntoItsAtlas()
    {
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tile in SpriteCompiler.Compile(theme).Tiles)
            {
                names.Add(tile.Name);
            }

            Assert.Contains("floor-feature", names);
            Assert.Contains($"prop-{TileGrammar.PropCount - 1}", names);
        }
    }

    [Fact]
    public void TheTwentyFloors_DrawTwentyDifferentFeatures()
    {
        var themes = ThemeCatalog.Shipped;

        for (var left = 0; left < themes.Count; left++)
        {
            for (var right = left + 1; right < themes.Count; right++)
            {
                Assert.False(
                    SamePixels(TileGrammar.Feature(themes[left]), TileGrammar.Feature(themes[right])),
                    $"{themes[left].Id} and {themes[right].Id} draw the same signature feature.");
            }
        }
    }

    [Fact]
    public void TheTwentyFloors_DrawTwentyDifferentWallFaces()
    {
        var themes = ThemeCatalog.Shipped;

        for (var left = 0; left < themes.Count; left++)
        {
            for (var right = left + 1; right < themes.Count; right++)
            {
                Assert.False(
                    SamePixels(
                        TileGrammar.WallFace(themes[left], WallSide.North),
                        TileGrammar.WallFace(themes[right], WallSide.North)),
                    $"{themes[left].Id} and {themes[right].Id} draw the same wall face.");
            }
        }
    }

    [Fact]
    public void TheTwentyFloors_DrawTwentyDifferentEmblems()
    {
        var themes = ThemeCatalog.Shipped;

        for (var left = 0; left < themes.Count; left++)
        {
            for (var right = left + 1; right < themes.Count; right++)
            {
                Assert.False(
                    SamePixels(TileGrammar.Prop(themes[left], 2), TileGrammar.Prop(themes[right], 2)),
                    $"{themes[left].Id} and {themes[right].Id} share an emblem.");
            }
        }
    }

    [Fact]
    public void TheShippedThemes_UseEveryMotifAndFeatureKind()
    {
        var floors = new HashSet<FloorMotif>();
        var walls = new HashSet<WallMotif>();
        var features = new HashSet<FeatureKind>();
        var ambience = new HashSet<AmbientKind>();

        foreach (var theme in ThemeCatalog.Shipped)
        {
            floors.Add(theme.Floor);
            walls.Add(theme.Wall);
            features.Add(theme.Feature);
            ambience.Add(theme.Ambient);
        }

        // Twenty floors cannot have twenty five different motifs, but they must not collapse to
        // three recolours: the shipped set uses the whole grammar.
        Assert.True(floors.Count >= 10, $"Only {floors.Count} floor motifs ship.");
        Assert.True(walls.Count >= 8, $"Only {walls.Count} wall motifs ship.");
        Assert.Equal(Enum.GetValues<FeatureKind>().Length, features.Count);
        Assert.True(ambience.Count >= 10, $"Only {ambience.Count} kinds of weather ship.");
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

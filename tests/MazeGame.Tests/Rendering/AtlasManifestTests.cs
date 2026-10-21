using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MazeGame.UI.Rendering.Pixels;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies the manifest the pixel renderer reads: every shipped theme is described, every tile the
/// renderer asks for exists, and a malformed file fails loudly.
/// </summary>
/// <remarks>
/// These tests read the committed output, so they are also the guard on the compiler: a tile that
/// is renamed, dropped or moved out of its atlas makes the game's draw calls fail, and that is
/// caught here rather than at the first frame of a floor.
/// </remarks>
public sealed class AtlasManifestTests
{
    private static readonly string[] PlayerFacings = ["north", "east", "south", "west"];

    private static bool IsFont(ThemeAtlas atlas) =>
        string.Equals(atlas.ThemeId, PixelFont.AtlasId, StringComparison.Ordinal);

    [Fact]
    public void ShippedManifest_DescribesAShippedAtlasForEveryFloor()
    {
        var atlases = AtlasManifest.LoadShipped();

        // Three floors plus the font sheet, which is an atlas too but not a theme.
        Assert.Equal(4, atlases.Count);

        foreach (var theme in new[] { "shallows", "rootbound", "drowned" })
        {
            Assert.True(atlases.ContainsKey(theme), $"The manifest does not describe '{theme}'.");
        }

        Assert.True(atlases.ContainsKey(PixelFont.AtlasId), "The manifest does not describe the font.");

        var directory = AssetPaths.GeneratedDirectory();

        foreach (var atlas in atlases.Values)
        {
            Assert.True(atlas.Width > 0 && atlas.Height > 0, $"{atlas.ThemeId} has no size.");
            Assert.True(File.Exists(Path.Combine(directory, atlas.FileName)), $"{atlas.FileName} is missing.");
        }

        foreach (var theme in atlases.Values.Where(atlas => !IsFont(atlas)))
        {
            Assert.True(theme.FloorVariantCount >= 2, $"{theme.ThemeId} draws {theme.FloorVariantCount} floor variants.");
        }
    }

    [Fact]
    public void ShippedManifest_GivesEveryThemeTheTilesTheRendererDraws()
    {
        // The renderer composes these names inline, so this list is the contract: adding a draw
        // call without a tile is a red test rather than an exception mid-run.
        var shared = new List<string> { "wall-north", "wall-east", "wall-south", "wall-west", "barrier", "barrier-cracked", "arch-locked", "arch-open", "prop-0", "prop-1" };

        foreach (var facing in PlayerFacings)
        {
            shared.Add($"player-{facing}-idle");
            shared.Add($"player-{facing}-walk0");
            shared.Add($"player-{facing}-walk1");
            shared.Add($"player-{facing}-walk2");
            shared.Add($"killer-{facing}-idle");
            shared.Add($"killer-{facing}-walk0");
            shared.Add($"killer-{facing}-walk1");
        }

        for (var frame = 0; frame < 3; frame++)
        {
            shared.Add($"key-{frame}");
            shared.Add($"wisp-{frame}");
        }

        foreach (var atlas in AtlasManifest.LoadShipped().Values.Where(atlas => !IsFont(atlas)))
        {
            var required = new List<string>(shared);

            // The floor variant count is per theme, so it is added per theme: a theme with two
            // variants must not be asked for a third.
            for (var variant = 0; variant < atlas.FloorVariantCount; variant++)
            {
                required.Add($"floor-{variant}");
            }

            foreach (var tile in required)
            {
                Assert.True(atlas.Tiles.ContainsKey(tile), $"{atlas.ThemeId} has no tile '{tile}'.");
            }
        }
    }

    [Fact]
    public void ShippedManifest_PutsEveryTileOnTheCellGrid()
    {
        foreach (var atlas in AtlasManifest.LoadShipped().Values.Where(atlas => !IsFont(atlas)))
        {
            foreach (var tile in atlas.Tiles.Values)
            {
                Assert.Equal(PixelLayout.CellSize, tile.Width);
                Assert.Equal(PixelLayout.CellSize, tile.Height);
                Assert.True(tile.X >= 0 && tile.Y >= 0, $"{tile.Name} has a negative position.");
                Assert.True(
                    tile.X + tile.Width <= atlas.Width && tile.Y + tile.Height <= atlas.Height,
                    $"{tile.Name} falls outside the {atlas.Width}x{atlas.Height} atlas.");
            }
        }
    }

    [Fact]
    public void ShippedManifest_GivesTheFontAGlyphForEveryPrintableCharacter()
    {
        var atlas = AtlasManifest.LoadShipped()[PixelFont.AtlasId];

        Assert.Equal(PixelFont.Advance, atlas.Tile("glyph-65").Width);
        Assert.Equal(PixelFont.LineHeight, atlas.Tile("glyph-65").Height);
        Assert.Equal(PixelFont.IconSize, atlas.Tile("heart-full").Width);
        Assert.Equal(PixelFont.IconSize, atlas.Tile("pip").Height);

        for (var character = ' '; character <= '~'; character++)
        {
            Assert.True(
                atlas.Tiles.ContainsKey($"glyph-{(int)character}"),
                $"The font has no glyph for '{character}' ({(int)character}).");
        }
    }

    [Fact]
    public void Parse_ReadsAnExplicitTileSize()
    {
        var atlas = AtlasManifest.Parse(
            "theme\tfont\tFont\t32x16\t2 tiles\tabc\tfont.png\n" +
            "tile\tfont\tglyph-65\t0\t0\t6\t8\n" +
            "tile\tfont\theart-full\t8\t0\t8\t8\n")["font"];

        Assert.Equal(6, atlas.Tile("glyph-65").Width);
        Assert.Equal(8, atlas.Tile("glyph-65").Height);
        Assert.Equal(8, atlas.Tile("heart-full").Width);
    }

    [Fact]
    public void Parse_RejectsATileSizeThatIsNotAPositiveInteger()
    {
        var error = Assert.Throws<FormatException>(() => AtlasManifest.Parse(
            "theme\ttest\tOrganicCave\t32x16\t1 tiles\tabc\ttest.png\n" +
            "tile\ttest\tfloor-0\t0\t0\t0\t8\n"));

        Assert.Contains("is not a tile size", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadShipped_FindsTheCommittedFolder()
    {
        Assert.True(File.Exists(Path.Combine(AssetPaths.GeneratedDirectory(), "manifest.txt")));
    }

    [Fact]
    public void Tile_ThrowsForANameTheThemeDoesNotHave()
    {
        var atlas = AtlasManifest.Parse(
            "theme\ttest\tOrganicCave\t32x16\t1 tiles\tabc\ttest.png\n" +
            "tile\ttest\tfloor-0\t0\t0\n")["test"];

        var error = Assert.Throws<KeyNotFoundException>(() => atlas.Tile("floor-9"));

        Assert.Contains("floor-9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_IgnoresLineKindsItDoesNotKnow()
    {
        var atlas = AtlasManifest.Parse(
            "# a comment\n" +
            "theme\ttest\tOrganicCave\t32x16\t2 tiles\tabc\ttest.png\n" +
            "palette\ttest\tfloor\t#102030\n" +
            "tile\ttest\tfloor-0\t0\t0\n" +
            "tile\ttest\tfloor-1\t16\t0\n")["test"];

        Assert.Equal(2, atlas.Tiles.Count);
        Assert.Equal(2, atlas.FloorVariantCount);
    }

    [Fact]
    public void Parse_RejectsATileOutsideItsAtlas()
    {
        var error = Assert.Throws<FormatException>(() => AtlasManifest.Parse(
            "theme\ttest\tOrganicCave\t16x16\t1 tiles\tabc\ttest.png\n" +
            "tile\ttest\tfloor-0\t16\t0\n"));

        Assert.Contains("falls outside", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsADuplicateTileName()
    {
        var error = Assert.Throws<FormatException>(() => AtlasManifest.Parse(
            "theme\ttest\tOrganicCave\t32x16\t2 tiles\tabc\ttest.png\n" +
            "tile\ttest\tfloor-0\t0\t0\n" +
            "tile\ttest\tfloor-0\t16\t0\n"));

        Assert.Contains("already has a tile", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsACountThatDisagreesWithTheTileLines()
    {
        var error = Assert.Throws<FormatException>(() => AtlasManifest.Parse(
            "theme\ttest\tOrganicCave\t32x16\t3 tiles\tabc\ttest.png\n" +
            "tile\ttest\tfloor-0\t0\t0\n"));

        Assert.Contains("lists 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsATileWithoutATheme()
    {
        var error = Assert.Throws<FormatException>(() => AtlasManifest.Parse("tile\ttest\tfloor-0\t0\t0\n"));

        Assert.Contains("unknown theme", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ThrowsWhenTheFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mazegame-missing-{Guid.NewGuid():N}.txt");

        Assert.Throws<FileNotFoundException>(() => AtlasManifest.Load(path));
    }
}

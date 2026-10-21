using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the compiler end to end: the sheet layout, the tileset and cast it must contain,
/// byte-identical recompiles, and the check pass against the committed output.
/// </summary>
public sealed class SpriteCompilerTests
{
    [Fact]
    public void AnAtlas_HoldsTheTilesetAndTheWholeCast()
    {
        var theme = ThemeCatalog.ById("shallows")!;

        var atlas = SpriteCompiler.Compile(theme);
        var names = atlas.Tiles.Select(tile => tile.Name).ToList();

        // floor variants + signature feature + plain wall + 4 thin wall faces + 16 edges + 47 blobs
        // + 16 player poses + 12 killer poses + 3 key + 3 wisp + 2 barrier + 2 arch + 3 props.
        Assert.Equal(theme.VariantCount + 110, atlas.Tiles.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());

        foreach (var expected in new[]
                 {
                     "floor-0", "floor-feature", "wall", "edge-0", "edge-15", "blob-0", "blob-46",
                     "player-south-idle", "player-west-walk2", "killer-north-walk1",
                     "key-2", "wisp-1", "barrier", "barrier-cracked", "arch-locked", "arch-open",
                     "prop-0", "prop-1", "prop-2",
                 })
        {
            Assert.Contains(expected, names);
        }

        // Eight to a row, and every tile inside the canvas with no overlap.
        Assert.Equal(SpriteCompiler.Columns * CaveTiles.TileSize, atlas.Canvas.Width);
        Assert.Equal(((atlas.Tiles.Count + SpriteCompiler.Columns - 1) / SpriteCompiler.Columns) * CaveTiles.TileSize, atlas.Canvas.Height);
        Assert.Equal(atlas.Tiles.Count, atlas.Tiles.Select(tile => (tile.X, tile.Y)).Distinct().Count());
    }

    [Fact]
    public void EveryTile_IsDrawn_AndTheTilesetIsFullyOpaque()
    {
        var atlas = SpriteCompiler.Compile(ThemeCatalog.ById("rootbound")!);

        foreach (var tile in atlas.Tiles)
        {
            var drawn = 0;

            for (var row = 0; row < tile.Height; row++)
            {
                for (var column = 0; column < tile.Width; column++)
                {
                    if (atlas.Canvas[tile.X + column, tile.Y + row] != PixelCanvas.Transparent)
                    {
                        drawn++;
                    }
                }
            }

            Assert.True(drawn > 0, $"{tile.Name} is empty");

            // A map tile is a full square: a transparent pixel would show the window's background
            // through the floor. Sprites are cut-outs and are allowed their transparent margin.
            // The signature feature is a decal: it is deliberately transparent outside its
            // object, so it is not a map tile even though its name starts with "floor".
            var isMapTile = (tile.Name.StartsWith("floor", StringComparison.Ordinal) &&
                             !string.Equals(tile.Name, "floor-feature", StringComparison.Ordinal)) ||
                            tile.Name.StartsWith("edge", StringComparison.Ordinal) ||
                            tile.Name.StartsWith("blob", StringComparison.Ordinal) ||
                            tile.Name == "wall";

            if (isMapTile)
            {
                Assert.Equal(tile.Width * tile.Height, drawn);
            }
        }
    }

    [Fact]
    public void CompilingTheSameThemeTwice_GivesTheSameBytes()
    {
        var theme = ThemeCatalog.ById("drowned")!;

        Assert.Equal(SpriteCompiler.Compile(theme).Png, SpriteCompiler.Compile(theme).Png);
    }

    [Fact]
    public void TwoFloorVariants_AreNotTheSameTile()
    {
        var theme = ThemeCatalog.ById("shallows")!;

        Assert.NotEqual(Pixels(CaveTiles.Floor(theme, 0)), Pixels(CaveTiles.Floor(theme, 1)));
    }

    [Fact]
    public void TheShippedThemes_PassTheCheckAgainstTheCommittedAtlases()
    {
        var problems = SpriteCompiler.Check(ThemeCatalog.Shipped, RepositoryPath(SpriteCompiler.DefaultOutputDirectory));

        Assert.Empty(problems);
    }

    [Fact]
    public void AChangedAtlas_IsReportedAsDrift()
    {
        var directory = CopyCommittedAtlases();

        try
        {
            var path = Path.Combine(directory, SpriteCompiler.FileNameOf(ThemeCatalog.Shipped[0]));
            var bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            var problems = SpriteCompiler.Check(ThemeCatalog.Shipped, directory);

            Assert.Contains(problems, problem => problem.Contains("no longer matches", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TwoThemesThatOnlyDifferInHue_FailTheCheck()
    {
        var shallows = ThemeCatalog.ById("shallows")!;
        var tint = shallows with { Id = "shallows-tint", Name = "Tint", Hue = shallows.Hue + 2 };

        var directory = CopyCommittedAtlases();

        try
        {
            var problems = SpriteCompiler.Check([shallows, tint], directory);

            Assert.Contains(problems, problem => problem.Contains("too close in colour", StringComparison.Ordinal));
            Assert.Contains(problems, problem => problem.Contains("structural parameters", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheManifest_NamesEveryTileAndItsPosition()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"assetforge-{Guid.NewGuid():N}");

        try
        {
            var summaries = SpriteCompiler.WriteAll(ThemeCatalog.Shipped, directory);
            var manifest = File.ReadAllText(Path.Combine(directory, SpriteCompiler.ManifestFileName));

            Assert.Equal(ThemeCatalog.Shipped.Count, summaries.Count);
            Assert.Equal(SpriteCompiler.ExpectedManifest(ThemeCatalog.Shipped), manifest);
            Assert.Contains("tile\tshallows\tblob-46\t", manifest, StringComparison.Ordinal);
            Assert.Contains("tile\tshallows\tarch-open\t", manifest, StringComparison.Ordinal);

            foreach (var theme in ThemeCatalog.Shipped)
            {
                Assert.True(File.Exists(Path.Combine(directory, SpriteCompiler.FileNameOf(theme))));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static byte[] Pixels(PixelCanvas canvas) =>
        canvas.ToRgba(Palette.Derive(ThemeCatalog.Shipped[0]));

    /// <summary>Copies the committed atlases into a temporary directory the test may modify.</summary>
    private static string CopyCommittedAtlases()
    {
        var source = RepositoryPath(SpriteCompiler.DefaultOutputDirectory);
        var target = Path.Combine(Path.GetTempPath(), $"assetforge-{Guid.NewGuid():N}");

        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return target;
    }

    /// <summary>Resolves a repository-relative path by walking up from the test binary.</summary>
    private static string RepositoryPath(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MazeGame.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find MazeGame.sln above the test binary.");
        }

        return Path.Combine(directory.FullName, relative);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MazeGame.AssetForge;

/// <summary>One tile inside an atlas.</summary>
/// <param name="Name">The tile's name, unique within its atlas.</param>
/// <param name="X">The left edge of the tile in atlas pixels.</param>
/// <param name="Y">The top edge of the tile in atlas pixels.</param>
/// <param name="Width">The tile's width in pixels.</param>
/// <param name="Height">The tile's height in pixels.</param>
public sealed record TileInfo(string Name, int X, int Y, int Width, int Height);

/// <summary>One compiled theme: the canvas, its tiles and the encoded PNG.</summary>
/// <param name="Theme">The theme that was compiled.</param>
/// <param name="Canvas">The atlas, before palette expansion.</param>
/// <param name="Tiles">Where each tile sits.</param>
/// <param name="Png">The encoded file, ready to write.</param>
public sealed record Atlas(Theme Theme, PixelCanvas Canvas, IReadOnlyList<TileInfo> Tiles, byte[] Png);

/// <summary>
/// The sprite compiler: turns a theme into an atlas PNG plus a manifest, and checks that the
/// committed atlases still match what the code would produce.
/// </summary>
/// <remarks>
/// <para>
/// An atlas holds, in this order: floor variants, the plain wall, the 16 edge tiles, the 47 blob
/// tiles, and the cast (player and killer poses, key, wisp, barrier, arch). Everything is 16x16 and
/// laid out eight to a row, so a tile's position is a pure function of its index — no packing
/// heuristic, no drift.
/// </para>
/// <para>
/// The compiler is not referenced by the game. Its output is committed, so a fresh clone builds and
/// plays with no tool step, and <see cref="Check"/> is what catches an atlas that drifted from the
/// code that claims to produce it.
/// </para>
/// </remarks>
public static class SpriteCompiler
{
    /// <summary>Where the atlases are committed, relative to the repository root.</summary>
    public const string DefaultOutputDirectory = "src/MazeGame.UI/Content/Generated";

    /// <summary>The manifest's file name.</summary>
    public const string ManifestFileName = "manifest.txt";

    /// <summary>How many tiles fit on one row of an atlas.</summary>
    public const int Columns = 8;

    /// <summary>The smallest palette distance two neighbouring themes must have.</summary>
    public const double MinimumThemeDistance = Palette.MinimumThemeDistance;

    /// <summary>The smallest number of structural parameters two neighbouring themes must differ in.</summary>
    public const int MinimumStructuralDifferences = 2;

    /// <summary>Compiles one theme into an atlas.</summary>
    /// <param name="theme">The theme to draw.</param>
    /// <returns>The atlas, with the PNG already encoded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static Atlas Compile(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var tiles = new List<(string Name, PixelCanvas Canvas)>();

        for (var variant = 0; variant < theme.VariantCount; variant++)
        {
            tiles.Add(($"floor-{variant}", TileGrammar.Floor(theme, variant)));
        }

        tiles.Add(("wall", TileGrammar.Wall(theme)));

        // Thin faces for the walls between cells, which is what today's maze draws: the solid wall
        // tiles below serve floors that grow chasm and veto cells.
        foreach (var side in Enum.GetValues<WallSide>())
        {
            tiles.Add((WallStrips.NameOf(side), TileGrammar.WallFace(theme, side)));
        }

        for (var mask = 0; mask < Autotiler.EdgeTileCount; mask++)
        {
            tiles.Add(($"edge-{mask}", Autotiler.EdgeTile(theme, mask)));
        }

        for (var index = 0; index < Autotiler.BlobTileCount; index++)
        {
            tiles.Add(($"blob-{index}", Autotiler.BlobTile(theme, Autotiler.BlobMasks[index])));
        }

        foreach (var facing in Enum.GetValues<RigFacing>())
        {
            foreach (var frame in Enum.GetValues<PlayerFrame>())
            {
                tiles.Add(($"player-{FacingName(facing)}-{PlayerFrameName(frame)}", Rig.Player(theme, facing, frame)));
            }
        }

        foreach (var facing in Enum.GetValues<RigFacing>())
        {
            foreach (var frame in Enum.GetValues<KillerFrame>())
            {
                tiles.Add(($"killer-{FacingName(facing)}-{KillerFrameName(frame)}", Rig.Killer(theme, facing, frame)));
            }
        }

        for (var frame = 0; frame < 3; frame++)
        {
            tiles.Add(($"key-{frame}", Rig.Key(theme, frame)));
            tiles.Add(($"wisp-{frame}", Rig.Wisp(theme, frame)));
        }

        tiles.Add(("barrier", Rig.Barrier(theme, cracked: false)));
        tiles.Add(("barrier-cracked", Rig.Barrier(theme, cracked: true)));
        tiles.Add(("arch-locked", Rig.Arch(theme, open: false)));
        tiles.Add(("arch-open", Rig.Arch(theme, open: true)));

        for (var prop = 0; prop < TileGrammar.PropCount; prop++)
        {
            tiles.Add(($"prop-{prop}", TileGrammar.Prop(theme, prop)));
        }

        var rows = (tiles.Count + Columns - 1) / Columns;
        var canvas = new PixelCanvas(CaveTiles.TileSize * Columns, CaveTiles.TileSize * rows);
        var placed = new List<TileInfo>(tiles.Count);

        for (var index = 0; index < tiles.Count; index++)
        {
            var x = (index % Columns) * CaveTiles.TileSize;
            var y = (index / Columns) * CaveTiles.TileSize;
            canvas.Stamp(tiles[index].Canvas, x, y, skipTransparent: false);
            placed.Add(new TileInfo(tiles[index].Name, x, y, CaveTiles.TileSize, CaveTiles.TileSize));
        }

        var png = Png.Encode(canvas.Width, canvas.Height, canvas.ToRgba(Palette.Derive(theme)));

        return new Atlas(theme, canvas, new ReadOnlyCollection<TileInfo>(placed), png);
    }

    /// <summary>Compiles <paramref name="themes"/> and writes the PNGs and the manifest.</summary>
    /// <param name="themes">The themes to write, in manifest order.</param>
    /// <param name="directory">The directory to write into; created when missing.</param>
    /// <returns>One summary line per theme.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static IReadOnlyList<string> WriteAll(IReadOnlyList<Theme> themes, string directory)
    {
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(directory);

        Directory.CreateDirectory(directory);

        var summaries = new List<string>(themes.Count);
        var atlases = new List<Atlas>(themes.Count);

        foreach (var theme in themes)
        {
            var atlas = Compile(theme);
            var fileName = FileNameOf(theme);
            File.WriteAllBytes(Path.Combine(directory, fileName), atlas.Png);
            atlases.Add(atlas);
            summaries.Add(SummaryLine(atlas, fileName));
        }

        var font = FontAtlas.Compile();
        File.WriteAllBytes(Path.Combine(directory, FontAtlas.FileName), font.Png);

        File.WriteAllText(
            Path.Combine(directory, ManifestFileName),
            BuildManifest(atlases, summaries, font),
            new UTF8Encoding(false));

        return new ReadOnlyCollection<string>(summaries);
    }

    /// <summary>
    /// Checks the themes and the committed output, without writing anything.
    /// </summary>
    /// <param name="themes">The themes the game ships, in floor order.</param>
    /// <param name="directory">The committed output directory.</param>
    /// <returns>Every problem found; empty means the atlas set is healthy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static IReadOnlyList<string> Check(IReadOnlyList<Theme> themes, string directory)
    {
        ArgumentNullException.ThrowIfNull(themes);
        ArgumentNullException.ThrowIfNull(directory);

        var problems = new List<string>();

        foreach (var theme in themes)
        {
            try
            {
                theme.Validate();
                Palette.Derive(theme).Validate();
            }
            catch (ArgumentException error)
            {
                problems.Add($"{theme.Id}: {error.Message}");
            }
            catch (InvalidOperationException error)
            {
                problems.Add($"{theme.Id}: {error.Message}");
            }
        }

        for (var index = 1; index < themes.Count; index++)
        {
            var previous = themes[index - 1];
            var next = themes[index];
            var distance = Palette.Derive(previous).DistanceTo(Palette.Derive(next));

            if (distance < MinimumThemeDistance)
            {
                problems.Add(
                    $"{previous.Id} and {next.Id} are too close in colour " +
                    $"({distance.ToString("0.0", CultureInfo.InvariantCulture)} < {MinimumThemeDistance}).");
            }

            var structural = previous.StructuralDifferences(next);

            if (structural < MinimumStructuralDifferences)
            {
                problems.Add(
                    $"{previous.Id} and {next.Id} differ in {structural} structural parameters " +
                    $"(need {MinimumStructuralDifferences}); a hue swap is not a theme.");
            }
        }

        foreach (var theme in themes)
        {
            var first = Compile(theme).Png;
            var second = Compile(theme).Png;

            if (!first.AsSpan().SequenceEqual(second))
            {
                problems.Add($"{theme.Id}: two compiles of the same theme differ; the compiler is not deterministic.");
                continue;
            }

            var path = Path.Combine(directory, FileNameOf(theme));

            if (!File.Exists(path))
            {
                problems.Add($"{theme.Id}: {path} is missing; run the compiler and commit the output.");
                continue;
            }

            if (!first.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                problems.Add($"{theme.Id}: {path} no longer matches the compiler; re-run and commit it.");
            }
        }

        var fontFirst = FontAtlas.Compile().Png;
        var fontSecond = FontAtlas.Compile().Png;

        if (!fontFirst.AsSpan().SequenceEqual(fontSecond))
        {
            problems.Add("font: two compiles of the same font differ; the compiler is not deterministic.");
        }

        var fontPath = Path.Combine(directory, FontAtlas.FileName);

        if (!File.Exists(fontPath))
        {
            problems.Add($"font: {fontPath} is missing; run the compiler and commit the output.");
        }
        else if (!fontFirst.AsSpan().SequenceEqual(File.ReadAllBytes(fontPath)))
        {
            problems.Add($"font: {fontPath} no longer matches the compiler; re-run and commit it.");
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);

        if (themes.Count > 0 && !File.Exists(manifestPath))
        {
            problems.Add($"{manifestPath} is missing; run the compiler and commit the output.");
        }
        else if (File.Exists(manifestPath))
        {
            var expected = ExpectedManifest(themes);
            if (!string.Equals(expected, File.ReadAllText(manifestPath), StringComparison.Ordinal))
            {
                problems.Add($"{manifestPath} is out of date; re-run the compiler and commit it.");
            }
        }

        return new ReadOnlyCollection<string>(problems);
    }

    /// <summary>Returns the file name an atlas is written to.</summary>
    /// <param name="theme">The theme the atlas belongs to.</param>
    /// <returns>The file name, including the extension.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static string FileNameOf(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return $"{theme.Id}-tiles.png";
    }

    /// <summary>Builds the manifest text a fresh compile would write.</summary>
    /// <param name="themes">The themes, in manifest order.</param>
    /// <returns>The whole file, including the header comment.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themes"/> is null.</exception>
    public static string ExpectedManifest(IReadOnlyList<Theme> themes)
    {
        ArgumentNullException.ThrowIfNull(themes);

        var atlases = themes.Select(Compile).ToList();

        return BuildManifest(
            atlases,
            atlases.Select(atlas => SummaryLine(atlas, FileNameOf(atlas.Theme))).ToList(),
            FontAtlas.Compile());
    }

    private static string BuildManifest(
        IReadOnlyList<Atlas> atlases,
        IReadOnlyList<string> summaries,
        FontSheet font)
    {
        var text = new StringBuilder();
        text.Append("# generated by tools/AssetForge; do not edit by hand\n");

        for (var index = 0; index < atlases.Count; index++)
        {
            text.Append(summaries[index]).Append('\n');
            AppendTiles(text, atlases[index].Theme.Id, atlases[index].Tiles);
        }

        text.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"theme\t{FontAtlas.Id}\t{FontAtlas.Family}\t{font.Canvas.Width}x{font.Canvas.Height}" +
            $"\t{font.Tiles.Count} tiles\t{Png.Checksum(font.Png).ToString("x8", CultureInfo.InvariantCulture)}" +
            $"\t{FontAtlas.FileName}\n"));
        AppendTiles(text, FontAtlas.Id, font.Tiles);

        return text.ToString();
    }

    private static void AppendTiles(StringBuilder text, string id, IReadOnlyList<TileInfo> tiles)
    {
        foreach (var tile in tiles)
        {
            text.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"tile\t{id}\t{tile.Name}\t{tile.X}\t{tile.Y}\t{tile.Width}\t{tile.Height}\n"));
        }
    }

    private static string SummaryLine(Atlas atlas, string fileName)
    {
        var checksum = Png.Checksum(atlas.Png).ToString("x8", CultureInfo.InvariantCulture);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"theme\t{atlas.Theme.Id}\t{atlas.Theme.Family}\t{atlas.Canvas.Width}x{atlas.Canvas.Height}" +
            $"\t{atlas.Tiles.Count} tiles\t{checksum}\t{fileName}");
    }

    private static string FacingName(RigFacing facing) => facing switch
    {
        RigFacing.North => "north",
        RigFacing.East => "east",
        RigFacing.South => "south",
        _ => "west",
    };

    private static string PlayerFrameName(PlayerFrame frame) => frame switch
    {
        PlayerFrame.Idle => "idle",
        PlayerFrame.Walk0 => "walk0",
        PlayerFrame.Walk1 => "walk1",
        _ => "walk2",
    };

    private static string KillerFrameName(KillerFrame frame) => frame switch
    {
        KillerFrame.Idle => "idle",
        KillerFrame.Walk0 => "walk0",
        _ => "walk1",
    };
}

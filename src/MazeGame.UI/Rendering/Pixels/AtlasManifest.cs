using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>One tile inside a committed atlas.</summary>
/// <param name="Name">The tile's name, unique within its theme.</param>
/// <param name="X">The left edge of the tile in atlas pixels.</param>
/// <param name="Y">The top edge of the tile in atlas pixels.</param>
/// <param name="Width">The tile's width in pixels.</param>
/// <param name="Height">The tile's height in pixels.</param>
public sealed record AtlasTile(string Name, int X, int Y, int Width, int Height);

/// <summary>
/// One theme's atlas as the manifest describes it: where the PNG is, how big it is, and where each
/// named tile sits inside it.
/// </summary>
public sealed class ThemeAtlas
{
    private readonly IReadOnlyDictionary<string, AtlasTile> _tiles;

    /// <summary>Creates the atlas description.</summary>
    /// <param name="themeId">The theme's stable id, which is also the PNG's name.</param>
    /// <param name="family">The shape family the tiles were drawn in.</param>
    /// <param name="width">The PNG's width in pixels.</param>
    /// <param name="height">The PNG's height in pixels.</param>
    /// <param name="checksum">The checksum the compiler recorded for the PNG.</param>
    /// <param name="fileName">The PNG's file name, relative to the generated folder.</param>
    /// <param name="tiles">Every tile, by name.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public ThemeAtlas(
        string themeId,
        string family,
        int width,
        int height,
        string checksum,
        string fileName,
        IReadOnlyDictionary<string, AtlasTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(themeId);
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(checksum);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(tiles);

        ThemeId = themeId;
        Family = family;
        Width = width;
        Height = height;
        Checksum = checksum;
        FileName = fileName;
        _tiles = tiles;

        var variants = 0;
        while (tiles.ContainsKey($"floor-{variants}"))
        {
            variants++;
        }

        FloorVariantCount = variants;
    }

    /// <summary>Gets the theme's stable id.</summary>
    public string ThemeId { get; }

    /// <summary>Gets the shape family the tiles were drawn in.</summary>
    public string Family { get; }

    /// <summary>Gets the PNG's width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the PNG's height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the checksum the compiler recorded.</summary>
    public string Checksum { get; }

    /// <summary>Gets the PNG's file name, relative to the generated folder.</summary>
    public string FileName { get; }

    /// <summary>Gets every tile, by name.</summary>
    public IReadOnlyDictionary<string, AtlasTile> Tiles => _tiles;

    /// <summary>Gets how many <c>floor-n</c> variants the atlas holds.</summary>
    public int FloorVariantCount { get; }

    /// <summary>Returns the tile called <paramref name="name"/>.</summary>
    /// <param name="name">The tile's name.</param>
    /// <returns>The tile's position.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the theme has no such tile. Missing art is a bug in the compiler or the theme
    /// table, never something to draw a placeholder for.
    /// </exception>
    public AtlasTile Tile(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_tiles.TryGetValue(name, out var tile))
        {
            throw new KeyNotFoundException($"Theme '{ThemeId}' has no tile called '{name}'.");
        }

        return tile;
    }
}

/// <summary>
/// Reads <c>manifest.txt</c>: the contract between the sprite compiler and the game.
/// </summary>
/// <remarks>
/// The format is tab-separated and deliberately trivial, because it is the one file both halves of
/// the project must agree on. One <c>theme</c> line declares an atlas and one <c>tile</c> line
/// locates a tile inside it; everything else is ignored so a later compiler can add lines without
/// breaking an older game. Parsing validates the file as it reads it — a tile outside its atlas, a
/// duplicate name or a count that disagrees with the tile lines is a <see cref="FormatException"/>,
/// not something to paper over at draw time.
/// </remarks>
public static class AtlasManifest
{
    /// <summary>Parses manifest text.</summary>
    /// <param name="text">The whole file.</param>
    /// <returns>Every atlas, by theme id.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when a line is malformed or the file disagrees with itself.</exception>
    public static IReadOnlyDictionary<string, ThemeAtlas> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var declared = new List<(string ThemeId, string Family, int Width, int Height, int Count, string Checksum, string FileName, int Line)>();
        var tiles = new Dictionary<string, Dictionary<string, AtlasTile>>(StringComparer.Ordinal);

        var lines = text.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            var number = index + 1;

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');

            switch (fields[0])
            {
                case "theme":
                    declared.Add(ParseTheme(fields, number));

                    if (!tiles.ContainsKey(fields[1]))
                    {
                        tiles[fields[1]] = new Dictionary<string, AtlasTile>(StringComparer.Ordinal);
                    }

                    break;

                case "tile":
                    ParseTile(fields, number, tiles);
                    break;

                default:
                    // Forward compatibility: a compiler may add a line kind the game does not know.
                    break;
            }
        }

        var atlases = new Dictionary<string, ThemeAtlas>(StringComparer.Ordinal);

        foreach (var theme in declared)
        {
            if (!tiles.TryGetValue(theme.ThemeId, out var themeTiles))
            {
                throw new FormatException($"Manifest line {theme.Line}: theme '{theme.ThemeId}' has no tile lines.");
            }

            if (themeTiles.Count != theme.Count)
            {
                throw new FormatException(
                    $"Manifest line {theme.Line}: '{theme.ThemeId}' declares {theme.Count} tiles but lists {themeTiles.Count}.");
            }

            foreach (var tile in themeTiles.Values)
            {
                if (tile.X + tile.Width > theme.Width || tile.Y + tile.Height > theme.Height)
                {
                    throw new FormatException(
                        $"Manifest line {theme.Line}: '{tile.Name}' at {tile.X},{tile.Y} falls outside " +
                        $"the {theme.Width}x{theme.Height} atlas.");
                }
            }

            atlases[theme.ThemeId] = new ThemeAtlas(
                theme.ThemeId,
                theme.Family,
                theme.Width,
                theme.Height,
                theme.Checksum,
                theme.FileName,
                new ReadOnlyDictionary<string, AtlasTile>(themeTiles));
        }

        return new ReadOnlyDictionary<string, ThemeAtlas>(atlases);
    }

    /// <summary>Reads and parses a manifest file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>Every atlas, by theme id.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="FormatException">Thrown when the file is malformed.</exception>
    public static IReadOnlyDictionary<string, ThemeAtlas> Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The atlas manifest is missing: {path}", path);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>Reads the manifest that ships with the game.</summary>
    /// <returns>Every shipped atlas, by theme id.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the generated folder cannot be found.</exception>
    /// <exception cref="FormatException">Thrown when the manifest is malformed.</exception>
    public static IReadOnlyDictionary<string, ThemeAtlas> LoadShipped() =>
        Load(Path.Combine(AssetPaths.GeneratedDirectory(), "manifest.txt"));

    private static (string ThemeId, string Family, int Width, int Height, int Count, string Checksum, string FileName, int Line) ParseTheme(
        string[] fields,
        int line)
    {
        if (fields.Length < 7)
        {
            throw new FormatException($"Manifest line {line}: a theme line has 7 fields, not {fields.Length}.");
        }

        var size = fields[3].Split('x');

        if (size.Length != 2 ||
            !int.TryParse(size[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(size[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
            width <= 0 ||
            height <= 0)
        {
            throw new FormatException($"Manifest line {line}: '{fields[3]}' is not a WxH size.");
        }

        var countField = fields[4].Split(' ');
        var count = 0;

        if (countField.Length == 2 && countField[1] == "tiles" &&
            int.TryParse(countField[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            count = parsed;
        }
        else
        {
            throw new FormatException($"Manifest line {line}: '{fields[4]}' is not a tile count.");
        }

        return (fields[1], fields[2], width, height, count, fields[5], fields[6], line);
    }

    private static void ParseTile(
        string[] fields,
        int line,
        Dictionary<string, Dictionary<string, AtlasTile>> tiles)
    {
        if (fields.Length < 5)
        {
            throw new FormatException($"Manifest line {line}: a tile line has 5 fields, not {fields.Length}.");
        }

        var theme = fields[1];
        var name = fields[2];

        if (!tiles.TryGetValue(theme, out var themeTiles))
        {
            // A tile before its theme line: the manifest is written theme-first, so this is a bug.
            throw new FormatException($"Manifest line {line}: tile for unknown theme '{theme}'.");
        }

        if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) || x < 0 ||
            !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) || y < 0)
        {
            throw new FormatException($"Manifest line {line}: '{fields[3]},{fields[4]}' is not a position.");
        }

        var width = PixelLayout.CellSize;
        var height = PixelLayout.CellSize;

        // Tiles are 16x16 by default, which is why the two size fields are optional: an atlas of
        // maze tiles does not need to say so on every line, but the font sheet (6x8 cells and 8x8
        // icons) does.
        if (fields.Length >= 7)
        {
            if (!int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) || width <= 0 ||
                !int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out height) || height <= 0)
            {
                throw new FormatException($"Manifest line {line}: '{fields[5]}x{fields[6]}' is not a tile size.");
            }
        }

        var tile = new AtlasTile(name, x, y, width, height);

        if (!themeTiles.TryAdd(name, tile))
        {
            throw new FormatException($"Manifest line {line}: '{theme}' already has a tile called '{name}'.");
        }
    }
}

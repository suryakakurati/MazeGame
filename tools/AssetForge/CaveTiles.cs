using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// The organic-cave family: floors of grit and boulders, walls of stacked rock with moss on top.
/// </summary>
/// <remarks>
/// <para>
/// Every routine here is a pure function of <c>(theme, variant)</c>. The seed comes from the theme
/// id and the tile name, so a tile is the same on every machine and in every run, and a change to
/// the drawing code shows up as a changed PNG rather than as a random wobble.
/// </para>
/// <para>
/// Nothing reads a colour: the routines write <see cref="PaletteRole"/> values, and the palette is
/// applied when the atlas is written. A new theme therefore needs no new drawing code at all.
/// </para>
/// </remarks>
public static class CaveTiles
{
    /// <summary>The side of one tile in pixels.</summary>
    public const int TileSize = 16;

    /// <summary>Draws one floor variant.</summary>
    /// <param name="theme">The theme whose parameters shape the stones.</param>
    /// <param name="variant">The variant number, 0-based; decides the layout and the dither parity.</param>
    /// <returns>A floor tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the variant is negative.</exception>
    public static PixelCanvas Floor(Theme theme, int variant)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (variant < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, "A variant number cannot be negative.");
        }

        var canvas = new PixelCanvas(TileSize, TileSize);
        var random = new SeededRandom(Seed($"{theme.Id}/floor-{variant}"));

        canvas.Fill(Index(PaletteRole.Floor));
        canvas.Dither(0, 0, TileSize, TileSize, Index(PaletteRole.FloorDark), variant & 1);

        // Three boulders, placed on a jittered grid so variants never stack in one corner. A
        // roundness of 0 gives angular rubble (radius 1) and 1 gives pebbles (radius 3).
        var radius = 1 + (int)Math.Round(theme.BoulderRoundness * 2);

        for (var stone = 0; stone < 3; stone++)
        {
            var centreX = 3 + (stone * 5) + random.NextInt(2) - (stone % 2);
            var centreY = 4 + random.NextInt(8);

            canvas.Ellipse(centreX, centreY, radius, Math.Max(1, radius - 1), Index(PaletteRole.Wall));

            if (radius > 1)
            {
                canvas.Ellipse(centreX - 1, centreY - 1, radius - 1, 1, Index(PaletteRole.WallTop));
            }
        }

        canvas.Speckle(Index(PaletteRole.FloorDark), theme.SpeckleRate * 0.35, random);
        canvas.Speckle(Index(PaletteRole.WallEdge), theme.SpeckleRate * 0.10, random);

        if (theme.MossDensity > 0)
        {
            var patches = 1 + (int)Math.Round(theme.MossDensity * 2);

            for (var patch = 0; patch < patches; patch++)
            {
                canvas.Ellipse(
                    random.NextInt(TileSize),
                    random.NextInt(TileSize),
                    1 + (int)Math.Round(theme.MossDensity * 2),
                    1,
                    Index(PaletteRole.Moss));
            }
        }

        var motes = (int)Math.Round(theme.GlowStrength * 3);

        for (var mote = 0; mote < motes; mote++)
        {
            canvas.Set(random.NextInt(TileSize), random.NextInt(TileSize), Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws the wall tile: courses of rock, a lit top and a damp lower edge.</summary>
    /// <param name="theme">The theme whose parameters shape the wall.</param>
    /// <returns>A wall tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Wall(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(TileSize, TileSize);
        var random = new SeededRandom(Seed($"{theme.Id}/wall"));

        canvas.Fill(Index(PaletteRole.Wall));

        // A lit top edge and a dark foot: the two things that make a flat fill read as a solid.
        canvas.Rect(0, 0, TileSize, 2, Index(PaletteRole.WallTop));
        canvas.Dither(0, 2, TileSize, 1, Index(PaletteRole.WallTop), 1);
        canvas.Rect(0, TileSize - 2, TileSize, 2, Index(PaletteRole.WallEdge));

        // Chips and cracks: short verticals that break the courses without changing the silhouette.
        var chips = 2 + (int)Math.Round(theme.BoulderRoundness * 3);

        for (var chip = 0; chip < chips; chip++)
        {
            var column = 1 + random.NextInt(TileSize - 2);
            var top = 3 + random.NextInt(TileSize - 7);
            canvas.Rect(column, top, 1, 2 + random.NextInt(3), Index(PaletteRole.WallEdge));
        }

        // Moss hangs from the top where the wall meets the floor above it.
        if (theme.MossDensity > 0)
        {
            var drips = 1 + (int)Math.Round(theme.MossDensity * 4);

            for (var drip = 0; drip < drips; drip++)
            {
                var column = random.NextInt(TileSize);
                canvas.Rect(column, 2, 1, 1 + (int)Math.Round(theme.MossDensity * 4), Index(PaletteRole.Moss));
            }
        }

        var motes = (int)Math.Round(theme.GlowStrength * 2);

        for (var mote = 0; mote < motes; mote++)
        {
            canvas.Set(random.NextInt(TileSize), TileSize - 3 - random.NextInt(2), Index(PaletteRole.Glow));
        }

        return canvas;
    }


    /// <summary>Draws one prop for the cave family.</summary>
    /// <param name="theme">The theme whose palette roles shape it.</param>
    /// <param name="index">0 is a mushroom cluster, 1 is a drip column.</param>
    /// <returns>A 16x16 prop with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0..1.</exception>
    public static PixelCanvas Prop(Theme theme, int index)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return index switch
        {
            0 => Mushrooms(theme),
            1 => Drips(theme),
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "The cave family has two props."),
        };
    }

    /// <summary>Draws a cluster of three glowing caps on pale stems.</summary>
    /// <param name="theme">The theme whose palette roles and glow are used.</param>
    /// <returns>A 16x16 prop.</returns>
    private static PixelCanvas Mushrooms(Theme theme)
    {
        var canvas = new PixelCanvas(TileSize, TileSize);
        var random = new SeededRandom(Seed($"{theme.Id}/prop-mushrooms"));

        canvas.Ellipse(8, 14, 5, 1, Index(PaletteRole.Shadow));

        var caps = 2 + (int)Math.Round(theme.GlowStrength * 2);

        for (var cap = 0; cap < caps; cap++)
        {
            var centreX = 4 + (cap * 3) + random.NextInt(2);
            var centreY = 8 + random.NextInt(4);
            var radius = 2 + random.NextInt(2);

            // A stem, then the cap over it: the cap is the part that reads at 1x.
            canvas.Rect(centreX, centreY, 1, 15 - centreY, Index(PaletteRole.Bone));
            canvas.Ellipse(centreX, centreY, radius, Math.Max(1, radius - 1), Index(PaletteRole.Glow));
            canvas.Set(centreX - 1, centreY - 1, Index(PaletteRole.UiText));
        }

        return canvas;
    }

    /// <summary>Draws a stalactite with a drip falling from it.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <returns>A 16x16 prop.</returns>
    private static PixelCanvas Drips(Theme theme)
    {
        var canvas = new PixelCanvas(TileSize, TileSize);
        var random = new SeededRandom(Seed($"{theme.Id}/prop-drips"));

        // A wet spike hanging from the top edge, with moss where it meets the rock.
        var width = 3 + random.NextInt(2);
        var height = 6 + random.NextInt(4);

        canvas.Ellipse(8, 0, width, height, Index(PaletteRole.Wall));
        canvas.Line(8, height - 1, 8, height + 1, Index(PaletteRole.WallEdge));
        canvas.Set(8, height + 2, Index(PaletteRole.Water));
        canvas.Rect(6, 0, 4, 1, Index(PaletteRole.Moss));

        return canvas;
    }

    /// <summary>Returns the palette index for a role, as a canvas stores it.</summary>
    /// <param name="role">The role to write.</param>
    /// <returns>The index of that role.</returns>
    public static byte Index(PaletteRole role) => (byte)role;

    /// <summary>
    /// Derives a stable seed from a tile's name, so tiles never depend on string hashing (which is
    /// randomised per process) or on the order they happen to be drawn in.
    /// </summary>
    /// <param name="name">The tile's identity, for example <c>shallows/floor-1</c>.</param>
    /// <returns>A seed that is the same in every process.</returns>
    internal static int Seed(string name)
    {
        const int Prime = 16777619;
        var hash = unchecked((int)2166136261);

        foreach (var character in name)
        {
            hash = unchecked((hash ^ character) * Prime);
        }

        return hash & 0x7FFFFFFF;
    }
}

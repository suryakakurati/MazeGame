using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// Builds the wall tileset from neighbour masks: 16 edge tiles and the 47 blob tiles.
/// </summary>
/// <remarks>
/// <para>
/// A tile is named by the sides that are open, so tile <i>n</i> provably matches mask <i>n</i>: the
/// drawing routine takes the same mask the renderer will use to pick the tile. There is no separate
/// "which tile is this?" decision that could drift.
/// </para>
/// <para>
/// The 47 blob tiles come from canonicalising all 256 eight-neighbour masks: a diagonal corner only
/// counts when both of its edges are open, which leaves exactly 47 distinct shapes. That table is
/// built once and asserted, so a mistake in the bit juggling fails at startup rather than by
/// producing a tile that no mask can ever select.
/// </para>
/// </remarks>
public static class Autotiler
{
    /// <summary>How many edge tiles a cardinal mask can produce.</summary>
    public const int EdgeTileCount = 16;

    /// <summary>How many blob tiles the canonical masks produce.</summary>
    public const int BlobTileCount = 47;

    // Eight-neighbour bit order, clockwise from north: N, NE, E, SE, S, SW, W, NW. The edge bits
    // sit on 1/4/16/64 and the corner bits on 2/8/32/128, so masking either set is a single `&`.
    private const int North = 1;
    private const int East = 4;
    private const int South = 16;
    private const int West = 64;

    private const int CornerNorthEast = 2;
    private const int CornerSouthEast = 8;
    private const int CornerSouthWest = 32;
    private const int CornerNorthWest = 128;

    private const int BandWidth = 5;

    private const int EdgeBits = North | East | South | West;

    private const int CornerBits = CornerNorthEast | CornerSouthEast | CornerSouthWest | CornerNorthWest;

    private static readonly int[] CanonicalMasks;

    private static readonly ReadOnlyCollection<int> Masks;

    private static readonly byte[] ClassOfMask;

    static Autotiler()
    {
        var classOf = new byte[256];
        var masks = new List<int>();
        var seen = new Dictionary<int, int>(BlobTileCount);

        for (var mask = 0; mask < 256; mask++)
        {
            var canonical = Canonicalise(mask);

            if (!seen.TryGetValue(canonical, out var index))
            {
                index = masks.Count;
                seen.Add(canonical, index);
                masks.Add(canonical);
            }

            classOf[mask] = (byte)index;
        }

        if (masks.Count != BlobTileCount)
        {
            throw new InvalidOperationException(
                $"The blob table has {masks.Count} classes, not {BlobTileCount}.");
        }

        CanonicalMasks = masks.ToArray();
        Masks = new ReadOnlyCollection<int>(CanonicalMasks);
        ClassOfMask = classOf;
    }

    /// <summary>Gets the 47 canonical eight-neighbour masks, in class order.</summary>
    public static IReadOnlyList<int> BlobMasks => Masks;

    /// <summary>Returns the blob tile class for an eight-neighbour open mask.</summary>
    /// <param name="mask">
    /// Bits, clockwise from north: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128.
    /// </param>
    /// <returns>A class in 0..46, the index of the canonical mask it reduces to.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the mask is outside 0..255.</exception>
    public static int BlobClass(int mask)
    {
        if (mask is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), mask, "An open mask has eight bits.");
        }

        return ClassOfMask[mask];
    }

    /// <summary>Draws a cardinally masked wall tile: 16 variants.</summary>
    /// <param name="theme">The theme whose colours and grit shape the tile.</param>
    /// <param name="openMask">Bits N=1, E=2, S=4, W=8 for each open side.</param>
    /// <returns>A 16x16 wall tile with floor showing through the open sides.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the mask is outside 0..15.</exception>
    public static PixelCanvas EdgeTile(Theme theme, int openMask)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (openMask is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(openMask), openMask, "An edge mask has four bits.");
        }

        var canvas = new PixelCanvas(CaveTiles.TileSize, CaveTiles.TileSize);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/edge-{openMask}"));

        canvas.Stamp(TileGrammar.Wall(theme), 0, 0, skipTransparent: false);

        var edges = 0;

        if ((openMask & 1) != 0)
        {
            edges |= North;
        }

        if ((openMask & 2) != 0)
        {
            edges |= East;
        }

        if ((openMask & 4) != 0)
        {
            edges |= South;
        }

        if ((openMask & 8) != 0)
        {
            edges |= West;
        }

        Carve(canvas, theme, random, edges, corners: 0);

        return canvas;
    }

    /// <summary>Draws a blob wall tile for a canonical eight-neighbour mask.</summary>
    /// <param name="theme">The theme whose colours and grit shape the tile.</param>
    /// <param name="mask">
    /// Bits, clockwise from north: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128. Any mask in
    /// 0..255 is accepted; it is canonicalised first, so two masks of the same class draw the same
    /// tile.
    /// </param>
    /// <returns>A 16x16 wall tile.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the mask is outside 0..255.</exception>
    public static PixelCanvas BlobTile(Theme theme, int mask)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (mask is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), mask, "An open mask has eight bits.");
        }

        var canonical = Canonicalise(mask);
        var canvas = new PixelCanvas(CaveTiles.TileSize, CaveTiles.TileSize);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/blob-{BlobClass(mask)}"));

        canvas.Stamp(TileGrammar.Wall(theme), 0, 0, skipTransparent: false);

        Carve(canvas, theme, random, canonical & EdgeBits, corners: canonical & CornerBits);

        return canvas;
    }

    /// <summary>
    /// Clears a corner bit unless both of its edges are open, which is what reduces 256 masks to 47.
    /// </summary>
    private static int Canonicalise(int mask)
    {
        if ((mask & North) == 0 || (mask & East) == 0)
        {
            mask &= ~CornerNorthEast;
        }

        if ((mask & South) == 0 || (mask & East) == 0)
        {
            mask &= ~CornerSouthEast;
        }

        if ((mask & South) == 0 || (mask & West) == 0)
        {
            mask &= ~CornerSouthWest;
        }

        if ((mask & North) == 0 || (mask & West) == 0)
        {
            mask &= ~CornerNorthWest;
        }

        return mask;
    }

    /// <summary>Carves the open sides (and lone diagonal corners) out of a wall tile.</summary>
    private static void Carve(PixelCanvas canvas, Theme theme, SeededRandom random, int edges, int corners)
    {
        var size = CaveTiles.TileSize;

        if ((edges & North) != 0)
        {
            Band(canvas, theme, random, 0, 0, size, BandWidth);
            Lip(canvas, 0, BandWidth - 1, size, horizontal: true);
        }

        if ((edges & South) != 0)
        {
            Band(canvas, theme, random, 0, size - BandWidth, size, BandWidth);
            Lip(canvas, 0, size - BandWidth, size, horizontal: true);
        }

        if ((edges & West) != 0)
        {
            Band(canvas, theme, random, 0, 0, BandWidth, size);
            Lip(canvas, BandWidth - 1, 0, size, horizontal: false);
        }

        if ((edges & East) != 0)
        {
            Band(canvas, theme, random, size - BandWidth, 0, BandWidth, size);
            Lip(canvas, size - BandWidth, 0, size, horizontal: false);
        }

        // A diagonal that is open while both of its edges are shut is a hole in the corner of the
        // rock, not a passage: three pixels of floor and a dark rim, so it reads as a gap to see
        // through rather than one to walk through.
        if ((corners & CornerNorthEast) != 0)
        {
            Notch(canvas, theme, random, size - 4, 0);
        }

        if ((corners & CornerSouthEast) != 0)
        {
            Notch(canvas, theme, random, size - 4, size - 4);
        }

        if ((corners & CornerSouthWest) != 0)
        {
            Notch(canvas, theme, random, 0, size - 4);
        }

        if ((corners & CornerNorthWest) != 0)
        {
            Notch(canvas, theme, random, 0, 0);
        }
    }

    /// <summary>Fills a rectangle of floor with a little grit, so a carved side is not a flat band.</summary>
    private static void Band(PixelCanvas canvas, Theme theme, SeededRandom random, int x, int y, int width, int height)
    {
        canvas.Rect(x, y, width, height, CaveTiles.Index(PaletteRole.Floor));
        canvas.Dither(x, y, width, height, CaveTiles.Index(PaletteRole.FloorDark), 1);

        var grit = Math.Max(1, (int)Math.Round(theme.SpeckleRate * width * height * 0.08));
        for (var speck = 0; speck < grit; speck++)
        {
            canvas.Set(x + random.NextInt(width), y + random.NextInt(height), CaveTiles.Index(PaletteRole.WallEdge));
        }
    }

    /// <summary>Draws the dark rim where a carved band meets the rock.</summary>
    private static void Lip(PixelCanvas canvas, int x, int y, int length, bool horizontal)
    {
        if (horizontal)
        {
            canvas.Rect(x, y, length, 1, CaveTiles.Index(PaletteRole.WallEdge));
            return;
        }

        canvas.Rect(x, y, 1, length, CaveTiles.Index(PaletteRole.WallEdge));
    }

    /// <summary>Carves a small diagonal peephole with a dark rim.</summary>
    private static void Notch(PixelCanvas canvas, Theme theme, SeededRandom random, int x, int y)
    {
        Band(canvas, theme, random, x, y, 4, 4);
        Lip(canvas, x, y + 3, 4, horizontal: true);
        Lip(canvas, x + 3, y, 4, horizontal: false);
    }
}

using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// The worked-stone family: flagstone floors, block walls and carved bands, i.e. the floors somebody
/// built and something later flooded.
/// </summary>
/// <remarks>
/// <para>
/// Every routine is a pure function of <c>(theme, variant)</c>, seeded from the theme id and the
/// tile's name, so a tile is identical in every process and a change in the drawing code shows up as
/// a changed PNG rather than as a random wobble.
/// </para>
/// <para>
/// Nothing reads a colour: the routines write <see cref="PaletteRole"/> values and the palette is
/// applied when the atlas is written. The difference between this family and the cave is
/// <b>geometry</b> — straight courses, offset joints, square corners — not a tint, which is what
/// makes the two floors read as different places.
/// </para>
/// </remarks>
public static class WorkedStoneTiles
{
    private const int Size = CaveTiles.TileSize;

    private const int BandWidth = 8;

    /// <summary>Draws one flagstone floor variant.</summary>
    /// <param name="theme">The theme whose courses, grit and glow shape the stones.</param>
    /// <param name="variant">The variant number, 0-based; decides the sheen, the carving and the joints.</param>
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

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/stone-floor-{variant}"));
        var course = CourseHeight(theme);

        canvas.Fill(Index(PaletteRole.Floor));
        canvas.Dither(0, 0, Size, Size, Index(PaletteRole.FloorDark), variant & 1);

        // Courses of flagstones with offset joints: straight lines, square corners, and a one-pixel
        // joint so a floor reads as laid slabs rather than as rubble.
        for (var row = 0; row < Size; row += course)
        {
            var bottom = Math.Min(row + course - 1, Size - 1);

            canvas.Rect(0, bottom, Size, 1, Index(PaletteRole.WallEdge));
            canvas.Dither(0, bottom - 1 < 0 ? 0 : bottom - 1, Size, 1, Index(PaletteRole.FloorDark), 1);

            var offset = (row / course) % 2 == 0 ? 0 : BandWidth / 2;

            for (var column = offset; column < Size; column += BandWidth)
            {
                if (column <= 0)
                {
                    continue;
                }

                canvas.Rect(column, row, 1, Math.Max(1, course - 1), Index(PaletteRole.WallEdge));
            }
        }

        if (theme.MossDensity > 0)
        {
            var tufts = 1 + (int)Math.Round(theme.MossDensity * 4);

            for (var tuft = 0; tuft < tufts; tuft++)
            {
                var column = random.NextInt(Size);
                var row = Math.Min(Size - 1, (random.NextInt(Size / course) * course) + course - 1);

                canvas.Rect(column, row, 1 + random.NextInt(2), 1, Index(PaletteRole.Moss));
            }
        }

        // Variant 2 is the cracked slab; the others carry the two signatures that make a flooded
        // hall read as flooded: standing water and a carved band.
        if (variant % 3 == 2)
        {
            canvas.Line(3, 4, 7, 9, Index(PaletteRole.WallEdge));
            canvas.Line(7, 9, 5, 13, Index(PaletteRole.WallEdge));
            canvas.Set(12, 3, Index(PaletteRole.FloorDark));
        }
        else if (variant % 3 == 1)
        {
            var band = Math.Max(2, CourseHeight(theme) - 2);

            for (var step = 2; step < Size - 2; step += 3)
            {
                canvas.Rect(step, band, 2, 1, Index(PaletteRole.Accent));
                canvas.Set(step, band + 1, Index(PaletteRole.Glow));
            }
        }
        else
        {
            var pools = 1 + random.NextInt(2);

            for (var pool = 0; pool < pools; pool++)
            {
                canvas.Ellipse(
                    2 + random.NextInt(Size - 4),
                    2 + random.NextInt(Size - 4),
                    2 + random.NextInt(2),
                    1,
                    Index(PaletteRole.Water));
            }
        }

        canvas.Speckle(Index(PaletteRole.FloorDark), theme.SpeckleRate * 0.30, random);
        canvas.Speckle(Index(PaletteRole.WallTop), theme.SpeckleRate * 0.08, random);

        var motes = (int)Math.Round(theme.GlowStrength * 2);

        for (var mote = 0; mote < motes; mote++)
        {
            canvas.Set(random.NextInt(Size), random.NextInt(Size), Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws the block wall: courses, lit tops, dark joints and a carved band.</summary>
    /// <param name="theme">The theme whose courses, grit and glow shape the blocks.</param>
    /// <returns>A wall tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Wall(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/stone-wall"));
        var course = CourseHeight(theme);

        canvas.Fill(Index(PaletteRole.Wall));

        for (var row = 0; row < Size; row += course)
        {
            var bottom = Math.Min(row + course - 1, Size - 1);

            // The top of each course catches the light; the joint below it is the shadow.
            canvas.Rect(0, row, Size, 1, Index(PaletteRole.WallTop));
            canvas.Rect(0, bottom, Size, 1, Index(PaletteRole.WallEdge));

            var offset = (row / course) % 2 == 0 ? 0 : BandWidth / 2;

            for (var column = offset; column < Size; column += BandWidth)
            {
                if (column <= 0)
                {
                    continue;
                }

                canvas.Rect(column, row, 1, Math.Max(1, course), Index(PaletteRole.WallEdge));

                // A chip out of a block corner, so the courses are cut stone and not a grid.
                if (random.NextInt(3) == 0)
                {
                    canvas.Set(column + 1, row + 1, Index(PaletteRole.WallEdge));
                    canvas.Set(column + 1, row, Index(PaletteRole.Wall));
                }
            }
        }

        // A carved band across the middle: the one flourish the family allows itself.
        var carve = Math.Clamp((Size / 2) - 1, 2, Size - 3);

        for (var column = 1; column < Size - 1; column += 2)
        {
            canvas.Set(column, carve, Index(PaletteRole.Accent));
        }

        canvas.Rect(0, carve - 1, Size, 1, Index(PaletteRole.WallEdge));
        canvas.Rect(0, carve + 1, Size, 1, Index(PaletteRole.WallEdge));

        if (theme.MossDensity > 0)
        {
            var drips = 1 + (int)Math.Round(theme.MossDensity * 4);

            for (var drip = 0; drip < drips; drip++)
            {
                var column = random.NextInt(Size);
                canvas.Rect(column, 1, 1, 1 + (int)Math.Round(theme.MossDensity * 3), Index(PaletteRole.Moss));
            }
        }

        var motes = (int)Math.Round(theme.GlowStrength * 2);

        for (var mote = 0; mote < motes; mote++)
        {
            canvas.Set(random.NextInt(Size), Size - 2 - random.NextInt(2), Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws one thin wall face: a course of cut stone with a lit rim and a shadowed foot.</summary>
    /// <param name="theme">The theme whose courses and grit shape the band.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Face(Theme theme, WallSide side)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/stone-strip-{side}"));

        // Offsets run from the cell's outer edge inward: the body, then a lit rim, then the
        // contact shadow that touches the floor. Same geometry as every other family.
        for (var offset = 0; offset < WallStrips.Thickness; offset++)
        {
            var role = offset switch
            {
                3 => PaletteRole.WallTop,
                4 => PaletteRole.WallEdge,
                _ => PaletteRole.Wall,
            };

            WallStrips.Paint(canvas, side, offset, Index(role));
        }

        // Masonry joints across the band: straight, evenly spaced, so the wall reads as blocks.
        var spacing = Math.Max(3, CourseHeight(theme) + 2);

        for (var along = spacing; along < Size; along += spacing)
        {
            WallStrips.Paint(canvas, side, 0, Index(PaletteRole.WallEdge), along);
            WallStrips.Paint(canvas, side, 1, Index(PaletteRole.WallEdge), along);
            WallStrips.Paint(canvas, side, 2, Index(PaletteRole.WallTop), along);
        }

        if (theme.MossDensity > 0)
        {
            var tufts = 1 + (int)Math.Round(theme.MossDensity * 3);

            for (var tuft = 0; tuft < tufts; tuft++)
            {
                WallStrips.Paint(canvas, side, 2, Index(PaletteRole.Moss), random.NextInt(Size));
            }
        }

        var motes = (int)Math.Round(theme.GlowStrength * 2);

        for (var mote = 0; mote < motes; mote++)
        {
            WallStrips.Paint(canvas, side, 1, Index(PaletteRole.Glow), random.NextInt(Size));
        }

        return canvas;
    }

    /// <summary>Draws one prop: a hanging chain, or a stone marker with a flame.</summary>
    /// <param name="theme">The theme whose palette roles are used.</param>
    /// <param name="index">0 for the chain, 1 for the marker.</param>
    /// <returns>A 16x16 sprite with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is not 0 or 1.</exception>
    public static PixelCanvas Prop(Theme theme, int index)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (index is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The worked-stone family draws two props.");
        }

        var canvas = new PixelCanvas(Size, Size);

        if (index == 0)
        {
            // A chain hanging from the ceiling: alternating links, so it reads as chain at 1x.
            for (var link = 0; link < 4; link++)
            {
                var top = 1 + (link * 4);
                var role = link % 2 == 0 ? PaletteRole.Bone : PaletteRole.WallTop;

                canvas.Rect(7, top, 2, 1, Index(role));
                canvas.Set(7, top + 1, Index(role));
                canvas.Set(8, top + 1, Index(role));
                canvas.Set(7, top + 2, Index(role));
                canvas.Set(8, top + 2, Index(role));
                canvas.Set(7, top + 3, Index(PaletteRole.WallEdge));
                canvas.Set(8, top + 3, Index(PaletteRole.WallEdge));
            }

            canvas.Rect(6, 0, 4, 1, Index(PaletteRole.WallEdge));
            canvas.Set(8, 15, Index(PaletteRole.Glow));

            return canvas;
        }

        // A stone marker with a lit bowl: the family's vertical accent, taller than it is wide.
        canvas.Rect(5, 9, 6, 2, Index(PaletteRole.WallTop));
        canvas.Rect(6, 11, 4, 4, Index(PaletteRole.Wall));
        canvas.Rect(6, 11, 4, 1, Index(PaletteRole.WallTop));
        canvas.Rect(6, 14, 4, 1, Index(PaletteRole.WallEdge));
        canvas.Rect(7, 5, 2, 4, Index(PaletteRole.WallEdge));
        canvas.Ellipse(8, 3, 2, 2, Index(PaletteRole.Glow));
        canvas.Set(8, 3, Index(PaletteRole.Accent));
        canvas.Rect(5, 8, 6, 1, Index(PaletteRole.Accent));
        canvas.Set(6, 12, Index(PaletteRole.Glow));

        return canvas;
    }

    /// <summary>Returns the theme's course height, clamped to something a tile can draw.</summary>
    /// <param name="theme">The theme to read.</param>
    /// <returns>A course height between 2 and 8 px.</returns>
    private static int CourseHeight(Theme theme) => Math.Clamp(theme.CourseHeight, 2, 8);

    /// <summary>Returns the palette index for a role, as a canvas stores it.</summary>
    /// <param name="role">The role to write.</param>
    /// <returns>The index of that role.</returns>
    private static byte Index(PaletteRole role) => CaveTiles.Index(role);
}

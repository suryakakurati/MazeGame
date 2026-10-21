using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// The wrong-organic family: floors of matted roots and veins, walls of knotted fibre, and the pods
/// that grow out of them.
/// </summary>
/// <remarks>
/// <para>
/// This family is a different <i>shape language</i>, not a different tint: the cave family draws
/// round boulders on grit, this one draws long wandering fibres with knots where they meet, so a
/// Rootbound floor cannot be mistaken for a re-coloured Shallows one at a glance.
/// </para>
/// <para>
/// Every routine writes <see cref="PaletteRole"/> values and takes its variation from
/// <c>(theme, variant)</c> and a seed built from the theme id and the tile name, so a tile is
/// identical on every machine and a change to the drawing code shows up as a changed PNG rather
/// than as a random wobble.
/// </para>
/// </remarks>
public static class WrongOrganicTiles
{
    private const int Size = CaveTiles.TileSize;

    /// <summary>Draws one floor variant: packed fibres, knots, dark gaps and sap beads.</summary>
    /// <param name="theme">The theme whose palette roles, density and glow are used.</param>
    /// <param name="variant">The variant number, 0-based; decides which way the fibres run.</param>
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
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/floor-{variant}"));

        canvas.Fill(Index(PaletteRole.Floor));

        // The membrane under the fibres: a dim checker so the surface is never a flat fill.
        canvas.Dither(0, 0, Size, Size, Index(PaletteRole.FloorDark), variant & 1);

        Fibres(canvas, theme, variant, random);

        // Knots where fibres meet: a body with a lit crown, so they read as thicker growth.
        var knots = 1 + (int)Math.Round(theme.VeinDensity * 2);

        for (var knot = 0; knot < knots; knot++)
        {
            var centreX = 2 + random.NextInt(Size - 4);
            var centreY = 2 + random.NextInt(Size - 4);
            var radius = 1 + random.NextInt(2);

            canvas.Ellipse(centreX, centreY, radius + 1, radius, Index(PaletteRole.Wall));
            canvas.Ellipse(centreX, centreY - 1, radius, Math.Max(1, radius - 1), Index(PaletteRole.WallTop));
        }

        canvas.Speckle(Index(PaletteRole.FloorDark), theme.SpeckleRate * 0.2, random);

        // Sap pools in the low spots and catches the light.
        var beads = (int)Math.Round(theme.GlowStrength * 3);

        for (var bead = 0; bead < beads; bead++)
        {
            canvas.Set(random.NextInt(Size), random.NextInt(Size), Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>Draws the solid wall: root mass with a lit ridge and bulbs growing out of it.</summary>
    /// <param name="theme">The theme whose palette roles and density are used.</param>
    /// <returns>A wall tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Wall(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/wall"));

        canvas.Fill(Index(PaletteRole.Wall));

        // Lit ridge on top and a dark foot below: the same convention every family uses, so walls
        // from different families still read as the same kind of object.
        canvas.Rect(0, 0, Size, 2, Index(PaletteRole.WallTop));
        canvas.Dither(0, 2, Size, 1, Index(PaletteRole.WallTop), 1);
        canvas.Rect(0, Size - 2, Size, 2, Index(PaletteRole.WallEdge));

        // Fibres knotted through the slab, drawn as bent polylines rather than straight scratches.
        var tangles = 2 + (int)Math.Round(theme.VeinDensity * 4);

        for (var tangle = 0; tangle < tangles; tangle++)
        {
            var startY = 2 + random.NextInt(Size - 5);
            var endY = Math.Clamp(startY + random.NextInt(7) - 3, 2, Size - 3);
            var midX = 4 + random.NextInt(Size - 8);

            canvas.Line(0, startY, midX, endY, Index(PaletteRole.WallEdge));
            canvas.Line(midX, endY, Size - 1, Math.Clamp(endY + random.NextInt(5) - 2, 1, Size - 2), Index(PaletteRole.Moss));
        }

        // Bulbs: a soft body and a bright centre, so the wall looks alive rather than carved.
        var bulbs = 1 + (int)Math.Round(theme.MossDensity * 2);

        for (var bulb = 0; bulb < bulbs; bulb++)
        {
            var centreX = 2 + random.NextInt(Size - 4);
            var centreY = 4 + random.NextInt(Size - 8);
            var radius = 1 + random.NextInt(2);

            canvas.Ellipse(centreX, centreY, radius + 1, radius, Index(PaletteRole.Accent));
            canvas.Set(centreX, centreY, Index(PaletteRole.Glow));
        }

        canvas.Speckle(Index(PaletteRole.WallEdge), theme.SpeckleRate * 0.12, random);

        return canvas;
    }

    /// <summary>Draws the thin wall face this family uses on a closed cell edge.</summary>
    /// <param name="theme">The theme whose palette roles and density are used.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Face(Theme theme, WallSide side)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/strip-{WallStrips.NameOf(side)}"));

        // Same cross-section as the cave's band (rock, lit rim, contact shadow), so a wall reads the
        // same from either family while its surface texture differs.
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

        var fibres = 2 + (int)Math.Round(theme.VeinDensity * 4);

        for (var fibre = 0; fibre < fibres; fibre++)
        {
            var along = random.NextInt(Size);
            var offset = 1 + random.NextInt(2);

            WallStrips.Paint(canvas, side, offset, Index(random.NextInt(3) == 0 ? PaletteRole.Moss : PaletteRole.WallEdge), along);
        }

        // Wet sap along the seam: the one bright thing on an otherwise matte band.
        var beads = 1 + (int)Math.Round(theme.GlowStrength * 2);

        for (var bead = 0; bead < beads; bead++)
        {
            WallStrips.Paint(canvas, side, 3, Index(PaletteRole.Accent), 1 + random.NextInt(Size - 2));
            WallStrips.Paint(canvas, side, 4, Index(PaletteRole.Glow), 1 + random.NextInt(Size - 2));
        }

        return canvas;
    }

    /// <summary>Draws one of the family's two props.</summary>
    /// <param name="theme">The theme whose palette roles and density are used.</param>
    /// <param name="index">
    /// 0 for a root knot with a sap bead, 1 for a hanging cluster of pods.
    /// </param>
    /// <returns>A 16x16 sprite with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 0 or 1.</exception>
    public static PixelCanvas Prop(Theme theme, int index)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (index is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "This family has two props.");
        }

        return index == 0 ? Knot(theme) : Pods(theme);
    }

    /// <summary>Draws a knot of roots with a bead of sap at its heart.</summary>
    private static PixelCanvas Knot(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/prop-0"));

        canvas.Ellipse(8, 14, 4, 1, Index(PaletteRole.Shadow));

        // Three overlapping bodies make a knot; the alternating shading is what makes the strands
        // legible at 16 px.
        canvas.Ellipse(7, 9, 4, 3, Index(PaletteRole.Wall));
        canvas.Ellipse(10, 10, 3, 3, Index(PaletteRole.WallEdge));
        canvas.Ellipse(8, 7, 3, 2, Index(PaletteRole.Wall));

        canvas.Line(4, 12, 2, 15, Index(PaletteRole.Moss));
        canvas.Line(12, 8, 14, 5, Index(PaletteRole.Moss));
        canvas.Line(9, 6, 12, 3, Index(PaletteRole.WallTop));

        canvas.Set(8, 9, Index(PaletteRole.Glow));
        canvas.Set(7, 9, Index(PaletteRole.Accent));
        canvas.Set(8, 10, Index(PaletteRole.Accent));

        if (theme.GlowStrength > 0.5)
        {
            canvas.Set(6, 8, Index(PaletteRole.Glow));
        }

        for (var speck = 0; speck < 1 + (int)Math.Round(theme.SpeckleRate * 4); speck++)
        {
            canvas.Set(3 + random.NextInt(10), 5 + random.NextInt(8), Index(PaletteRole.Moss));
        }

        return canvas;
    }

    /// <summary>Draws a hanging stem with two pods, the family's second prop.</summary>
    private static PixelCanvas Pods(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/prop-1"));

        canvas.Line(8, 0, 8, 6, Index(PaletteRole.WallEdge));
        canvas.Line(8, 6, 5, 10, Index(PaletteRole.WallEdge));
        canvas.Line(8, 6, 11, 9, Index(PaletteRole.WallEdge));

        canvas.Ellipse(5, 12, 2, 3, Index(PaletteRole.Flesh));
        canvas.Ellipse(5, 12, 1, 2, Index(PaletteRole.Accent));
        canvas.Set(5, 12, Index(PaletteRole.Glow));

        canvas.Ellipse(11, 11, 2, 3, Index(PaletteRole.Flesh));
        canvas.Ellipse(11, 11, 1, 1, Index(PaletteRole.Accent));
        canvas.Set(11, 11, Index(PaletteRole.Glow));

        canvas.Line(5, 15, 3, 15, Index(PaletteRole.Shadow));
        canvas.Line(11, 14, 13, 15, Index(PaletteRole.Shadow));

        if (theme.MossDensity > 0.25)
        {
            canvas.Line(9, 4, 12, 2, Index(PaletteRole.Moss));
        }

        for (var speck = 0; speck < 1 + (int)Math.Round(theme.GlowStrength * 2); speck++)
        {
            canvas.Set(2 + random.NextInt(12), 13 + random.NextInt(2), Index(PaletteRole.Glow));
        }

        return canvas;
    }

    /// <summary>
    /// Draws the floor's fibres: long wandering strands whose direction comes from the variant, so
    /// neighbouring cells of the same floor never repeat the same stamp.
    /// </summary>
    private static void Fibres(PixelCanvas canvas, Theme theme, int variant, SeededRandom random)
    {
        var strands = 2 + (int)Math.Round(theme.VeinDensity * 4);
        var direction = variant % 3;
        var main = Index(PaletteRole.Wall);
        var shade = Index(PaletteRole.WallEdge);
        var fibre = random.NextInt(3) == 0 ? Index(PaletteRole.Moss) : main;

        for (var strand = 0; strand < strands; strand++)
        {
            var start = 1 + random.NextInt(Size - 2);
            var bend = random.NextInt(5) - 2;
            var end = Math.Clamp(start + bend, 0, Size - 1);

            switch (direction)
            {
                case 0:
                    canvas.Line(0, start, Size - 1, end, strand % 2 == 0 ? fibre : main);
                    canvas.Line(0, start + 1, Size - 1, end + 1, shade);
                    break;

                case 1:
                    canvas.Line(start, 0, end, Size - 1, strand % 2 == 0 ? fibre : main);
                    canvas.Line(start + 1, 0, end + 1, Size - 1, shade);
                    break;

                default:
                    canvas.Line(0, start, Size - 1, end, strand % 2 == 0 ? fibre : main);
                    canvas.Line(start, 0, end, Size - 1, shade);
                    break;
            }
        }
    }

    private static byte Index(PaletteRole role) => CaveTiles.Index(role);
}

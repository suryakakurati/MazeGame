using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>
/// The motifs that make one floor's surface different from another's: ripples, drift, mosaic,
/// bone, crystal, silk, plate, mire, grate — plus the signature object and emblem each floor
/// carries.
/// </summary>
/// <remarks>
/// <para>
/// A theme is still data. This file contains the shapes that data can choose, exactly as
/// <see cref="CaveTiles"/>, <see cref="WorkedStoneTiles"/> and <see cref="WrongOrganicTiles"/>
/// contain three more. A motif is read at 16 px by its <i>silhouette</i>: horizontal water lines,
/// a 4x4 tessera grid, a bone laid across the cell, a grate's dark slits. Hue only decides which
/// place it is, not what kind of place it is.
/// </para>
/// <para>
/// Every routine is a pure function of the theme and a seed built from its id and the tile name,
/// so the atlas is byte-identical on every machine and a change to the drawing code turns into a
/// deterministic PNG diff rather than a random wobble.
/// </para>
/// </remarks>
public static class MotifTiles
{
    private const int Size = CaveTiles.TileSize;

    /// <summary>Draws one floor variant in the theme's chosen <see cref="FloorMotif"/>.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <param name="variant">The variant number, 0-based.</param>
    /// <returns>A 16x16 floor tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the variant is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown for a motif the three families already draw.</exception>
    public static PixelCanvas Floor(Theme theme, int variant)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (variant < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, "A variant number cannot be negative.");
        }

        return theme.Floor switch
        {
            FloorMotif.Ripples => Ripples(theme, variant),
            FloorMotif.Drift => Drift(theme, variant),
            FloorMotif.Mosaic => Mosaic(theme, variant),
            FloorMotif.Bones => Bones(theme, variant),
            FloorMotif.Shards => Shards(theme, variant),
            FloorMotif.Web => Web(theme, variant),
            FloorMotif.Plates => Plates(theme, variant),
            FloorMotif.Mire => Mire(theme, variant),
            FloorMotif.Vents => Vents(theme, variant),
            _ => throw new InvalidOperationException(
                $"'{theme.Floor}' is drawn by {theme.Family}, not by the motif grammar."),
        };
    }

    /// <summary>Draws one solid wall in the theme's chosen <see cref="WallMotif"/>.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <returns>A 16x16 wall tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown for a motif the three families already draw.</exception>
    public static PixelCanvas Wall(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return theme.Wall switch
        {
            WallMotif.Ribbed => Ribbed(theme),
            WallMotif.StackedPlate => StackedPlate(theme),
            WallMotif.Crystal => CrystalWall(theme),
            WallMotif.Silk => Silk(theme),
            WallMotif.Bone => BoneWall(theme),
            WallMotif.Grate => GrateWall(theme),
            WallMotif.Frost => Frost(theme),
            _ => throw new InvalidOperationException(
                $"'{theme.Wall}' is drawn by {theme.Family}, not by the motif grammar."),
        };
    }

    /// <summary>Draws one thin wall face for a motif this file owns.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Face(Theme theme, WallSide side)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/motif-strip-{WallStrips.NameOf(side)}"));

        // The shared cross-section: body, body, lit rim, contact shadow. WallStrips repaints the
        // outer edge, the rim and the foot afterwards, so every family has the same silhouette.
        for (var offset = 0; offset < WallStrips.Thickness; offset++)
        {
            var role = offset switch
            {
                3 => PaletteRole.WallTop,
                4 => PaletteRole.WallEdge,
                _ => PaletteRole.Wall,
            };

            WallStrips.Paint(canvas, side, offset, I(role));
        }

        switch (theme.Wall)
        {
            case WallMotif.Ribbed:
                for (var along = 2; along < Size; along += 3)
                {
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.WallTop), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.WallEdge), along);
                }

                break;

            case WallMotif.StackedPlate:
                for (var along = 4; along < Size; along += 5)
                {
                    WallStrips.Paint(canvas, side, 0, I(PaletteRole.WallEdge), along);
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.WallTop), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.Accent), along);
                }

                break;

            case WallMotif.Crystal:
                for (var along = 1; along < Size; along += 4)
                {
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.WallTop), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.Accent), along + 1);
                    WallStrips.Paint(canvas, side, 3, I(PaletteRole.WallEdge), along + 2);
                }

                break;

            case WallMotif.Silk:
                for (var along = 1; along < Size; along += 3)
                {
                    WallStrips.Paint(canvas, side, 1 + (along % 2), I(PaletteRole.WallEdge), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.Moss), along + 1);
                }

                break;

            case WallMotif.Bone:
                // Stacked bone is a pale wall: the two body rows are bone and lit bone, which is
                // also what keeps the band clearly brighter than a floor full of bone chips.
                WallStrips.Paint(canvas, side, 1, I(PaletteRole.Bone));
                WallStrips.Paint(canvas, side, 2, I(PaletteRole.Bone));

                for (var along = 2; along < Size; along += 6)
                {
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.WallTop), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.Bone), along + 2);
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.Wall), along + 4);
                }

                break;

            case WallMotif.Grate:
                for (var along = 2; along < Size; along += 4)
                {
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.WallEdge), along);
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.Glow), along + 1);
                    WallStrips.Paint(canvas, side, 3, I(PaletteRole.WallTop), along + 1);
                }

                break;

            case WallMotif.Frost:
                for (var along = 1; along < Size; along += 4)
                {
                    WallStrips.Paint(canvas, side, 1, I(PaletteRole.Bone), along);
                    WallStrips.Paint(canvas, side, 2, I(PaletteRole.WallTop), along + 1);
                    WallStrips.Paint(canvas, side, 3, I(PaletteRole.Accent), along + 3);
                }

                break;

            default:
                break;
        }

        if (theme.GlowStrength > 0)
        {
            var beads = (int)Math.Round(theme.GlowStrength * 2);

            for (var bead = 0; bead < beads; bead++)
            {
                WallStrips.Paint(canvas, side, 3, I(PaletteRole.Glow), 1 + random.NextInt(Size - 2));
            }
        }

        // A twelve-pixel maker's mark along the body row: every theme's id is pressed into its
        // band, so two floors that share a wall grammar still cannot draw the same wall face.
        var mark = CaveTiles.Seed($"{theme.Id}/face-mark-{WallStrips.NameOf(side)}");
        var zero = theme.Wall == WallMotif.Bone ? PaletteRole.Bone : PaletteRole.WallTop;

        for (var bit = 0; bit < 12; bit++)
        {
            WallStrips.Paint(
                canvas,
                side,
                1,
                I(((mark >> bit) & 1) == 0 ? zero : PaletteRole.Accent),
                bit + 1);
        }

        return canvas;
    }

    /// <summary>Draws the floor's signature object on a transparent 16x16 cell.</summary>
    /// <param name="theme">The theme whose feature is drawn.</param>
    /// <returns>A 16x16 sprite with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Feature(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = theme.Feature switch
        {
            FeatureKind.Pool => Pool(theme),
            FeatureKind.Nest => Nest(theme),
            FeatureKind.Rune => Rune(theme),
            FeatureKind.Grate => Grate(theme),
            FeatureKind.Clock => Clock(theme),
            FeatureKind.Mirror => Mirror(theme),
            FeatureKind.Crystal => Crystal(theme),
            FeatureKind.Web => CornerWeb(theme),
            FeatureKind.Bones => BonePile(theme),
            FeatureKind.Seal => Seal(theme),
            _ => Heart(theme),
        };

        // The signature object's own maker's mark, in its bottom row. Eleven features are shared
        // by twenty floors, so this is what makes two pools on two floors different artifacts.
        var mark = CaveTiles.Seed($"{theme.Id}/feature-mark");

        for (var bit = 0; bit < 12; bit++)
        {
            canvas.Set(bit + 2, Size - 1, I(((mark >> bit) & 1) == 0 ? PaletteRole.WallEdge : PaletteRole.Accent));
        }

        return canvas;
    }

    /// <summary>
    /// Draws the floor's emblem: a small, mirrored glyph unique to this theme, used as a rare
    /// carved mark in the maze and as the motif on its cards.
    /// </summary>
    /// <param name="theme">The theme whose emblem is drawn.</param>
    /// <returns>A 16x16 sprite with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Sigil(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/sigil"));

        // The spine and the ring are shared so every emblem reads as a mark; the branches are
        // seeded by the id, which is what makes each floor's mark its own.
        canvas.Rect(7, 3, 2, 10, I(PaletteRole.Accent));
        canvas.Set(7, 2, I(PaletteRole.Glow));
        canvas.Set(8, 13, I(PaletteRole.Glow));
        Ring(canvas, 8, 7, 4, I(PaletteRole.Glow));

        for (var branch = 0; branch < 3; branch++)
        {
            var y = 4 + random.NextInt(6);
            var length = 2 + random.NextInt(3);

            for (var step = 1; step <= length; step++)
            {
                canvas.Set(Math.Max(0, 7 - step), y - (step / 2), I(PaletteRole.WallTop));
                canvas.Set(Math.Min(Size - 1, 8 + step), y - (step / 2), I(PaletteRole.WallTop));
            }

            canvas.Set(Math.Max(0, 6 - length), y - (length / 2), I(PaletteRole.Glow));
            canvas.Set(Math.Min(Size - 1, 9 + length), y - (length / 2), I(PaletteRole.Glow));
        }

        // A base and two feet: the mark stands on something rather than floating in a cell.
        canvas.Rect(4, 13, 8, 1, I(PaletteRole.WallEdge));
        canvas.Set(4, 12, I(PaletteRole.WallTop));
        canvas.Set(11, 12, I(PaletteRole.WallTop));

        // The feature pixel is a single hint at what the floor is about.
        canvas.Set(8, 7, I(PaletteRole.UiText));

        return canvas;
    }

    private static PixelCanvas Ripples(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/ripples-{variant}"));

        for (var line = 0; line < 4; line++)
        {
            var baseY = 2 + (line * 4) + ((variant + line) % 2);
            var colour = line % 2 == 0 ? PaletteRole.Water : PaletteRole.FloorDark;
            var crest = line % 2 == 0 ? PaletteRole.WallTop : PaletteRole.Water;

            for (var x = 0; x < Size; x++)
            {
                var lift = ((x + (line * 3)) % 6 < 3) ? 0 : 1;
                canvas.Set(x, Math.Clamp(baseY + lift, 0, Size - 1), I(colour));

                if (x % 5 == 0)
                {
                    canvas.Set(x, Math.Clamp(baseY + 1 + lift, 0, Size - 1), I(crest));
                }
            }
        }

        for (var glint = 0; glint < 2; glint++)
        {
            canvas.Set(2 + random.NextInt(Size - 4), 2 + random.NextInt(Size - 4), I(PaletteRole.Glow));
        }

        if (variant % 2 == 1)
        {
            canvas.Ellipse(11, 12, 3, 1, I(PaletteRole.Water));
            canvas.Set(11, 12, I(PaletteRole.Glow));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Drift(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/drift-{variant}"));

        // Long, low dunes: a lit crest over a dark trough. The direction alternates by variant so
        // neighbouring cells do not all lean the same way.
        var lean = variant % 2 == 0 ? 1 : -1;

        for (var dune = 0; dune < 3; dune++)
        {
            var y = 3 + (dune * 4);
            var start = random.NextInt(4);

            for (var x = start; x < Size - 2; x += 1)
            {
                var rise = Math.Clamp(y + ((x - start) / 6 * lean), 0, Size - 1);
                canvas.Set(x, rise, I(PaletteRole.WallTop));
                canvas.Set(x, Math.Clamp(rise + 1, 0, Size - 1), I(PaletteRole.FloorDark));
            }
        }

        // A few pebbles poking through the drift; they catch the accent rather than the stone.
        for (var stone = 0; stone < 3; stone++)
        {
            var x = 2 + random.NextInt(Size - 4);
            var y = 2 + random.NextInt(Size - 4);

            canvas.Ellipse(x, y, 1, 1, I(PaletteRole.WallEdge));
            canvas.Set(x - 1, y - 1, I(PaletteRole.Accent));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Mosaic(Theme theme, int variant)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/mosaic-{variant}"));

        // Grout is the ground; every tessera is one pixel away from its neighbours.
        canvas.Fill(I(PaletteRole.FloorDark));

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var x = (column * 4) + 1;
                var y = (row * 4) + 1;
                var pick = random.NextInt(10);

                var role = pick switch
                {
                    0 => PaletteRole.WallTop,
                    1 => PaletteRole.FloorDark,
                    2 => PaletteRole.Accent,
                    _ => PaletteRole.Floor,
                };

                canvas.Rect(x, y, 3, 3, I(role));

                // A lit upper edge on some tesserae: the floor reads as a laid surface, not a
                // checkerboard.
                if (pick > 2)
                {
                    canvas.Rect(x, y, 3, 1, I(PaletteRole.WallTop));
                }
            }
        }

        if (variant % 2 == 1)
        {
            canvas.Line(3, 6, 8, 11, I(PaletteRole.WallEdge));
            canvas.Line(8, 11, 12, 9, I(PaletteRole.WallEdge));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Bones(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/bones-{variant}"));

        for (var bone = 0; bone < 3; bone++)
        {
            var y = 3 + (bone * 4) + random.NextInt(2);
            var startX = 1 + random.NextInt(3);
            var endX = Size - 2 - random.NextInt(3);

            // A dark socket under the bone makes it read as lying on the floor, not printed on it.
            canvas.Line(startX, y + 1, endX, y + 1, I(PaletteRole.WallEdge));
            canvas.Line(startX, y, endX, y, I(PaletteRole.Bone));
            canvas.Line(startX + 1, y - 1, endX - 1, y - 1, I(PaletteRole.WallTop));

            canvas.Ellipse(startX, y, 1, 1, I(PaletteRole.Bone));
            canvas.Ellipse(endX, y, 1, 1, I(PaletteRole.Bone));
            canvas.Set(startX, y - 1, I(PaletteRole.WallTop));
            canvas.Set(endX, y - 1, I(PaletteRole.WallTop));

            if (bone == 1)
            {
                canvas.Set((startX + endX) / 2, y, I(PaletteRole.WallEdge));
            }
        }

        for (var chip = 0; chip < 2; chip++)
        {
            canvas.Set(2 + random.NextInt(Size - 4), 2 + random.NextInt(Size - 4), I(PaletteRole.Accent));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Shards(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/shards-{variant}"));

        for (var shard = 0; shard < 3; shard++)
        {
            var x = 3 + (shard * 4) + random.NextInt(2);
            var height = 4 + random.NextInt(5);
            var top = 2 + random.NextInt(3);

            // A rhombus drawn as stacked rows: it reads as a faceted crystal at 16 px and does
            // not depend on a polygon helper the canvas does not have.
            for (var row = 0; row < height; row++)
            {
                var width = 1 + (int)Math.Round(Math.Sin(Math.PI * row / Math.Max(1, height - 1)) * 2.0);

                canvas.Rect(x - (width / 2), top + row, Math.Max(1, width), 1, I(row < height / 2 ? PaletteRole.WallTop : PaletteRole.Wall));
            }

            canvas.Set(x, top, I(PaletteRole.Glow));
            canvas.Set(x + 1, top + (height / 2), I(PaletteRole.Accent));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Web(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/web-{variant}"));

        var anchorX = variant % 2 == 0 ? 2 : Size - 3;
        var anchorY = variant < 2 ? 2 : Size - 3;

        for (var spoke = 0; spoke < 5; spoke++)
        {
            var angle = (Math.PI * 2 * spoke / 5.0) + (variant * 0.4);
            var endX = (int)Math.Round(anchorX + (Math.Cos(angle) * 13));
            var endY = (int)Math.Round(anchorY + (Math.Sin(angle) * 13));

            canvas.Line(anchorX, anchorY, endX, endY, I(PaletteRole.Moss));
        }

        for (var ring = 1; ring <= 2; ring++)
        {
            var radius = ring * 4;
            var points = 10;

            for (var point = 0; point < points; point++)
            {
                var angle = Math.PI * 2 * point / points;
                var nextAngle = Math.PI * 2 * (point + 1) / points;

                canvas.Line(
                    (int)Math.Round(anchorX + (Math.Cos(angle) * radius)),
                    (int)Math.Round(anchorY + (Math.Sin(angle) * radius)),
                    (int)Math.Round(anchorX + (Math.Cos(nextAngle) * radius)),
                    (int)Math.Round(anchorY + (Math.Sin(nextAngle) * radius)),
                    I(PaletteRole.WallTop));
            }
        }

        canvas.Set(Math.Clamp(anchorX + 5, 0, Size - 1), Math.Clamp(anchorY + 6, 0, Size - 1), I(PaletteRole.Glow));
        canvas.Set(Math.Clamp(anchorX - 3, 0, Size - 1), Math.Clamp(anchorY + 4, 0, Size - 1), I(PaletteRole.Glow));

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Plates(Theme theme, int variant)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/plates-{variant}"));

        canvas.Fill(I(PaletteRole.WallEdge));

        for (var row = 0; row < 3; row++)
        {
            var top = 1 + (row * 5);
            var offset = row % 2 == 0 ? 1 : 3;

            for (var column = 0; column < 2; column++)
            {
                var x = offset + (column * 8);

                canvas.Rect(x, top, 6, 4, I(PaletteRole.Floor));
                canvas.Rect(x, top, 6, 1, I(PaletteRole.WallTop));
                canvas.Rect(x, top + 3, 6, 1, I(PaletteRole.FloorDark));
                canvas.Set(x + 1, top + 1, I(PaletteRole.Accent));
                canvas.Set(x + 4, top + 1, I(PaletteRole.Accent));
            }
        }

        // Rust bleeding down from the seams: two short, bent stains.
        for (var stain = 0; stain < 2; stain++)
        {
            var x = 3 + random.NextInt(Size - 6);
            var y = 4 + random.NextInt(5);

            canvas.Line(x, y, x + 1, y + 3, I(PaletteRole.Moss));
            canvas.Set(x + 1, y + 3, I(PaletteRole.Accent));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Mire(Theme theme, int variant)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/mire-{variant}"));

        canvas.Fill(I(PaletteRole.FloorDark));

        for (var pool = 0; pool < 4; pool++)
        {
            var x = 2 + random.NextInt(Size - 4);
            var y = 2 + random.NextInt(Size - 4);

            canvas.Ellipse(x, y, 2 + random.NextInt(3), 1 + random.NextInt(2), I(PaletteRole.Floor));
            canvas.Ellipse(x - 1, y - 1, 1 + random.NextInt(2), 1, I(PaletteRole.Moss));
        }

        for (var bubble = 0; bubble < 3; bubble++)
        {
            var x = 2 + random.NextInt(Size - 4);
            var y = 2 + random.NextInt(Size - 4);

            canvas.Set(x, y, I(PaletteRole.Water));
            canvas.Set(x, y - 1, I(PaletteRole.WallTop));

            if (bubble == 0)
            {
                canvas.Set(x + 1, y, I(PaletteRole.Glow));
            }
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Vents(Theme theme, int variant)
    {
        var canvas = Base(theme, variant);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/vents-{variant}"));

        for (var row = 0; row < 3; row++)
        {
            var top = 2 + (row * 5);

            canvas.Rect(1, top, 14, 3, I(PaletteRole.WallEdge));
            canvas.Rect(1, top, 14, 1, I(PaletteRole.WallTop));

            for (var x = 2; x < Size - 1; x += 3)
            {
                var lit = (x + variant) % 4 == 0;
                canvas.Set(x, top + 1, I(lit ? PaletteRole.Glow : PaletteRole.FloorDark));
                canvas.Set(x + 1, top + 1, I(PaletteRole.FloorDark));
            }

            canvas.Rect(1, top + 2, 14, 1, I(PaletteRole.WallEdge));
            canvas.Set(1, top, I(PaletteRole.Accent));
            canvas.Set(14, top, I(PaletteRole.Accent));
        }

        for (var ember = 0; ember < 2; ember++)
        {
            canvas.Set(1 + random.NextInt(Size - 2), 12 + random.NextInt(3), I(PaletteRole.Hazard));
        }

        Speckle(canvas, theme, random);
        return canvas;
    }

    private static PixelCanvas Ribbed(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/ribbed-wall"));

        canvas.Fill(I(PaletteRole.Wall));

        // Vertical ribs: a lit crown, a body and a dark groove between each pair.
        for (var x = 0; x < Size; x += 3)
        {
            canvas.Rect(x, 0, 1, Size, I(PaletteRole.WallTop));
            canvas.Rect(x + 1, 0, 1, Size, I(PaletteRole.Wall));
            canvas.Rect(x + 2, 0, 1, Size, I(PaletteRole.WallEdge));
        }

        canvas.Rect(0, 0, Size, 2, I(PaletteRole.WallTop));
        canvas.Rect(0, Size - 2, Size, 2, I(PaletteRole.WallEdge));

        var beads = 2 + (int)Math.Round(theme.GlowStrength * 2);

        for (var bead = 0; bead < beads; bead++)
        {
            var x = 1 + random.NextInt(Size / 3) * 3;
            canvas.Set(x, 2 + random.NextInt(Size - 5), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas StackedPlate(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/plate-wall"));

        canvas.Fill(I(PaletteRole.Wall));

        for (var row = 0; row < 4; row++)
        {
            var top = row * 4;

            canvas.Rect(0, top, Size, 1, I(PaletteRole.WallTop));
            canvas.Rect(0, top + 3, Size, 1, I(PaletteRole.WallEdge));
            canvas.Set(2, top + 1, I(PaletteRole.Accent));
            canvas.Set(13, top + 1, I(PaletteRole.Accent));
        }

        for (var streak = 0; streak < 2; streak++)
        {
            var x = 3 + random.NextInt(Size - 6);
            canvas.Line(x, 2 + random.NextInt(6), x + 1, 8 + random.NextInt(6), I(PaletteRole.Moss));
        }

        return canvas;
    }

    private static PixelCanvas CrystalWall(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/crystal-wall"));

        canvas.Fill(I(PaletteRole.Wall));

        // Facets: diagonals that cross, with one bright cut edge each.
        for (var facet = 0; facet < 4; facet++)
        {
            var startX = facet * 4;
            canvas.Line(startX, 0, Math.Min(Size - 1, startX + 8), Size - 1, I(PaletteRole.WallEdge));
            canvas.Line(Math.Min(Size - 1, startX + 1), 0, Math.Min(Size - 1, startX + 9), Size - 1, I(PaletteRole.WallTop));
        }

        canvas.Rect(0, 0, Size, 2, I(PaletteRole.WallTop));
        canvas.Rect(0, Size - 2, Size, 2, I(PaletteRole.WallEdge));

        for (var glint = 0; glint < 3; glint++)
        {
            canvas.Set(1 + random.NextInt(Size - 2), 2 + random.NextInt(Size - 4), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Silk(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/silk-wall"));

        canvas.Fill(I(PaletteRole.Wall));

        for (var x = 0; x < Size; x += 4)
        {
            canvas.Line(x, 0, Math.Min(Size - 1, x + 7), Size - 1, I(PaletteRole.WallEdge));
            canvas.Line(x, Size - 1, Math.Min(Size - 1, x + 7), 0, I(PaletteRole.Moss));
        }

        canvas.Rect(0, 0, Size, 2, I(PaletteRole.WallTop));
        canvas.Rect(0, Size - 2, Size, 2, I(PaletteRole.WallEdge));

        for (var strand = 0; strand < 3; strand++)
        {
            var x = 2 + random.NextInt(Size - 4);
            var length = 3 + random.NextInt(4);

            canvas.Rect(x, 0, 1, length, I(PaletteRole.WallTop));
            canvas.Set(x, length, I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas BoneWall(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/bone-wall"));

        canvas.Fill(I(PaletteRole.WallEdge));

        for (var row = 0; row < 4; row++)
        {
            var top = 1 + (row * 4);

            canvas.Rect(0, top, Size, 2, I(PaletteRole.Bone));
            canvas.Rect(0, top, Size, 1, I(PaletteRole.WallTop));
            canvas.Ellipse(1, top + 1, 1, 1, I(PaletteRole.Bone));
            canvas.Ellipse(Size - 2, top + 1, 1, 1, I(PaletteRole.Bone));

            var joint = 3 + random.NextInt(Size - 6);
            canvas.Rect(joint, top, 1, 2, I(PaletteRole.WallEdge));
        }

        for (var fleck = 0; fleck < 2; fleck++)
        {
            canvas.Set(1 + random.NextInt(Size - 2), 1 + random.NextInt(Size - 2), I(PaletteRole.Moss));
        }

        return canvas;
    }

    private static PixelCanvas GrateWall(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/grate-wall"));

        canvas.Fill(I(PaletteRole.WallEdge));

        for (var x = 1; x < Size; x += 4)
        {
            canvas.Rect(x, 0, 2, Size, I(PaletteRole.Wall));
            canvas.Rect(x, 0, 1, Size, I(PaletteRole.WallTop));
        }

        canvas.Rect(0, 0, Size, 2, I(PaletteRole.WallTop));
        canvas.Rect(0, Size - 2, Size, 2, I(PaletteRole.WallEdge));

        for (var glow = 0; glow < 4; glow++)
        {
            canvas.Set(2 + random.NextInt(3) * 4, 2 + random.NextInt(Size - 4), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Frost(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/frost-wall"));

        canvas.Fill(I(PaletteRole.Wall));

        // Radially cracked ice: long bright fractures over a pale body.
        for (var crack = 0; crack < 5; crack++)
        {
            var x = random.NextInt(Size);
            var y = random.NextInt(Size);

            canvas.Line(x, y, Math.Clamp(x + random.NextInt(9) - 4, 0, Size - 1), Math.Clamp(y + random.NextInt(9) - 4, 0, Size - 1), I(PaletteRole.WallTop));
            canvas.Set(x, y, I(PaletteRole.Bone));
        }

        canvas.Rect(0, 0, Size, 2, I(PaletteRole.Bone));
        canvas.Rect(0, Size - 2, Size, 2, I(PaletteRole.WallEdge));

        for (var glint = 0; glint < 2; glint++)
        {
            canvas.Set(1 + random.NextInt(Size - 2), 2 + random.NextInt(Size - 4), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Pool(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-pool"));

        canvas.Ellipse(8, 13, 6, 2, I(PaletteRole.Shadow));
        canvas.Ellipse(8, 10, 6, 3, I(PaletteRole.WallTop));
        canvas.Ellipse(8, 10, 5, 2, I(PaletteRole.Water));

        for (var glint = 0; glint < 3; glint++)
        {
            canvas.Set(5 + random.NextInt(6), 9 + random.NextInt(2), I(PaletteRole.Glow));
        }

        canvas.Set(3, 10, I(PaletteRole.Accent));
        canvas.Set(13, 9, I(PaletteRole.Accent));

        return canvas;
    }

    private static PixelCanvas Nest(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-nest"));

        canvas.Ellipse(8, 11, 6, 3, I(PaletteRole.Shadow));
        canvas.Ellipse(8, 10, 6, 3, I(PaletteRole.WallEdge));

        for (var fibre = 0; fibre < 9; fibre++)
        {
            var angle = Math.PI * 2 * fibre / 9;

            canvas.Line(
                8,
                10,
                (int)Math.Round(8 + (Math.Cos(angle) * 6)),
                (int)Math.Round(10 + (Math.Sin(angle) * 3)),
                I(PaletteRole.Moss));
        }

        canvas.Ellipse(6, 9, 1, 2, I(PaletteRole.Bone));
        canvas.Ellipse(9, 8, 1, 2, I(PaletteRole.Bone));
        canvas.Ellipse(11, 9, 1, 1, I(PaletteRole.Accent));
        canvas.Set(6, 8, I(PaletteRole.Glow));
        canvas.Set(9, 7, I(PaletteRole.Glow));

        if (theme.GlowStrength > 0.6)
        {
            canvas.Set(4, 8, I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Rune(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-rune"));

        canvas.Ellipse(8, 13, 5, 1, I(PaletteRole.Shadow));
        Ring(canvas, 8, 8, 5, I(PaletteRole.WallEdge));
        Ring(canvas, 8, 8, 4, I(PaletteRole.Accent));

        canvas.Line(8, 4, 8, 12, I(PaletteRole.Glow));
        canvas.Line(5, 7, 11, 7, I(PaletteRole.Glow));
        canvas.Line(6, 10, 8, 12, I(PaletteRole.WallTop));
        canvas.Line(10, 10, 8, 12, I(PaletteRole.WallTop));

        for (var crack = 0; crack < 2; crack++)
        {
            var x = 2 + random.NextInt(12);
            canvas.Set(x, 13, I(PaletteRole.WallEdge));
        }

        return canvas;
    }

    private static PixelCanvas Grate(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-grate"));

        canvas.Rect(2, 5, 12, 9, I(PaletteRole.WallEdge));
        canvas.Rect(3, 6, 10, 7, I(PaletteRole.FloorDark));

        for (var slot = 0; slot < 3; slot++)
        {
            canvas.Rect(4, 7 + (slot * 2), 8, 1, I(PaletteRole.WallEdge));

            if (slot == 1)
            {
                canvas.Rect(5, 9, 3, 1, I(PaletteRole.Hazard));
                canvas.Set(8, 9, I(PaletteRole.Glow));
            }
        }

        canvas.Rect(2, 5, 12, 1, I(PaletteRole.WallTop));
        canvas.Set(2, 5, I(PaletteRole.Accent));
        canvas.Set(13, 5, I(PaletteRole.Accent));
        canvas.Set(2, 13, I(PaletteRole.Accent));
        canvas.Set(13, 13, I(PaletteRole.Accent));

        for (var ember = 0; ember < 2; ember++)
        {
            canvas.Set(4 + random.NextInt(9), 6 + random.NextInt(2), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Clock(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-clock"));

        canvas.Ellipse(8, 14, 5, 1, I(PaletteRole.Shadow));
        Ring(canvas, 8, 8, 6, I(PaletteRole.WallTop));
        canvas.Ellipse(8, 8, 5, 5, I(PaletteRole.FloorDark));

        for (var tick = 0; tick < 12; tick++)
        {
            var angle = Math.PI * 2 * tick / 12;
            var x = (int)Math.Round(8 + (Math.Cos(angle) * 4));
            var y = (int)Math.Round(8 + (Math.Sin(angle) * 4));

            canvas.Set(x, y, tick % 3 == 0 ? I(PaletteRole.Glow) : I(PaletteRole.WallTop));
        }

        canvas.Line(8, 8, 8, 5, I(PaletteRole.Accent));
        canvas.Line(8, 8, 11, 9, I(PaletteRole.Glow));
        canvas.Set(8, 8, I(PaletteRole.UiText));

        for (var gear = 0; gear < 2; gear++)
        {
            canvas.Set(1 + random.NextInt(3), 2 + random.NextInt(3), I(PaletteRole.WallEdge));
        }

        return canvas;
    }

    private static PixelCanvas Mirror(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-mirror"));

        canvas.Ellipse(8, 14, 5, 1, I(PaletteRole.Shadow));
        canvas.Rect(4, 2, 8, 11, I(PaletteRole.WallEdge));
        canvas.Rect(5, 3, 6, 9, I(PaletteRole.Water));
        canvas.Line(6, 4, 9, 11, I(PaletteRole.Bone));
        canvas.Line(9, 4, 6, 11, I(PaletteRole.Accent));
        canvas.Rect(4, 12, 8, 1, I(PaletteRole.WallTop));

        for (var glare = 0; glare < 2; glare++)
        {
            canvas.Set(6 + random.NextInt(4), 4 + random.NextInt(7), I(PaletteRole.Glow));
        }

        return canvas;
    }

    private static PixelCanvas Crystal(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-crystal"));

        canvas.Ellipse(8, 14, 5, 1, I(PaletteRole.Shadow));

        for (var shard = 0; shard < 3; shard++)
        {
            var x = 4 + (shard * 4);
            var top = 3 + (shard % 2);
            var height = 8 + random.NextInt(3);

            for (var row = 0; row < height; row++)
            {
                var width = 1 + (int)Math.Round(Math.Sin(Math.PI * row / Math.Max(1, height - 1)) * 2.0);
                canvas.Rect(x - (width / 2), top + row, Math.Max(1, width), 1, I(row < height / 2 ? PaletteRole.WallTop : PaletteRole.Accent));
            }

            canvas.Set(x, top, I(PaletteRole.Glow));
        }

        canvas.Set(7, 12, I(PaletteRole.WallEdge));
        canvas.Set(10, 13, I(PaletteRole.WallEdge));

        return canvas;
    }

    private static PixelCanvas CornerWeb(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);

        for (var spoke = 0; spoke < 4; spoke++)
        {
            var angle = (Math.PI / 2 * spoke) + 0.35;
            canvas.Line(
                2,
                2,
                Math.Clamp(2 + (int)Math.Round(Math.Cos(angle) * 15), 0, Size - 1),
                Math.Clamp(2 + (int)Math.Round(Math.Sin(angle) * 15), 0, Size - 1),
                I(PaletteRole.Moss));
        }

        for (var ring = 1; ring <= 2; ring++)
        {
            var radius = ring * 4;

            for (var point = 0; point < 6; point++)
            {
                var angle = Math.PI / 2 * (point / 6.0);
                var next = Math.PI / 2 * ((point + 1) / 6.0);

                canvas.Line(
                    2 + (int)Math.Round(Math.Cos(angle) * radius),
                    2 + (int)Math.Round(Math.Sin(angle) * radius),
                    2 + (int)Math.Round(Math.Cos(next) * radius),
                    2 + (int)Math.Round(Math.Sin(next) * radius),
                    I(PaletteRole.WallTop));
            }
        }

        canvas.Set(8, 7, I(PaletteRole.Glow));
        canvas.Set(5, 10, I(PaletteRole.Glow));

        return canvas;
    }

    private static PixelCanvas BonePile(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-bones"));

        canvas.Ellipse(8, 13, 6, 2, I(PaletteRole.Shadow));

        canvas.Line(2, 11, 13, 13, I(PaletteRole.Bone));
        canvas.Line(3, 12, 12, 14, I(PaletteRole.WallTop));
        canvas.Line(4, 13, 14, 10, I(PaletteRole.Bone));

        // A small skull: a round body, two sockets and a jaw, readable at 16 px.
        canvas.Ellipse(8, 7, 4, 4, I(PaletteRole.Bone));
        canvas.Rect(5, 9, 6, 2, I(PaletteRole.Bone));
        canvas.Set(6, 6, I(PaletteRole.Shadow));
        canvas.Set(9, 6, I(PaletteRole.Shadow));
        canvas.Set(8, 9, I(PaletteRole.WallEdge));
        canvas.Line(6, 10, 9, 10, I(PaletteRole.WallEdge));
        canvas.Set(8, 3, I(PaletteRole.Glow));

        for (var fleck = 0; fleck < 2; fleck++)
        {
            canvas.Set(2 + random.NextInt(12), 12 + random.NextInt(2), I(PaletteRole.Accent));
        }

        return canvas;
    }

    private static PixelCanvas Seal(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/feature-seal"));

        canvas.Ellipse(8, 14, 5, 1, I(PaletteRole.Shadow));

        // Two ribbon tails, then the wax over them.
        canvas.Line(6, 10, 3, 15, I(PaletteRole.WallTop));
        canvas.Line(10, 10, 13, 15, I(PaletteRole.WallTop));
        canvas.Ellipse(8, 8, 5, 5, I(PaletteRole.Hazard));
        canvas.Ellipse(8, 8, 4, 4, I(PaletteRole.Accent));

        canvas.Line(6, 8, 10, 8, I(PaletteRole.Glow));
        canvas.Line(8, 6, 8, 10, I(PaletteRole.Glow));
        canvas.Set(8, 8, I(PaletteRole.UiText));

        for (var drip = 0; drip < 2; drip++)
        {
            canvas.Set(5 + random.NextInt(7), 12 + random.NextInt(2), I(PaletteRole.Hazard));
        }

        return canvas;
    }

    private static PixelCanvas Heart(Theme theme)
    {
        var canvas = new PixelCanvas(Size, Size);

        canvas.Ellipse(8, 14, 5, 1, I(PaletteRole.Shadow));
        canvas.Ellipse(6, 7, 3, 3, I(PaletteRole.Flesh));
        canvas.Ellipse(10, 7, 3, 3, I(PaletteRole.Flesh));
        canvas.Rect(4, 8, 9, 4, I(PaletteRole.Flesh));
        canvas.Line(4, 11, 8, 14, I(PaletteRole.Flesh));
        canvas.Line(12, 11, 8, 14, I(PaletteRole.Flesh));
        canvas.Rect(4, 8, 9, 1, I(PaletteRole.Hazard));

        // Veins and one bright artery: the thing is still working.
        canvas.Line(5, 9, 7, 11, I(PaletteRole.Accent));
        canvas.Line(11, 9, 9, 11, I(PaletteRole.Accent));
        canvas.Line(8, 4, 10, 2, I(PaletteRole.Hazard));
        canvas.Line(8, 4, 6, 2, I(PaletteRole.Hazard));
        canvas.Set(6, 6, I(PaletteRole.Glow));
        canvas.Set(10, 6, I(PaletteRole.Glow));

        return canvas;
    }

    /// <summary>Fills the tile and adds the shared dark checker every floor has underneath.</summary>
    private static PixelCanvas Base(Theme theme, int variant)
    {
        var canvas = new PixelCanvas(Size, Size);

        canvas.Fill(I(PaletteRole.Floor));
        canvas.Dither(0, 0, Size, Size, I(PaletteRole.FloorDark), variant & 1);

        return canvas;
    }

    /// <summary>Adds the theme's grit and a couple of glowing flecks, shared by every motif.</summary>
    private static void Speckle(PixelCanvas canvas, Theme theme, SeededRandom random)
    {
        canvas.Speckle(I(PaletteRole.FloorDark), theme.SpeckleRate * 0.24, random);

        var flecks = (int)Math.Round(theme.GlowStrength * 2);

        for (var fleck = 0; fleck < flecks; fleck++)
        {
            canvas.Set(random.NextInt(Size), random.NextInt(Size), I(PaletteRole.Glow));
        }
    }

    /// <summary>Draws a one-pixel ring, used for emblems, runes and clock faces.</summary>
    private static void Ring(PixelCanvas canvas, int centreX, int centreY, int radius, byte index)
    {
        var steps = Math.Max(12, radius * 6);

        for (var step = 0; step < steps; step++)
        {
            var angle = Math.PI * 2 * step / steps;

            canvas.Set(
                (int)Math.Round(centreX + (Math.Cos(angle) * radius)),
                (int)Math.Round(centreY + (Math.Sin(angle) * radius)),
                index);
        }
    }

    private static byte I(PaletteRole role) => CaveTiles.Index(role);
}

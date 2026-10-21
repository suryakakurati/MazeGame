using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.AssetForge;

/// <summary>Which side of a cell a thin wall face sits on.</summary>
public enum WallSide
{
    /// <summary>The wall is on the cell's top edge; the floor is below it.</summary>
    North,

    /// <summary>The wall is on the cell's right edge; the floor is to its left.</summary>
    East,

    /// <summary>The wall is on the cell's bottom edge; the floor is above it.</summary>
    South,

    /// <summary>The wall is on the cell's left edge; the floor is to its right.</summary>
    West,
}

/// <summary>
/// The thin wall faces: a band of rock along one edge of a cell, on a transparent background.
/// </summary>
/// <remarks>
/// <para>
/// The maze stores walls <b>between</b> cells, so a room is a floor tile with up to four rock bands
/// around it — the <see cref="Autotiler"/>'s solid tiles are for the floors that later grow chasm
/// and veto cells, not for today's layout. A band is drawn outer side first: rock, then a lit rim,
/// then a contact shadow where it meets the floor, which is the cheapest way for a 5 px strip to
/// read as a solid with a lit edge rather than as a painted line.
/// </para>
/// <para>
/// Bands overlap at corners on purpose: two perpendicular faces meeting at a cell corner cover the
/// same pixel twice with the same palette roles, so a corner needs no special case and no gaps can
/// appear between a wall run and its neighbour.
/// </para>
/// </remarks>
public static class WallStrips
{
    /// <summary>How thick a wall band is, in pixels.</summary>
    public const int Thickness = 5;

    private const int Size = CaveTiles.TileSize;

    /// <summary>Returns the four thin wall faces, in the order north, east, south, west.</summary>
    /// <param name="theme">The theme whose palette roles and grit are used.</param>
    /// <returns>One 16x16 face per side, transparent everywhere the band is not.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas[] All(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return
        [
            Face(theme, WallSide.North),
            Face(theme, WallSide.East),
            Face(theme, WallSide.South),
            Face(theme, WallSide.West),
        ];
    }

    /// <summary>Draws one thin wall face.</summary>
    /// <param name="theme">The theme whose palette roles and grit are used.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Face(Theme theme, WallSide side)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var canvas = theme.Wall switch
        {
            WallMotif.CutBlock => WorkedStoneTiles.Face(theme, side),
            WallMotif.WovenRoot => WrongOrganicTiles.Face(theme, side),
            WallMotif.MossRock => OrganicFace(theme, side),
            _ => MotifTiles.Face(theme, side),
        };

        // The edge treatment is shared by every family, and painted last so no texture can bury it:
        // a near-black outline on the outer row, a lit rim and then the contact shadow against the
        // floor. A wall is a five-pixel band over a floor tile, so those rows are what say "this is
        // a wall" — a family that painted moss over its shadow made the band read as part of the
        // floor, and a body row the same value as a floor tile made it disappear entirely.
        Paint(canvas, side, 0, CaveTiles.Index(PaletteRole.WallEdge));
        Paint(canvas, side, Thickness - 2, CaveTiles.Index(PaletteRole.WallTop));
        Paint(canvas, side, Thickness - 1, CaveTiles.Index(PaletteRole.WallEdge));

        return canvas;
    }

    /// <summary>Draws the cave's band: rock with a lit rim, chips, moss and a few glowing motes.</summary>
    /// <param name="theme">The theme whose palette roles and grit are used.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    private static PixelCanvas OrganicFace(Theme theme, WallSide side)
    {
        var canvas = new PixelCanvas(Size, Size);
        var random = new SeededRandom(CaveTiles.Seed($"{theme.Id}/strip-{NameOf(side)}"));

        // Offsets run from the outer edge inward, so one loop draws every side: 0..2 are rock,
        // 3 is the lit rim and 4 is the shadow that touches the floor.
        for (var offset = 0; offset < Thickness; offset++)
        {
            var role = offset switch
            {
                3 => PaletteRole.WallTop,
                4 => PaletteRole.WallEdge,
                _ => PaletteRole.Wall,
            };

            Paint(canvas, side, offset, (byte)role);
        }

        // Chips break the band's silhouette without moving its edge, the same trick the solid wall
        // tile uses: short strokes of the dark role inside the rock.
        var chips = 1 + (int)Math.Round(theme.BoulderRoundness * 3);

        for (var chip = 0; chip < chips; chip++)
        {
            var along = 1 + random.NextInt(Size - 2);
            var from = 1 + random.NextInt(2);
            var length = 1 + random.NextInt(2);

            for (var step = 0; step < length; step++)
            {
                Paint(canvas, side, Math.Min(Thickness - 2, from + step), CaveTiles.Index(PaletteRole.WallEdge), along);
            }
        }

        // Moss grows on the rock and hangs over the rim, so a damp floor looks damp from the wall.
        if (theme.MossDensity > 0)
        {
            var drips = 1 + (int)Math.Round(theme.MossDensity * 4);

            for (var drip = 0; drip < drips; drip++)
            {
                var along = random.NextInt(Size);

                Paint(canvas, side, 2, CaveTiles.Index(PaletteRole.Moss), along);

                if (drip % 2 == 0)
                {
                    Paint(canvas, side, 3, CaveTiles.Index(PaletteRole.Moss), along);
                }
            }
        }

        var motes = (int)Math.Round(theme.GlowStrength * 2);

        for (var mote = 0; mote < motes; mote++)
        {
            Paint(canvas, side, 1, CaveTiles.Index(PaletteRole.Glow), 1 + random.NextInt(Size - 2));
        }

        return canvas;
    }

    /// <summary>Returns the manifest name of one face.</summary>
    /// <param name="side">The side the face belongs to.</param>
    /// <returns>The tile name, for example <c>wall-north</c>.</returns>
    public static string NameOf(WallSide side) => side switch
    {
        WallSide.North => "wall-north",
        WallSide.East => "wall-east",
        WallSide.South => "wall-south",
        _ => "wall-west",
    };

    /// <summary>
    /// Writes one pixel of a band, addressed along the wall and across it.
    /// </summary>
    /// <param name="canvas">The face being drawn.</param>
    /// <param name="side">Which edge the band runs along.</param>
    /// <param name="offset">Distance from the cell's outer edge, 0 to 4.</param>
    /// <param name="role">The palette index to write.</param>
    /// <param name="along">Position along the wall; defaults to a full-width band.</param>
    internal static void Paint(PixelCanvas canvas, WallSide side, int offset, byte role, int along = -1)
    {
        var start = along < 0 ? 0 : along;
        var end = along < 0 ? Size : along + 1;

        for (var step = start; step < end; step++)
        {
            var (x, y) = side switch
            {
                WallSide.North => (step, offset),
                WallSide.East => (Size - 1 - offset, step),
                WallSide.South => (step, Size - 1 - offset),
                _ => (offset, step),
            };

            canvas.Set(x, y, role);
        }
    }
}

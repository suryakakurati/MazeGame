using System;

namespace MazeGame.AssetForge;

/// <summary>
/// Chooses the drawing routines for a theme's <see cref="TileFamily"/>.
/// </summary>
/// <remarks>
/// <para>
/// A theme is data; the family is the shape language that data is spoken in. Three families ship —
/// boulders and moss for the cave, courses and carved bands for the drowned halls, matted roots and
/// veins for Rootbound — and each one supplies the same four pieces: a floor, a solid wall, a thin
/// wall face and two props. Nothing else in the compiler knows which family it is drawing.
/// </para>
/// <para>
/// This is the only place a family is named, which is what keeps a new family a new file plus one
/// record rather than a branch in every routine.
/// </para>
/// </remarks>
public static class TileGrammar
{
    /// <summary>How many prop tiles each theme compiles: two scenery props and the floor's emblem.</summary>
    public const int PropCount = 3;

    /// <summary>Draws one floor variant for <paramref name="theme"/>'s family.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <param name="variant">The variant number, 0-based.</param>
    /// <returns>A 16x16 floor tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the variant is negative.</exception>
    public static PixelCanvas Floor(Theme theme, int variant)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return theme.Floor switch
        {
            FloorMotif.Flagstones => WorkedStoneTiles.Floor(theme, variant),
            FloorMotif.Roots => WrongOrganicTiles.Floor(theme, variant),
            FloorMotif.Boulders => CaveTiles.Floor(theme, variant),
            _ => MotifTiles.Floor(theme, variant),
        };
    }

    /// <summary>Draws the solid wall tile for <paramref name="theme"/>'s family.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <returns>A 16x16 wall tile, fully opaque.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Wall(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return theme.Wall switch
        {
            WallMotif.CutBlock => WorkedStoneTiles.Wall(theme),
            WallMotif.WovenRoot => WrongOrganicTiles.Wall(theme),
            WallMotif.MossRock => CaveTiles.Wall(theme),
            _ => MotifTiles.Wall(theme),
        };
    }

    /// <summary>Draws one thin wall face for <paramref name="theme"/>'s family.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <param name="side">Which edge of the cell the band runs along.</param>
    /// <returns>A 16x16 face, transparent except for the band.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <remarks>
    /// The band's geometry is shared by every family — <see cref="WallStrips.Thickness"/> rows on the
    /// same edge — and <see cref="WallStrips.Face"/> picks the family's texture, so a wall reads as
    /// the same obstacle everywhere and still looks like the place it is in.
    /// </remarks>
    public static PixelCanvas WallFace(Theme theme, WallSide side)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return WallStrips.Face(theme, side);
    }

    /// <summary>Draws one prop for <paramref name="theme"/>'s family.</summary>
    /// <param name="theme">The theme being drawn.</param>
    /// <param name="index">The prop index, 0 to <see cref="PropCount"/> - 1.</param>
    /// <returns>A 16x16 prop with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0..1.</exception>
    public static PixelCanvas Prop(Theme theme, int index)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (index is < 0 or >= PropCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"A theme has {PropCount} props.");
        }

        if (index == PropCount - 1)
        {
            return MotifTiles.Sigil(theme);
        }

        return theme.Family switch
        {
            TileFamily.WorkedStone => WorkedStoneTiles.Prop(theme, index),
            TileFamily.WrongOrganic => WrongOrganicTiles.Prop(theme, index),
            _ => CaveTiles.Prop(theme, index),
        };
    }

    /// <summary>Draws the floor's signature object, which is scattered a few times per maze.</summary>
    /// <param name="theme">The theme whose feature is drawn.</param>
    /// <returns>A 16x16 sprite with a transparent background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static PixelCanvas Feature(Theme theme) => MotifTiles.Feature(theme);
}

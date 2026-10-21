using System;
using System.Collections.Generic;

namespace MazeGame.AssetForge;

/// <summary>One colour, 8 bits per channel.</summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
public readonly record struct Rgb(byte Red, byte Green, byte Blue);

/// <summary>
/// The named slots every tile draws with, in index order: <see cref="Transparent"/> is always 0 and
/// every other value is a colour.
/// </summary>
public enum PaletteRole
{
    /// <summary>Nothing drawn; encodes with alpha 0.</summary>
    Transparent = 0,

    /// <summary>The floor of the maze.</summary>
    Floor,

    /// <summary>The floor's darker speckle.</summary>
    FloorDark,

    /// <summary>The body of a wall.</summary>
    Wall,

    /// <summary>The lit top of a wall.</summary>
    WallTop,

    /// <summary>The wall's dark lower edge.</summary>
    WallEdge,

    /// <summary>The moss or growth growing over stone.</summary>
    Moss,

    /// <summary>The theme's signature colour, used for props and highlights.</summary>
    Accent,

    /// <summary>The colour that actually glows: keys, wisps, the arch.</summary>
    Glow,

    /// <summary>The player.</summary>
    Flesh,

    /// <summary>Bone, teeth and the killer's plates.</summary>
    Bone,

    /// <summary>Standing water.</summary>
    Water,

    /// <summary>Damage and danger, including a card's drawback line.</summary>
    Hazard,

    /// <summary>Text on the UI.</summary>
    UiText,

    /// <summary>Secondary text and rules on the UI.</summary>
    UiDim,

    /// <summary>The shadow drawn under a figure.</summary>
    Shadow,
}

/// <summary>
/// A theme's 16 colours, indexed by <see cref="PaletteRole"/>.
/// </summary>
/// <remarks>
/// <para>
/// A theme names hues, not colours: <see cref="Derive"/> walks a fixed lightness ladder so two
/// themes always have the same contrast between floor, wall and text, and a bad hue can never make
/// a floor unreadable. That is the "contrast is guaranteed rather than hoped for" rule from the
/// plan.
/// </para>
/// <para>
/// The palette is a lookup table, so a canvas can hold indices and stay theme-blind.
/// </para>
/// </remarks>
public sealed class Palette
{
    /// <summary>How many slots a palette has.</summary>
    public const int Size = 16;

    /// <summary>The smallest weighted distance two derived palettes may have to count as distinct.</summary>
    public const double MinimumThemeDistance = 24.0;

    private readonly Rgb[] _colours;

    /// <summary>Creates a palette from exactly <see cref="Size"/> colours.</summary>
    /// <param name="colours">The colours, in <see cref="PaletteRole"/> order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colours"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the count is wrong.</exception>
    public Palette(IReadOnlyList<Rgb> colours)
    {
        ArgumentNullException.ThrowIfNull(colours);

        if (colours.Count != Size)
        {
            throw new ArgumentException($"A palette has exactly {Size} colours; got {colours.Count}.", nameof(colours));
        }

        _colours = new Rgb[Size];
        for (var index = 0; index < Size; index++)
        {
            _colours[index] = colours[index];
        }
    }

    /// <summary>Gets the colour at <paramref name="role"/>.</summary>
    /// <param name="role">The slot to read.</param>
    /// <returns>The colour; transparent for <see cref="PaletteRole.Transparent"/>.</returns>
    public Rgb this[PaletteRole role] => _colours[(int)role];

    /// <summary>Gets the colour at <paramref name="index"/>.</summary>
    /// <param name="index">The palette index, as stored in a canvas.</param>
    /// <returns>The colour; black for the transparent index.</returns>
    public Rgb this[byte index] => _colours[index];

    /// <summary>Derives a theme's palette from its hues, using the fixed lightness ladder.</summary>
    /// <param name="theme">The theme whose hues and accents are used.</param>
    /// <returns>A palette whose contrast does not depend on the theme.</returns>
    /// <remarks>
    /// The ladder deliberately leaves a wide gap between <see cref="PaletteRole.Floor"/> and
    /// <see cref="PaletteRole.Wall"/>, with a bright <see cref="PaletteRole.WallTop"/> rim and a
    /// near-black <see cref="PaletteRole.WallEdge"/> foot. Walls in this maze are thin bands painted
    /// over a floor tile, so they have one or two pixels to announce themselves: if the wall body sat
    /// a shade above the floor, the maze read as one flat surface with lit edges.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static Palette Derive(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var hue = theme.Hue;
        var accent = theme.AccentHue;

        return new Palette(
        [
            new Rgb(0, 0, 0),
            Hsl(hue, 0.28, 0.20),
            Hsl(hue, 0.24, 0.13),
            Hsl(hue + 6, 0.22, 0.40),
            Hsl(hue + 6, 0.26, 0.58),
            Hsl(hue + 6, 0.20, 0.07),
            Hsl(hue - 34, 0.45, 0.31),
            Hsl(accent, 0.62, 0.54),
            Hsl(accent, 0.86, 0.72),
            Hsl(hue + 300, 0.45, 0.55),
            Hsl(hue, 0.12, 0.79),
            Hsl(200, 0.50, 0.42),
            Hsl(25, 0.80, 0.55),
            Hsl(50, 0.12, 0.92),
            Hsl(50, 0.10, 0.55),
            Hsl(hue, 0.30, 0.05),
        ]);
    }

    /// <summary>
    /// Returns how far apart two palettes are, in a weighted RGB metric over their coloured slots.
    /// </summary>
    /// <param name="other">The palette to compare with.</param>
    /// <returns>A distance where 0 means identical; roughly 0–765 in practice.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    /// <remarks>
    /// Weights 2/4/3 (red/green/blue) approximate how much each channel matters to the eye. It is
    /// not CIELAB, but it is stable, integer-cheap and good enough to catch two themes that are
    /// the same colour.
    /// </remarks>
    public double DistanceTo(Palette other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var total = 0;

        for (var role = (int)PaletteRole.Floor; role < Size; role++)
        {
            var left = _colours[role];
            var right = other._colours[role];

            total += (2 * Square(left.Red - right.Red)) +
                     (4 * Square(left.Green - right.Green)) +
                     (3 * Square(left.Blue - right.Blue));
        }

        return (double)total / (Size - 1);
    }

    /// <summary>Checks that every coloured slot is drawn and that no two share a colour.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two roles collide, which would make a tile read as the wrong material.
    /// </exception>
    public void Validate()
    {
        for (var left = (int)PaletteRole.Floor; left < Size; left++)
        {
            for (var right = left + 1; right < Size; right++)
            {
                if (_colours[left] == _colours[right])
                {
                    throw new InvalidOperationException(
                        $"{(PaletteRole)left} and {(PaletteRole)right} are the same colour.");
                }
            }
        }
    }

    private static int Square(int value) => value * value;

    /// <summary>Converts a hue/saturation/lightness triple to 8-bit RGB.</summary>
    /// <param name="hue">The hue in degrees; wrapped into 0..360.</param>
    /// <param name="saturation">Saturation in 0..1.</param>
    /// <param name="lightness">Lightness in 0..1.</param>
    /// <returns>The colour.</returns>
    private static Rgb Hsl(double hue, double saturation, double lightness)
    {
        var wrapped = ((hue % 360) + 360) % 360;
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var secondary = chroma * (1 - Math.Abs(((wrapped / 60) % 2) - 1));
        var match = lightness - (chroma / 2);

        var (red, green, blue) = wrapped switch
        {
            < 60 => (chroma, secondary, 0.0),
            < 120 => (secondary, chroma, 0.0),
            < 180 => (0.0, chroma, secondary),
            < 240 => (0.0, secondary, chroma),
            < 300 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary),
        };

        return new Rgb(Channel(red + match), Channel(green + match), Channel(blue + match));

        static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
    }
}

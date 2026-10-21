using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// What drifts through a floor's air. Presentation only: the same maze can be a snowfield or a
/// furnace without a rule changing.
/// </summary>
public enum FloorAmbient
{
    /// <summary>Slow, glowing spores hanging in the lantern light.</summary>
    Motes,

    /// <summary>Bubbles rising through standing water.</summary>
    Bubbles,

    /// <summary>Soft spores that pulse and settle.</summary>
    Spores,

    /// <summary>Embers rising and fading out.</summary>
    Embers,

    /// <summary>Water falling in short, fast drops.</summary>
    Drips,

    /// <summary>Dust turning slowly in the air.</summary>
    Dust,

    /// <summary>Moths fluttering in irregular arcs.</summary>
    Moths,

    /// <summary>Petals or leaves tumbling down.</summary>
    Petals,

    /// <summary>Ash falling in a steady drift.</summary>
    Ash,

    /// <summary>Rain driven at an angle.</summary>
    Rain,

    /// <summary>Restless static that flickers in place.</summary>
    Static,

    /// <summary>Fireflies wandering and blinking.</summary>
    Fireflies,
}

/// <summary>
/// One floor's visual identity outside the atlas: its accent, its weather, how warm its light is
/// and how heavy its fog sits.
/// </summary>
/// <remarks>
/// The atlas owns the tiles; this record owns everything that moves or is drawn in window pixels.
/// It is a separate table from the compiler's <c>ThemeCatalog</c> because the game must not
/// reference the tool, and the two are held together by tests that walk every shipped floor.
/// </remarks>
/// <param name="ThemeId">The floor's atlas id.</param>
/// <param name="AccentHue">The accent hue in degrees, matching the compiler's theme.</param>
/// <param name="Ambient">What drifts through this floor's air.</param>
/// <param name="Warmth">
/// -1 for a cold blue light, +1 for a hot amber one. 0 is the neutral lantern.
/// </param>
/// <param name="FogDensity">0 to 1: how much colour the letterbox and the unlit cells take.</param>
public sealed record FloorStyle(
    string ThemeId,
    double AccentHue,
    FloorAmbient Ambient,
    double Warmth,
    double FogDensity)
{
    /// <summary>Gets the accent colour: the floor's cards, rules and HUD marks.</summary>
    public Color Accent => Hsl(AccentHue, 0.62, 0.54);

    /// <summary>Gets the colour a particle is drawn in.</summary>
    /// <remarks>
    /// The accent is the floor's signature, but particles need to sit one step brighter so they
    /// read against a lit floor. Embers and fireflies get the warm side of the hue wheel; the
    /// rest keep the accent's own hue.
    /// </remarks>
    public Color AmbientColor => Ambient switch
    {
        FloorAmbient.Embers or FloorAmbient.Fireflies => Hsl(38, 0.82, 0.66),
        FloorAmbient.Bubbles or FloorAmbient.Rain => Hsl(196, 0.60, 0.72),
        FloorAmbient.Ash => Hsl(40, 0.10, 0.72),
        FloorAmbient.Moths => Hsl(52, 0.52, 0.68),
        _ => Hsl(AccentHue, 0.58, 0.66),
    };

    /// <summary>
    /// Gets the colour the letterbox is cleared to: the floor's own hue, drowned in near-black.
    /// </summary>
    public Color Fog
    {
        get
        {
            var accent = Accent;
            var lift = 0.06 + (FogDensity * 0.06);

            return new Color(
                (byte)Math.Clamp(3 + (accent.R * lift), 0, 255),
                (byte)Math.Clamp(7 + (accent.G * lift), 0, 255),
                (byte)Math.Clamp(9 + (accent.B * lift), 0, 255),
                (byte)255);
        }
    }

    /// <summary>
    /// Gets the multiplier applied to the lightmap: what makes a lantern feel warm or cold.
    /// </summary>
    /// <remarks>
    /// The values stay close to 1 so the floor's contrast is still set by the atlas: warmth is a
    /// cast on the light, not a second grade that can make a wall disappear.
    /// </remarks>
    public Color LightTint
    {
        get
        {
            var warm = Math.Clamp(Warmth, -1, 1);
            var red = 1.0 + (warm * 0.14);
            var blue = 1.0 - (warm * 0.20);
            var green = 1.0 + (warm * 0.04);

            return new Color(Channel(red), Channel(green), Channel(blue), (byte)255);
        }
    }

    /// <summary>Converts a hue/saturation/lightness triple to an opaque Raylib colour.</summary>
    /// <param name="hue">The hue in degrees; wrapped into 0..360.</param>
    /// <param name="saturation">Saturation in 0..1.</param>
    /// <param name="lightness">Lightness in 0..1.</param>
    /// <returns>The colour.</returns>
    public static Color Hsl(double hue, double saturation, double lightness)
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

        return new Color(Channel(red + match), Channel(green + match), Channel(blue + match), (byte)255);
    }

    private static byte Channel(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
}

/// <summary>
/// The twenty floors' visual identities, in floor order and keyed by atlas id.
/// </summary>
/// <remarks>
/// This is content, not a rule. A floor whose id is missing fails loudly: falling back to another
/// floor's weather is how two places start to feel the same.
/// </remarks>
public static class FloorStyleCatalog
{
    private static readonly FloorStyle[] Styles =
    [
        new("shallows", 320, FloorAmbient.Motes, 0.0, 0.30),
        new("rootbound", 40, FloorAmbient.Spores, 0.45, 0.42),
        new("drowned", 190, FloorAmbient.Drips, -0.45, 0.55),
        new("saltchoir", 58, FloorAmbient.Dust, 0.30, 0.38),
        new("ember", 45, FloorAmbient.Embers, 0.90, 0.50),
        new("gilded", 12, FloorAmbient.Dust, 0.70, 0.36),
        new("hush", 300, FloorAmbient.Static, -0.70, 0.68),
        new("gallery", 210, FloorAmbient.Dust, -0.40, 0.32),
        new("warren", 70, FloorAmbient.Dust, 0.10, 0.45),
        new("orchard", 92, FloorAmbient.Petals, 0.25, 0.34),
        new("blindmile", 250, FloorAmbient.Static, -0.85, 0.72),
        new("foundry", 28, FloorAmbient.Embers, 0.95, 0.54),
        new("mothgarden", 78, FloorAmbient.Moths, 0.60, 0.40),
        new("ossuary", 20, FloorAmbient.Dust, 0.18, 0.48),
        new("tide", 160, FloorAmbient.Bubbles, -0.55, 0.52),
        new("bramble", 142, FloorAmbient.Petals, 0.30, 0.46),
        new("vault", 268, FloorAmbient.Dust, -0.60, 0.58),
        new("loom", 232, FloorAmbient.Motes, -0.25, 0.44),
        new("ashgarden", 35, FloorAmbient.Ash, 0.50, 0.60),
        new("deep", 205, FloorAmbient.Fireflies, -0.90, 0.66),
    ];

    /// <summary>Gets the shipped styles, in floor order.</summary>
    public static IReadOnlyList<FloorStyle> Shipped { get; } = new ReadOnlyCollection<FloorStyle>(Styles);

    /// <summary>Returns the style for a theme id.</summary>
    /// <param name="themeId">The floor's atlas id.</param>
    /// <returns>The matching style.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no shipped floor has that id.</exception>
    public static FloorStyle For(string themeId)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        foreach (var style in Styles)
        {
            if (string.Equals(style.ThemeId, themeId, StringComparison.Ordinal))
            {
                return style;
            }
        }

        throw new KeyNotFoundException(
            $"No style for theme '{themeId}'. Known themes: {string.Join(", ", Styles.Select(style => style.ThemeId))}.");
    }

    /// <summary>Returns the style for a floor at a position in the campaign.</summary>
    /// <param name="floorNumber">The one-based floor number.</param>
    /// <returns>The style for that floor's theme.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such floor.</exception>
    public static FloorStyle ForFloor(int floorNumber) => For(FloorCatalog.ByNumber(floorNumber).ThemeId);
}

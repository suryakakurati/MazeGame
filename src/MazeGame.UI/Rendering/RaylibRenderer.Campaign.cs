using System;
using System.Collections.Generic;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Maps each floor of the campaign to the colour it is drawn in.
/// </summary>
/// <remarks>
/// The screens themselves are set in the compiled face by <see cref="Pixels.PixelCards"/>; this
/// partial exists because the letterbox, the HUD and every card need the same accent, and Core
/// never knows about colours.
/// </remarks>
public sealed partial class RaylibRenderer
{
    /// <summary>
    /// Maps a floor's theme id to the accent colour its cards use.
    /// </summary>
    /// <param name="themeId">The floor's theme id.</param>
    internal static Color AccentFor(string themeId) => ThemeAccent(themeId);

    /// <summary>
    /// Returns the accent colour of a floor's theme: the hue its cards, rules and letterbox glow
    /// are drawn in.
    /// </summary>
    /// <param name="themeId">The floor's theme id, for example <c>shallows</c>.</param>
    /// <returns>The theme's accent colour.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no shipped theme has that id. A floor with no colour must fail loudly rather
    /// than borrow another floor's, which is what a silent fallback would do.
    /// </exception>
    /// <remarks>
    /// The hues live in <see cref="FloorStyleCatalog"/>, which is the one table the cards, the
    /// weather and the letterbox all read.
    /// </remarks>
    public static Color ThemeAccent(string themeId) => FloorStyleCatalog.For(themeId).Accent;
}

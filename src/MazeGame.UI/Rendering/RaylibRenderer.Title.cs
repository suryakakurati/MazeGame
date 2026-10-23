using System;
using System.Numerics;
using MazeGame.UI.Rendering.Pixels;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// The title screen: the same cave, seen from further away, with the game's name over it.
/// </summary>
/// <remarks>
/// <para>
/// This is a partial of <see cref="RaylibRenderer"/> rather than its own class so it can reuse the
/// cave palette, the vignette and the grain instead of growing a second copy of them. It draws no
/// game state at all: a title screen has no rules, and the frontend owns the menu selection.
/// </para>
/// <para>
/// The type is set by <see cref="PixelCards.Title"/> in the compiled face, the same one the cards
/// and the maze's labels use, so the first screen a player sees is already the game's typography.
/// </para>
/// <para>
/// Everything moves on the clock (spore drift, the breathing glow behind the wordmark, the pulse
/// of the selected item), so the screen is alive without needing input to be interesting.
/// </para>
/// </remarks>
public sealed partial class RaylibRenderer
{
    /// <summary>
    /// Draws the title screen with the menu item at <paramref name="selectedItem"/> highlighted.
    /// </summary>
    /// <param name="font">The compiled face the title is set in.</param>
    /// <param name="selectedItem">
    /// Zero-based index of the highlighted menu entry. The renderer only highlights it; the
    /// frontend owns the selection itself.
    /// </param>
    /// <param name="hasContinue">Whether a saved run is on the menu.</param>
    /// <param name="gardenBlooms">The garden grown across finished runs, or zero for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static void DrawTitle(PixelFont font, int selectedItem, bool hasContinue = false, int gardenBlooms = 0)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var time = Raylib.GetTime();
        var breath = 0.7 + (Math.Sin(time * 0.8) * 0.3);
        var centerX = width / 2f;

        DrawBackground();

        // Three faint pools of living light: green behind the name, cyan low left, pink high right,
        // so the frame is lit the way the cave is rather than with a flat gradient.
        Raylib.DrawCircleGradient(
            new Vector2(centerX, height * 0.30f),
            360,
            Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(22 * breath)),
            Transparent);
        Raylib.DrawCircleGradient(
            new Vector2(width * 0.16f, height * 0.82f),
            230,
            Rgba(GlowCyan.R, GlowCyan.G, GlowCyan.B, 20),
            Transparent);
        Raylib.DrawCircleGradient(
            new Vector2(width * 0.86f, height * 0.16f),
            210,
            Rgba(GlowPink.R, GlowPink.G, GlowPink.B, 16),
            Transparent);

        DrawTitleSpores(time);

        PixelCards.Title(font, selectedItem, hasContinue, gardenBlooms);

        DrawVignette(false);
        DrawGrain();
    }

    /// <summary>
    /// Drifts glowing spores across the whole window. Unlike the in-game motes these are not tied
    /// to the lantern, because there is no player yet: they just keep the cave breathing.
    /// </summary>
    /// <param name="time">Current time in seconds.</param>
    private static void DrawTitleSpores(double time)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();

        for (var index = 0; index < MoteCount; index++)
        {
            var seedX = Frac(Math.Sin((index * 12.9898) + 4.1414) * 43758.5453);
            var seedY = Frac(Math.Sin((index * 78.233) + 1.618) * 12345.6789);
            var drift = Frac(seedX + (time * (0.004 + (seedY * 0.008))));
            var sway = Math.Sin((time * 0.35) + (index * 1.7)) * 0.012;
            var x = (drift + sway) * width;
            var y = Frac(seedY + (time * 0.003)) * height;

            var color = (index % 3) switch
            {
                0 => GlowGreen,
                1 => GlowCyan,
                _ => GlowPink,
            };

            Raylib.DrawCircle(
                (int)x,
                (int)y,
                1.2f + (float)(seedX * 1.2),
                Rgba(color.R, color.G, color.B, (byte)(30 + (seedY * 70))));
        }
    }
}

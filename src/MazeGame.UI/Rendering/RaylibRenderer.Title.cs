using System;
using System.Numerics;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// The title screen: the same cave, seen from further away, with the game's name over it.
/// </summary>
/// <remarks>
/// <para>
/// This is a partial of <see cref="RaylibRenderer"/> rather than its own class so it can reuse the
/// cave palette, the vignette, the grain and the text helpers instead of growing a second copy of
/// them. It draws no game state at all: a title screen has no rules, and the frontend owns the
/// menu selection.
/// </para>
/// <para>
/// Everything moves on the clock (spore drift, the breathing glow behind the wordmark, the pulse
/// of the selected item), so the screen is alive without needing input to be interesting.
/// </para>
/// </remarks>
public sealed partial class RaylibRenderer
{
    private const int TitleFontSize = 44;
    private const int WordmarkFontSize = 66;
    private const int MenuFontSize = 22;

    /// <summary>
    /// Draws the title screen with the menu item at <paramref name="selectedItem"/> highlighted.
    /// </summary>
    /// <param name="selectedItem">
    /// Zero-based index of the highlighted menu entry (0 = descend, 1 = leave). The renderer only
    /// highlights it; the frontend owns the selection itself.
    /// </param>
    public void DrawTitle(int selectedItem)
    {
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

        TextCentered("A DESCENT IN THE DARK", centerX, 118, 12, Rgba(HintText.R, HintText.G, HintText.B, 210), 3.4f);
        TextCentered("BIOLUMINESCENT", centerX, 148, TitleFontSize, Rgba(214, 255, 236, 245), 5.0f);
        TextCentered("DEPTHS", centerX, 198, WordmarkFontSize, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, 245), 7.0f);

        // A thin broken rule with a glowing seed in the gap: the smallest possible flourish that
        // still separates the name from the promise below it.
        const float ruleY = 300f;
        Raylib.DrawLineEx(
            new Vector2(centerX - 96, ruleY), new Vector2(centerX - 16, ruleY), 1f, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, 100));
        Raylib.DrawLineEx(
            new Vector2(centerX + 16, ruleY), new Vector2(centerX + 96, ruleY), 1f, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, 100));
        Raylib.DrawCircleV(new Vector2(centerX, ruleY), 2.4f, Rgba(206, 255, 236, 230));

        TextCentered(
            "COLLECT THE KEYS. OPEN THE ARCH. DO NOT BE CAUGHT.",
            centerX,
            322,
            12,
            Rgba(HintText.R, HintText.G, HintText.B, 200),
            1.8f);

        DrawTitleMenuItem("DESCEND", centerX, 412, selectedItem == 0, time);
        DrawTitleMenuItem("LEAVE", centerX, 468, selectedItem == 1, time);

        TextCentered(
            "W/S OR ARROWS: SELECT      ENTER: CONFIRM      ESC: LEAVE",
            centerX,
            height - 82,
            12,
            Rgba(HintText.R, HintText.G, HintText.B, 180),
            1.6f);
        TextCentered("COP3503H / GROUP 5", centerX, height - 48, 10, Rgba(HintText.R, HintText.G, HintText.B, 110), 2.8f);

        DrawVignette(false);
        DrawGrain();
    }

    /// <summary>
    /// Draws one menu entry, with a breathing halo and a pair of chevrons when it is selected.
    /// </summary>
    /// <param name="label">The text to draw.</param>
    /// <param name="centerX">Horizontal centre of the entry, in pixels.</param>
    /// <param name="y">Top edge of the text, in pixels.</param>
    /// <param name="selected">Whether this entry is the highlighted one.</param>
    /// <param name="time">Current time in seconds, used for the pulse.</param>
    /// <remarks>
    /// The chevrons are triangles rather than glyphs because raylib's built-in font only covers
    /// ASCII; a decorative character would silently render as a box.
    /// </remarks>
    private void DrawTitleMenuItem(string label, float centerX, float y, bool selected, double time)
    {
        if (selected)
        {
            var pulse = 0.6 + (Math.Sin(time * 2.4) * 0.4);
            Raylib.DrawCircleGradient(
                new Vector2(centerX, y + (MenuFontSize / 2f)),
                110,
                Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(26 * pulse)),
                Transparent);
        }

        var accent = selected ? GlowGreen : HintText;
        var alpha = selected ? (byte)Math.Clamp(200 + (Math.Sin(time * 3.2) * 55), 0, 255) : (byte)120;
        TextCentered(label, centerX, y, MenuFontSize, Rgba(accent.R, accent.G, accent.B, alpha), 4.5f);

        if (!selected)
        {
            return;
        }

        var offset = (TextWidth(label, MenuFontSize, 4.5f) / 2f) + 30;
        var middle = y + (MenuFontSize / 2f);
        var chevron = Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, 220);

        Raylib.DrawTriangle(
            new Vector2(centerX + offset, middle - 5),
            new Vector2(centerX + offset, middle + 5),
            new Vector2(centerX + offset + 7, middle),
            chevron);
        Raylib.DrawTriangle(
            new Vector2(centerX - offset, middle - 5),
            new Vector2(centerX - offset, middle + 5),
            new Vector2(centerX - offset - 7, middle),
            chevron);
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

using System;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// The campaign cards — floor intro, floor cleared, run summary and caught — drawn with the
/// compiled bitmap font instead of the system typeface.
/// </summary>
/// <remarks>
/// <para>
/// These are the screens between floors, so they are drawn over the dimmed world after the canvas
/// is blitted: text lands on whole window pixels at an integer scale, which keeps it as crisp as
/// the tiles under it.
/// </para>
/// <para>
/// A card is three bands — a small caption, a large heading in the floor's accent, and the prompt —
/// with a two-pixel rule between heading and prompt. That is the whole design: the floors differ by
/// their accent and their name, and the type is the same everywhere, which is what makes the
/// screens read as one game.
/// </para>
/// </remarks>
public static class PixelCards
{
    private static readonly Color Backdrop = new(2, 7, 9, 236);

    private static readonly Color Text = new(238, 240, 236, 255);

    private static readonly Color Dim = new(138, 152, 150, 255);

    private static readonly Color Danger = new(255, 138, 126, 255);

    /// <summary>Draws the card shown before a floor begins.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floor">The floor about to start.</param>
    /// <param name="floorCount">How many floors the run has.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void FloorIntro(PixelFont font, FloorDefinition floor, int floorCount, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(floor);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        font.DrawCentered(
            FormattableString.Invariant($"FLOOR {floor.Number} OF {floorCount}"),
            width / 2,
            centreY - 104,
            2,
            Dim);

        Heading(font, floor.Name.ToUpperInvariant(), width / 2, centreY - 70, width - 96, accent);
        Rule(width / 2, centreY + 4, accent);
        font.DrawCentered("DESCEND", width / 2, centreY + 20, 2, Text);
        font.DrawCentered("ENTER / SPACE: BEGIN", width / 2, centreY + 52, 2, Dim);
    }

    /// <summary>Draws the card shown when a floor's exit is reached.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floor">The floor that was cleared.</param>
    /// <param name="hearts">Hearts the player still has.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void FloorCleared(PixelFont font, FloorDefinition floor, int hearts, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(floor);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        Heading(font, "FLOOR CLEARED", width / 2, centreY - 96, width - 96, accent);
        font.DrawCentered(floor.Name.ToUpperInvariant(), width / 2, centreY - 56, 2, Dim);
        DrawHearts(font, width / 2, centreY - 24, hearts);
        Rule(width / 2, centreY + 24, accent);
        font.DrawCentered("ENTER: DESCEND", width / 2, centreY + 40, 2, Dim);
    }

    /// <summary>Draws the run summary after the last floor.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floorCount">How many floors the run had.</param>
    /// <param name="hearts">Hearts the player finished with.</param>
    /// <param name="accent">The final floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static void RunComplete(PixelFont font, int floorCount, int hearts, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        Heading(font, "YOU ESCAPED", width / 2, centreY - 104, width - 96, accent);
        font.DrawCentered("THE DEPTHS", width / 2, centreY - 62, 2, Dim);
        font.DrawCentered(
            FormattableString.Invariant($"{floorCount} FLOORS CLEARED"),
            width / 2,
            centreY - 30,
            2,
            Text);
        DrawHearts(font, width / 2, centreY + 2, hearts);
        Rule(width / 2, centreY + 48, accent);
        font.DrawCentered("R / ENTER: NEW RUN      ESC: QUIT", width / 2, centreY + 64, 2, Dim);
    }

    /// <summary>Draws the card shown when the killer catches the player.</summary>
    /// <param name="font">The loaded font.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static void Caught(PixelFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        Heading(font, "THE THING FOUND YOU", width / 2, centreY - 40, width - 96, Danger);
        Rule(width / 2, centreY + 4, Danger);
        font.DrawCentered("R / ENTER: NEW RUN      ESC: QUIT", width / 2, centreY + 20, 2, Dim);
    }

    /// <summary>
    /// Draws a heading at the largest integer scale that fits the card.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="text">The heading.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="maxWidth">The widest the heading may draw.</param>
    /// <param name="colour">The heading's colour.</param>
    /// <remarks>
    /// A floor can be named anything, and a name that runs off the window would be worse than a
    /// slightly smaller one. Three steps is enough: 4 is the designed size, 2 is the smallest that
    /// still reads like a title.
    /// </remarks>
    private static void Heading(PixelFont font, string text, int centreX, int y, int maxWidth, Color colour)
    {
        var scale = 4;

        while (scale > 2 && PixelFont.MeasureWidth(text, scale) > maxWidth)
        {
            scale--;
        }

        font.DrawCentered(text, centreX, y, scale, colour);
    }

    /// <summary>Draws a row of hearts centred on <paramref name="centreX"/>.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="hearts">How many hearts to show.</param>
    private static void DrawHearts(PixelFont font, int centreX, int y, int hearts)
    {
        const int scale = 3;
        const int gap = 4;
        const int shown = 8;
        var count = Math.Clamp(hearts, 0, shown);
        var icon = PixelFont.IconSize * scale;

        if (count == 0)
        {
            font.DrawCentered("NO HEARTS LEFT", centreX, y, 2, Dim);
            return;
        }

        var total = (count * icon) + ((count - 1) * gap);
        var x = centreX - (total / 2);

        for (var index = 0; index < count; index++)
        {
            font.DrawIcon("heart-full", x + (index * (icon + gap)), y, scale, Color.White);
        }

        if (hearts > shown)
        {
            font.Draw(
                FormattableString.Invariant($"+{hearts - shown}"),
                centreX + (total / 2) + gap,
                y,
                2,
                Dim);
        }
    }

    /// <summary>Paints the dimmed backdrop that every card sits on.</summary>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    private static void Begin(int width, int height) =>
        Raylib.DrawRectangle(0, 0, width, height, Backdrop);

    /// <summary>Draws the two-pixel rule under a heading.</summary>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="accent">The rule's colour.</param>
    private static void Rule(int centreX, int y, Color accent)
    {
        const int halfWidth = 120;

        Raylib.DrawRectangle(
            centreX - halfWidth,
            y,
            halfWidth * 2,
            2,
            new Color(accent.R, accent.G, accent.B, (byte)150));
    }
}

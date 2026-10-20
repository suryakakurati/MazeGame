using System;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// The campaign's non-game screens: the floor intro card, the floor-cleared card and the run
/// summary. Split into a partial so the cave renderer keeps its palette and text helpers.
/// </summary>
public sealed partial class RaylibRenderer
{
    /// <summary>
    /// Covers the window while a floor is introduced: its number, its name and the theme's
    /// accent colour, so the player knows where they are before the chase starts.
    /// </summary>
    /// <param name="floor">The floor about to start.</param>
    /// <param name="floorCount">How many floors the run has, for the "n of m" line.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public void DrawFloorIntro(FloorDefinition floor, int floorCount)
    {
        ArgumentNullException.ThrowIfNull(floor);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var accent = AccentFor(floor.ThemeId);
        var centerX = width / 2f;
        var centerY = height / 2f;

        Raylib.DrawRectangle(0, 0, width, height, Rgba(1, 5, 7, 226));
        Raylib.DrawCircleGradient(
            new System.Numerics.Vector2(centerX, centerY),
            320,
            Rgba(accent.R, accent.G, accent.B, 26),
            Transparent);

        TextCentered($"FLOOR {floor.Number} OF {floorCount}", centerX, centerY - 96, BannerSubtitleSize, HintText);
        TextCentered(floor.Name.ToUpperInvariant(), centerX, centerY - 62, BannerTitleSize + 4, accent, 2.6f);

        Raylib.DrawLineEx(
            new System.Numerics.Vector2(centerX - 140, centerY - 8),
            new System.Numerics.Vector2(centerX + 140, centerY - 8),
            1.0f,
            Rgba(accent.R, accent.G, accent.B, 120));

        TextCentered("DESCEND", centerX, centerY + 26, BannerSubtitleSize, HudText);
        TextCentered("ENTER / SPACE: BEGIN", centerX, centerY + 58, 12, HintText);
    }

    /// <summary>
    /// Draws the card shown when a floor's exit is reached: the floor's name, the hearts carried
    /// forward and the way on.
    /// </summary>
    /// <param name="floor">The floor that was cleared.</param>
    /// <param name="hearts">Hearts the player still has.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public void DrawFloorCleared(FloorDefinition floor, int hearts)
    {
        ArgumentNullException.ThrowIfNull(floor);

        DrawCampaignPanel(
            "FLOOR CLEARED",
            $"{floor.Name.ToUpperInvariant()}  ·  {hearts} {HeartWord(hearts)} LEFT",
            "ENTER: DESCEND",
            AccentFor(floor.ThemeId));
    }

    /// <summary>
    /// Draws the run summary after the final floor: floors cleared, hearts left and the prompt for
    /// a fresh run.
    /// </summary>
    /// <param name="floorCount">How many floors the run had.</param>
    /// <param name="hearts">Hearts the player finished with.</param>
    public void DrawRunComplete(int floorCount, int hearts) =>
        DrawCampaignPanel(
            "YOU ESCAPED THE DEPTHS",
            $"{floorCount} FLOORS  ·  {hearts} {HeartWord(hearts)} LEFT",
            "R / ENTER: NEW RUN      ESC: QUIT",
            ExitOpenColor);

    /// <summary>
    /// Draws a centred glass panel with a title, a subtitle and a prompt, in the same language as
    /// the existing ending card.
    /// </summary>
    private void DrawCampaignPanel(string title, string subtitle, string prompt, Color accent)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        const float panelWidth = 490;
        const float panelHeight = 178;
        var left = (width - panelWidth) / 2f;
        var top = (height - panelHeight) / 2f;

        Raylib.DrawRectangleRounded(new Rectangle(left, top, panelWidth, panelHeight), 0.08f, 6, Rgba(2, 8, 10, 240));
        Raylib.DrawRectangleRoundedLines(
            new Rectangle(left, top, panelWidth, panelHeight),
            0.08f,
            6,
            Rgba(accent.R, accent.G, accent.B, 130));

        TextCentered(title, width / 2f, top + 30, BannerTitleSize, accent, 2.4f);
        TextCentered(subtitle, width / 2f, top + 76, BannerSubtitleSize, HudText);

        Raylib.DrawLineEx(
            new System.Numerics.Vector2(left + 70, top + 108),
            new System.Numerics.Vector2(left + panelWidth - 70, top + 108),
            1.0f,
            Rgba(accent.R, accent.G, accent.B, 90));

        TextCentered(prompt, width / 2f, top + 128, BannerSubtitleSize, HintText);
    }

    /// <summary>
    /// Maps a floor's theme id to the accent colour its cards use. Core never knows about colours;
    /// this is the one place the three shipped themes are recognised.
    /// </summary>
    /// <param name="themeId">The floor's theme id.</param>
    private static Color AccentFor(string themeId) => themeId switch
    {
        "shallows" => GlowGreen,
        "rootbound" => GlowPink,
        "drowned" => GlowCyan,
        _ => ExitOpenColor,
    };

    /// <summary>Returns "HEART" or "HEARTS" for a count, for the panel subtitles.</summary>
    private static string HeartWord(int hearts) => hearts == 1 ? "HEART" : "HEARTS";
}

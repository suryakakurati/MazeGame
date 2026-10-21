using System;
using System.Collections.Generic;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// The draft between two floors — the one campaign screen still set in the system typeface. The
/// floor intro, floor-cleared and run-summary cards are drawn by
/// <see cref="Pixels.PixelCards"/> in the compiled font instead.
/// </summary>
public sealed partial class RaylibRenderer
{
    /// <summary>
    /// Draws the draft between two floors: the offers as cards, the run's totals underneath the
    /// header, and the skip-to-heal as a visible alternative.
    /// </summary>
    /// <param name="campaign">The run, whose open draft is being chosen from.</param>
    /// <param name="selectedIndex">The card the keyboard is on, clamped to the offers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="campaign"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the campaign has no open draft; the frontend is responsible for only asking
    /// for this screen at the right moment.
    /// </exception>
    public void DrawDraft(Campaign campaign, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var draft = campaign.Draft
            ?? throw new InvalidOperationException("There is no draft to draw.");

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var accent = AccentFor(campaign.CurrentFloor.ThemeId);
        var centerX = width / 2f;
        var offers = draft.Offers;
        var selected = offers.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, offers.Count - 1);

        Raylib.DrawRectangle(0, 0, width, height, Rgba(1, 5, 7, 238));

        TextCentered($"FLOOR {campaign.FloorNumber} CLEARED", centerX, 64, BannerSubtitleSize, HintText);
        TextCentered("CHOOSE A CARD", centerX, 90, BannerTitleSize, accent, 2.6f);
        TextCentered(campaign.Modifiers.Describe(), centerX, 134, 12, HudText);

        if (offers.Count == 0)
        {
            TextCentered("OF COURSE IT IS EMPTY", centerX, height / 2f - 30, BannerSubtitleSize, HudText);
            TextCentered("S: TAKE THE HEART", centerX, height / 2f + 2, BannerSubtitleSize, HintText);
            return;
        }

        // Up to three cards sit in one row; four or more wrap into two, because a card narrower
        // than this cannot hold its own sentence.
        var columns = offers.Count <= 3 ? offers.Count : 2;
        var rows = (offers.Count + columns - 1) / columns;
        const float gap = 12f;
        const float sideMargin = 28f;
        var usable = width - (2 * sideMargin);
        var cardWidth = (usable - (gap * (columns - 1))) / columns;

        // The card height follows the number of rows so the whole grid stays inside the window.
        // Three rows cannot happen with today's catalog (the most a draft offers is four), but the
        // grid should not assume that: a future +2 offer card would otherwise draw off-screen.
        var cardHeight = rows switch
        {
            1 => 330f,
            2 => 250f,
            _ => 165f,
        };
        var top = 176f;

        for (var index = 0; index < offers.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var left = sideMargin + (column * (cardWidth + gap));
            var cardTop = top + (row * (cardHeight + gap));

            DrawCardFace(offers[index], index + 1, left, cardTop, cardWidth, cardHeight, index == selected, accent);
        }

        var prompt = offers.Count == 1
            ? "ENTER: TAKE    S: SKIP FOR A HEART"
            : "A / D: CHOOSE    ENTER: TAKE    S: SKIP FOR A HEART";

        TextCentered(prompt, centerX, height - 78, BannerSubtitleSize, HudText);
        TextCentered(
            $"SKIP HEALS ONE HEART  ·  NEXT FLOOR: {campaign.HeartsOnNextFloor} {HeartWord(campaign.HeartsOnNextFloor)}",
            centerX,
            height - 54,
            12,
            HintText);
    }

    /// <summary>
    /// Draws one card: its tier, its name, and the sentence that states both halves of the trade.
    /// </summary>
    /// <param name="card">The card to draw.</param>
    /// <param name="number">Its one-based position in the draft, for the number key that takes it.</param>
    /// <param name="left">Left edge in pixels.</param>
    /// <param name="top">Top edge in pixels.</param>
    /// <param name="cardWidth">Width in pixels.</param>
    /// <param name="cardHeight">Height in pixels.</param>
    /// <param name="isSelected">Whether the keyboard is on this card.</param>
    /// <param name="accent">The floor's accent colour, used for the selection ring.</param>
    private void DrawCardFace(
        Card card,
        int number,
        float left,
        float top,
        float cardWidth,
        float cardHeight,
        bool isSelected,
        Color accent)
    {
        var tierColor = TierColor(card.Tier);
        var body = isSelected ? Rgba(5, 20, 22, 244) : Rgba(3, 12, 14, 232);
        var border = isSelected ? accent : Rgba(tierColor.R, tierColor.G, tierColor.B, 70);

        Raylib.DrawRectangleRounded(new Rectangle(left, top, cardWidth, cardHeight), 0.06f, 6, body);
        Raylib.DrawRectangleRoundedLines(
            new Rectangle(left, top, cardWidth, cardHeight),
            0.06f,
            6,
            border);

        // A second, tighter ring on the chosen card: a 1 px line is hard to see against the cave,
        // and the selection has to be obvious without reading the prompt.
        if (isSelected)
        {
            Raylib.DrawRectangleRoundedLines(
                new Rectangle(left + 3, top + 3, cardWidth - 6, cardHeight - 6),
                0.05f,
                6,
                Rgba(accent.R, accent.G, accent.B, 90));
        }

        var centerX = left + (cardWidth / 2f);
        var inner = (int)(cardWidth - 24);

        // A card narrower than this cannot hold the face at 12 px without running off its edge, so
        // the sentence is set smaller rather than allowed to overflow. (Today's widest grid is
        // three cards across, which stays above the threshold.)
        var faceSize = cardWidth < 150 ? 10 : 12;
        var faceLineHeight = faceSize + 5;

        TextCentered(TierWord(card.Tier), centerX, top + 18, 11, tierColor, 2.0f);

        var nameLines = Wrap(card.Name.ToUpperInvariant(), inner, 16);
        for (var index = 0; index < nameLines.Count; index++)
        {
            TextCentered(nameLines[index], centerX, top + 44 + (index * 20), 16, HudText, 1.8f);
        }

        var lineY = top + 60 + (nameLines.Count * 20);
        var faceLines = Wrap(card.FaceText, inner, faceSize);

        // The number key label sits at the bottom of the card, so the sentence stops above it.
        // Nine shipped faces all fit; the cap is a guard so a future long face cannot draw over
        // the label.
        var maxLines = Math.Max(1, (int)((top + cardHeight - 46 - lineY) / faceLineHeight));

        for (var index = 0; index < faceLines.Count && index < maxLines; index++)
        {
            var line = faceLines[index];

            if (index == maxLines - 1 && faceLines.Count > maxLines)
            {
                line = $"{line.TrimEnd('.', ' ')}…";
            }

            TextCentered(line, centerX, lineY, faceSize, HintText);
            lineY += faceLineHeight;
        }

        TextCentered(
            isSelected ? "TAKE" : $"[{number}]",
            centerX,
            top + cardHeight - 30,
            11,
            isSelected ? accent : Rgba(120, 150, 140, 255),
            2.0f);
    }

    /// <summary>Returns the label a card's tier is drawn with.</summary>
    private static string TierWord(CardTier tier) => tier switch
    {
        CardTier.Common => "COMMON",
        CardTier.Uncommon => "UNCOMMON",
        CardTier.Rare => "RARE",
        _ => "CURSE",
    };

    /// <summary>Returns the colour a card's tier is drawn in.</summary>
    private static Color TierColor(CardTier tier) => tier switch
    {
        CardTier.Common => HudText,
        CardTier.Uncommon => GlowGreen,
        CardTier.Rare => GlowPink,
        _ => DangerText,
    };

    /// <summary>
    /// Splits <paramref name="text"/> into lines that fit <paramref name="maxWidth"/> pixels.
    /// </summary>
    /// <remarks>
    /// The card face is a full sentence, and a single line of it would run off a 192 px card. The
    /// split is by words so the sentence stays readable, and a word wider than the card is left on
    /// its own line rather than being broken mid-word (Raylib's font has no hyphenation).
    /// </remarks>
    private List<string> Wrap(string text, int maxWidth, int size)
    {
        var lines = new List<string>();
        var line = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";

            if (line.Length > 0 && TextWidth(candidate, size) > maxWidth)
            {
                lines.Add(line);
                line = word;
                continue;
            }

            line = candidate;
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Maps a floor's theme id to the accent colour its cards use. Core never knows about colours;
    /// this is the one place the three shipped themes are recognised.
    /// </summary>
    /// <param name="themeId">The floor's theme id.</param>
    internal static Color AccentFor(string themeId) => themeId switch
    {
        "shallows" => GlowGreen,
        "rootbound" => GlowPink,
        "drowned" => GlowCyan,
        _ => ExitOpenColor,
    };

    /// <summary>Returns "HEART" or "HEARTS" for a count, for the panel subtitles.</summary>
    private static string HeartWord(int hearts) => hearts == 1 ? "HEART" : "HEARTS";
}

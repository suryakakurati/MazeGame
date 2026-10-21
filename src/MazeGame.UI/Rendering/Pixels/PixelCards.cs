using System;
using System.Collections.Generic;
using MazeGame.Core.Game;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Every screen outside the maze — title, draft and the four campaign cards — drawn with the
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

    private static readonly Color Glow = new(126, 236, 186, 255);

    /// <summary>
    /// Draws the title's type: the wordmark, the promise and the two menu entries, in the same
    /// face the maze's HUD and cards use.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="selectedItem">The highlighted menu entry, zero-based.</param>
    /// <remarks>
    /// The atmosphere behind this — the cave gradient, the breathing blooms and the drifting
    /// spores — is painted by the renderer first, so this method only sets type on top of it.
    /// Every size is derived from the window, because the frame is resizable: the wordmark is as
    /// wide as the window can hold, and the menu keeps its distance below it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static void Title(PixelFont font, int selectedItem)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var time = Raylib.GetTime();
        var breath = 0.74 + (Math.Sin(time * 1.5) * 0.26);

        var captionScale = width >= 760 ? 2 : 1;
        font.DrawCentered("A DESCENT IN THE DARK", centreX, (int)(height * 0.105f), captionScale, Dim);

        var wordScale = Math.Clamp(width / 128, 4, 7);
        var wordY = (int)(height * 0.145f);
        font.DrawCentered("BIOLUMINESCENT", centreX, wordY, wordScale, new Color(232, 248, 240, 255));

        var markScale = Math.Clamp(width / 112, 5, 9);
        var markY = wordY + (PixelFont.LineHeight * wordScale) + 14;
        font.DrawCentered("DEPTHS", centreX, markY, markScale, Fade(Glow, breath));

        // A pixel rule either side of a single bright seed, which is the title screen's one flourish.
        var ruleY = markY + (PixelFont.LineHeight * markScale) + 30;
        DrawTitleRule(centreX, ruleY, Glow);

        font.DrawCentered(
            "COLLECT THE KEYS. OPEN THE ARCH. DO NOT BE CAUGHT.",
            centreX,
            ruleY + 18,
            width >= 760 ? 2 : 1,
            Dim);

        var menuScale = Math.Clamp(width / 260, 2, 4);
        var menuY = Math.Max(ruleY + 58, (int)(height * 0.54f));

        DrawMenuItem(font, "DESCEND", centreX, menuY, selectedItem == 0, time, menuScale);
        DrawMenuItem(font, "LEAVE", centreX, menuY + menuScale * 10 + 14, selectedItem == 1, time, menuScale);

        font.DrawCentered(
            "W/S OR ARROWS: SELECT   ENTER: CONFIRM   ESC: LEAVE",
            centreX,
            height - 78,
            1,
            Dim);
        font.DrawCentered("COP3503H / GROUP 5", centreX, height - 44, 1, Fade(Dim, 0.62));
    }

    /// <summary>
    /// Draws the draft between two floors: the offers as cards, the run's totals above them, and
    /// the skip-to-heal as a visible alternative.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="campaign">The run, whose open draft is being chosen from.</param>
    /// <param name="selectedIndex">The card the keyboard is on, clamped to the offers.</param>
    /// <param name="accent">The cleared floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the campaign has no open draft; the frontend only asks for this screen at the
    /// right moment.
    /// </exception>
    public static void Draft(PixelFont font, Campaign campaign, int selectedIndex, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(campaign);

        var draft = campaign.Draft
            ?? throw new InvalidOperationException("There is no draft to draw.");

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var offers = draft.Offers;
        var selected = offers.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, offers.Count - 1);

        Begin(width, height);

        FloorEmblem.DrawCentered(campaign.CurrentFloor.ThemeId, centreX, 10, 2, accent);
        font.DrawCentered(
            FormattableString.Invariant($"FLOOR {campaign.FloorNumber} CLEARED"),
            centreX,
            42,
            2,
            Dim);
        Heading(font, "CHOOSE A CARD", centreX, 66, width - 48, accent);
        font.DrawCentered(
            Fit(campaign.Modifiers.Describe(), 1, width - 48),
            centreX,
            96,
            1,
            Dim);

        if (offers.Count == 0)
        {
            font.DrawCentered("OF COURSE IT IS EMPTY", centreX, height / 2 - 30, 2, Text);
            font.DrawCentered("S: TAKE THE HEART", centreX, height / 2 + 4, 2, Dim);
            return;
        }

        // Up to three cards sit in one row; four or more wrap into two, because a card narrower
        // than this cannot hold its own sentence.
        const int gap = 14;
        const int sideMargin = 24;
        var columns = offers.Count <= 3 ? offers.Count : 2;
        var rows = (offers.Count + columns - 1) / columns;
        var usable = width - (2 * sideMargin);
        var cardWidth = (usable - (gap * (columns - 1))) / columns;

        var promptTop = height - 66;
        const int headerBottom = 120;
        var room = Math.Max(96, promptTop - headerBottom - 10);

        // A card is a little taller than it is wide, which is what makes it read as a card rather
        // than a panel; the grid is then centred in the room between the header and the prompt, so
        // a tall window has no dead band under the cards.
        var cardHeight = Math.Min(
            (room - (gap * (rows - 1))) / rows,
            Math.Max(260, (int)(cardWidth * 1.15f)));
        var gridHeight = (rows * cardHeight) + ((rows - 1) * gap);
        var top = headerBottom + ((room - gridHeight) / 2);

        for (var index = 0; index < offers.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var left = sideMargin + (column * (cardWidth + gap));
            var cardTop = top + (row * (cardHeight + gap));

            DrawDraftCard(font, offers[index], index + 1, left, cardTop, cardWidth, cardHeight, index == selected, accent);
        }

        var prompt = offers.Count == 1
            ? "ENTER: TAKE   S: SKIP FOR A HEART"
            : "A / D: CHOOSE   ENTER: TAKE   S: SKIP FOR A HEART";
        var promptScale = width >= 700 ? 2 : 1;

        font.DrawCentered(Fit(prompt, promptScale, width - 24), centreX, height - 56, promptScale, Text);
        font.DrawCentered(
            Fit($"SKIP HEALS ONE HEART - NEXT FLOOR: {campaign.HeartsOnNextFloor} {HeartWord(campaign.HeartsOnNextFloor)}", 1, width - 24),
            centreX,
            height - 30,
            1,
            Dim);
    }

    /// <summary>
    /// Draws a gate: two descent conditions, one of which must be taken.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="offer">The open condition offer.</param>
    /// <param name="selectedIndex">The condition the keyboard is on.</param>
    /// <param name="accent">The cleared floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void ConditionPick(PixelFont font, ConditionOffer offer, int selectedIndex, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(offer);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var options = offer.Options;
        var selected = options.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, options.Count - 1);

        Begin(width, height);

        font.DrawCentered("THE DESCENT SHIFTS", centreX, 38, 2, Dim);
        Heading(font, "CHOOSE A RULE", centreX, 62, width - 64, accent);
        font.DrawCentered(
            "IT APPLIES TO EVERY FLOOR BELOW",
            centreX,
            98,
            2,
            Dim);

        const int sideMargin = 36;
        const int gap = 18;
        var cardWidth = Math.Min(360, (width - (2 * sideMargin) - gap) / 2);
        var cardHeight = Math.Min(300, height - 260);
        var gridWidth = (cardWidth * options.Count) + (gap * Math.Max(0, options.Count - 1));
        var left = centreX - (gridWidth / 2);
        var top = 136;

        for (var index = 0; index < options.Count; index++)
        {
            DrawConditionCard(
                font,
                options[index],
                index + 1,
                left + (index * (cardWidth + gap)),
                top,
                cardWidth,
                cardHeight,
                index == selected,
                accent);
        }

        var promptScale = width >= 700 ? 2 : 1;
        font.DrawCentered(
            Fit("A / D: CHOOSE   ENTER: TAKE THE RULE", promptScale, width - 24),
            centreX,
            height - 54,
            promptScale,
            Text);
        font.DrawCentered("THERE IS NO SKIP", centreX, height - 26, 1, Dim);
    }

    /// <summary>
    /// Draws one descent condition: its name, its one-line rule and the take prompt.
    /// </summary>
    private static void DrawConditionCard(
        PixelFont font,
        DescentCondition condition,
        int number,
        int left,
        int top,
        int cardWidth,
        int cardHeight,
        bool isSelected,
        Color accent)
    {
        var body = isSelected ? new Color(6, 24, 26, 248) : new Color(3, 12, 14, 238);
        var border = isSelected ? accent : Fade(accent, 0.38);

        Raylib.DrawRectangle(left, top, cardWidth, cardHeight, body);
        Raylib.DrawRectangle(left, top, cardWidth, 2, Fade(accent, isSelected ? 0.95 : 0.55));
        Frame(left, top, cardWidth, cardHeight, border);

        if (isSelected)
        {
            Frame(left + 3, top + 3, cardWidth - 6, cardHeight - 6, Fade(accent, 0.30));
            Raylib.DrawRectangle(left, top + 5, 3, cardHeight - 10, accent);
        }

        var centreX = left + (cardWidth / 2);
        var inner = cardWidth - 24;

        font.DrawCentered("DESCENT RULE", centreX, top + 14, 1, Dim);

        var nameScale = cardWidth >= 240 ? 3 : 2;
        var lines = Wrap(DescentConditionCatalog.Name(condition).ToUpperInvariant(), nameScale, inner);
        var y = top + 34;

        foreach (var line in lines)
        {
            font.DrawCentered(line, centreX, y, nameScale, Text);
            y += (PixelFont.LineHeight * nameScale) + 6;
        }

        var descriptionScale = cardWidth >= 240 ? 2 : 1;
        var description = Wrap(DescentConditionCatalog.Description(condition), descriptionScale, inner);
        var lineHeight = (PixelFont.LineHeight * descriptionScale) + 4;

        foreach (var line in description)
        {
            font.DrawCentered(line, centreX, y, descriptionScale, Dim);
            y += lineHeight;
        }

        font.DrawCentered(
            isSelected ? "TAKE" : FormattableString.Invariant($"[{number}]"),
            centreX,
            top + cardHeight - 28,
            2,
            isSelected ? accent : Fade(Dim, 0.7));
    }

    /// <summary>Draws the card shown before a floor begins.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floor">The floor about to start.</param>
    /// <param name="floorCount">How many floors the run has.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <param name="conditions">The descent conditions in force, or an empty list.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void FloorIntro(
        PixelFont font,
        FloorDefinition floor,
        int floorCount,
        Color accent,
        IReadOnlyList<DescentCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(conditions);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        // Every floor announces itself with its own mark and palette before it says its name:
        // the emblem is what a returning player recognises from a screenshot.
        var style = FloorStyleCatalog.For(floor.ThemeId);

        FloorEmblem.DrawCentered(floor.ThemeId, width / 2, centreY - 156, 4, accent);
        DrawPaletteStrip(style, width / 2, centreY - 114);

        font.DrawCentered(
            FormattableString.Invariant($"FLOOR {floor.Number} OF {floorCount}"),
            width / 2,
            centreY - 100,
            2,
            Dim);

        Heading(font, floor.Name.ToUpperInvariant(), width / 2, centreY - 68, width - 96, accent);

        if (floor.Gimmick is { Length: > 0 } gimmick)
        {
            font.DrawCentered(Fit(gimmick.ToUpperInvariant(), 2, width - 96), width / 2, centreY - 40, 2, Dim);
        }

        if (conditions.Count > 0)
        {
            font.DrawCentered(
                Fit(DescentConditionCatalog.Line(conditions), 1, width - 64),
                width / 2,
                centreY - 18,
                1,
                accent);
        }

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

        FloorEmblem.DrawCentered(floor.ThemeId, width / 2, centreY - 148, 4, accent);
        DrawPaletteStrip(FloorStyleCatalog.For(floor.ThemeId), width / 2, centreY - 106);

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
    /// <param name="themeId">The final floor's theme, whose emblem and palette close the run.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void RunComplete(PixelFont font, int floorCount, int hearts, Color accent, string themeId)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(themeId);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        FloorEmblem.DrawCentered(themeId, width / 2, centreY - 164, 4, accent);
        DrawPaletteStrip(FloorStyleCatalog.For(themeId), width / 2, centreY - 122);

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
    /// <param name="floorNumber">The floor the run ended on, one-based.</param>
    /// <param name="floorCount">How many floors the run had.</param>
    /// <param name="floorName">The name of the floor the run ended on.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> or <paramref name="floorName"/> is null.</exception>
    /// <remarks>
    /// A lost run has to say how far it got: the floor reached is the score, so it is the one
    /// number on the card and it is drawn larger than the heading's own supporting line. The
    /// floor's name sits under it so the run ends on a place, not just a count.
    /// </remarks>
    public static void Caught(PixelFont font, int floorNumber, int floorCount, string floorName)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(floorName);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreY = height / 2;

        Begin(width, height);

        Heading(font, "THE THING FOUND YOU", width / 2, centreY - 74, width - 96, Danger);
        font.DrawCentered("YOU MADE IT TO", width / 2, centreY - 28, 2, Dim);
        font.DrawCentered(
            FormattableString.Invariant($"FLOOR {floorNumber} OF {floorCount}"),
            width / 2,
            centreY - 4,
            3,
            Text);
        font.DrawCentered(Fit(floorName.ToUpperInvariant(), 2, width - 96), width / 2, centreY + 28, 2, Dim);
        Rule(width / 2, centreY + 58, Danger);
        font.DrawCentered("R / ENTER: NEW RUN      ESC: QUIT", width / 2, centreY + 74, 2, Dim);
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

    /// <summary>
    /// Draws five small colour blocks that summarise the floor's look: accent, weather, fog and
    /// the two ends of its light. It is the quickest possible glance at what a floor is made of.
    /// </summary>
    /// <param name="style">The floor's style.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    private static void DrawPaletteStrip(FloorStyle style, int centreX, int y)
    {
        const int cell = 8;
        const int gap = 4;
        const int count = 5;
        var total = (count * cell) + ((count - 1) * gap);
        var left = centreX - (total / 2);

        var colours = new[]
        {
            style.Accent,
            style.AmbientColor,
            style.Fog,
            FloorStyle.Hsl(style.AccentHue, 0.40, 0.32),
            style.LightTint,
        };

        for (var index = 0; index < count; index++)
        {
            Raylib.DrawRectangle(left + (index * (cell + gap)), y, cell, 4, colours[index]);
        }
    }

    /// <summary>
    /// Draws the title's rule: a dotted pixel line either side of one bright seed.
    /// </summary>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The row to draw on.</param>
    /// <param name="colour">The rule's colour.</param>
    private static void DrawTitleRule(int centreX, int y, Color colour)
    {
        const int dash = 7;
        const int gap = 5;
        const int dashes = 9;
        var span = (dashes * dash) + ((dashes - 1) * gap);
        var left = centreX - (span / 2);

        for (var index = 0; index < dashes; index++)
        {
            var x = left + (index * (dash + gap));
            var fade = 0.35 + (0.65 * (1.0 - (Math.Abs((dashes - 1) / 2.0 - index) / (dashes / 2.0))));

            Raylib.DrawRectangle(x, y, dash, 2, Fade(colour, fade));
        }

        Raylib.DrawRectangle(centreX - 2, y - 3, 5, 7, new Color(232, 255, 246, 255));
    }

    /// <summary>
    /// Draws one title menu entry, in brackets when it is the highlighted one.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="label">The entry's label.</param>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="selected">Whether the entry is highlighted.</param>
    /// <param name="time">Current time in seconds, for the pulse.</param>
    /// <param name="scale">The integer scale.</param>
    private static void DrawMenuItem(PixelFont font, string label, int centreX, int y, bool selected, double time, int scale)
    {
        if (selected)
        {
            // The highlight breathes, but never below 45%: the menu is the only thing to read.
            var pulse = 0.72 + (Math.Sin(time * 3.0) * 0.27);

            font.DrawCentered($"[ {label} ]", centreX, y, scale, Fade(Glow, pulse));
            return;
        }

        font.DrawCentered(label, centreX, y, scale, Fade(Dim, 0.72));
    }

    /// <summary>
    /// Draws one offer: its tier, its name and the sentence that states both halves of the trade.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="card">The card to draw.</param>
    /// <param name="number">Its one-based position in the draft.</param>
    /// <param name="left">Left edge in pixels.</param>
    /// <param name="top">Top edge in pixels.</param>
    /// <param name="cardWidth">Width in pixels.</param>
    /// <param name="cardHeight">Height in pixels.</param>
    /// <param name="isSelected">Whether the keyboard is on this card.</param>
    /// <param name="accent">The floor's accent colour, used for the selection frame.</param>
    private static void DrawDraftCard(
        PixelFont font,
        Card card,
        int number,
        int left,
        int top,
        int cardWidth,
        int cardHeight,
        bool isSelected,
        Color accent)
    {
        var tier = TierColor(card.Tier);
        var body = isSelected ? new Color(6, 24, 26, 248) : new Color(3, 12, 14, 238);
        var border = isSelected ? accent : Fade(tier, 0.42);

        Raylib.DrawRectangle(left, top, cardWidth, cardHeight, body);
        Raylib.DrawRectangle(left, top, cardWidth, 2, Fade(tier, isSelected ? 0.95 : 0.55));
        Frame(left, top, cardWidth, cardHeight, border);

        if (isSelected)
        {
            Frame(left + 3, top + 3, cardWidth - 6, cardHeight - 6, Fade(accent, 0.30));
            Raylib.DrawRectangle(left, top + 5, 3, cardHeight - 10, accent);
        }

        var centreX = left + (cardWidth / 2);
        var inner = cardWidth - 22;

        font.DrawCentered(TierWord(card.Tier), centreX, top + 12, 1, tier);

        // The name is the card's identity, so it takes the largest scale that fits; a name too
        // long for three letters' worth of room wraps rather than shrinking into a smudge.
        var nameScale = cardWidth >= 200 ? 3 : 2;
        while (nameScale > 2 && PixelFont.MeasureWidth(card.Name, nameScale) > inner)
        {
            nameScale--;
        }

        var nameY = top + 26;
        var nameLines = Wrap(card.Name.ToUpperInvariant(), nameScale, inner);

        foreach (var line in nameLines)
        {
            font.DrawCentered(line, centreX, nameY, nameScale, Text);
            nameY += (PixelFont.LineHeight * nameScale) + 6;
        }

        // The face is a full sentence: two pixels per glyph when the card has the width for it,
        // one when it does not, which is the difference between a 22 and a 44 character line.
        var faceScale = cardWidth >= 240 ? 2 : 1;
        var faceLine = (PixelFont.LineHeight * faceScale) + 4;
        var footerY = top + cardHeight - 26;
        var faceLines = Wrap(card.FaceText, faceScale, inner);
        var maxLines = Math.Max(1, (footerY - 8 - nameY) / faceLine);
        var faceY = nameY + 4;

        for (var index = 0; index < faceLines.Count && index < maxLines; index++)
        {
            var line = faceLines[index];

            if (index == maxLines - 1 && faceLines.Count > maxLines)
            {
                line = Fit($"{line.TrimEnd('.', ' ')}...", faceScale, inner);
            }

            font.DrawCentered(line, centreX, faceY, faceScale, Dim);
            faceY += faceLine;
        }

        font.DrawCentered(
            isSelected ? "TAKE" : FormattableString.Invariant($"[{number}]"),
            centreX,
            footerY,
            2,
            isSelected ? accent : Fade(Dim, 0.7));
    }

    /// <summary>Draws a one-pixel frame one pixel inside the given rectangle.</summary>
    /// <param name="left">Left edge.</param>
    /// <param name="top">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="colour">The frame's colour.</param>
    private static void Frame(int left, int top, int width, int height, Color colour)
    {
        Raylib.DrawRectangle(left, top, width, 1, colour);
        Raylib.DrawRectangle(left, top + height - 1, width, 1, colour);
        Raylib.DrawRectangle(left, top, 1, height, colour);
        Raylib.DrawRectangle(left + width - 1, top, 1, height, colour);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines that fit <paramref name="maxWidth"/>, breaking
    /// between words and hard-splitting a word that is wider than a whole line.
    /// </summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="scale">The scale it will be drawn at.</param>
    /// <param name="maxWidth">The widest a line may draw.</param>
    /// <returns>The lines, in order.</returns>
    private static List<string> Wrap(string text, int scale, int maxWidth)
    {
        var lines = new List<string>();
        var line = string.Empty;
        var perLine = CharsPerLine(scale, maxWidth);

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";

            if (candidate.Length <= perLine)
            {
                line = candidate;
                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(line);
                line = word;
                continue;
            }

            // A single word longer than a whole line is broken rather than left to overflow.
            lines.Add(word[..perLine]);
            line = word[perLine..];
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Returns <paramref name="text"/> cut to fit <paramref name="maxWidth"/> at
    /// <paramref name="scale"/>, with an ellipsis when anything was dropped.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="scale">The scale it will be drawn at.</param>
    /// <param name="maxWidth">The widest it may draw.</param>
    /// <returns>The text, possibly shortened.</returns>
    private static string Fit(string text, int scale, int maxWidth)
    {
        if (text.Length == 0 || PixelFont.MeasureWidth(text, scale) <= maxWidth)
        {
            return text;
        }

        var perLine = CharsPerLine(scale, maxWidth);

        return perLine <= 3 ? text[..perLine] : $"{text[..(perLine - 3)].TrimEnd(' ', '.', ',')}...";
    }

    /// <summary>
    /// Returns how many glyphs fit in <paramref name="maxWidth"/> at <paramref name="scale"/>.
    /// </summary>
    /// <param name="scale">The integer scale.</param>
    /// <param name="maxWidth">The available width in pixels.</param>
    /// <returns>At least one character.</returns>
    /// <remarks>
    /// <see cref="PixelFont.MeasureWidth"/> reports <c>(length * advance - 1) * scale</c>, because
    /// the trailing gap is not drawn. Inverting that exactly is what keeps a wrapped line from
    /// overflowing by one glyph, which is the difference between a card that looks set and one
    /// that looks broken.
    /// </remarks>
    private static int CharsPerLine(int scale, int maxWidth) =>
        Math.Max(1, ((maxWidth / scale) + 1) / PixelFont.Advance);

    /// <summary>Returns the label a card's tier is drawn with.</summary>
    /// <param name="tier">The tier.</param>
    private static string TierWord(CardTier tier) => tier switch
    {
        CardTier.Common => "COMMON",
        CardTier.Uncommon => "UNCOMMON",
        CardTier.Rare => "RARE",
        _ => "CURSE",
    };

    /// <summary>Returns the colour a card's tier is drawn in.</summary>
    /// <param name="tier">The tier.</param>
    private static Color TierColor(CardTier tier) => tier switch
    {
        CardTier.Common => Text,
        CardTier.Uncommon => new Color(140, 236, 168, 255),
        CardTier.Rare => new Color(248, 152, 224, 255),
        _ => Danger,
    };

    /// <summary>Returns "HEART" or "HEARTS" for a count.</summary>
    /// <param name="hearts">The number of hearts.</param>
    private static string HeartWord(int hearts) => hearts == 1 ? "HEART" : "HEARTS";

    /// <summary>Returns <paramref name="colour"/> multiplied to <paramref name="alpha"/> (0 to 1).</summary>
    /// <param name="colour">The source colour.</param>
    /// <param name="alpha">The multiplier.</param>
    private static Color Fade(Color colour, double alpha) =>
        new(colour.R, colour.G, colour.B, (byte)Math.Clamp(alpha * 255, 0, 255));

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

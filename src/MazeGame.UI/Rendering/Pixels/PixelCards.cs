using System;
using System.Collections.Generic;
using System.Numerics;
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
    /// <param name="hasContinue">
    /// Whether a saved run exists. CONTINUE is drawn only when it is; the frontend's highlight
    /// skips the entry too, so the two always agree about what is on the menu.
    /// </param>
    /// <param name="gardenBlooms">
    /// The garden the player has grown across finished runs, or zero for a first launch. It is
    /// set under the rule as a quiet line: a tally, not a score.
    /// </param>
    /// <remarks>
    /// The atmosphere behind this — the cave gradient, the breathing blooms and the drifting
    /// spores — is painted by the renderer first, so this method only sets type on top of it.
    /// Every size is derived from the window, because the frame is resizable: the wordmark is as
    /// wide as the window can hold, and the menu keeps its distance below it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public static void Title(PixelFont font, int selectedItem, bool hasContinue = false, int gardenBlooms = 0)
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
            "PICK THE BLOOMS. OPEN THE ARCH. DO NOT BE CAUGHT.",
            centreX,
            ruleY + 18,
            width >= 760 ? 2 : 1,
            Dim);

        if (gardenBlooms > 0)
        {
            font.DrawCentered($"THE GARDEN HOLDS {gardenBlooms} BLOOMS", centreX, ruleY + 44, 1, Fade(Glow, 0.7));
        }

        var menuScale = Math.Clamp(width / 260, 2, 4);
        var menuY = Math.Max(ruleY + 58, (int)(height * 0.54f));

        var rowStep = (menuScale * 10) + 14;
        var row = 0;

        DrawMenuItem(font, "DESCEND", centreX, menuY, selectedItem == 0, time, menuScale);
        row++;

        if (hasContinue)
        {
            DrawMenuItem(font, "CONTINUE", centreX, menuY + (rowStep * row), selectedItem == 1, time, menuScale);
            row++;
        }

        DrawMenuItem(font, "TUTORIAL", centreX, menuY + (rowStep * row), selectedItem == (hasContinue ? 2 : 1), time, menuScale);
        row++;

        DrawMenuItem(font, "CARDS", centreX, menuY + (rowStep * row), selectedItem == (hasContinue ? 3 : 2), time, menuScale);
        row++;

        DrawMenuItem(font, "LEAVE", centreX, menuY + (rowStep * row), selectedItem == (hasContinue ? 4 : 3), time, menuScale);

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

        // The reroll key gets its own line below, with its price on it: a prompt that named R
        // here as well would be the width of the window, and the key would be the part cut off.
        var prompt = offers.Count == 1
            ? "ENTER: TAKE   S: SKIP"
            : "A / D: CHOOSE   ENTER: TAKE   S: SKIP";
        var promptScale = width >= 700 ? 2 : 1;

        font.DrawCentered(Fit(prompt, promptScale, width - 24), centreX, height - 56, promptScale, Text);
        font.DrawCentered(
            Fit($"SKIP HEALS ONE HEART - NEXT FLOOR: {campaign.HeartsOnNextFloor} {HeartWord(campaign.HeartsOnNextFloor)}", 1, width - 24),
            centreX,
            height - 30,
            1,
            Dim);

        // The reroll price is part of the card: the first one costs a heart, and the second one
        // costs two, so the player can see the ladder before pressing the key.
        var rerollCost = campaign.DraftRerollCost;
        var rerollColour = campaign.CanRerollDraft ? Glow : Fade(Dim, 0.55);
        font.DrawCentered(
            Fit(
                campaign.CanRerollDraft
                    ? FormattableString.Invariant($"R: REROLL FOR {rerollCost} {HeartWord(rerollCost).ToUpperInvariant()}")
                    : "R: NOT ENOUGH HEARTS TO REROLL",
                1,
                width - 24),
            centreX,
            height - 16,
            1,
            rerollColour);
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
    /// Draws a safe room's card: its name, its prose, its price and its options.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="campaign">The run, whose open room is being read.</param>
    /// <param name="selectedIndex">The option the keyboard is on, clamped to the card.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the campaign has no open room; the frontend only asks for this screen at the
    /// right moment.
    /// </exception>
    /// <remarks>
    /// The whole card is one column of lines rather than a grid of cards: a room asks one question
    /// with two or three answers, and a list reads faster than a row. Once it is resolved the
    /// options are replaced by what the choice actually did, so the player learns the odds.
    /// </remarks>
    public static void Room(PixelFont font, Campaign campaign, int selectedIndex, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(campaign);

        var room = campaign.Room
            ?? throw new InvalidOperationException("There is no room card to draw.");

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var options = room.Options;
        var selected = options.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, options.Count - 1);

        Begin(width, height);

        FloorEmblem.DrawCentered(campaign.CurrentFloor.ThemeId, centreX, 10, 2, accent);
        font.DrawCentered("SAFE ROOM", centreX, 42, 2, Glow);
        Heading(font, room.Name.ToUpperInvariant(), centreX, 66, width - 48, accent);

        var proseScale = width >= 700 ? 2 : 1;
        var y = 100;

        foreach (var line in Wrap(room.Prompt, proseScale, width - 80))
        {
            font.DrawCentered(line, centreX, y, proseScale, Text);
            y += (PixelFont.LineHeight * proseScale) + 4;
        }

        font.DrawCentered(Fit(room.Price, 2, width - 48), centreX, y + 6, 2, Dim);

        // The casino's wheel turns while the card is open: the table is the one room whose whole
        // promise is a spin, so it is shown rather than described.
        var wheelBottom = y + 44;

        if (room.Kind == RoomKind.LongTable)
        {
            const int radius = 46;
            DrawRoulette(centreX, y + 22 + radius, radius, Raylib.GetTime(), room.IsResolved);
            wheelBottom = y + 22 + (radius * 2) + 12;
        }

        var rowHeight = 46;
        var listTop = Math.Max(
            wheelBottom,
            Math.Max(y + 44, height / 2 - ((options.Count * rowHeight) / 2)));
        var rowWidth = Math.Min(width - 96, 520);
        var left = centreX - (rowWidth / 2);

        if (room.IsResolved)
        {
            var outcome = room.Outcome ?? "IT IS DONE";
            font.DrawCentered(Fit(outcome, 2, width - 48), centreX, listTop + 20, 2, Glow);
            font.DrawCentered("ENTER: LEAVE", centreX, height - 54, 2, Text);
            return;
        }

        for (var index = 0; index < options.Count; index++)
        {
            var top = listTop + (index * rowHeight);
            var isSelected = index == selected;
            var colour = isSelected ? accent : Fade(accent, 0.40);

            Raylib.DrawRectangle(left, top, rowWidth, rowHeight - 6, isSelected ? new Color(6, 24, 26, 248) : new Color(3, 12, 14, 238));
            Frame(left, top, rowWidth, rowHeight - 6, colour);

            if (isSelected)
            {
                Raylib.DrawRectangle(left, top + 4, 3, rowHeight - 14, accent);
            }

            font.DrawCentered(
                Fit(options[index].Label, 2, rowWidth - 24),
                centreX,
                top + 6,
                2,
                isSelected ? Text : Dim);

            if (options[index].Note is { } note)
            {
                font.DrawCentered(Fit(note, 1, rowWidth - 24), centreX, top + 24, 1, isSelected ? Glow : Dim);
            }
        }

        // A shop's card face under the list. A name and a price is not enough to spend a heart on:
        // the player has not memorised the catalog, and the draft's own cards are the reference.
        if (selected >= 0 && options[selected].Preview is { Length: > 0 } preview)
        {
            var previewWidth = Math.Min(width - 72, 660);
            var previewLines = Wrap(preview, 1, previewWidth);
            var previewTop = listTop + (options.Count * rowHeight) + 6;
            var maxLines = Math.Max(0, (height - 96 - previewTop) / (PixelFont.LineHeight + 3));

            if (maxLines > 0)
            {
                font.DrawCentered("CARD PREVIEW", centreX, previewTop, 1, Dim);

                var lineY = previewTop + PixelFont.LineHeight + 3;
                var shown = Math.Min(previewLines.Count, maxLines);

                for (var index = 0; index < shown; index++)
                {
                    var line = previewLines[index];

                    if (index == shown - 1 && previewLines.Count > shown)
                    {
                        line = Fit($"{line.TrimEnd('.', ' ')}...", 1, previewWidth);
                    }

                    font.DrawCentered(line, centreX, lineY, 1, Text);
                    lineY += PixelFont.LineHeight + 3;
                }
            }
        }

        var promptScale = width >= 700 ? 2 : 1;

        font.DrawCentered(
            Fit("A / D: CHOOSE   ENTER: TAKE   ESC: LEAVE", promptScale, width - 24),
            centreX,
            height - 54,
            promptScale,
            Text);
        font.DrawCentered(
            // No denominator: the run has no heart ceiling to compare against.
            Fit($"HEARTS {campaign.Hearts}", 1, width - 24),
            centreX,
            height - 28,
            1,
            Dim);
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

    /// <summary>Draws the page shown when a floor's exit is reached.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floor">The floor that was cleared.</param>
    /// <param name="hearts">Hearts the player still has.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <param name="page">The floor's page of the gardener's book.</param>
    /// <param name="moodLine">
    /// The line the gardener adds when the run is in a particular state, or <see langword="null"/>.
    /// </param>
    /// <param name="garden">How many blooms the garden is worth so far.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    /// <para>
    /// This is a page, not a HUD line: a full-window panel with a double frame, the chapter's
    /// title at heading size and the prose at two pixels per glyph. The old card drew the story at
    /// one pixel per glyph under a row of hearts, which on a laptop was the smallest text on the
    /// screen — the story was there, but nobody could read it.
    /// </para>
    /// <para>
    /// The layout is measured from the panel rather than pinned to the window's centre, so a
    /// resized frame grows the margins instead of pushing the footer off the bottom.
    /// </para>
    /// </remarks>
    public static void FloorCleared(
        PixelFont font,
        FloorDefinition floor,
        int hearts,
        Color accent,
        StoryPage page,
        string? moodLine,
        int garden)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(page);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();

        Begin(width, height);

        var panel = PagePanel(width, height, accent);
        var centreX = width / 2;
        var panelLeft = (int)panel.X;
        var panelTop = (int)panel.Y;
        var panelWidth = (int)panel.Width;
        var panelHeight = (int)panel.Height;
        var innerWidth = panelWidth - 64;
        var top = panelTop + 22;

        font.DrawCentered(
            FormattableString.Invariant($"FLOOR {floor.Number} CLEARED"),
            centreX,
            top,
            1,
            Dim);

        var pageNumber = FormattableString.Invariant($"PAGE {page.FloorNumber}");
        font.Draw(pageNumber, panelLeft + panelWidth - 30 - PixelFont.MeasureWidth(pageNumber, 1), top, 1, Fade(accent, 0.7));

        top += PixelFont.LineHeight + 14;

        // The chapter title is the page's identity, so it takes heading size and only drops a
        // step for a title that would run off the panel.
        var titleScale = 3;
        while (titleScale > 2 && PixelFont.MeasureWidth(page.Title, titleScale) > innerWidth)
        {
            titleScale--;
        }

        font.DrawCentered(page.Title, centreX, top, titleScale, accent);
        top += (PixelFont.LineHeight * titleScale) + 12;

        DrawTitleRule(centreX, top, Fade(accent, 0.8));
        top += 26;

        // The prose is the point: two pixels per glyph whenever the panel can hold a sentence at
        // that size, wrapped to the panel rather than clipped.
        var lineScale = innerWidth >= 380 ? 2 : 1;
        var lineHeight = (PixelFont.LineHeight * lineScale) + 7;

        foreach (var line in page.Lines)
        {
            foreach (var row in Wrap(line, lineScale, innerWidth))
            {
                font.DrawCentered(row, centreX, top, lineScale, Text);
                top += lineHeight;
            }
        }

        if (moodLine is not null)
        {
            top += 4;
            font.DrawCentered(Fit(moodLine, 1, innerWidth), centreX, top, 1, Dim);
            top += PixelFont.LineHeight + 8;
        }

        top += 8;
        DrawGarden(centreX, top, garden, accent, Glow);
        top += 20;

        font.DrawCentered(
            Fit(FormattableString.Invariant($"GARDEN {garden}   HEARTS {hearts}"), 1, innerWidth),
            centreX,
            top,
            1,
            Fade(accent, 0.75));

        Rule(centreX, panelTop + panelHeight - 74, accent);
        font.DrawCentered(
            "ENTER: TURN THE PAGE",
            centreX,
            panelTop + panelHeight - 56,
            2,
            Text);
    }

    /// <summary>
    /// Draws the Echo's card: the gardener's voice, read in the dark with the hunt held still.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="title">The page title, which names the floor's chapter.</param>
    /// <param name="lines">The lines to read, the last of which teaches the floor's rule.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    /// The card is deliberately plain: this is a voice, not a menu. It never offers a choice and
    /// it never has a price, because an interaction that can kill you without a decision is the one
    /// thing the grammar forbids.
    /// </remarks>
    public static void Echo(PixelFont font, string title, IReadOnlyList<string> lines, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(lines);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;

        Begin(width, height);

        var panel = PagePanel(width, height, accent);
        var panelTop = (int)panel.Y;
        var panelHeight = (int)panel.Height;
        var innerWidth = (int)panel.Width - 64;
        var top = panelTop + 26;

        font.DrawCentered("THE ECHO", centreX, top, 1, Glow);
        top += PixelFont.LineHeight + 16;

        var titleScale = 3;
        while (titleScale > 2 && PixelFont.MeasureWidth(title, titleScale) > innerWidth)
        {
            titleScale--;
        }

        font.DrawCentered(title, centreX, top, titleScale, accent);
        top += (PixelFont.LineHeight * titleScale) + 12;

        DrawTitleRule(centreX, top, Fade(accent, 0.8));
        top += 26;

        var lineScale = innerWidth >= 380 ? 2 : 1;
        var lineHeight = (PixelFont.LineHeight * lineScale) + 8;

        foreach (var line in lines)
        {
            foreach (var row in Wrap(line, lineScale, innerWidth))
            {
                font.DrawCentered(row, centreX, top, lineScale, Text);
                top += lineHeight;
            }
        }

        top += 6;

        // What the Echo actually gives, in the one place the player is already reading. A story
        // beat that only speaks is a cutscene; this one leaves a heart in the hand.
        font.DrawCentered("IT WARMS YOU: ONE HEART", centreX, top, 1, Glow);
        top += PixelFont.LineHeight + 10;

        Rule(centreX, panelTop + panelHeight - 74, accent);
        font.DrawCentered("ENTER: LET GO", centreX, panelTop + panelHeight - 56, 2, Text);
    }

    /// <summary>
    /// Draws the garden: one small bloom for every bloom picked this run, plus one for every page
    /// turned.
    /// </summary>
    /// <param name="centreX">The centre column.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="count">How many marks to draw.</param>
    /// <param name="accent">The marks' colour.</param>
    /// <param name="highlight">The colour of every fifth mark, so the row can be counted.</param>
    /// <remarks>
    /// It is a strip rather than a number because the strip is what grows: by the Deep it is the
    /// widest thing on the card, and it exists only in this run. Past the window's width the row
    /// stops growing instead of wrapping, so a long run cannot push the card's footer off screen.
    /// </remarks>
    private static void DrawGarden(int centreX, int y, int count, Color accent, Color highlight)
    {
        const int Mark = 4;
        const int Gap = 3;
        const int MaxMarks = 64;

        if (count <= 0)
        {
            return;
        }

        var shown = Math.Min(count, MaxMarks);
        var rowWidth = (shown * (Mark + Gap)) - Gap;
        var x = centreX - (rowWidth / 2);

        for (var index = 0; index < shown; index++)
        {
            Raylib.DrawRectangle(
                x + (index * (Mark + Gap)),
                y,
                Mark,
                Mark,
                (index % 5) == 4 ? highlight : accent);
        }
    }

    /// <summary>Draws the run summary after the last floor.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="floorCount">How many floors the run had.</param>
    /// <param name="hearts">Hearts the player finished with.</param>
    /// <param name="accent">The final floor's accent colour.</param>
    /// <param name="themeId">The final floor's theme, whose emblem and palette close the run.</param>
    /// <param name="garden">How many blooms the run's garden came to.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    /// The garden is the run's only score: every bloom picked and every page turned, drawn as a
    /// strip that will not exist once the window closes. It is not an unlock and it is not saved.
    /// </remarks>
    public static void RunComplete(
        PixelFont font,
        int floorCount,
        int hearts,
        Color accent,
        string themeId,
        int garden)
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
        DrawGarden(width / 2, centreY + 24, garden, accent, Glow);
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
    /// Draws the pause menu over a frozen floor: continue, read the cards, save and leave, or
    /// abandon the run.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="selectedItem">
    /// The highlighted entry (0 = continue, 1 = cards, 2 = save and quit, 3 = abandon run).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    /// <remarks>
    /// The world is still drawn underneath, dimmed rather than replaced: the pause is a hold on
    /// this floor, not another screen, so the player can see where they left off. SAVE &amp; QUIT
    /// and ABANDON RUN both return to the title and differ only in whether the save survives, so
    /// the two are named apart rather than left to be told apart after the fact.
    /// </remarks>
    public static void Pause(PixelFont font, int selectedItem)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var panelWidth = Math.Min(width - 64, 420);
        var panelHeight = 392;
        var left = centreX - (panelWidth / 2);
        var top = (height / 2) - (panelHeight / 2);
        var time = Raylib.GetTime();

        Raylib.DrawRectangle(0, 0, width, height, new Color(2, 7, 9, 196));
        Raylib.DrawRectangle(left + 7, top + 7, panelWidth, panelHeight, new Color(0, 0, 0, 130));
        Raylib.DrawRectangle(left, top, panelWidth, panelHeight, new Color(4, 11, 13, 250));
        Frame(left, top, panelWidth, panelHeight, Fade(Glow, 0.55));
        Frame(left + 4, top + 4, panelWidth - 8, panelHeight - 8, Fade(Glow, 0.18));
        Raylib.DrawRectangle(left, top, panelWidth, 2, Fade(Glow, 0.9));

        font.DrawCentered("PAUSED", centreX, top + 26, 3, Glow);
        font.DrawCentered("THE DARK WAITS", centreX, top + 66, 1, Dim);

        DrawMenuItem(font, "CONTINUE", centreX, top + 100, selectedItem == 0, time, 3);
        DrawMenuItem(font, "RUN DETAILS", centreX, top + 142, selectedItem == 1, time, 3);
        DrawMenuItem(font, "CARDS", centreX, top + 184, selectedItem == 2, time, 3);
        DrawMenuItem(font, "SAVE & QUIT", centreX, top + 226, selectedItem == 3, time, 3);
        DrawMenuItem(font, "ABANDON RUN", centreX, top + 268, selectedItem == 4, time, 3);

        Rule(centreX, top + panelHeight - 62, Glow);
        font.DrawCentered(
            Fit("W/S: SELECT   ENTER: CONFIRM   ESC: RESUME", 1, panelWidth - 24),
            centreX,
            top + panelHeight - 46,
            1,
            Text);
    }

    /// <summary>
    /// Draws the card shown the moment an altar's relic is taken: a gem, its family, its name and
    /// the promise it makes for the rest of the run.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="kind">The relic that was taken.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    /// <remarks>
    /// The floor is frozen while this is up, for the same reason the Echo is: a relic changes the
    /// run, not the floor, so the player should read what they gained before the hunt resumes. The
    /// gem's colour is the relic's family, which is also the colour it wears on the run details
    /// card, so the two read as the same thing.
    /// </remarks>
    public static void RelicFound(PixelFont font, RelicKind kind, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);

        var card = RelicCatalog.CardFor(kind);
        var tint = PixelRenderer.RelicTint(kind);
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var panelWidth = Math.Min(width - 64, 520);
        const int panelHeight = 330;
        var left = centreX - (panelWidth / 2);
        var top = (height / 2) - (panelHeight / 2);

        Begin(width, height);

        Raylib.DrawRectangle(0, 0, width, height, new Color(2, 7, 9, 208));
        Raylib.DrawRectangle(left + 7, top + 7, panelWidth, panelHeight, new Color(0, 0, 0, 140));
        Raylib.DrawRectangle(left, top, panelWidth, panelHeight, new Color(4, 11, 13, 250));
        Frame(left, top, panelWidth, panelHeight, Fade(tint, 0.7));
        Frame(left + 4, top + 4, panelWidth - 8, panelHeight - 8, Fade(tint, 0.2));
        Raylib.DrawRectangle(left, top, panelWidth, 2, Fade(tint, 0.95));

        // The gem is drawn from the same three rectangles the floor's altar uses, so the card
        // points back at the thing the player just touched.
        var gemY = top + 42;
        Raylib.DrawCircle(centreX, gemY, 20.0f, Fade(tint, 0.14));
        Raylib.DrawCircle(centreX, gemY, 12.0f, Fade(tint, 0.24));
        Raylib.DrawRectangle(centreX - 5, gemY - 10, 10, 10, tint);
        Raylib.DrawRectangle(centreX - 8, gemY - 4, 16, 7, tint);
        Raylib.DrawRectangle(centreX - 5, gemY + 3, 10, 5, tint);

        font.DrawCentered("RELIC FOUND", centreX, top + 72, 1, Fade(tint, 1.0));
        Heading(font, card.Name.ToUpperInvariant(), centreX, top + 92, panelWidth - 48, accent);
        font.DrawCentered(
            Fit($"{FamilyWord(kind)} - KEPT FOR THE REST OF THE RUN", 1, panelWidth - 40),
            centreX,
            top + 140,
            1,
            Dim);

        Rule(centreX, top + 160, tint);

        var lines = Wrap(card.FaceText, 2, panelWidth - 72);

        for (var index = 0; index < lines.Count && index < 3; index++)
        {
            font.DrawCentered(lines[index], centreX, top + 180 + (index * 22), 2, Text);
        }

        Rule(centreX, top + panelHeight - 62, tint);
        font.DrawCentered(
            Fit("ENTER / SPACE: CONTINUE", 2, panelWidth - 24),
            centreX,
            top + panelHeight - 46,
            2,
            Text);
    }

    /// <summary>
    /// Draws the run details card: what the run is, and every card and relic it is carrying.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="campaign">The run to describe.</param>
    /// <param name="scroll">The first carried item to draw, clamped to the list.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    /// The pause menu names the floor and the hearts; this is the page that says why the run plays
    /// the way it does. A relic is tagged as a relic rather than folded in with the drafted cards,
    /// because the player earned it by walking to an altar rather than by paying a card's price.
    /// </remarks>
    public static void RunDetails(PixelFont font, Campaign campaign, int scroll, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(campaign);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        const int left = 32;
        const int rowHeight = 50;
        var right = width - left;
        const int top = 152;
        var visible = Math.Max(1, (height - top - 92) / rowHeight);
        var carried = campaign.Modifiers.Cards;
        var maxScroll = Math.Max(0, carried.Count - visible);
        var first = Math.Clamp(scroll, 0, maxScroll);
        var last = Math.Min(carried.Count, first + visible);
        var relics = 0;

        foreach (var held in carried)
        {
            if (RelicCatalog.ById(held.Id) is not null)
            {
                relics++;
            }
        }

        Begin(width, height);

        font.DrawCentered("THE RUN", centreX, 22, 1, Glow);
        Heading(font, "WHAT YOU CARRY", centreX, 38, width - 72, accent);
        font.DrawCentered(
            FormattableString.Invariant(
                $"FLOOR {campaign.FloorNumber:00} / {campaign.FloorCount:00}   HEARTS {campaign.Hearts}   BLOOMS {campaign.BloomsPicked}   GARDEN {campaign.GardenCount}   RELICS {relics}"),
            centreX,
            88,
            1,
            Dim);
        font.DrawCentered(
            Fit(campaign.Modifiers.Describe(), 1, width - 72),
            centreX,
            104,
            1,
            Text);

        var conditions = DescentConditionCatalog.Line(campaign.ActiveConditions);
        var rule = conditions.Length > 0
            ? conditions
            : campaign.CurrentFloor.Gimmick ?? string.Empty;

        if (rule.Length > 0)
        {
            font.DrawCentered(Fit(rule, 1, width - 72), centreX, 120, 1, Fade(accent, 1.0));
        }

        Rule(centreX, 140, accent);

        if (carried.Count == 0)
        {
            font.DrawCentered("NOTHING YET - THE FIRST DRAFT IS STILL AHEAD", centreX, top + 40, 2, Dim);
        }

        for (var row = first; row < last; row++)
        {
            var held = carried[row];
            var y = top + ((row - first) * rowHeight);
            var relic = RelicCatalog.KindOf(held.Id);
            var tag = relic is null ? TierWord(held.Tier) : "RELIC";
            var tagColor = relic is { } kind ? PixelRenderer.RelicTint(kind) : TierColor(held.Tier);

            font.Draw(Fit(held.Name.ToUpperInvariant(), 2, right - left - 96), left, y, 2, Text);
            font.Draw(tag, right - PixelFont.MeasureWidth(tag, 1), y + 4, 1, tagColor);

            var lines = Wrap(held.FaceText, 1, right - left);

            for (var line = 0; line < lines.Count && line < 2; line++)
            {
                var text = line == 1 && lines.Count > 2 ? $"{lines[line]} ..." : lines[line];
                font.Draw(Fit(text, 1, right - left), left, y + 20 + (line * 11), 1, Dim);
            }

            Raylib.DrawRectangle(left, y + rowHeight - 3, right - left, 1, Fade(Dim, 0.16));
        }

        if (carried.Count > 0)
        {
            font.DrawCentered(
                FormattableString.Invariant($"ITEMS {first + 1}-{last} OF {carried.Count}"),
                centreX,
                height - 62,
                1,
                Dim);
        }

        font.DrawCentered(
            Fit("W/S OR ARROWS: SCROLL   ESC: BACK", width >= 700 ? 2 : 1, width - 24),
            centreX,
            height - 38,
            width >= 700 ? 2 : 1,
            Text);
    }

    /// <summary>
    /// Draws the card codex: every shipped card, its rarity and its trade, one scrolling row each.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="scroll">The first row to draw, clamped to the catalog.</param>
    /// <param name="accent">The accent colour for the heading and the rule.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    /// <remarks>
    /// The catalog is the game's whole bargain, so it is readable in one place rather than only
    /// when a draft happens to offer a card: a row is a name, its tier on the right, and the same
    /// sentence the card's own face carries. The rows are in catalog order, which is tier order,
    /// so the super-rare is the last row.
    /// </remarks>
    public static void Codex(PixelFont font, int scroll, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var cards = CardCatalog.All;
        const int left = 32;
        const int rowHeight = 48;
        var right = width - left;
        var top = 118;
        var visible = Math.Max(1, (height - top - 74) / rowHeight);
        var maxScroll = Math.Max(0, cards.Count - visible);
        var first = Math.Clamp(scroll, 0, maxScroll);
        var last = Math.Min(cards.Count, first + visible);

        Begin(width, height);

        font.DrawCentered("THE CATALOG", centreX, 26, 1, Glow);
        Heading(font, "EVERY CARD", centreX, 44, width - 72, accent);
        font.DrawCentered("COMMON, UNCOMMON, RARE - AND ONE SUPER RARE", centreX, 92, 1, Dim);
        Rule(centreX, 108, accent);

        for (var row = first; row < last; row++)
        {
            var card = cards[row];
            var y = top + ((row - first) * rowHeight);
            var tier = TierWord(card.Tier);

            font.Draw(card.Name.ToUpperInvariant(), left, y, 2, Text);
            font.Draw(tier, right - PixelFont.MeasureWidth(tier, 1), y + 4, 1, TierColor(card.Tier));

            var lines = Wrap(card.FaceText, 1, right - left);

            for (var line = 0; line < lines.Count && line < 2; line++)
            {
                var text = line == 1 && lines.Count > 2 ? $"{lines[line]} ..." : lines[line];
                font.Draw(Fit(text, 1, right - left), left, y + 20 + (line * 11), 1, Dim);
            }

            Raylib.DrawRectangle(left, y + rowHeight - 3, right - left, 1, Fade(Dim, 0.16));
        }

        font.DrawCentered(
            FormattableString.Invariant($"CARDS {first + 1}-{last} OF {cards.Count}"),
            centreX,
            height - 62,
            1,
            Dim);
        font.DrawCentered(
            Fit("W/S OR ARROWS: SCROLL   ESC: BACK", width >= 700 ? 2 : 1, width - 24),
            centreX,
            height - 38,
            width >= 700 ? 2 : 1,
            Text);
    }

    /// <summary>
    /// Draws the interactive tutorial: the practice chamber's current instruction, and the card
    /// that ends the lesson.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="practice">The practice floor being played.</param>
    /// <param name="accent">The accent colour for the strip and the rule.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    /// The floor itself is drawn underneath by the pixel renderer; this only adds the line to read
    /// and replaces the footer's ability hints, which the practice floor deliberately does not
    /// have, with the keys it does use.
    /// </remarks>
    public static void Practice(PixelFont font, PracticeTutorial practice, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(practice);

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;
        var promptScale = width >= 760 ? 2 : 1;
        var lines = Wrap(practice.Prompt, promptScale, width - 64);
        var lineHeight = (PixelFont.LineHeight * promptScale) + 4;
        var stripHeight = 24 + (lines.Count * lineHeight) + 10;

        // The lesson sits at the foot of the chamber rather than over it: the HUD above the maze
        // carries the bloom count the lesson keeps talking about, and the cells stay readable.
        var footer = RaylibLayout.FooterHeight;
        var stripTop = height - footer - stripHeight;
        Raylib.DrawRectangle(0, stripTop, width, stripHeight, new Color(3, 12, 14, 234));
        Raylib.DrawRectangle(0, stripTop - 2, width, 2, Fade(accent, 0.75));
        font.DrawCentered("PRACTICE", centreX, stripTop + 8, 1, Glow);

        var y = stripTop + 24;

        foreach (var line in lines)
        {
            font.DrawCentered(line, centreX, y, promptScale, Text);
            y += lineHeight;
        }

        // The footer's ability hints are for the real floors: here the only keys are walking and
        // leaving, so the strip says exactly that.
        Raylib.DrawRectangle(0, height - footer, width, footer, new Color(3, 12, 14, 234));
        font.DrawCentered(
            practice.IsComplete
                ? "ENTER: BEGIN THE DESCENT   ESC: TITLE"
                : FormattableString.Invariant(
                    $"STEP {Math.Min(practice.CompletedSteps + 1, PracticeTutorial.StepCount)} OF {PracticeTutorial.StepCount}   WASD / ARROWS: WALK   ESC: TITLE"),
            centreX,
            height - (footer / 2) - (PixelFont.LineHeight / 2),
            1,
            practice.IsComplete ? accent : Dim);

        if (!practice.IsComplete)
        {
            return;
        }

        // The lesson is over: a panel over a room the player already cleared.
        var panelWidth = Math.Min(width - 64, 480);
        var panelHeight = 210;
        var left = centreX - (panelWidth / 2);
        var top = (height / 2) - (panelHeight / 2);

        Raylib.DrawRectangle(0, 0, width, height, new Color(2, 7, 9, 170));
        Raylib.DrawRectangle(left + 7, top + 7, panelWidth, panelHeight, new Color(0, 0, 0, 130));
        Raylib.DrawRectangle(left, top, panelWidth, panelHeight, new Color(4, 11, 13, 250));
        Frame(left, top, panelWidth, panelHeight, Fade(Glow, 0.55));
        Heading(font, "THE ARCH IS OPEN", centreX, top + 26, panelWidth - 48, Glow);

        var body = Wrap(practice.Prompt, 1, panelWidth - 40);
        var bodyY = top + 70;

        foreach (var line in body)
        {
            font.DrawCentered(line, centreX, bodyY, 1, Text);
            bodyY += 12;
        }

        Rule(centreX, top + panelHeight - 58, Glow);
        font.DrawCentered("ENTER: BEGIN THE DESCENT   ESC: TITLE", centreX, top + panelHeight - 42, 1, Dim);
    }

    /// <summary>
    /// Draws one page of the guided tutorial.
    /// </summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="pageIndex">The page to draw, clamped into the tutorial.</param>
    /// <param name="accent">The accent colour for the page's rule and key line.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    /// <remarks>
    /// The tutorial is a book, not a simulation: each page states one idea, then names the exact
    /// key that does it. The dots at the foot are the page count, so a player always knows how
    /// much explaining is left before they can descend.
    /// </remarks>
    public static void DrawTutorial(PixelFont font, int pageIndex, Color accent)
    {
        ArgumentNullException.ThrowIfNull(font);

        var page = Tutorial.Page(pageIndex);
        var pageNumber = Math.Clamp(pageIndex, 0, Tutorial.PageCount - 1);
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var centreX = width / 2;

        Begin(width, height);

        font.DrawCentered(page.Caption, centreX, 32, 1, Glow);
        Heading(font, page.Title, centreX, 52, width - 72, accent);
        Rule(centreX, 94, accent);

        var lineScale = width >= 680 ? 2 : 1;
        var lineHeight = (PixelFont.LineHeight * lineScale) + 12;
        var y = 118;

        foreach (var line in page.Lines)
        {
            foreach (var row in Wrap(line, lineScale, width - 96))
            {
                font.DrawCentered(row, centreX, y, lineScale, Text);
                y += lineHeight;
            }

            y += 10;
        }

        if (page.Keys.Length > 0)
        {
            var keysY = Math.Max(y + 8, height - 142);

            Raylib.DrawRectangle(
                centreX - Math.Min(width - 80, 340),
                keysY - 10,
                Math.Min(width - 80, 340) * 2,
                2,
                Fade(accent, 0.5));

            foreach (var row in Wrap(page.Keys, 1, width - 80))
            {
                font.DrawCentered(row, centreX, keysY, 1, accent);
                keysY += PixelFont.LineHeight + 4;
            }
        }

        // The page dots: one small mark per page, the current one bright.
        var dots = Tutorial.PageCount;
        const int DotsGap = 10;
        var dotsWidth = (dots * DotsGap) - (DotsGap - 4);
        var dotX = centreX - (dotsWidth / 2);
        var dotY = height - 84;

        for (var index = 0; index < dots; index++)
        {
            Raylib.DrawRectangle(
                dotX + (index * DotsGap),
                dotY,
                4,
                4,
                index == pageNumber ? accent : Fade(Dim, 0.45));
        }

        font.DrawCentered(
            FormattableString.Invariant($"PAGE {pageNumber + 1} OF {Tutorial.PageCount}"),
            centreX,
            height - 66,
            1,
            Dim);

        // The guide is read one page at a time, but the last page is the offer to walk the loop:
        // the prompt says so there, and P does it from any page.
        var promptScale = width >= 900 ? 2 : 1;
        var prompt = pageNumber >= Tutorial.PageCount - 1
            ? "ENTER: PRACTISE THE BASICS   ESC: TITLE"
            : "ENTER: NEXT PAGE   A / D: PAGE   P: PRACTISE   ESC: TITLE";

        font.DrawCentered(Fit(prompt, promptScale, width - 24), centreX, height - 44, promptScale, Text);
    }

    /// <summary>
    /// Draws the casino's roulette wheel, turning on the clock.
    /// </summary>
    /// <param name="centreX">The wheel's centre column.</param>
    /// <param name="centreY">The wheel's centre row.</param>
    /// <param name="radius">The wheel's outer radius in pixels.</param>
    /// <param name="time">Current time in seconds, which drives the spin.</param>
    /// <param name="resolved">Whether the card has been played, which slows the wheel to a stop.</param>
    /// <remarks>
    /// Twelve pockets in the house's two colours, a gold rim, and one pale ball running the other
    /// way. It is drawn from primitives so it costs nothing to compile and cannot drift from the
    /// card's palette.
    /// </remarks>
    private static void DrawRoulette(int centreX, int centreY, int radius, double time, bool resolved)
    {
        const int pockets = 12;
        const float pocketWidth = 360f / pockets;
        var spin = resolved ? time * 12.0 : time * 42.0;
        var centre = new Vector2(centreX, centreY);

        Raylib.DrawCircle(centreX, centreY, radius + 3, new Color(232, 198, 120, 235));

        for (var index = 0; index < pockets; index++)
        {
            var start = (float)(spin + (index * pocketWidth));
            var colour = (index % 2) == 0
                ? new Color(18, 26, 32, 255)
                : new Color(150, 52, 58, 255);

            Raylib.DrawCircleSector(centre, radius, start, start + pocketWidth - 1.5f, 8, colour);
        }

        Raylib.DrawCircle(centreX, centreY, radius * 0.30f, new Color(232, 198, 120, 255));
        Raylib.DrawCircle(centreX, centreY, (radius * 0.30f) - 2, new Color(24, 20, 16, 255));

        // The ball runs against the wheel, as a ball does.
        var ballAngle = -(time * (resolved ? 0.7 : 2.4));
        var ballRadius = radius * 0.74f;
        var ballX = centreX + (Math.Cos(ballAngle) * ballRadius);
        var ballY = centreY + (Math.Sin(ballAngle) * ballRadius);

        Raylib.DrawCircle((int)ballX, (int)ballY, 3.5f, new Color(250, 246, 226, 255));

        // The pointer at the top: wound left, point, right, which is the way Raylib keeps it.
        Raylib.DrawTriangle(
            new Vector2(centreX - 7, centreY - radius - 9),
            new Vector2(centreX, centreY - radius + 3),
            new Vector2(centreX + 7, centreY - radius - 9),
            new Color(250, 232, 178, 255));
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
    /// Paints the full-window page a cleared floor turns: a dark sheet inside a double frame, with
    /// a shadow so it reads as a pop-up rather than as a second HUD strip.
    /// </summary>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    /// <param name="accent">The floor's accent colour.</param>
    /// <returns>The page's inner rectangle.</returns>
    private static Rectangle PagePanel(int width, int height, Color accent)
    {
        var margin = Math.Clamp(width / 18, 18, 56);
        var left = margin;
        var top = margin;
        var panelWidth = width - (2 * margin);
        var panelHeight = height - (2 * margin);

        Raylib.DrawRectangle(left + 7, top + 7, panelWidth, panelHeight, new Color(0, 0, 0, 120));
        Raylib.DrawRectangle(left, top, panelWidth, panelHeight, new Color(4, 11, 13, 250));

        Frame(left, top, panelWidth, panelHeight, Fade(accent, 0.62));
        Frame(left + 4, top + 4, panelWidth - 8, panelHeight - 8, Fade(accent, 0.22));
        Raylib.DrawRectangle(left, top, panelWidth, 2, Fade(accent, 0.9));
        Raylib.DrawRectangle(left, top + panelHeight - 2, panelWidth, 2, Fade(accent, 0.9));

        return new Rectangle(left, top, panelWidth, panelHeight);
    }

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

    /// <summary>Returns the word the relic card names a family with.</summary>
    /// <param name="kind">The relic.</param>
    private static string FamilyWord(RelicKind kind) => RelicCatalog.FamilyOf(kind) switch
    {
        RelicFamily.Body => "BODY",
        RelicFamily.Light => "LIGHT",
        RelicFamily.Hunt => "THE HUNT",
        RelicFamily.Garden => "THE GARDEN",
        _ => "RITE",
    };

    /// <summary>Returns the label a card's tier is drawn with.</summary>
    /// <param name="tier">The tier.</param>
    private static string TierWord(CardTier tier) => tier switch
    {
        CardTier.Common => "COMMON",
        CardTier.Uncommon => "UNCOMMON",
        CardTier.Rare => "RARE",
        CardTier.SuperRare => "SUPER RARE",
        _ => "CURSE",
    };

    /// <summary>Returns the colour a card's tier is drawn in.</summary>
    /// <param name="tier">The tier.</param>
    private static Color TierColor(CardTier tier) => tier switch
    {
        CardTier.Common => Text,
        CardTier.Uncommon => new Color(140, 236, 168, 255),
        CardTier.Rare => new Color(248, 152, 224, 255),
        CardTier.SuperRare => new Color(255, 214, 120, 255),
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

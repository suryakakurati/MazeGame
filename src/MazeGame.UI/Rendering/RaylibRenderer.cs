using System;
using System.Numerics;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.UI.Rendering.Pixels;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Draws the frame around the maze: the title, the status strips, the hints, the hearts, the
/// damage flash and the grain, in the same compiled face the cards use.
/// </summary>
/// <remarks>
/// <para>
/// The maze itself — rock, moss, the explorer and the killer — is drawn from the compiled atlases
/// by <see cref="Pixels.PixelRenderer"/>, which then calls <see cref="DrawOverlay(GameState)"/> so the chrome
/// has one implementation rather than two that can drift.
/// </para>
/// <para>
/// Frames must be bracketed by <c>Raylib.BeginDrawing</c> / <c>Raylib.EndDrawing</c>, which the
/// frontend does: this class fills in its part of a frame and never calls those itself.
/// </para>
/// </remarks>
public sealed partial class RaylibRenderer
{
    // ---- Palette: bioluminescent cave ------------------------------------------------
    private static readonly Color CaveTop = new(6, 18, 24, 255);
    private static readonly Color CaveBottom = new(1, 4, 8, 255);
    private static readonly Color GlowGreen = new(86, 240, 160, 255);
    private static readonly Color GlowCyan = new(70, 232, 224, 255);
    private static readonly Color GlowPink = new(255, 111, 168, 255);
    private static readonly Color GlowBlue = new(104, 168, 255, 255);
    private static readonly Color Lantern = new(255, 232, 160, 255);
    private static readonly Color ExitLockedColor = new(58, 92, 104, 255);
    private static readonly Color ExitOpenColor = new(70, 232, 224, 255);
    private static readonly Color HudText = new(208, 233, 220, 255);
    private static readonly Color HintText = new(110, 145, 132, 255);
    private static readonly Color DangerText = new(255, 128, 176, 255);
    private static readonly Color Panel = new(4, 12, 14, 190);
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private const int MoteCount = 70;
    private const int GrainCount = 110;

    /// <summary>How long the red flash lasts after a heart is lost, in seconds.</summary>
    private const double DamageFlashSeconds = 0.35;

    /// <summary>Left edge of the heart row in the footer, in pixels.</summary>
    private const float OriginHeartX = 26f;



    private PixelFont? _font;

    // Presentation memory for the damage flash: the hearts drawn last frame and when the count
    // dropped. -1 means "nothing seen yet", so opening a game never flashes.
    private int _heartsSeen = -1;
    private double _lastHitTime = double.NegativeInfinity;

    /// <summary>
    /// Creates the renderer. Must be constructed <b>after</b> the window exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the renderer is built before <c>Raylib.InitWindow</c>.
    /// </exception>
    public RaylibRenderer()
    {
        if (!Raylib.IsWindowReady())
        {
            throw new InvalidOperationException("RaylibRenderer must be created after Raylib.InitWindow.");
        }
    }

    /// <summary>
    /// Sets the compiled face the interface's labels are drawn in.
    /// </summary>
    /// <param name="font">The font the world is already using.</param>
    /// <remarks>
    /// The world renderer loads the face once and shares it here, rather than this class keeping a
    /// second copy of the same texture. A renderer that is asked for its overlay before this is
    /// called fails loudly: silently falling back to the system typeface is the inconsistency this
    /// method exists to remove.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
    public void UsePixelFont(PixelFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        _font = font;
    }

    /// <summary>
    /// Forgets which cells have been explored, for the start of a new game.
    /// </summary>
    /// <remarks>
    /// <see cref="Cell"/> is a value type, so cells of a new maze compare equal to cells of the
    /// old one at the same position. Without this reset a replay would start with the previous
    /// cave already drawn in, which would read as a bug in the maze rather than as UI memory.
    /// </remarks>
    public void Reset()
    {
        _heartsSeen = -1;
        _lastHitTime = double.NegativeInfinity;
    }

    /// <inheritdoc />
    /// <summary>
    /// Draws the interface over a frame someone else painted: the damage flash, the status strip,
    /// the hints and the grain.
    /// </summary>
    /// <param name="state">The floor being played.</param>
    /// <remarks>
    /// The pixel renderer draws its world into a virtual canvas and then calls this, so the HUD is
    /// one implementation rather than two that can drift. It is in window pixels on purpose: the
    /// chrome sits in the letterbox around the canvas, which keeps the tiles at whole-pixel scale
    /// and the type at its designed size.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
    public void DrawOverlay(GameState state) => DrawOverlay(state, null);

    /// <summary>
    /// Draws the interface with a floor's own identity: its emblem, number, name and gimmick, and
    /// an accent that runs through the strips and every status chip.
    /// </summary>
    /// <param name="state">The floor being played.</param>
    /// <param name="chrome">The floor's identity, or null for the neutral interface.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
    public void DrawOverlay(GameState state, FloorChrome? chrome)
    {
        ArgumentNullException.ThrowIfNull(state);

        TrackDamage(state);
        DrawDamageFlash();
        DrawHud(state, chrome);
        DrawHints(state, chrome);
        DrawGrain();
    }

    /// <summary>Builds the inset rectangle of a floor tile.</summary>
    /// <summary>
    /// Paints the cave behind the maze: a near-black teal gradient plus a few huge, very faint
    /// blooms of green, cyan and pink light, as if the tunnels continue into glowing water.
    /// </summary>
    private static void DrawBackground()
    {
        Raylib.DrawRectangleGradientV(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), CaveTop, CaveBottom);

        var time = Raylib.GetTime();

        DrawBloom(0.16, 0.22, 210, GlowGreen, 10 + (Math.Sin(time * 0.4) * 4));
        DrawBloom(0.84, 0.36, 240, GlowPink, 9 + (Math.Sin((time * 0.3) + 1.7) * 3));
        DrawBloom(0.62, 0.82, 260, GlowBlue, 10 + (Math.Sin((time * 0.25) + 3.1) * 4));
        DrawBloom(0.30, 0.68, 180, GlowCyan, 7 + (Math.Sin((time * 0.5) + 0.6) * 3));
    }

    /// <summary>Draws one huge, very transparent radial bloom at a fraction of the window size.</summary>
    private static void DrawBloom(double xFraction, double yFraction, float radius, Color color, double alpha)
    {
        var center = new Vector2(
            (float)(Raylib.GetScreenWidth() * xFraction),
            (float)(Raylib.GetScreenHeight() * yFraction));

        Raylib.DrawCircleGradient(center, radius, Rgba(color.R, color.G, color.B, (byte)Math.Clamp(alpha, 0, 255)), Transparent);
    }

    /// <summary>
    /// Draws the run's hearts in the footer beside the control hints. One pip per heart, with a
    /// count once the run holds more than the row can draw.
    /// </summary>
    /// <remarks>
    /// There is no ceiling any more, so the row must not imply one: drawing empty slots for a cap
    /// would say "the rest are lost" about hearts that are not. Eight pips is the widest that
    /// still leaves the hints room on the minimum window; past that the number is printed.
    /// </remarks>
    /// <param name="state">The game to read the heart count from.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="centerY">Vertical centre of the hearts.</param>
    private void DrawHearts(GameState state, float x, float centerY)
    {
        const float size = 15f;
        const float gap = 6f;
        const int maxPips = 8;
        var shown = Math.Clamp(state.Hearts, 0, maxPips);
        var lastHeart = state.Hearts == 1;

        for (var index = 0; index < shown; index++)
        {
            var pulse = lastHeart ? 0.65 + (Math.Sin(Raylib.GetTime() * 7.0) * 0.35) : 0.0;
            var color = Rgba(255, 96, 138, (byte)(185 + (pulse * 70)));

            DrawHeart(x + (index * (size + gap)) + (size / 2f), centerY, size, color);
        }

        if (state.Hearts > maxPips)
        {
            var count = FormattableString.Invariant($"x{state.Hearts}");

            Text(
                count,
                x + (maxPips * (size + gap)),
                centerY - (PixelFont.LineHeight / 2f),
                1,
                Rgba(255, 120, 158, 235));
        }
    }

    /// <summary>Returns how wide the footer's heart row is, for the hint's left edge.</summary>
    /// <param name="state">The game to read the heart count from.</param>
    private static float HeartsWidth(GameState state) =>
        Math.Clamp(state.Hearts, 0, 8) * 21f + (state.Hearts > 8 ? 26f : 0f);

    /// <summary>
    /// Draws one small pip per hunter beside the hearts, so a floor with two or three of them says
    /// so at a glance rather than only in the floor's name.
    /// </summary>
    /// <remarks>
    /// Nothing is drawn on a one-hunter floor: a single pip would read as a status light for a
    /// rule that has not changed, and the footer is the tightest strip on the screen.
    /// </remarks>
    /// <param name="state">The game to read the hunter count from.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="centreY">Vertical centre of the pips.</param>
    private static void DrawHunterPips(GameState state, float x, float centreY)
    {
        if (state.HunterCount <= 1)
        {
            return;
        }

        var pulse = 0.55 + (Math.Sin(Raylib.GetTime() * 4.0) * 0.2);
        var colour = Rgba(DangerText.R, DangerText.G, DangerText.B, (byte)(150 + (pulse * 80)));

        // Two upright fangs: a shape a rectangle can draw exactly, so the pip is as crisp as the
        // pixel face beside it. (A triangle would be anti-aliased into a smudge at this size.)
        for (var index = 0; index < state.HunterCount; index++)
        {
            var cx = x + (index * 12f);

            Raylib.DrawRectangle((int)cx, (int)centreY - 6, 3, 11, colour);
            Raylib.DrawRectangle((int)cx + 4, (int)centreY - 3, 2, 8, colour);
        }
    }

    /// <summary>Draws one small heart from two lobes and a triangle, on the pixel grid.</summary>
    private static void DrawHeart(float centerX, float centerY, float size, Color color)
    {
        var lobe = size * 0.27f;
        var top = centerY - (size * 0.16f);

        Raylib.DrawCircleV(new Vector2(centerX - lobe, top), lobe, color);
        Raylib.DrawCircleV(new Vector2(centerX + lobe, top), lobe, color);
        // Counter-clockwise as raylib wants it (left, point, right): the other order is culled,
        // which left the heart as two circles with no point and the pips invisible.
        Raylib.DrawTriangle(
            new Vector2(centerX - (lobe * 1.9f), top + (lobe * 0.35f)),
            new Vector2(centerX, centerY + (size * 0.44f)),
            new Vector2(centerX + (lobe * 1.9f), top + (lobe * 0.35f)),
            color);
    }

    /// <summary>
    /// Notes the moment a heart is lost so the next few frames can flash red.
    /// </summary>
    /// <remarks>
    /// This is a read-only comparison against a rule value: the renderer has no event stream yet,
    /// and a one-frame drop in <see cref="GameState.Hearts"/> is exactly the signal it needs.
    /// </remarks>
    private void TrackDamage(GameState state)
    {
        if (_heartsSeen >= 0 && state.Hearts < _heartsSeen)
        {
            _lastHitTime = Raylib.GetTime();
        }

        _heartsSeen = state.Hearts;
    }

    /// <summary>
    /// Paints a short red wash over the frame after a heart is lost, so the cost of a mistake is
    /// visible even though the player's cell does not change.
    /// </summary>
    private void DrawDamageFlash()
    {
        var elapsed = Raylib.GetTime() - _lastHitTime;

        if (elapsed < 0 || elapsed >= DamageFlashSeconds)
        {
            return;
        }

        var strength = 1.0 - (elapsed / DamageFlashSeconds);

        Raylib.DrawRectangle(
            0,
            0,
            Raylib.GetScreenWidth(),
            Raylib.GetScreenHeight(),
            Rgba(190, 24, 60, (byte)(strength * 64)));
    }

    /// <summary>
    /// Draws the sprint bar under the status pills, and nothing at all when the bar is full.
    /// </summary>
    /// <remarks>
    /// The bar only appears once stamina has been spent, so a player who never sprints is not
    /// given another meter to read. It turns danger-pink while the bar cannot be used, which is
    /// the same colour the game already uses for damage.
    /// </remarks>
    private void DrawStamina(GameState state, bool showLabel)
    {
        if (state.Config.SprintStaminaSeconds <= 0 || state.SprintStaminaFraction >= 0.999)
        {
            return;
        }

        const float trackWidth = 132f;
        const float trackHeight = 4f;
        var left = (float)RaylibLayout.OriginX;
        var top = RaylibLayout.HudHeight - 10f;
        var filled = trackWidth * (float)state.SprintStaminaFraction;
        var colour = state.IsSprinting ? GlowCyan : Rgba(HintText.R, HintText.G, HintText.B, 255);

        Raylib.DrawRectangleRec(
            new Rectangle(left, top, trackWidth, trackHeight),
            Rgba(8, 26, 28, 200));
        Raylib.DrawRectangleRec(new Rectangle(left, top, filled, trackHeight), colour);

        if (showLabel)
        {
            Text("SPRINT", left + trackWidth + 8, top - 5, 1, HintText);
        }
    }

    /// <summary>
    /// Draws the status strip as floating glass pills: keys, barriers and the exit, each with a
    /// glowing dot in its own colour. When the killer is close an extra warning appears, so the
    /// player gets a cue that is not only visual atmosphere.
    /// </summary>
    private void DrawHud(GameState state, FloorChrome? chrome)
    {
        var width = Raylib.GetScreenWidth();
        var accent = chrome?.Style.Accent ?? GlowGreen;
        var danger = state.EnemyGlow.Count > 0;
        var pulse = 0.55 + (Math.Sin(Raylib.GetTime() * 3.2) * 0.45);

        Raylib.DrawRectangle(0, 0, width, RaylibLayout.HudHeight, Panel);
        Raylib.DrawLineEx(
            new Vector2(0, RaylibLayout.HudHeight - 1),
            new Vector2(width, RaylibLayout.HudHeight - 1),
            1.5f,
            Rgba(accent.R, accent.G, accent.B, 90));
        Raylib.DrawRectangle(
            RaylibLayout.OriginX,
            RaylibLayout.HudHeight - 3,
            width - (2 * RaylibLayout.OriginX),
            1,
            Rgba(accent.R, accent.G, accent.B, 42));

        // The status chips are laid out from the right, so the floor's name on the left gets
        // whatever room is left rather than a hard-coded column that breaks on a narrow window.
        var chips = new (string Label, string Value, Color Colour)[]
        {
            ("BLOOMS", $"{state.KeysCollected}/{state.Config.KeyCount}", GlowPink),
            ("EXIT", state.IsExitUnlocked ? "OPEN" : "LOCKED", state.IsExitUnlocked ? ExitOpenColor : ExitLockedColor),
        };

        var chipWidth = 0;

        foreach (var chip in chips)
        {
            chipWidth += ChipWidth(chip.Label, chip.Value) + 6;
        }

        var chipX = (float)(width - RaylibLayout.OriginX - chipWidth + 6);
        const float chipY = 8f;
        var powerupLeft = width - RaylibLayout.OriginX - PowerupRowWidth;

        foreach (var chip in chips)
        {
            DrawStatChip(chipX, chipY, chip.Label, chip.Value, chip.Colour);
            chipX += ChipWidth(chip.Label, chip.Value) + 6;
        }

        var left = (float)RaylibLayout.OriginX;
        var textX = left;
        var room = Math.Min(chipX, powerupLeft) - left - 28;

        if (chrome is not null)
        {
            var style = chrome.Style;

            FloorEmblem.Draw(style.ThemeId, (int)left, 8, 3, accent);
            textX = left + 30;
            room = Math.Min(chipX, powerupLeft) - textX - 18;

            Text($"FLOOR {chrome.Number:00}", textX, 2, 2, accent);
            Text(FitText(chrome.Name.ToUpperInvariant(), 2, room), textX, 19, 2, HudText);

            var line = danger ? "IT IS CLOSE" : chrome.Gimmick?.ToUpperInvariant();

            if (!string.IsNullOrEmpty(line))
            {
                var colour = danger
                    ? Rgba(DangerText.R, DangerText.G, DangerText.B, (byte)(120 + (pulse * 120)))
                    : HintText;

                Text(FitText(line, 1, room), textX, 36, 1, colour);
            }

            var conditions = DescentConditionCatalog.Line(chrome.Conditions);

            if (conditions.Length > 0)
            {
                Text(FitText(conditions, 1, room), textX, 45, 1, accent);
            }
        }

        DrawPowerups(state, width - RaylibLayout.OriginX, RaylibLayout.HudHeight - 27f, accent);
        DrawStamina(state, chrome is null);

        if (danger)
        {
            Raylib.DrawRectangle(
                RaylibLayout.OriginX,
                RaylibLayout.HudHeight - 3,
                width - (2 * RaylibLayout.OriginX),
                2,
                Rgba(DangerText.R, DangerText.G, DangerText.B, (byte)(50 + (pulse * 100))));
        }
    }

    /// <summary>Width of one powerup charge slot.</summary>
    private const float PowerupSlotWidth = 58f;

    /// <summary>Gap between two powerup charge slots.</summary>
    private const float PowerupGap = 5f;

    /// <summary>Width of the whole row of five slots.</summary>
    private const float PowerupRowWidth = (5 * PowerupSlotWidth) + (4 * PowerupGap);

    /// <summary>
    /// Draws one slot per powerup: its key, its remaining charges and whether it is active.
    /// </summary>
    /// <param name="state">The floor, for its remaining charges and active states.</param>
    /// <param name="rightEdge">The x the row ends at, so it stays against the right margin.</param>
    /// <param name="top">The y of the row.</param>
    /// <param name="accent">The floor's accent, used for a slot that still has charges.</param>
    /// <remarks>
    /// The row sits under the key and exit chips in the HUD's second line. A zero count is dim
    /// rather than hidden, so an empty tool reads as spent instead of missing, and a slot that is
    /// doing something (a live veil, a drag, a burning lantern) glows in the highlight colour.
    /// The key letters are the same five the footer prints, so the row is a reminder rather than
    /// a second control scheme.
    /// </remarks>
    private void DrawPowerups(GameState state, float rightEdge, float top, Color accent)
    {
        var charges = state.PowerupsRemaining;
        var slots = new (string Key, int Count, bool Active)[]
        {
            ("SPC", charges.Blink, false),
            ("V", charges.Veil, state.IsVeiled),
            ("T", charges.Drag, state.IsDragActive),
            ("L", charges.Lantern, state.IsLanternActive),
            ("F", charges.Shove, false),
        };

        var x = rightEdge - PowerupRowWidth;

        foreach (var slot in slots)
        {
            var empty = slot.Count == 0;
            var colour = empty
                ? Rgba(HintText.R, HintText.G, HintText.B, 105)
                : slot.Active ? GlowCyan : accent;
            var rectangle = new Rectangle(x, top, PowerupSlotWidth, 22);

            Raylib.DrawRectangleRounded(rectangle, 0.55f, 6, Rgba(3, 10, 12, 210));
            Raylib.DrawRectangleRoundedLines(
                rectangle,
                0.55f,
                6,
                Rgba(colour.R, colour.G, colour.B, (byte)(slot.Active ? 185 : 60)));

            Text(slot.Key, x + 7, top + 8, 1, colour);

            var value = FormattableString.Invariant($"{slot.Count}");
            Text(value, x + PowerupSlotWidth - 9 - TextWidth(value, 1), top + 8, 1, colour);

            x += PowerupSlotWidth + PowerupGap;
        }
    }

    /// <summary>Returns the width of one status chip.</summary>
    private static int ChipWidth(string label, string value) =>
        14 + TextWidth(label, 1) + 6 + TextWidth(value, 2) + 10;

    /// <summary>
    /// Draws one compact status chip: a glowing dot, a label and its value, in the floor's accent.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="label">The small all-caps label.</param>
    /// <param name="value">The value, drawn brighter.</param>
    /// <param name="accent">The value's colour.</param>
    private void DrawStatChip(float x, float y, string label, string value, Color accent)
    {
        var width = ChipWidth(label, value);
        var rectangle = new Rectangle(x, y, width, 32);

        Raylib.DrawRectangleRounded(rectangle, 0.5f, 8, Rgba(3, 10, 12, 215));
        Raylib.DrawRectangleRoundedLines(rectangle, 0.5f, 8, Rgba(accent.R, accent.G, accent.B, 65));
        Raylib.DrawCircleV(new Vector2(x + 10, y + 16), 2.6f, accent);

        Text(label, x + 16, y + 12, 1, Rgba(HudText.R, HudText.G, HudText.B, 160));
        Text(value, x + 16 + TextWidth(label, 1) + 6, y + 9, 2, accent);
    }

    /// <summary>
    /// Truncates <paramref name="text"/> with an ellipsis so a long floor name or gimmick fits the
    /// room the status chips leave.
    /// </summary>
    private static string FitText(string text, int scale, float maxWidth)
    {
        if (maxWidth <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        // The face is fixed-advance, so the cut is arithmetic rather than a search: how many
        // glyphs fit, less the three an ellipsis takes.
        var glyphs = (int)(((maxWidth / scale) + 1) / PixelFont.Advance);

        if (glyphs >= text.Length)
        {
            return text;
        }

        return glyphs <= 3
            ? text[..Math.Max(1, glyphs)]
            : text[..(glyphs - 3)].TrimEnd() + "...";
    }

    /// <summary>
    /// Draws the control hints on a translucent footer strip, switching to the aiming controls
    /// while the player is placing a barrier.
    /// </summary>
    private void DrawHints(GameState state, FloorChrome? chrome)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var top = height - RaylibLayout.FooterHeight;
        var accent = chrome?.Style.Accent ?? GlowGreen;

        Raylib.DrawRectangle(0, top, width, RaylibLayout.FooterHeight, Panel);
        Raylib.DrawLineEx(
            new Vector2(0, top),
            new Vector2(width, top),
            1.0f,
            Rgba(accent.R, accent.G, accent.B, 55));

        var centreY = top + (RaylibLayout.FooterHeight / 2f);
        var heartsEnd = OriginHeartX + HeartsWidth(state);

        DrawHearts(state, OriginHeartX, centreY);
        DrawHunterPips(state, heartsEnd + 4, centreY);

        // Inside a safe room the footer says what the room is: the pause is the one rule the
        // player has to be told about, because nothing moving is exactly when it is unclear.
        var text = state.IsInSafeRoom
            ? "SAFE ROOM   E: LOOK   WASD: WALK   ESC: PAUSE"
            : state.IsSafeRoomDoorInSight
                ? "THE ROOM IS NEAR   WASD: MOVE   SHIFT: RUN   SPACE: BLINK   V: VEIL   T: SLOW   L: LIGHT   F: SHOVE"
                : "WASD: MOVE   SHIFT: RUN   SPACE: BLINK   V: VEIL   T: SLOW   L: LIGHT   F: SHOVE   ESC: PAUSE";

        // The floor's own clock goes in front of the keys while it matters: a tide that is about
        // to turn and locks still barring the arch are both things the footer can count.
        if (state.Rule == FloorRule.Tide)
        {
            var tide = state.IsTideIn
                ? $"TIDE IN {(int)Math.Ceiling(state.TideSecondsLeft)}s   "
                : $"TIDE OUT {(int)Math.Ceiling(state.TideSecondsLeft)}s   ";
            text = tide + text;
        }
        else if (state.Locks.Count > 0)
        {
            text = $"LOCKS {state.Locks.Count - state.LocksRemaining}/{state.Locks.Count}   " + text;
        }
        var color = state.IsInSafeRoom || state.IsSafeRoomDoorInSight ? GlowGreen : HintText;

        // The hints are centred in what the hearts and the hunter pips leave, not in the whole
        // strip: on a narrow window the long play hint would otherwise run straight under them.
        var left = heartsEnd + 12f + (state.HunterCount > 1 ? (state.HunterCount * 12f) + 6f : 0f);
        var room = width - left - 16f;

        // A window too narrow for the long hint drops a whole scale rather than a point: the face
        // only has integer sizes, and one that half-fits is worse than a smaller one that fits.
        var scale = 2;

        if (TextWidth(text, scale) > room)
        {
            scale = 1;
        }

        var textTop = top + ((RaylibLayout.FooterHeight - (PixelFont.LineHeight * scale)) / 2f);
        Text(text, left + ((room - TextWidth(text, scale)) / 2f), textTop, scale, color);
    }

    /// <summary>
    /// Darkens the window edges with a soft gradient so the maze feels enclosed by rock, and
    /// tints that frame pink while the killer is near.
    /// </summary>
    private static void DrawVignette(bool danger)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var tint = danger ? Rgba(30, 6, 20, 165) : Rgba(1, 6, 8, 135);

        Raylib.DrawRectangleGradientV(0, 0, width, 80, tint, Transparent);
        Raylib.DrawRectangleGradientV(0, height - 92, width, 92, Transparent, Rgba(1, 5, 7, 175));
        Raylib.DrawRectangleGradientH(0, 0, 84, height, tint, Transparent);
        Raylib.DrawRectangleGradientH(width - 84, 0, 84, height, Transparent, tint);
    }

    /// <summary>
    /// Sprinkles a little film grain over the frame. Positions come from a hash of the grain
    /// index and a slow frame clock, so it shimmers without keeping any state.
    /// </summary>
    private static void DrawGrain()
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var frame = (int)(Raylib.GetTime() * 12);

        for (var index = 0; index < GrainCount; index++)
        {
            var x = Hash(index, frame, 901) * width;
            var y = Hash(index, frame, 902) * height;
            var alpha = (byte)(6 + (Hash(index, frame, 903) * 12));

            Raylib.DrawCircle((int)x, (int)y, 1.0f, Rgba(190, 255, 225, alpha));
        }
    }

    /// <summary>Stable pseudo-random value in [0, 1) for a cell and a salt.</summary>
    private static float Hash(int row, int column, int salt) =>
        (float)Frac((Math.Sin((row * 127.1) + (column * 311.7) + (salt * 74.7)) * 43758.5453));

    /// <summary>Returns the fractional part of a value, the basis of the hashes above.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    /// <summary>
    /// Builds a colour from 0-255 channels. The explicit byte parameters keep the call on the
    /// byte constructor, because <c>Color</c> also has int and float constructors.
    /// </summary>
    private static Color Rgba(byte red, byte green, byte blue, byte alpha) => new(red, green, blue, alpha);

    /// <summary>Gets the compiled face, which must have been supplied before anything is drawn.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no font was set.</exception>
    private PixelFont Font =>
        _font ?? throw new InvalidOperationException("Call UsePixelFont before drawing the interface.");

    /// <summary>Draws a label in the compiled face at a whole-pixel integer scale.</summary>
    /// <param name="text">The text.</param>
    /// <param name="x">The left edge, rounded to a pixel.</param>
    /// <param name="y">The top edge, rounded to a pixel.</param>
    /// <param name="scale">The integer scale.</param>
    /// <param name="color">The tint.</param>
    private void Text(string text, float x, float y, int scale, Color color) =>
        Font.Draw(text, (int)MathF.Round(x), (int)MathF.Round(y), scale, color);

    /// <summary>Measures a label the same way <see cref="Text"/> draws it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="scale">The integer scale.</param>
    /// <returns>The width in pixels.</returns>
    private static int TextWidth(string text, int scale) => PixelFont.MeasureWidth(text, scale);
}

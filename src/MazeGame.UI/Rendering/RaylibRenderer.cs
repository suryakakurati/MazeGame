using System;
using System.Numerics;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Draws the game as a bioluminescent underground cave: mossy rock walls, glowing spores,
/// mushrooms and lichen, a small lantern-bearing explorer and a spindly creature lit from
/// within.
/// </summary>
/// <remarks>
/// <para>
/// The renderer owns <b>presentation only</b>. What the player is allowed to see is a game
/// rule (<see cref="GameState.IsVisible"/>); this class decides how to make that look like a
/// cave. It keeps one piece of its own state — the cells the player has already seen — so
/// explored tunnels stay faintly visible instead of blinking in and out as the lantern moves.
/// </para>
/// <para>
/// Nothing is loaded from disk: the art is built from Raylib primitives (rounded tiles, thick
/// strokes with rounded caps for rock, small polygons for the characters) plus deterministic
/// hashes per cell, so the cave is detailed but reproducible and needs no texture pipeline.
/// The palette runs green, pink and cyan on near-black teal, and every glow is a radial
/// gradient rather than a flat colour.
/// </para>
/// <para>
/// Frames must be bracketed by <c>Raylib.BeginDrawing</c> / <c>Raylib.EndDrawing</c>, which the
/// frontend does: <see cref="Draw"/> fills in one frame and never calls those itself.
/// </para>
/// </remarks>
public sealed partial class RaylibRenderer : IRenderer
{
    // ---- Palette: bioluminescent cave ------------------------------------------------
    private static readonly Color CaveTop = new(6, 18, 24, 255);
    private static readonly Color CaveBottom = new(1, 4, 8, 255);
    private static readonly Color FloorDim = new(6, 16, 16, 255);
    private static readonly Color Grout = new(3, 9, 10, 255);
    private static readonly Color Rock = new(13, 23, 22, 255);
    private static readonly Color RockRim = new(33, 62, 53, 255);
    private static readonly Color Moss = new(47, 122, 79, 255);
    private static readonly Color GlowGreen = new(86, 240, 160, 255);
    private static readonly Color GlowCyan = new(70, 232, 224, 255);
    private static readonly Color GlowPink = new(255, 111, 168, 255);
    private static readonly Color GlowBlue = new(104, 168, 255, 255);
    private static readonly Color PlayerCloak = new(8, 20, 22, 255);
    private static readonly Color PlayerRim = new(96, 224, 176, 255);
    private static readonly Color PlayerFace = new(206, 236, 220, 255);
    private static readonly Color Lantern = new(255, 232, 160, 255);
    private static readonly Color KillerSkin = new(9, 8, 14, 255);
    private static readonly Color KillerRim = new(196, 74, 150, 255);
    private static readonly Color KillerEye = new(255, 158, 214, 255);
    private static readonly Color ExitLockedColor = new(58, 92, 104, 255);
    private static readonly Color ExitOpenColor = new(70, 232, 224, 255);
    private static readonly Color BarrierBody = new(18, 48, 34, 255);
    private static readonly Color BarrierThorn = new(88, 176, 122, 255);
    private static readonly Color CursorColor = new(96, 245, 210, 255);
    private static readonly Color HudText = new(208, 233, 220, 255);
    private static readonly Color HintText = new(110, 145, 132, 255);
    private static readonly Color DangerText = new(255, 128, 176, 255);
    private static readonly Color Panel = new(4, 12, 14, 190);
    private static readonly Color PanelEdge = new(70, 200, 150, 52);
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private const int HudFontSize = 15;
    private const int HintFontSize = 14;
    private const int BannerTitleSize = 30;
    private const int BannerSubtitleSize = 14;
    private const float TextSpacing = 1.4f;
    private const int MoteCount = 70;
    private const int GrainCount = 110;

    /// <summary>
    /// The four wall sides of a cell, with the hash salt that gives each side its own lichen.
    /// Stored once so the wall passes never allocate a direction array.
    /// </summary>
    private static readonly (Direction Direction, int Salt)[] WallSides =
    [
        (Direction.North, 10),
        (Direction.South, 20),
        (Direction.West, 30),
        (Direction.East, 40),
    ];

    private readonly HashSet<Cell> _explored = [];
    private readonly Font _font;

    /// <summary>
    /// Creates the renderer. Must be constructed <b>after</b> the window exists, because the
    /// default raylib font is captured here and only exists once the graphics device is up.
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

        _font = Raylib.GetFontDefault();
    }

    /// <summary>
    /// Forgets which cells have been explored, for the start of a new game.
    /// </summary>
    /// <remarks>
    /// <see cref="Cell"/> is a value type, so cells of a new maze compare equal to cells of the
    /// old one at the same position. Without this reset a replay would start with the previous
    /// cave already drawn in, which would read as a bug in the maze rather than as UI memory.
    /// </remarks>
    public void Reset() => _explored.Clear();

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // The killer's aura is the cue that something is close, so it drives the dread version
        // of the light: a little dimmer, a little faster, and colder at the edges.
        var danger = state.EnemyGlow.Count > 0;
        var light = Flicker(danger);

        DrawBackground();

        // Two passes: every floor and every rock first, then the lit details. Drawing the rock
        // for both cells that share a wall would otherwise cover the neighbour's lit rim, and a
        // second pass keeps each cell's edge glowing on its own side of the seam.
        for (var row = 0; row < state.Maze.Rows; row++)
        {
            for (var column = 0; column < state.Maze.Columns; column++)
            {
                DrawCellBase(state, state.Maze.GetCell(row, column));
            }
        }

        for (var row = 0; row < state.Maze.Rows; row++)
        {
            for (var column = 0; column < state.Maze.Columns; column++)
            {
                DrawCellDetails(state, state.Maze.GetCell(row, column), light);
            }
        }

        DrawEnemyAura(state);
        DrawMotes(state, light);
        DrawAimCursor(state);

        // The vignette darkens the cave, not the interface: it goes on before the HUD so the
        // status pills and hints keep their contrast.
        DrawVignette(danger);
        DrawHud(state);
        DrawHints(state);
        DrawEndBanner(state);
        DrawGrain();
    }

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
    /// First pass for one cell: its floor slab and the dark rock of its walls, for anything the
    /// player has seen. Unexplored cells are left to the cave darkness.
    /// </summary>
    private void DrawCellBase(GameState state, Cell cell)
    {
        var visible = state.IsVisible(cell);
        var known = visible || _explored.Contains(cell);

        if (visible)
        {
            _explored.Add(cell);
        }

        if (!known)
        {
            return;
        }

        var x = RaylibLayout.CellX(cell.Column);
        var y = RaylibLayout.CellY(cell.Row);

        if (visible)
        {
            DrawFloor(cell, x, y);
        }
        else
        {
            Raylib.DrawRectangleRounded(FloorRect(x, y), 0.22f, 5, FloorDim);
        }

        DrawWalls(cell, visible, x, y);
    }

    /// <summary>
    /// Second pass for one cell: the lit rim of its walls, the cave's glowing flora, whatever
    /// is standing there, and the falloff of the lantern.
    /// </summary>
    private static void DrawCellDetails(GameState state, Cell cell, double light)
    {
        if (!state.IsVisible(cell))
        {
            return;
        }

        var x = RaylibLayout.CellX(cell.Column);
        var y = RaylibLayout.CellY(cell.Row);

        DrawWallDetails(cell, x, y);
        DrawFlora(cell, x, y);
        DrawContents(state, cell, x, y);
        DrawTorchFalloff(state, cell, x, y, light);
    }

    /// <summary>Builds the inset rectangle of a floor tile.</summary>
    private static Rectangle FloorRect(int x, int y) =>
        new(x + 2, y + 2, RaylibLayout.CellSize - 4, RaylibLayout.CellSize - 4);

    /// <summary>
    /// Draws a floor tile: a rounded slab tinted per cell, with a couple of grit specks and a
    /// crack. The per-cell hash keeps the variation stable as the player walks around.
    /// </summary>
    private static void DrawFloor(Cell cell, int x, int y)
    {
        var tint = Hash(cell.Row, cell.Column, 1);

        var floor = Rgba(7, (byte)(22 + (tint * 12)), (byte)(21 + (tint * 10)), 255);
        Raylib.DrawRectangleRounded(FloorRect(x, y), 0.22f, 5, floor);

        // Grout: a darker inner border keeps the tiles readable without a grid of lines.
        Raylib.DrawRectangleRoundedLines(FloorRect(x, y), 0.22f, 5, Rgba(Grout.R, Grout.G, Grout.B, 160));

        var gritX = x + 6 + (Hash(cell.Row, cell.Column, 2) * 26);
        var gritY = y + 6 + (Hash(cell.Row, cell.Column, 3) * 26);

        Raylib.DrawCircle((int)gritX, (int)gritY, 1.2f, Rgba(30, 74, 62, 130));

        if (Hash(cell.Row, cell.Column, 4) > 0.55f)
        {
            var crackStart = new Vector2(x + 8 + (Hash(cell.Row, cell.Column, 5) * 20), y + 8);
            var crackEnd = new Vector2(crackStart.X + 6, crackStart.Y + 12);

            Raylib.DrawLineEx(crackStart, crackEnd, 1.0f, Rgba(2, 9, 10, 150));
        }
    }

    /// <summary>
    /// Draws the dark rock of every standing wall of a cell, as thick rounded strokes. The
    /// rounded caps make corners read as joined boulders rather than as a grid.
    /// </summary>
    private static void DrawWalls(Cell cell, bool visible, int x, int y)
    {
        foreach (var (direction, _) in WallSides)
        {
            if (cell.HasWall(direction))
            {
                var (start, end) = WallSegment(direction, x, y);
                DrawRock(start, end, visible);
            }
        }
    }

    /// <summary>
    /// Draws the lit rim, mossy fringe and glowing lichen of a cell's walls. This runs in a
    /// separate pass so a neighbouring cell's rock cannot paint over the rim.
    /// </summary>
    private static void DrawWallDetails(Cell cell, int x, int y)
    {
        foreach (var (direction, salt) in WallSides)
        {
            if (!cell.HasWall(direction))
            {
                continue;
            }

            var (start, end) = WallSegment(direction, x, y);

            Raylib.DrawLineEx(start, end, 1.6f, Rgba(RockRim.R, RockRim.G, RockRim.B, 210));

            var mossAlpha = (byte)(60 + (Hash(cell.Row, cell.Column, salt) * 80));
            Raylib.DrawLineEx(start, end, 2.0f, Rgba(Moss.R, Moss.G, Moss.B, mossAlpha));

            var speckCount = Hash(cell.Row, cell.Column, salt + 1) > 0.45f ? 2 : 1;

            for (var index = 0; index < speckCount; index++)
            {
                var t = 0.2f + (Hash(cell.Row, cell.Column, salt + 2 + index) * 0.6f);
                var point = new Vector2(
                    start.X + ((end.X - start.X) * t),
                    start.Y + ((end.Y - start.Y) * t));

                var speck = Hash(cell.Row, cell.Column, salt + 6 + index);
                var color = speck < 0.4f ? GlowGreen : speck < 0.7f ? GlowCyan : GlowPink;
                var alpha = (byte)(120 + (speck * 110));

                Raylib.DrawCircleGradient(point, 6.0f, Rgba(color.R, color.G, color.B, (byte)(alpha / 3)), Transparent);
                Raylib.DrawCircleV(point, 1.6f, Rgba(color.R, color.G, color.B, alpha));
            }
        }
    }

    /// <summary>Returns the start and end points of one wall of a cell.</summary>
    private static (Vector2 Start, Vector2 End) WallSegment(Direction direction, int x, int y)
    {
        var inset = 2;
        var far = RaylibLayout.CellSize - inset;

        return direction switch
        {
            Direction.North => (new Vector2(x + inset, y + inset), new Vector2(x + far, y + inset)),
            Direction.South => (new Vector2(x + inset, y + far), new Vector2(x + far, y + far)),
            Direction.West => (new Vector2(x + inset, y + inset), new Vector2(x + inset, y + far)),
            _ => (new Vector2(x + far, y + inset), new Vector2(x + far, y + far)),
        };
    }

    /// <summary>Draws one rock stroke, with rounded ends, in the current rock shade.</summary>
    private static void DrawRock(Vector2 start, Vector2 end, bool visible)
    {
        var thickness = RaylibLayout.WallThickness;
        var rock = visible ? Rock : Rgba(8, 18, 18, 255);

        Raylib.DrawLineEx(start, end, thickness, rock);
        Raylib.DrawCircleV(start, thickness / 2f, rock);
        Raylib.DrawCircleV(end, thickness / 2f, rock);
    }

    /// <summary>
    /// Scatters the cave's own light: small glowing mushrooms and spore clusters in some cells.
    /// They are stable per cell, so they read as growing there rather than as random noise.
    /// </summary>
    private static void DrawFlora(Cell cell, int x, int y)
    {
        DrawMossTufts(cell, x, y);

        if (Hash(cell.Row, cell.Column, 51) > 0.22f)
        {
            return;
        }

        var offsetX = 7 + (Hash(cell.Row, cell.Column, 52) * 22);
        var offsetY = 7 + (Hash(cell.Row, cell.Column, 53) * 22);
        var color = Hash(cell.Row, cell.Column, 54) < 0.5f ? GlowGreen : GlowPink;

        var basePoint = new Vector2((float)(x + offsetX), (float)(y + offsetY + 4));
        var capPoint = new Vector2((float)(x + offsetX), (float)(y + offsetY));

        Raylib.DrawCircleGradient(capPoint, 10, Rgba(color.R, color.G, color.B, 46), Transparent);
        Raylib.DrawLineEx(basePoint, capPoint, 1.4f, Rgba(58, 118, 78, 220));
        Raylib.DrawCircleV(capPoint, 2.4f, color);

        // A second, smaller cap makes the cluster read as a little colony of fungus.
        if (Hash(cell.Row, cell.Column, 55) > 0.5f)
        {
            var second = new Vector2(capPoint.X + 4, capPoint.Y + 2);
            Raylib.DrawCircleGradient(second, 7, Rgba(color.R, color.G, color.B, 34), Transparent);
            Raylib.DrawLineEx(new Vector2(second.X, second.Y + 3), second, 1.0f, Rgba(58, 118, 78, 180));
            Raylib.DrawCircleV(second, 1.6f, Rgba(color.R, color.G, color.B, 220));
        }
    }

    /// <summary>
    /// Grows moss along the foot of the walls, where damp rock meets the floor. It is what stops
    /// a floor tile from reading as a plain rounded square.
    /// </summary>
    private static void DrawMossTufts(Cell cell, int x, int y)
    {
        var size = RaylibLayout.CellSize;

        foreach (var (direction, salt) in WallSides)
        {
            if (!cell.HasWall(direction) || Hash(cell.Row, cell.Column, salt + 20) < 0.45f)
            {
                continue;
            }

            for (var tuft = 0; tuft < 3; tuft++)
            {
                var t = 0.15 + (Hash(cell.Row, cell.Column, salt + 21 + tuft) * 0.7);
                var depth = 6 + (Hash(cell.Row, cell.Column, salt + 31 + tuft) * 5);

                var point = direction switch
                {
                    Direction.North => new Vector2((float)(x + (size * t)), (float)(y + depth)),
                    Direction.South => new Vector2((float)(x + (size * t)), (float)(y + size - depth)),
                    Direction.West => new Vector2((float)(x + depth), (float)(y + (size * t))),
                    _ => new Vector2((float)(x + size - depth), (float)(y + (size * t))),
                };

                var radius = 1.2f + (Hash(cell.Row, cell.Column, salt + 41 + tuft) * 1.8f);
                var alpha = (byte)(70 + (Hash(cell.Row, cell.Column, salt + 51 + tuft) * 70));

                Raylib.DrawCircleV(point, radius, Rgba(Moss.R, Moss.G, Moss.B, alpha));
            }
        }
    }

    /// <summary>
    /// Draws whatever stands on a visible cell. The player is drawn last so the explorer is
    /// never hidden by another object sharing the tile.
    /// </summary>
    private static void DrawContents(GameState state, Cell cell, int x, int y)
    {
        var centerX = x + (RaylibLayout.CellSize / 2);
        var centerY = y + (RaylibLayout.CellSize / 2);

        if (state.Barriers.Contains(cell))
        {
            DrawBarrier(centerX, centerY, state.BarrierSeals.ContainsKey(cell));
        }

        if (cell == state.Exit)
        {
            DrawExit(state, x, y);
        }

        if (state.Keys.Contains(cell))
        {
            DrawKey(centerX, centerY);
        }

        if (cell == state.Lure)
        {
            DrawLure(centerX, centerY);
        }

        if (cell == state.Enemy)
        {
            DrawKiller(centerX, centerY);
        }

        if (cell == state.Player)
        {
            DrawPlayer(centerX, centerY);
        }
    }

    /// <summary>
    /// Draws the explorer: a small hooded figure holding a lantern, with a warm glow from the
    /// lantern and a cold green rim from the cave. Not a circle on a background.
    /// </summary>
    private static void DrawPlayer(int centerX, int centerY)
    {
        var time = Raylib.GetTime();
        var pulse = 0.85 + (Math.Sin(time * 2.4) * 0.15);

        // Lantern light: a wide cool halo from the cave and a small warm one at the hand.
        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY), 86, Rgba(80, 255, 200, 18), Transparent);
        Raylib.DrawCircleGradient(
            new Vector2(centerX + 9, centerY + 5), 30, Rgba(255, 224, 150, (byte)(38 * pulse)), Transparent);

        // Feet and cloak: a bigger tinted ellipse behind gives the silhouette a lit edge.
        Raylib.DrawCircle(centerX - 4, centerY + 14, 2.2f, Rgba(4, 12, 13, 255));
        Raylib.DrawCircle(centerX + 4, centerY + 14, 2.2f, Rgba(4, 12, 13, 255));
        Raylib.DrawEllipse(centerX, centerY + 3, 10, 11, Rgba(PlayerRim.R, PlayerRim.G, PlayerRim.B, 70));
        Raylib.DrawEllipse(centerX, centerY + 3, 9, 10, PlayerCloak);

        // Hood: a rim triangle slightly larger than the cloak one, so it catches the glow.
        var hoodRim = new Vector2(centerX, centerY - 17);
        var hoodLeftRim = new Vector2(centerX - 10, centerY - 2);
        var hoodRightRim = new Vector2(centerX + 10, centerY - 2);
        Raylib.DrawTriangle(hoodLeftRim, hoodRightRim, hoodRim, Rgba(PlayerRim.R, PlayerRim.G, PlayerRim.B, 55));

        var hoodTip = new Vector2(centerX, centerY - 15);
        var hoodLeft = new Vector2(centerX - 8, centerY - 2);
        var hoodRight = new Vector2(centerX + 8, centerY - 2);
        Raylib.DrawTriangle(hoodLeft, hoodRight, hoodTip, PlayerCloak);

        // The face inside the hood, and the lantern hanging by the right hand.
        Raylib.DrawCircle(centerX, centerY - 3, 3.6f, PlayerFace);
        Raylib.DrawCircle(centerX + 9, centerY + 7, 2.6f, Lantern);
        Raylib.DrawCircle(centerX + 9, centerY + 7, 5.0f, Rgba(Lantern.R, Lantern.G, Lantern.B, 90));
    }

    /// <summary>
    /// Draws the killer as a spindly, hunched silhouette with a pink rim and two bright eyes,
    /// swaying on thin legs. The point is that it reads as a creature, not as a threat dot.
    /// </summary>
    private static void DrawKiller(int centerX, int centerY)
    {
        var time = Raylib.GetTime();

        Raylib.DrawEllipse(centerX, centerY + 13, 9, 3.5f, Rgba(0, 0, 0, 90));

        // Three thin legs, each with a pink rim line drawn under a darker body line.
        for (var leg = 0; leg < 3; leg++)
        {
            var sway = Math.Sin((time * 1.6) + leg) * 2.0;
            var start = new Vector2(centerX, centerY + 3);
            var end = new Vector2(centerX - 8 + (leg * 8) + (float)sway, centerY + 16);

            Raylib.DrawLineEx(start, end, 3.0f, Rgba(KillerRim.R, KillerRim.G, KillerRim.B, 80));
            Raylib.DrawLineEx(start, end, 1.4f, KillerSkin);
        }

        // Torso and head: rim shape first, dark shape on top.
        Raylib.DrawEllipse(centerX, centerY + 1, 8.5f, 11.5f, Rgba(KillerRim.R, KillerRim.G, KillerRim.B, 70));
        Raylib.DrawEllipse(centerX, centerY + 1, 7.0f, 10.5f, KillerSkin);
        Raylib.DrawCircle(centerX, centerY - 10, 7.5f, Rgba(KillerRim.R, KillerRim.G, KillerRim.B, 80));
        Raylib.DrawCircle(centerX, centerY - 10, 6.5f, KillerSkin);

        // Two pale eyes and a couple of drifting wisps above the head.
        Raylib.DrawCircle(centerX - 2, centerY - 11, 1.6f, KillerEye);
        Raylib.DrawCircle(centerX + 2, centerY - 11, 1.6f, KillerEye);

        for (var wisp = 0; wisp < 2; wisp++)
        {
            var drift = Math.Sin((time * 1.1) + wisp) * 4;
            var from = new Vector2(centerX - 4 + (wisp * 8), centerY - 17);
            var to = new Vector2(from.X + (float)drift, from.Y - 8);

            Raylib.DrawLineEx(from, to, 1.0f, Rgba(KillerRim.R, KillerRim.G, KillerRim.B, 120));
        }
    }

    /// <summary>
    /// Draws a key as a small cluster of glowing cave fungus: a stalk, one bright cap and a
    /// scattering of spores. Pink, so it is never confused with the cyan exit.
    /// </summary>
    private static void DrawKey(int centerX, int centerY)
    {
        var time = Raylib.GetTime();
        var pulse = 0.8 + (Math.Sin((time * 3.0) + centerX) * 0.2);

        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY), 18, Rgba(GlowPink.R, GlowPink.G, GlowPink.B, (byte)(52 * pulse)), Transparent);

        Raylib.DrawLineEx(
            new Vector2(centerX, centerY + 6), new Vector2(centerX, centerY + 1), 2.0f, Rgba(58, 118, 78, 230));
        Raylib.DrawCircle(centerX - 2, centerY, 3.4f, GlowPink);
        Raylib.DrawCircle(centerX + 3, centerY + 2, 2.4f, Rgba(208, 92, 150, 255));
        Raylib.DrawCircle(centerX - 3, centerY - 1, 1.0f, Rgba(255, 220, 240, 220));
    }

    /// <summary>
    /// Draws the exit as a rounded stone arch with a glowing interior: cold cyan when open, a
    /// dull blue-grey with a bar across it while locked.
    /// </summary>
    private static void DrawExit(GameState state, int x, int y)
    {
        var rectangle = new Rectangle(x + 9, y + 6, 22, 28);
        var color = state.IsExitUnlocked ? ExitOpenColor : ExitLockedColor;

        Raylib.DrawRectangleRounded(rectangle, 0.45f, 6, Rgba(3, 10, 11, 200));
        Raylib.DrawRectangleRoundedLines(rectangle, 0.45f, 6, color);
        Raylib.DrawRectangleRoundedLines(new Rectangle(x + 11, y + 8, 18, 24), 0.45f, 6, Rgba(color.R, color.G, color.B, 90));

        var centerX = x + (RaylibLayout.CellSize / 2);

        if (!state.IsExitUnlocked)
        {
            Raylib.DrawRectangle(centerX - 7, y + 19, 14, 3, Rgba(color.R, color.G, color.B, 200));
            return;
        }

        var time = Raylib.GetTime();
        var pulse = 0.7 + (Math.Sin(time * 2.0) * 0.3);

        Raylib.DrawCircleGradient(
            new Vector2(centerX, y + 20), 30, Rgba(color.R, color.G, color.B, (byte)(46 * pulse)), Transparent);

        for (var beam = -1; beam <= 1; beam++)
        {
            var from = new Vector2(centerX + (beam * 5), y + 12);
            var to = new Vector2(centerX + (beam * 9), y - 12);
            Raylib.DrawLineEx(from, to, 1.0f, Rgba(color.R, color.G, color.B, 40));
        }
    }

    /// <summary>
    /// Draws a placed barrier as a thorny barricade of cave moss: a dark rounded block ringed
    /// with spikes and crossed by a vein of glowing sap.
    /// </summary>
    /// <param name="centerX">Horizontal centre of the cell, in pixels.</param>
    /// <param name="centerY">Vertical centre of the cell, in pixels.</param>
    /// <param name="isSealed">
    /// Whether the seal still holds. A sealed barrier is drawn as a taut, glowing cage — the
    /// thing the killer is currently treating as a wall — while a spent one is the same shape
    /// dimmed and cracked, so the two states are never confused at a glance.
    /// </param>
    private static void DrawBarrier(int centerX, int centerY, bool isSealed)
    {
        var block = new Rectangle(centerX - 15, centerY - 15, 30, 30);
        var time = Raylib.GetTime();
        var pulse = 0.6 + (Math.Sin(time * 3.4) * 0.4);
        var life = isSealed ? 1.0 : 0.4;

        if (isSealed)
        {
            // The seal itself: a breathing halo that says "this is holding something back".
            Raylib.DrawCircleGradient(
                new Vector2(centerX, centerY),
                26,
                Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(44 * pulse)),
                Transparent);
        }

        Raylib.DrawRectangleRounded(block, 0.3f, 5, BarrierBody);
        Raylib.DrawRectangleRoundedLines(
            block,
            0.3f,
            5,
            Rgba(BarrierThorn.R, BarrierThorn.G, BarrierThorn.B, (byte)(150 * life)));

        for (var thorn = 0; thorn < 6; thorn++)
        {
            var angle = (float)((thorn * Math.PI / 3.0) + (isSealed ? 0.0 : 0.18));
            var baseX = centerX + (MathF.Cos(angle) * 14);
            var baseY = centerY + (MathF.Sin(angle) * 14);
            var tipLength = isSealed ? 21 : 18;
            var tipX = centerX + (MathF.Cos(angle) * tipLength);
            var tipY = centerY + (MathF.Sin(angle) * tipLength);
            var wingX = -MathF.Sin(angle) * 3;
            var wingY = MathF.Cos(angle) * 3;

            Raylib.DrawTriangle(
                new Vector2(baseX + wingX, baseY + wingY),
                new Vector2(baseX - wingX, baseY - wingY),
                new Vector2(tipX, tipY),
                Rgba(BarrierThorn.R, BarrierThorn.G, BarrierThorn.B, (byte)(255 * life)));
        }

        // The sap vein is green while the seal holds and cools to pink once it has cracked: the
        // colour is the state, so no text is needed to explain it.
        var vein = isSealed
            ? Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(120 + (pulse * 110)))
            : Rgba(GlowPink.R, GlowPink.G, GlowPink.B, 90);

        Raylib.DrawLineEx(new Vector2(centerX - 8, centerY - 8), new Vector2(centerX + 7, centerY + 7), 1.2f, vein);
        Raylib.DrawLineEx(new Vector2(centerX + 7, centerY - 6), new Vector2(centerX - 5, centerY + 8), 1.0f, vein);

        if (isSealed)
        {
            Raylib.DrawCircleLines(centerX, centerY, 6, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(70 + (pulse * 110))));
            Raylib.DrawCircleV(new Vector2(centerX, centerY), 2.0f, Rgba(206, 255, 236, 230));
        }
    }

    /// <summary>
    /// Draws the wisp: a small cyan flame with a bright core, a soft halo and three orbiting
    /// sparks. It pulses faster than anything else in the cave, so the eye finds it even at the
    /// edge of the lantern light.
    /// </summary>
    private static void DrawLure(int centerX, int centerY)
    {
        var time = Raylib.GetTime();
        var pulse = 0.7 + (Math.Sin(time * 5.2) * 0.3);
        var sway = (float)(Math.Sin(time * 3.1) * 2.0);

        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY), 36, Rgba(GlowCyan.R, GlowCyan.G, GlowCyan.B, (byte)(30 * pulse)), Transparent);
        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY), 17, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(64 * pulse)), Transparent);

        // The flame: a tapered tip above a round body, so it reads as a wisp and not as a key.
        Raylib.DrawTriangle(
            new Vector2(centerX - 5, centerY - 1),
            new Vector2(centerX + 5, centerY - 1),
            new Vector2(centerX + sway, centerY - 15 - (float)(pulse * 4)),
            Rgba(GlowCyan.R, GlowCyan.G, GlowCyan.B, 210));
        Raylib.DrawCircleV(new Vector2(centerX, centerY), 6.5f, Rgba(148, 255, 226, 220));
        Raylib.DrawCircleV(new Vector2(centerX, centerY), 3.0f, Rgba(232, 255, 248, 255));

        for (var spark = 0; spark < 3; spark++)
        {
            var angle = (time * 1.7) + (spark * 2.1);
            var point = new Vector2(
                centerX + (float)(Math.Cos(angle) * 12),
                centerY + (float)(Math.Sin(angle) * 9));

            Raylib.DrawCircleV(point, 1.3f, Rgba(GlowGreen.R, GlowGreen.G, GlowGreen.B, (byte)(140 + (spark * 30))));
        }
    }

    /// <summary>
    /// Paints the killer's own light as a soft pink-violet halo with a few drifting spores, so
    /// the cells it reveals read as lit by the creature rather than by the player.
    /// </summary>
    private static void DrawEnemyAura(GameState state)
    {
        if (state.EnemyGlow.Count == 0)
        {
            return;
        }

        var centerX = RaylibLayout.CellX(state.Enemy.Column) + (RaylibLayout.CellSize / 2);
        var centerY = RaylibLayout.CellY(state.Enemy.Row) + (RaylibLayout.CellSize / 2);
        var radius = (float)((state.Config.EnemyAuraRadius + 1.4) * RaylibLayout.CellSize);
        var time = Raylib.GetTime();
        var pulse = 0.7 + (Math.Sin(time * 2.2) * 0.3);

        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY),
            radius,
            Rgba(168, 62, 196, (byte)Math.Clamp(46 * pulse, 0, 255)),
            Transparent);

        for (var spore = 0; spore < 6; spore++)
        {
            var angle = (time * 0.6) + (spore * 1.05);
            var distance = 10 + (spore * 5);
            var point = new Vector2(
                centerX + (float)(Math.Cos(angle) * distance),
                centerY + (float)(Math.Sin(angle) * distance));

            Raylib.DrawCircleV(point, 1.4f, Rgba(GlowPink.R, GlowPink.G, GlowPink.B, (byte)(90 + (spore * 20))));
        }
    }

    /// <summary>
    /// Drifts glowing motes through the lantern light. Only motes inside the player's reach are
    /// drawn, so they read as spores caught in the beam instead of decoration over the map.
    /// </summary>
    private static void DrawMotes(GameState state, double light)
    {
        var time = Raylib.GetTime();
        var mazeWidth = state.Maze.Columns * RaylibLayout.CellSize;
        var mazeHeight = state.Maze.Rows * RaylibLayout.CellSize;
        var radius = (state.Config.VisionRadius + 0.7) * RaylibLayout.CellSize;
        var centerX = RaylibLayout.CellX(state.Player.Column) + (RaylibLayout.CellSize / 2.0);
        var centerY = RaylibLayout.CellY(state.Player.Row) + (RaylibLayout.CellSize / 2.0);

        for (var index = 0; index < MoteCount; index++)
        {
            var seedX = Frac(Math.Sin((index * 12.9898) + 4.1414) * 43758.5453);
            var seedY = Frac(Math.Sin((index * 78.233) + 1.618) * 12345.6789);
            var drift = Frac(seedX + (time * (0.004 + (seedY * 0.008))));
            var sway = Math.Sin((time * 0.35) + (index * 1.7)) * 0.012;

            var x = RaylibLayout.OriginX + ((drift + sway) * mazeWidth);
            var y = RaylibLayout.OriginY + (Frac(seedY + (time * 0.003)) * mazeHeight);

            var offsetX = x - centerX;
            var offsetY = y - centerY;
            var distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);

            if (distanceSquared > radius * radius)
            {
                continue;
            }

            var fade = 1.0 - (Math.Sqrt(distanceSquared) / radius);
            var alpha = (byte)Math.Clamp((int)(fade * 90 * light), 0, 255);
            var color = (index % 3) switch
            {
                0 => GlowGreen,
                1 => GlowCyan,
                _ => GlowPink,
            };

            Raylib.DrawCircle((int)x, (int)y, 1.6f, Rgba(color.R, color.G, color.B, alpha));
        }
    }

    /// <summary>
    /// Draws the aiming reticle as four glowing corner brackets and a pulsing ring. It is drawn
    /// over the fog on purpose: the player may aim anywhere, including at unseen cells.
    /// </summary>
    /// <remarks>
    /// The reticle is green while aiming a barrier and cyan while aiming a wisp, the same colours
    /// those two objects glow with on the map, so the mode is readable without the footer text.
    /// </remarks>
    private static void DrawAimCursor(GameState state)
    {
        if (!state.IsAiming)
        {
            return;
        }

        var x = RaylibLayout.CellX(state.AimCursor.Column);
        var y = RaylibLayout.CellY(state.AimCursor.Row);
        var size = RaylibLayout.CellSize;
        var time = Raylib.GetTime();
        var pulse = 0.6 + (Math.Sin(time * 4.0) * 0.4);
        var bracket = 10;
        var margin = 4;
        var accent = state.IsPlacingLure ? GlowCyan : CursorColor;

        var color = Rgba(accent.R, accent.G, accent.B, (byte)(120 + (pulse * 120)));

        // Corner brackets, drawn as two short lines per corner.
        for (var corner = 0; corner < 4; corner++)
        {
            var left = corner is 0 or 2;
            var top = corner < 2;
            var cornerX = left ? x + margin : x + size - margin;
            var cornerY = top ? y + margin : y + size - margin;
            var horizontal = left ? bracket : -bracket;
            var vertical = top ? bracket : -bracket;

            Raylib.DrawLineEx(new Vector2(cornerX, cornerY), new Vector2(cornerX + horizontal, cornerY), 2.0f, color);
            Raylib.DrawLineEx(new Vector2(cornerX, cornerY), new Vector2(cornerX, cornerY + vertical), 2.0f, color);
        }

        Raylib.DrawCircleLines(
            x + (size / 2), y + (size / 2), 13, Rgba(accent.R, accent.G, accent.B, (byte)(40 + (pulse * 60))));
        Raylib.DrawCircleGradient(
            new Vector2(x + (size / 2), y + (size / 2)), 22, Rgba(accent.R, accent.G, accent.B, 18), Transparent);
    }

    /// <summary>
    /// Draws the status strip as floating glass pills: keys, barriers and the exit, each with a
    /// glowing dot in its own colour. When the killer is close an extra warning appears, so the
    /// player gets a cue that is not only visual atmosphere.
    /// </summary>
    private void DrawHud(GameState state)
    {
        var width = Raylib.GetScreenWidth();

        Raylib.DrawRectangle(0, 0, width, RaylibLayout.HudHeight, Panel);
        Raylib.DrawLineEx(
            new Vector2(0, RaylibLayout.HudHeight - 1),
            new Vector2(width, RaylibLayout.HudHeight - 1),
            1.5f,
            PanelEdge);

        var x = (float)RaylibLayout.OriginX;
        const float y = 15f;

        x = DrawStatPill(x, y, "KEYS", $"{state.KeysCollected}/{state.Config.KeyCount}", GlowPink) + 10;
        x = DrawStatPill(x, y, "BARRIERS", $"{state.BarriersRemaining}", GlowGreen) + 10;
        x = DrawStatPill(x, y, "WISPS", $"{state.LuresRemaining}", GlowCyan) + 10;
        DrawStatPill(x, y, "EXIT", state.IsExitUnlocked ? "OPEN" : "LOCKED", state.IsExitUnlocked ? ExitOpenColor : ExitLockedColor);

        if (state.EnemyGlow.Count == 0)
        {
            return;
        }

        var pulse = 0.55 + (Math.Sin(Raylib.GetTime() * 3.2) * 0.45);
        const string warning = "IT IS CLOSE";
        var warningWidth = TextWidth(warning, 12);

        Text(
            warning,
            width - RaylibLayout.OriginX - warningWidth,
            22,
            12,
            Rgba(DangerText.R, DangerText.G, DangerText.B, (byte)(120 + (pulse * 120))));

        Raylib.DrawRectangle(
            RaylibLayout.OriginX,
            RaylibLayout.HudHeight - 3,
            width - (2 * RaylibLayout.OriginX),
            2,
            Rgba(DangerText.R, DangerText.G, DangerText.B, (byte)(50 + (pulse * 100))));
    }

    /// <summary>
    /// Draws one status pill and returns the x coordinate just past it, so pills can be laid out
    /// left to right without hard-coded widths.
    /// </summary>
    private float DrawStatPill(float x, float y, string label, string value, Color accent)
    {
        var labelWidth = TextWidth(label, 12);
        var valueWidth = TextWidth(value, 15);
        var width = 26 + labelWidth + 8 + valueWidth + 14;
        var rectangle = new Rectangle(x, y, width, 32);

        Raylib.DrawRectangleRounded(rectangle, 0.5f, 8, Rgba(3, 10, 12, 210));
        Raylib.DrawRectangleRoundedLines(rectangle, 0.5f, 8, Rgba(accent.R, accent.G, accent.B, 70));

        Raylib.DrawCircleGradient(new Vector2(x + 14, y + 16), 9, Rgba(accent.R, accent.G, accent.B, 70), Transparent);
        Raylib.DrawCircleV(new Vector2(x + 14, y + 16), 3.0f, accent);

        Text(label, x + 24, y + 10, 12, Rgba(HudText.R, HudText.G, HudText.B, 170));
        Text(value, x + 24 + labelWidth + 8, y + 8, 15, accent);

        return x + width;
    }

    /// <summary>
    /// Draws the control hints on a translucent footer strip, switching to the aiming controls
    /// while the player is placing a barrier.
    /// </summary>
    private void DrawHints(GameState state)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var top = height - RaylibLayout.FooterHeight;

        Raylib.DrawRectangle(0, top, width, RaylibLayout.FooterHeight, Panel);
        Raylib.DrawLineEx(new Vector2(0, top), new Vector2(width, top), 1.0f, PanelEdge);

        var text = state.AimMode switch
        {
            PlacementMode.Barrier => "WASD: AIM    SPACE / ENTER: SEAL    ESC: CANCEL",
            PlacementMode.Lure => "WASD: AIM    SPACE / ENTER: LIGHT    ESC: CANCEL",
            _ => "WASD: MOVE    B: BARRIER    R: WISP    ESC: QUIT",
        };
        var color = state.AimMode switch
        {
            PlacementMode.Barrier => CursorColor,
            PlacementMode.Lure => GlowCyan,
            _ => HintText,
        };

        Text(text, (width - TextWidth(text, HintFontSize)) / 2f, top + 14, HintFontSize, color);
    }

    /// <summary>
    /// Ends the game on a card that matches the cave: dark glass, a glowing rule and the outcome
    /// in the cave's own colours, instead of a bare window that vanishes mid-chase.
    /// </summary>
    private void DrawEndBanner(GameState state)
    {
        if (state.Status == GameStatus.InProgress)
        {
            return;
        }

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        const float panelWidth = 470;
        const float panelHeight = 150;
        var left = (width - panelWidth) / 2f;
        var top = (height - panelHeight) / 2f;
        var accent = state.Status == GameStatus.Won ? ExitOpenColor : DangerText;

        Raylib.DrawRectangleRounded(new Rectangle(left, top, panelWidth, panelHeight), 0.08f, 6, Rgba(2, 8, 10, 236));
        Raylib.DrawRectangleRoundedLines(
            new Rectangle(left, top, panelWidth, panelHeight), 0.08f, 6, Rgba(accent.R, accent.G, accent.B, 120));

        var title = state.Status == GameStatus.Won ? "YOU ESCAPED" : "THE THING FOUND YOU";
        TextCentered(title, width / 2f, top + 36, BannerTitleSize, accent, 2.4f);

        Raylib.DrawLineEx(
            new Vector2(left + 70, top + 84),
            new Vector2(left + panelWidth - 70, top + 84),
            1.0f,
            Rgba(accent.R, accent.G, accent.B, 90));

        TextCentered("R / ENTER: DESCEND AGAIN      ESC: QUIT", width / 2f, top + 104, BannerSubtitleSize, HintText);
    }

    /// <summary>Draws text centred horizontally on <paramref name="centerX"/>.</summary>
    private void TextCentered(string text, float centerX, float y, int size, Color color, float spacing = TextSpacing) =>
        Text(text, centerX - (TextWidth(text, size, spacing) / 2f), y, size, color, spacing);

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

    /// <summary>
    /// Darkens a visible cell by its distance from the player, which is the lantern's falloff
    /// and the vignette in one. The colour is deep cave teal, not black, so shadows stay alive.
    /// </summary>
    private static void DrawTorchFalloff(GameState state, Cell cell, int x, int y, double light)
    {
        var rowDistance = cell.Row - state.Player.Row;
        var columnDistance = cell.Column - state.Player.Column;
        var distance = Math.Sqrt((rowDistance * rowDistance) + (columnDistance * columnDistance));

        var darkness = Math.Min(distance / (state.Config.VisionRadius + 1.0), 1.0);
        var alpha = (darkness * 0.86) + ((1.0 - light) * 0.4);
        var shaded = (byte)Math.Clamp((int)Math.Round(alpha * 255), 0, 172);

        // Cave-teal shadow rather than black, on the same rounded tile as the floor, so the edge
        // of the lantern is a soft pool instead of a grid of squares.
        Raylib.DrawRectangleRounded(FloorRect(x, y), 0.22f, 5, Rgba(2, 8, 9, shaded));
    }

    /// <summary>
    /// Returns the current lantern brightness, from a sum of sine waves at different speeds.
    /// The result is smooth and deterministic, so the light breathes instead of strobing, and
    /// no random numbers are needed. When the killer is near it quickens, dims and cools off.
    /// </summary>
    private static double Flicker(bool danger)
    {
        var time = Raylib.GetTime();
        var speed = danger ? 2.1 : 1.0;
        var wobble =
            (Math.Sin(time * 2.3 * speed) * 0.5) +
            (Math.Sin(time * 13.7 * speed) * 0.2) +
            (Math.Sin(time * 0.7) * 0.3);
        var breath = Math.Sin(time * 0.6) * (danger ? 0.03 : 0.012);

        return 0.86 + (wobble * 0.09) + breath - (danger ? 0.03 : 0.0);
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

    /// <summary>Draws text with a little letter spacing, which reads as deliberate typography.</summary>
    private void Text(string text, float x, float y, int size, Color color, float spacing = TextSpacing) =>
        Raylib.DrawTextEx(_font, text, new Vector2(x, y), size, spacing, color);

    /// <summary>Measures text the same way <see cref="Text"/> draws it.</summary>
    private int TextWidth(string text, int size, float spacing = TextSpacing) =>
        (int)Raylib.MeasureTextEx(_font, text, size, spacing).X;
}

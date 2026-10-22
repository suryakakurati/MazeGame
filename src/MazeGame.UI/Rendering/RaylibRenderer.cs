using System;
using System.Numerics;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.UI.Rendering.Pixels;
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
/// The maze itself is drawn from the compiled atlases by <see cref="Pixels.PixelRenderer"/>; this
/// class paints the frame around it — the cave gradient, the vignette, the grain and the status
/// strips — and is the one place the interface's geometry lives. Its labels are set in the same
/// compiled 5x7 face as the cards and the title, so no screen falls back to a system typeface.
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
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private const int MoteCount = 70;
    private const int GrainCount = 110;

    /// <summary>How long the red flash lasts after a heart is lost, in seconds.</summary>
    private const double DamageFlashSeconds = 0.35;

    /// <summary>Left edge of the heart row in the footer, in pixels.</summary>
    private const float OriginHeartX = 26f;

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
        _explored.Clear();
        _heartsSeen = -1;
        _lastHitTime = double.NegativeInfinity;
    }

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        TrackDamage(state);

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

        // The two figures are drawn after the cells, from their interpolated positions, so they
        // slide between tiles instead of hopping. The rules still see one cell per actor.
        DrawFigures(state);

        DrawEnemyAura(state);
        DrawMotes(state, light);
        DrawAimCursor(state);

        // The vignette darkens the cave, not the interface: it goes on before the HUD so the
        // status pills and hints keep their contrast.
        DrawVignette(danger);
        DrawOverlay(state);
    }

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

        // The lantern falloff goes on before anything the player must be able to read. It used
        // to be painted last, which dimmed the keys to a few percent of their own glow at the
        // edge of the torch; that is a rule-legal cell the player simply could not see.
        DrawTorchFalloff(state, cell, x, y, light);
        DrawContents(state, cell, x, y);
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
    /// Draws the things that belong to a cell and do not move between steps: barriers, the arch,
    /// keys and the wisp.
    /// </summary>
    /// <remarks>
    /// The player and the killer are deliberately <b>not</b> here: they are drawn once, after the
    /// grid, from an interpolated position (see <see cref="DrawFigures"/>). Drawing them from their
    /// cell would snap them a whole tile at a time and make the timed steps look broken.
    /// </remarks>
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
            DrawKey(cell, centerX, centerY);
        }

        if (cell == state.Lure)
        {
            DrawLure(centerX, centerY);
        }
    }

    /// <summary>
    /// Draws the killer between its two cells and the explorer on its own cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The killer slides because it is a creature at a distance: a smooth drift reads as something
    /// stalking, and the player never has to count its tiles.
    /// </para>
    /// <para>
    /// The explorer **snaps**. Its cell changes the instant a step is accepted, so drawing it there
    /// makes one press one tile exactly — the player can count the grid while running, which is the
    /// whole navigation skill of the game. Drawing it between cells (which this used to do) turned
    /// a 0.11 s cadence into a continuous glide at exactly the moment precision matters. The step
    /// fraction is still published for a walk-cycle animation; it is not a position.
    /// </para>
    /// </remarks>
    private static void DrawFigures(GameState state)
    {
        foreach (var hunter in state.Enemies)
        {
            // A sleeper is not drawn until it wakes: the floor rule has to be legible from the
            // first frame, and an empty cell where a hunter should be is the tell.
            if (hunter.Dormant || !state.IsVisible(hunter.Cell))
            {
                continue;
            }

            var killer = FigureCenter(hunter.From, hunter.Cell, hunter.StepFraction);
            DrawKiller(killer.X, killer.Y);
        }

        var player = CellCenter(state.Player);
        DrawPlayer(player.X, player.Y);
    }

    /// <summary>Returns the pixel centre of <paramref name="cell"/>.</summary>
    private static (int X, int Y) CellCenter(Cell cell) =>
        (RaylibLayout.CellX(cell.Column) + (RaylibLayout.CellSize / 2),
         RaylibLayout.CellY(cell.Row) + (RaylibLayout.CellSize / 2));

    /// <summary>
    /// Returns the pixel centre of a figure on its way from <paramref name="from"/> to
    /// <paramref name="to"/>, rounded to whole pixels.
    /// </summary>
    private static (int X, int Y) FigureCenter(Cell from, Cell to, double fraction)
    {
        var fromX = RaylibLayout.CellX(from.Column) + (RaylibLayout.CellSize / 2.0);
        var fromY = RaylibLayout.CellY(from.Row) + (RaylibLayout.CellSize / 2.0);
        var toX = RaylibLayout.CellX(to.Column) + (RaylibLayout.CellSize / 2.0);
        var toY = RaylibLayout.CellY(to.Row) + (RaylibLayout.CellSize / 2.0);

        var eased = Math.Clamp(fraction, 0, 1);

        return (
            (int)Math.Round(fromX + ((toX - fromX) * eased)),
            (int)Math.Round(fromY + ((toY - fromY) * eased)));
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
        // Counter-clockwise, as raylib culls the other order (see DrawHeart).
        Raylib.DrawTriangle(hoodLeftRim, hoodRim, hoodRightRim, Rgba(PlayerRim.R, PlayerRim.G, PlayerRim.B, 55));

        var hoodTip = new Vector2(centerX, centerY - 15);
        var hoodLeft = new Vector2(centerX - 8, centerY - 2);
        var hoodRight = new Vector2(centerX + 8, centerY - 2);
        Raylib.DrawTriangle(hoodLeft, hoodTip, hoodRight, PlayerCloak);

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
    /// Draws a key as a beacon rather than as scenery: a magenta pool on the floor, a shaft of
    /// light rising out of the cell, a pulsing ring, and the glowing fungus itself bobbing in the
    /// middle. Pink, so it is never confused with the cyan exit.
    /// </summary>
    /// <remarks>
    /// A collectible has to be findable at the edge of the lantern, where the torch falloff is at
    /// its strongest, so the beacon deliberately spills past the cell edge and the bright core is
    /// drawn last. The bob and the orbit are functions of time only, so nothing is stored per key.
    /// </remarks>
    /// <param name="cell">The key's cell, used to desynchronise neighbouring keys' animation.</param>
    /// <param name="centerX">Pixel x of the cell centre.</param>
    /// <param name="centerY">Pixel y of the cell centre.</param>
    private static void DrawKey(Cell cell, int centerX, int centerY)
    {
        var time = Raylib.GetTime();
        var phase = (cell.Row * 1.7) + (cell.Column * 0.9);
        var pulse = 0.75 + (Math.Sin((time * 3.2) + phase) * 0.25);
        var bob = (float)(Math.Sin((time * 2.1) + (cell.Column * 1.3)) * 1.8);

        // The pool of light on the floor: what makes the cell itself read as occupied.
        Raylib.DrawCircleGradient(
            new Vector2(centerX, centerY),
            32,
            Rgba(GlowPink.R, GlowPink.G, GlowPink.B, (byte)(44 + (pulse * 30))),
            Transparent);

        // The shaft: six stacked bands fading upwards, so a key is visible over a wall edge and
        // across a dark cell rather than being a dark blob on dark floor.
        for (var step = 0; step < 6; step++)
        {
            var width = 12 - step;
            var alpha = (byte)((18 + (pulse * 20)) * (1.0 - (step / 6.0)));

            Raylib.DrawRectangle(
                centerX - (width / 2),
                centerY - 6 - (step * 5),
                width,
                6,
                Rgba(255, 168, 208, alpha));
        }

        // A ring that breathes: reads as "pick me up", not as another mushroom.
        Raylib.DrawCircleLines(
            centerX,
            centerY,
            12 + (float)(pulse * 3),
            Rgba(255, 150, 200, (byte)(80 + (pulse * 80))));

        var capY = centerY + bob;

        Raylib.DrawCircleGradient(
            new Vector2(centerX, capY - 2),
            16,
            Rgba(255, 130, 190, 110),
            Transparent);
        Raylib.DrawLineEx(
            new Vector2(centerX, capY + 9),
            new Vector2(centerX, capY + 1),
            2.4f,
            Rgba(58, 118, 78, 240));
        Raylib.DrawCircleV(new Vector2(centerX - 2, capY), 5.0f, GlowPink);
        Raylib.DrawCircleV(new Vector2(centerX + 4, capY + 3), 3.2f, Rgba(214, 84, 150, 255));
        Raylib.DrawCircleV(new Vector2(centerX - 3, capY - 2), 1.6f, Rgba(255, 236, 248, 255));

        // Two sparks orbiting the cap, the detail that separates a key from a glowing pebble.
        for (var spark = 0; spark < 2; spark++)
        {
            var angle = (time * (1.6 + (spark * 0.4))) + (spark * Math.PI);
            var sparkX = centerX + (float)(Math.Cos(angle) * (9 + spark));
            var sparkY = capY - 3 + (float)(Math.Sin(angle) * 5);

            Raylib.DrawCircleV(new Vector2(sparkX, sparkY), 1.2f, Rgba(255, 210, 230, 225));
        }
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
                new Vector2(tipX, tipY),
                new Vector2(baseX - wingX, baseY - wingY),
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

        // The lantern is the explorer's light, so it snaps with the body: a glow that lags behind
        // the figure makes the lit area lag behind the cell the rules are actually using.
        var lantern = CellCenter(state.Player);
        var centerX = (double)lantern.X;
        var centerY = (double)lantern.Y;

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

        var text = state.AimMode switch
        {
            PlacementMode.Barrier => "WASD: AIM    SPACE / ENTER: SEAL    ESC: CANCEL",
            PlacementMode.Lure => "WASD: AIM    SPACE / ENTER: LIGHT    ESC: CANCEL",

            // Inside a safe room the footer says what the room is: the pause is the one rule the
            // player has to be told about, because nothing moving is exactly when it is unclear.
            _ => state.IsInSafeRoom
                ? "SAFE ROOM   E: LOOK   WASD: WALK   ESC: QUIT"
                : "WASD: MOVE   SHIFT: RUN   SPACE: BLINK   V: VEIL   T: SLOW   L: LIGHT   F: SHOVE   ESC: QUIT",
        };
        var color = state.AimMode switch
        {
            PlacementMode.Barrier => CursorColor,
            PlacementMode.Lure => GlowCyan,
            _ => state.IsInSafeRoom ? GlowGreen : HintText,
        };

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

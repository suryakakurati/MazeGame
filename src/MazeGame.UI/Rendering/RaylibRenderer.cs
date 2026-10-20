using System.Numerics;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using Raylib_cs;

namespace MazeGame.UI.Rendering;

/// <summary>
/// Draws the game in a Raylib window: the maze, its contents, fog of war, torch falloff and a
/// flickering light.
/// </summary>
/// <remarks>
/// <para>
/// The renderer owns <b>presentation only</b>. What the player is allowed to see is a game
/// rule (<see cref="GameState.IsVisible"/>); this class decides how to darken everything else.
/// It keeps one piece of its own state — the cells the player has already seen — so explored
/// corridors stay faintly visible instead of blinking in and out as the torch moves.
/// </para>
/// <para>
/// Frames must be bracketed by <c>Raylib.BeginDrawing</c> / <c>Raylib.EndDrawing</c>, which the
/// frontend does: <see cref="Draw"/> fills in one frame and never calls those itself.
/// </para>
/// </remarks>
public sealed class RaylibRenderer : IRenderer
{
    private static readonly Color FloorVisible = new(20, 22, 27, 255);
    private static readonly Color FloorRemembered = new(11, 12, 15, 255);
    private static readonly Color WallVisible = new(84, 92, 106, 255);
    private static readonly Color WallRemembered = new(38, 42, 49, 255);
    private static readonly Color PlayerLight = new(255, 214, 120, 255);
    private static readonly Color EnemyColor = new(206, 66, 82, 255);
    private static readonly Color KeyColor = new(247, 214, 92, 255);
    private static readonly Color ExitLockedColor = new(120, 126, 138, 255);
    private static readonly Color ExitOpenColor = new(70, 214, 190, 255);
    private static readonly Color BarrierColor = new(128, 138, 152, 255);
    private static readonly Color CursorColor = new(170, 210, 255, 255);
    private static readonly Color HudColor = new(206, 214, 226, 255);
    private static readonly Color HintColor = new(110, 118, 132, 255);
    private static readonly Color DangerColor = new(226, 96, 110, 255);

    private const int HudFontSize = 20;
    private const int HintFontSize = 16;
    private const int BannerFontSize = 34;

    private readonly HashSet<Cell> _explored = [];

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var light = Flicker();
        Raylib.ClearBackground(Color.Black);

        for (var row = 0; row < state.Maze.Rows; row++)
        {
            for (var column = 0; column < state.Maze.Columns; column++)
            {
                DrawCell(state, state.Maze.GetCell(row, column), light);
            }
        }

        DrawAimCursor(state);
        DrawHud(state);
        DrawHints(state);
        DrawEndBanner(state);
    }

    /// <summary>
    /// Draws one cell, skipping cells the player has never seen and dimming ones that are only
    /// remembered.
    /// </summary>
    private void DrawCell(GameState state, Cell cell, double light)
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

        Raylib.DrawRectangle(x, y, RaylibLayout.CellSize, RaylibLayout.CellSize,
            visible ? FloorVisible : FloorRemembered);
        DrawWalls(cell, visible, x, y);

        if (!visible)
        {
            return;
        }

        DrawContents(state, cell, x, y);
        DrawTorchFalloff(state, cell, x, y, light);
    }

    /// <summary>
    /// Draws every standing wall of a cell as a thick edge. Every side is drawn so the boundary
    /// of the light still shows a wall; overlapping edges simply draw twice.
    /// </summary>
    private static void DrawWalls(Cell cell, bool visible, int x, int y)
    {
        var color = visible ? WallVisible : WallRemembered;
        var thickness = RaylibLayout.WallThickness;
        var size = RaylibLayout.CellSize;

        if (cell.HasWall(Direction.North))
        {
            Raylib.DrawRectangle(x, y, size, thickness, color);
        }

        if (cell.HasWall(Direction.South))
        {
            Raylib.DrawRectangle(x, y + size - thickness, size, thickness, color);
        }

        if (cell.HasWall(Direction.West))
        {
            Raylib.DrawRectangle(x, y, thickness, size, color);
        }

        if (cell.HasWall(Direction.East))
        {
            Raylib.DrawRectangle(x + size - thickness, y, thickness, size, color);
        }
    }

    /// <summary>
    /// Draws the things standing on a visible cell: the exit, keys, barriers, the killer and the
    /// player (drawn last so it is never hidden).
    /// </summary>
    private static void DrawContents(GameState state, Cell cell, int x, int y)
    {
        var centerX = x + (RaylibLayout.CellSize / 2);
        var centerY = y + (RaylibLayout.CellSize / 2);

        if (state.Barriers.Contains(cell))
        {
            Raylib.DrawRectangle(x + 7, y + 7, RaylibLayout.CellSize - 14, RaylibLayout.CellSize - 14, BarrierColor);
            Raylib.DrawLine(x + 7, y + 7, x + RaylibLayout.CellSize - 7, y + RaylibLayout.CellSize - 7, FloorRemembered);
        }

        if (cell == state.Exit)
        {
            var color = state.IsExitUnlocked ? ExitOpenColor : ExitLockedColor;
            Raylib.DrawRectangleLinesEx(
                new Rectangle(x + 10, y + 10, RaylibLayout.CellSize - 20, RaylibLayout.CellSize - 20), 3, color);
        }

        if (state.Keys.Contains(cell))
        {
            Raylib.DrawCircle(centerX, centerY, 5, KeyColor);
            Raylib.DrawCircleLines(centerX, centerY, 9, KeyColor);
        }

        if (cell == state.Enemy)
        {
            Raylib.DrawCircleGradient(new Vector2(centerX, centerY), 16, Rgba(90, 20, 28, 255), Color.Black);
            Raylib.DrawCircle(centerX, centerY, 9, EnemyColor);
        }

        if (cell == state.Player)
        {
            Raylib.DrawCircleGradient(new Vector2(centerX, centerY), 20, Rgba(120, 92, 30, 255), Color.Black);
            Raylib.DrawCircle(centerX, centerY, 10, PlayerLight);
        }
    }

    /// <summary>
    /// Darkens a visible cell by its distance from the player, which is the torch's falloff and
    /// the vignette in one. The alpha is capped so distant cells are dimmed, not erased.
    /// </summary>
    private static void DrawTorchFalloff(GameState state, Cell cell, int x, int y, double light)
    {
        var rowDistance = cell.Row - state.Player.Row;
        var columnDistance = cell.Column - state.Player.Column;
        var distance = Math.Sqrt((rowDistance * rowDistance) + (columnDistance * columnDistance));

        var darkness = Math.Min(distance / (state.Config.VisionRadius + 1.0), 1.0);

        // A dimmer torch (flicker) pushes even nearby cells further into the dark.
        var alpha = (darkness * 0.85) + ((1.0 - light) * 0.4);
        var shaded = (byte)Math.Clamp((int)Math.Round(alpha * 255), 0, 175);

        Raylib.DrawRectangle(x, y, RaylibLayout.CellSize, RaylibLayout.CellSize, Rgba(0, 0, 0, shaded));
    }

    /// <summary>
    /// Draws the aiming reticle. It is drawn over the fog on purpose: the player may aim
    /// anywhere, including at cells they cannot see.
    /// </summary>
    private static void DrawAimCursor(GameState state)
    {
        if (!state.IsPlacingBarrier)
        {
            return;
        }

        var x = RaylibLayout.CellX(state.BarrierCursor.Column);
        var y = RaylibLayout.CellY(state.BarrierCursor.Row);
        var size = RaylibLayout.CellSize;
        var margin = 3;

        Raylib.DrawRectangleLinesEx(new Rectangle(x + margin, y + margin, size - (2 * margin), size - (2 * margin)), 2, CursorColor);
        Raylib.DrawLine(x + (size / 2), y + 10, x + (size / 2), y + size - 10, CursorColor);
        Raylib.DrawLine(x + 10, y + (size / 2), x + size - 10, y + (size / 2), CursorColor);
    }

    /// <summary>
    /// Draws the status strip: collected keys, remaining barriers and whether the exit is open.
    /// </summary>
    private static void DrawHud(GameState state)
    {
        var y = 18;

        Raylib.DrawText($"KEYS {state.KeysCollected}/{state.Config.KeyCount}", RaylibLayout.OriginX, y, HudFontSize, HudColor);
        Raylib.DrawText($"BARRIERS {state.BarriersRemaining}", RaylibLayout.OriginX + 170, y, HudFontSize, HudColor);
        Raylib.DrawText(
            state.IsExitUnlocked ? "EXIT OPEN" : "EXIT LOCKED",
            RaylibLayout.OriginX + 370,
            y,
            HudFontSize,
            state.IsExitUnlocked ? ExitOpenColor : ExitLockedColor);
    }

    /// <summary>
    /// Draws the control hints under the maze, switching to the aiming controls while the player
    /// is placing a barrier.
    /// </summary>
    private static void DrawHints(GameState state)
    {
        var y = Raylib.GetScreenHeight() - RaylibLayout.FooterHeight + 10;

        if (state.IsPlacingBarrier)
        {
            Raylib.DrawText(
                "AIMING  -  WASD/arrows: move cursor    B/Space/Enter: place    Esc: cancel",
                RaylibLayout.OriginX,
                y,
                HintFontSize,
                CursorColor);
            return;
        }

        Raylib.DrawText(
            "WASD/arrows: move    B or Space: aim a barrier    Q or Esc: quit",
            RaylibLayout.OriginX,
            y,
            HintFontSize,
            HintColor);
    }

    /// <summary>
    /// Covers the maze with the ending once the game is over, so the last frame still explains
    /// itself instead of leaving the window frozen mid-chase.
    /// </summary>
    private static void DrawEndBanner(GameState state)
    {
        if (state.Status == GameStatus.InProgress)
        {
            return;
        }

        var mazeWidth = state.Maze.Columns * RaylibLayout.CellSize;
        var mazeHeight = state.Maze.Rows * RaylibLayout.CellSize;
        var centerX = RaylibLayout.OriginX + (mazeWidth / 2);
        var centerY = RaylibLayout.OriginY + (mazeHeight / 2);

        Raylib.DrawRectangle(RaylibLayout.OriginX, RaylibLayout.OriginY, mazeWidth, mazeHeight, Rgba(0, 0, 0, 210));

        var headline = state.Status == GameStatus.Won ? "YOU ESCAPED" : "THE KILLER FOUND YOU";
        var color = state.Status == GameStatus.Won ? ExitOpenColor : DangerColor;
        DrawCenteredText(headline, centerX, centerY - 30, BannerFontSize, color);
        DrawCenteredText("press Q or Esc to close", centerX, centerY + 20, HintFontSize, HintColor);
    }

    /// <summary>Draws text horizontally centred on <paramref name="centerX"/>.</summary>
    private static void DrawCenteredText(string text, int centerX, int y, int fontSize, Color color) =>
        Raylib.DrawText(text, centerX - (Raylib.MeasureText(text, fontSize) / 2), y, fontSize, color);

    /// <summary>
    /// Builds an opaque colour from 0-255 channels. The cast keeps the call on the byte
    /// constructor, because <c>Color</c> also has int and float constructors and a mixed call
    /// would be ambiguous.
    /// </summary>
    private static Color Rgba(byte red, byte green, byte blue, byte alpha) =>
        new(red, green, blue, alpha);

    /// <summary>
    /// Returns the current torch brightness between roughly 0.75 and 0.95, from a sum of sine
    /// waves at different speeds. The result is smooth and deterministic, so the light breathes
    /// instead of strobing, and no random numbers are needed.
    /// </summary>
    private static double Flicker()
    {
        var time = Raylib.GetTime();
        var wobble = (Math.Sin(time * 2.3) * 0.5) + (Math.Sin(time * 13.7) * 0.2) + (Math.Sin(time * 0.7) * 0.3);

        return 0.86 + (wobble * 0.09);
    }
}

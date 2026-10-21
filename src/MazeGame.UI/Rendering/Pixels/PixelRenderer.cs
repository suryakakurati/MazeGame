using System;
using System.Collections.Generic;
using System.Numerics;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using Raylib_cs;

namespace MazeGame.UI.Rendering.Pixels;

/// <summary>
/// Draws one floor as pixel art: committed atlas tiles, autotiled wall faces, the cast, a per-cell
/// lightmap, and the existing HUD chrome around the canvas.
/// </summary>
/// <remarks>
/// <para>
/// The maze is a floor tile per cell with thin wall faces along its closed edges — the shape this
/// maze actually has, since walls live <i>between</i> cells. The compiler's solid wall and autotile
/// set is left for the floors that grow chasm and veto cells, where a cell is wall.
/// </para>
/// <para>
/// Nothing here reads a colour from the rules: the palette lives in the PNG, and this class only
/// decides <i>which</i> tile and what to multiply it by. Fog and the lantern falloff both go through
/// one small light texture, so the scene is darkened in a single multiply pass instead of a
/// gradient call per cell.
/// </para>
/// <para>
/// The figures snap to their cells, like the vector renderer: the sprite is chosen by the step's
/// fraction (a walk cycle), never drawn between two tiles.
/// </para>
/// </remarks>
public sealed class PixelRenderer : IRenderer, IDisposable
{
    /// <summary>Screen pixels kept free around the canvas, so it does not touch the HUD strips.</summary>
    private const int Margin = 12;

    /// <summary>About one cell in this many gets a prop.</summary>
    private const int PropSpacing = 7;

    private static readonly Color CanvasColour = new(3, 9, 11, 255);

    private static readonly Color Remembered = new(104, 118, 122, 255);

    private static readonly Color Cursor = new(210, 255, 225, 255);

    private readonly PixelView _view;

    private readonly RaylibRenderer _chrome = new();

    private readonly IReadOnlyDictionary<string, ThemeAtlas> _manifest;

    private readonly string _directory;

    private readonly Dictionary<string, TileAtlas> _atlases = new(StringComparer.Ordinal);

    private readonly HashSet<Cell> _explored = [];

    private readonly int _columns;

    private readonly int _rows;

    private Image _lightImage;

    private Texture2D _light;

    private int _lightStamp = int.MinValue;

    private string _themeId;

    private Direction _playerFacing = Direction.South;

    private Direction _enemyFacing = Direction.South;

    private bool _disposed;

    /// <summary>Creates the renderer and loads the manifest.</summary>
    /// <param name="config">The floor's settings, for the canvas size.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the atlases are missing.</exception>
    public PixelRenderer(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _columns = config.Columns;
        _rows = config.Rows;
        _manifest = AtlasManifest.LoadShipped();
        _directory = AssetPaths.GeneratedDirectory();
        _view = new PixelView(PixelLayout.CanvasWidth(config), PixelLayout.CanvasHeight(config));

        _themeId = FirstTheme();
        Font = new PixelFont(_manifest[PixelFont.AtlasId], _directory);

        _lightImage = Raylib.GenImageColor(_columns, _rows, Color.Black);
        _light = Raylib.LoadTextureFromImage(_lightImage);
        Raylib.SetTextureFilter(_light, TextureFilter.Bilinear);
    }

    /// <summary>Gets the theme being drawn.</summary>
    public string ThemeId => _themeId;

    /// <summary>Gets the compiled font, shared with the campaign cards.</summary>
    public PixelFont Font { get; }

    /// <summary>Gets the canvas width in atlas pixels.</summary>
    public int CanvasWidth => _view.Width;

    /// <summary>Gets the canvas height in atlas pixels.</summary>
    public int CanvasHeight => _view.Height;

    /// <summary>
    /// Chooses the theme whose atlas the next frames are drawn from.
    /// </summary>
    /// <param name="themeId">The floor's theme id, for example <c>shallows</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no committed atlas has that id. A floor with no art must fail loudly, not fall
    /// back to another floor's palette.
    /// </exception>
    public void SetTheme(string themeId)
    {
        ArgumentNullException.ThrowIfNull(themeId);

        if (string.Equals(themeId, _themeId, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(themeId, PixelFont.AtlasId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException("'font' is the text sheet, not a floor theme.");
        }

        if (!_manifest.ContainsKey(themeId))
        {
            throw new KeyNotFoundException(
                $"No committed atlas for theme '{themeId}'. Known themes: {string.Join(", ", _manifest.Keys)}.");
        }

        _themeId = themeId;
        _lightStamp = int.MinValue;
    }

    /// <summary>Forgets which cells have been explored, for the start of a new floor.</summary>
    public void Reset()
    {
        _explored.Clear();
        _lightStamp = int.MinValue;
        _playerFacing = Direction.South;
        _enemyFacing = Direction.South;
        _chrome.Reset();
    }

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var atlas = Atlas;
        TrackFacing(state);

        _view.Begin(CanvasColour);
        DrawCells(state, atlas);
        DrawProps(state, atlas);
        DrawEntities(state, atlas);
        DrawLight(state);
        PixelView.End();

        // The chrome is the existing HUD: pulled, glass status strip and hints. It draws in window
        // pixels, so it is applied after the canvas is scaled up, in the letterbox around it.
        var fit = _view.Present(RaylibLayout.HudHeight, RaylibLayout.FooterHeight, Margin);
        DrawAimCursor(state, fit);
        _chrome.DrawOverlay(state);

        // A lost run ends on a card, like the campaign screens do, so the last thing the player
        // sees is in the same type as everything else.
        if (state.Status == GameStatus.Caught)
        {
            PixelCards.Caught(Font);
        }
    }

    /// <summary>Releases the canvas, the light texture and every loaded atlas.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var atlas in _atlases.Values)
        {
            atlas.Dispose();
        }

        _atlases.Clear();
        Font.Dispose();

        if (_light.Id != 0)
        {
            Raylib.UnloadTexture(_light);
        }

        if (_lightImage.Width > 0)
        {
            Raylib.UnloadImage(_lightImage);
        }

        _view.Dispose();
    }

    /// <summary>Gets the atlas for the current theme, loading it the first time it is needed.</summary>
    private TileAtlas Atlas
    {
        get
        {
            if (!_atlases.TryGetValue(_themeId, out var atlas))
            {
                atlas = new TileAtlas(_manifest[_themeId], _directory);
                _atlases[_themeId] = atlas;
            }

            return atlas;
        }
    }

    /// <summary>Returns the first committed theme, used before a floor picks one.</summary>
    private string FirstTheme()
    {
        foreach (var id in _manifest.Keys)
        {
            // The font sheet is an atlas too, but it is not a place to draw a floor.
            if (!string.Equals(id, PixelFont.AtlasId, StringComparison.Ordinal))
            {
                return id;
            }
        }

        throw new InvalidOperationException("The committed manifest lists no themes.");
    }

    /// <summary>Draws every cell the player has seen: floor, then the wall faces on its edges.</summary>
    private void DrawCells(GameState state, TileAtlas atlas)
    {
        var variants = atlas.FloorVariantCount;

        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);
                var visible = state.IsVisible(cell);

                if (visible)
                {
                    _explored.Add(cell);
                }

                if (!visible && !_explored.Contains(cell))
                {
                    continue;
                }

                var tint = visible ? Color.White : Remembered;
                var x = PixelLayout.CellX(column);
                var y = PixelLayout.CellY(row);

                atlas.Draw($"floor-{Variant(row, column, variants)}", x, y, tint);
                DrawWalls(state, atlas, cell, x, y, tint);
            }
        }
    }

    /// <summary>
    /// Draws the wall faces that belong to this cell: the north and west ones always, and the south
    /// and east ones only on the floor's edge.
    /// </summary>
    /// <remarks>
    /// A wall between two cells is both the eastern cell's west face and the western cell's east
    /// face. Drawing only the north and west faces — plus the outer rim on the last row and column —
    /// paints each wall exactly once, so two neighbours cannot draw it twice at two positions.
    /// </remarks>
    private void DrawWalls(GameState state, TileAtlas atlas, Cell cell, int x, int y, Color tint)
    {
        DrawFace(atlas, state, cell, Direction.North, x, y, tint);
        DrawFace(atlas, state, cell, Direction.West, x, y, tint);

        if (cell.Row == _rows - 1)
        {
            DrawFace(atlas, state, cell, Direction.South, x, y, tint);
        }

        if (cell.Column == _columns - 1)
        {
            DrawFace(atlas, state, cell, Direction.East, x, y, tint);
        }
    }

    /// <summary>Draws one side's wall face, when that side is a wall.</summary>
    private static void DrawFace(
        TileAtlas atlas,
        GameState state,
        Cell cell,
        Direction direction,
        int x,
        int y,
        Color tint)
    {
        if (TileMask.WallFace(state.Maze, cell, direction) is { } tile)
        {
            atlas.Draw(tile, x, y, tint);
        }
    }

    /// <summary>
    /// Scatters the theme's props on the floor: one in every seven seen cells, chosen by position.
    /// </summary>
    /// <remarks>
    /// A prop is scenery, so it is skipped on every cell that holds something the player must see —
    /// a figure, a key, the arch, a barrier, the wisp — and it is drawn before the cast for the same
    /// reason. Position decides which cells get one, so a floor looks the same on every frame and a
    /// replay of the same seed is the same cave.
    /// </remarks>
    private void DrawProps(GameState state, TileAtlas atlas)
    {
        var props = atlas.PropCount;

        if (props == 0)
        {
            return;
        }

        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);

                if (!Known(state, cell) || IsBusy(state, cell))
                {
                    continue;
                }

                var hash = Hash(row, column);

                if (hash % PropSpacing != 0)
                {
                    continue;
                }

                atlas.Draw(
                    $"prop-{(hash / PropSpacing) % props}",
                    PixelLayout.CellX(column),
                    PixelLayout.CellY(row),
                    state.IsVisible(cell) ? Color.White : Remembered);
            }
        }
    }

    /// <summary>Returns whether a cell holds something that must not be covered by scenery.</summary>
    private static bool IsBusy(GameState state, Cell cell) =>
        cell == state.Player ||
        cell == state.Enemy ||
        cell == state.Exit ||
        cell == state.Lure ||
        state.Keys.Contains(cell) ||
        state.Barriers.Contains(cell);

    /// <summary>Returns a stable, positive hash of a cell, for variant and prop choices.</summary>
    private static int Hash(int row, int column) =>
        unchecked(((row * 73856093) ^ (column * 19349663)) & 0x7FFFFFFF);

    /// <summary>Draws the keys, the arch, the wisp, the barriers and the two figures.</summary>
    private void DrawEntities(GameState state, TileAtlas atlas)
    {
        var time = Raylib.GetTime();
        var frame = (int)(time * 3.0) % 3;

        foreach (var key in state.Keys)
        {
            if (!Known(state, key))
            {
                continue;
            }

            atlas.Draw($"key-{(frame + (key.Column + key.Row)) % 3}", PixelLayout.CellX(key.Column), PixelLayout.CellY(key.Row));
        }

        foreach (var barrier in state.Barriers)
        {
            if (!Known(state, barrier))
            {
                continue;
            }

            var sealedCell = state.BarrierSeals.ContainsKey(barrier);
            atlas.Draw(sealedCell ? "barrier" : "barrier-cracked", PixelLayout.CellX(barrier.Column), PixelLayout.CellY(barrier.Row));
        }

        if (state.Lure is { } lure && Known(state, lure))
        {
            atlas.Draw($"wisp-{frame}", PixelLayout.CellX(lure.Column), PixelLayout.CellY(lure.Row));
        }

        if (state.Exit is var exit && Known(state, exit))
        {
            atlas.Draw(
                state.IsExitUnlocked ? "arch-open" : "arch-locked",
                PixelLayout.CellX(exit.Column),
                PixelLayout.CellY(exit.Row));
        }

        atlas.Draw(
            $"killer-{NameOf(_enemyFacing)}-{EnemyFrame(state)}",
            PixelLayout.CellX(state.Enemy.Column),
            PixelLayout.CellY(state.Enemy.Row));

        atlas.Draw(
            $"player-{NameOf(_playerFacing)}-{PlayerFrame(state)}",
            PixelLayout.CellX(state.Player.Column),
            PixelLayout.CellY(state.Player.Row));
    }

    /// <summary>Returns the walk-cycle frame the explorer is on.</summary>
    private static string PlayerFrame(GameState state)
    {
        if (!state.IsPlayerStepping)
        {
            return "idle";
        }

        return Math.Clamp((int)(state.PlayerStepFraction * 3.0), 0, 2) switch
        {
            0 => "walk0",
            1 => "walk1",
            _ => "walk2",
        };
    }

    /// <summary>Returns the frame the killer is on: its gait only moves while it is stepping.</summary>
    private static string EnemyFrame(GameState state)
    {
        if (state.PreviousEnemy == state.Enemy)
        {
            return "idle";
        }

        return state.EnemyStepFraction < 0.5 ? "walk0" : "walk1";
    }

    /// <summary>Returns the name a facing is spelled with inside a tile name.</summary>
    private static string NameOf(Direction direction) => direction switch
    {
        Direction.North => "north",
        Direction.East => "east",
        Direction.South => "south",
        _ => "west",
    };

    /// <summary>Returns whether a cell has been seen, so an entity there may be drawn.</summary>
    private bool Known(GameState state, Cell cell) =>
        state.IsVisible(cell) || _explored.Contains(cell);

    /// <summary>
    /// Multiplies the scene by one texel per cell: lantern falloff, remembered cells, and the
    /// killer's cold light.
    /// </summary>
    private void DrawLight(GameState state)
    {
        var stamp = LightStamp(state);

        if (stamp != _lightStamp)
        {
            _lightStamp = stamp;
            UploadLight(state);
        }

        // The flicker rides on the draw's tint instead of being baked into the texture, so the
        // texture is only rebuilt when what is lit actually changes rather than ten times a second.
        var flicker = Flicker();
        var tint = new Color(flicker, flicker, flicker, (byte)255);

        Raylib.BeginBlendMode(BlendMode.Multiplied);
        Raylib.DrawTexturePro(
            _light,
            new Rectangle(0, 0, _columns, _rows),
            new Rectangle(
                PixelLayout.Pad,
                PixelLayout.HeaderHeight + PixelLayout.Pad,
                _columns * PixelLayout.CellSize,
                _rows * PixelLayout.CellSize),
            Vector2.Zero,
            0f,
            tint);
        Raylib.EndBlendMode();
    }

    /// <summary>
    /// Returns a value that changes exactly when the light would: the two figures' cells, how much
    /// has been seen, and a tenth-of-a-second bucket for the lantern's flicker.
    /// </summary>
    private int LightStamp(GameState state)
    {
        return HashCode.Combine(
            state.Player.Row,
            state.Player.Column,
            state.Enemy.Row,
            state.Enemy.Column,
            _explored.Count,
            state.EnemyGlow.Count,
            state.EffectiveVisionRadius);
    }

    /// <summary>Returns the lantern's flicker as a multiplier, from 0.88 to 1.0.</summary>
    private static byte Flicker()
    {
        var breath = 0.88 + (0.12 * (0.5 + (0.5 * Math.Sin(Raylib.GetTime() * 6.3))));
        return (byte)Math.Clamp((int)Math.Round(breath * 255.0), 0, 255);
    }

    /// <summary>Rebuilds the light texture from the floor's current visibility.</summary>
    private void UploadLight(GameState state)
    {
        var radius = Math.Max(1, state.EffectiveVisionRadius);
        var player = state.Player;

        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);
                double level;

                if (state.IsVisible(cell))
                {
                    var distance = Math.Max(Math.Abs(cell.Row - player.Row), Math.Abs(cell.Column - player.Column));
                    var falloff = 1.0 - (distance / (radius + 1.0));
                    level = 0.62 + (Math.Clamp(falloff, 0.0, 1.0) * 0.38);
                }
                else if (_explored.Contains(cell))
                {
                    level = 0.40;
                }
                else
                {
                    level = 0.0;
                }

                var brightness = (byte)Math.Clamp(level * 255.0, 0, 255);
                var colour = state.EnemyGlow.Count > 0 && state.EnemyGlow.Contains(cell)
                    ? new Color((byte)Math.Clamp(brightness * 0.80, 0, 255), (byte)Math.Clamp(brightness * 0.94, 0, 255), brightness, (byte)255)
                    : new Color(brightness, brightness, brightness, (byte)255);

                Raylib.ImageDrawPixel(ref _lightImage, column, row, colour);
            }
        }

        Raylib.UnloadTexture(_light);
        _light = Raylib.LoadTextureFromImage(_lightImage);
        Raylib.SetTextureFilter(_light, TextureFilter.Bilinear);
    }

    /// <summary>
    /// Draws a bracketed reticle on the aimed cell while the player is placing something.
    /// </summary>
    /// <remarks>
    /// Chrome rather than world: it is drawn after the canvas has been scaled, in window pixels, so
    /// the fog cannot dim the one thing the player needs to see while placing a barrier. The arms
    /// are whole screen pixels wide at any scale, so the recticle stays crisp at 2x and 3x.
    /// </remarks>
    private static void DrawAimCursor(GameState state, PixelLayout.PixelFit fit)
    {
        if (!state.IsAiming)
        {
            return;
        }

        var size = PixelLayout.CellSize * fit.Scale;
        var x = fit.X + (PixelLayout.CellX(state.AimCursor.Column) * fit.Scale);
        var y = fit.Y + (PixelLayout.CellY(state.AimCursor.Row) * fit.Scale);

        var arm = Math.Max(2, size / 4);
        var thickness = Math.Max(1, fit.Scale);
        var inset = Math.Max(1, fit.Scale / 2);

        // Four corner brackets: two rectangles each, so a 1 px arm at 1x and a 2 px arm at 2x.
        Raylib.DrawRectangle(x + inset, y + inset, arm, thickness, Cursor);
        Raylib.DrawRectangle(x + inset, y + inset, thickness, arm, Cursor);
        Raylib.DrawRectangle(x + size - inset - arm, y + inset, arm, thickness, Cursor);
        Raylib.DrawRectangle(x + size - inset - thickness, y + inset, thickness, arm, Cursor);
        Raylib.DrawRectangle(x + inset, y + size - inset - thickness, arm, thickness, Cursor);
        Raylib.DrawRectangle(x + inset, y + size - inset - arm, thickness, arm, Cursor);
        Raylib.DrawRectangle(x + size - inset - arm, y + size - inset - thickness, arm, thickness, Cursor);
        Raylib.DrawRectangle(x + size - inset - thickness, y + size - inset - arm, thickness, arm, Cursor);
    }

    /// <summary>
    /// Chooses which of a theme's floor variants a cell uses, by position, so the same floor always
    /// looks the same and no two neighbours share a variant by accident.
    /// </summary>
    private static int Variant(int row, int column, int variants) => Hash(row, column) % variants;

    /// <summary>Updates the direction each figure is facing from the cell it stepped from.</summary>
    private void TrackFacing(GameState state)
    {
        _playerFacing = Facing(state.PreviousPlayer, state.Player, _playerFacing);
        _enemyFacing = Facing(state.PreviousEnemy, state.Enemy, _enemyFacing);
    }

    /// <summary>Returns the direction from one cell to another, or <paramref name="fallback"/>.</summary>
    private static Direction Facing(Cell from, Cell to, Direction fallback)
    {
        if (to.Row < from.Row)
        {
            return Direction.North;
        }

        if (to.Row > from.Row)
        {
            return Direction.South;
        }

        if (to.Column > from.Column)
        {
            return Direction.East;
        }

        if (to.Column < from.Column)
        {
            return Direction.West;
        }

        return fallback;
    }
}

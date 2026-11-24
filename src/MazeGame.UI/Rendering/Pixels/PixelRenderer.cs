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
/// The rules move a figure a whole tile at the start of its step; the drawing glides it across
/// that tile over the step's length, and the camera follows the drawn explorer by the pixel, so
/// walking reads as continuous motion while the game itself stays on the grid. The sprite frame
/// still comes from the step's fraction (a walk cycle).
/// </para>
/// </remarks>
public sealed class PixelRenderer : IRenderer, IDisposable
{
    /// <summary>Screen pixels kept free around the canvas, so it does not touch the HUD strips.</summary>
    private const int Margin = 12;

    /// <summary>About one cell in this many gets a prop.</summary>
    private const int PropSpacing = 7;

    /// <summary>About one cell in this many gets the floor's signature object.</summary>
    private const int FeatureSpacing = 29;

    /// <summary>About one cell in this many gets the floor's carved emblem.</summary>
    private const int EmblemSpacing = 53;

    /// <summary>How many particles each floor's weather draws.</summary>
    private const int AmbientCount = 30;

    /// <summary>
    /// How many cells behind the player stay drawn after the lantern has moved on.
    /// </summary>
    /// <remarks>
    /// Memory used to be permanent for the whole floor, which quietly turned the fog into a map:
    /// by the middle of a run the screen was a drawing of everywhere the player had been, and the
    /// dark stopped being dark. Three cells is the room the player just left — enough to orient
    /// by, not enough to navigate a floor blind.
    /// </remarks>
    private const int MemoryRadius = 3;

    private static readonly Color CanvasColour = new(3, 9, 11, 255);

    /// <summary>
    /// The safe room's floor: a bright ivory at hue 40 and lightness 0.88. No floor palette uses a
    /// walkable surface anywhere near this light; the closest walkable tile in any of the twenty
    /// floors is over five hundred weighted units away.
    /// </summary>
    private static readonly Color RoomFloor = new(244, 232, 202, 255);

    /// <summary>The seams between the room's flagstones: deep umber, to carry the ivory.</summary>
    private static readonly Color RoomSeam = new(126, 104, 88, 255);

    /// <summary>The room's frame and brazier: a warm gold used by no floor's accent.</summary>
    private static readonly Color RoomFrame = new(242, 206, 136, 255);

    /// <summary>The room's shadow, used to set the pale floor off from the cave.</summary>
    private static readonly Color RoomShade = new(58, 46, 44, 255);

    /// <summary>How long the relic's name floats over the explorer after it is taken.</summary>
    private const double RelicAnnounceSeconds = 4.5;

    /// <summary>The floor whose relic has already been announced, so it happens once.</summary>
    private int _announcedRelicFloor = int.MinValue;

    /// <summary>When the current relic announcement fades out.</summary>
    private double _relicAnnounceUntil;

    /// <summary>The ring traced around every bloom: a saturated marigold no floor's scenery uses.</summary>
    private static readonly Color BloomGold = new(255, 196, 46, 255);

    /// <summary>The bloom's inner glow and twinkle: a pale, warm white-gold.</summary>
    private static readonly Color BloomGlow = new(255, 240, 170, 255);

    /// <summary>The hunter's rim and the bloom around its eyes: a deep blood red.</summary>
    private static readonly Color HunterRim = new(214, 22, 34, 255);

    /// <summary>The hunter's eyes themselves: a hot, near-white red.</summary>
    private static readonly Color HunterEye = new(255, 120, 96, 255);

    /// <summary>The Echo's mark: cold moonlight, deliberately nothing like a bloom's gold.</summary>
    private static readonly Color EchoPale = new(198, 206, 255, 255);

    /// <summary>The Echo's mark once it has been heard.</summary>
    private static readonly Color EchoDim = new(104, 112, 136, 255);

    /// <summary>
    /// How a seen-but-not-lit cell is tinted.
    /// </summary>
    /// <remarks>
    /// Brighter than the lantern's memory used to be: the maze is read from the part of the screen
    /// the torch is not on — the wall you just walked past, the passage you turned out of — and at
    /// 45% the floor and its walls both sank into the same black.
    /// </remarks>
    private static readonly Color Remembered = new(152, 167, 172, 255);

    private static readonly Color Cursor = new(210, 255, 225, 255);

    /// <summary>How a stunned hunter is tinted: cold and half-lit, so the reprieve is legible.</summary>
    private static readonly Color Stunned = new(150, 172, 205, 255);

    private PixelView _view;

    private readonly RaylibRenderer _chrome = new();

    private readonly IReadOnlyDictionary<string, ThemeAtlas> _manifest;

    private readonly string _directory;

    private readonly Dictionary<string, TileAtlas> _atlases = new(StringComparer.Ordinal);

    private readonly HashSet<Cell> _explored = [];

    private int _columns;

    private int _rows;

    private Image _lightImage;

    private Texture2D _light;

    /// <summary>
    /// How quickly a cell's light eases toward where it should be, in seconds: about a step's
    /// length to settle, so the lantern's edge rolls forward with the walk instead of jumping.
    /// </summary>
    private const double LightEaseSeconds = 0.08;

    /// <summary>A cell dimmer than this is drawn as unlit and skipped.</summary>
    private const float LightFloor = 0.02f;

    /// <summary>Each cell's current light, 0 to 1, row-major over the floor; eased every frame.</summary>
    private float[] _levels = [];

    /// <summary>How much each cell is "in the torch" rather than remembered, 0 to 1, eased the same way.</summary>
    private float[] _seen = [];

    /// <summary>How much each cell is in a hunter's cold light, 0 to 1, eased the same way.</summary>
    private float[] _glow = [];

    /// <summary>The floor size the eased arrays were made for.</summary>
    private (int Rows, int Columns) _levelSize;

    /// <summary>Whether the eased arrays hold a real frame yet; a new floor starts at its targets.</summary>
    private bool _levelsPrimed;

    /// <summary>
    /// The first cell the viewport touches, top-left. Loops over the viewport start here and run
    /// one cell further than the view, because a gliding camera straddles two cells.
    /// </summary>
    private PixelCamera _camera;

    /// <summary>The camera's left edge in atlas pixels from the floor's first column.</summary>
    private int _cameraX;

    /// <summary>The camera's top edge in atlas pixels from the floor's first row.</summary>
    private int _cameraY;

    /// <summary>Where the explorer is drawn this frame, in cells; fractional mid-step.</summary>
    private (double Row, double Column) _playerAt;

    private int _viewCells;

    private string _themeId;

    private FloorStyle _style;

    private FloorChrome? _floorChrome;

    private Direction _playerFacing = Direction.South;

    // One facing per hunter, in placement order, so two killers never share a walk direction.
    private readonly List<Direction> _enemyFacings = [];

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
        _viewCells = PixelLayout.ViewCells(config);
        _view = new PixelView(PixelLayout.CanvasWidth(_viewCells), PixelLayout.CanvasHeight(_viewCells));

        _themeId = FirstTheme();
        _style = FloorStyleCatalog.For(_themeId);
        Font = new PixelFont(_manifest[PixelFont.AtlasId], _directory);

        // The HUD and footer strips are drawn by the vector renderer, but they set their text in
        // the same compiled face as the canvas, so it has to be handed over before the first frame.
        _chrome.UsePixelFont(Font);

        _lightImage = Raylib.GenImageColor(_viewCells + 1, _viewCells + 1, Color.Black);
        _light = Raylib.LoadTextureFromImage(_lightImage);
        Raylib.SetTextureFilter(_light, TextureFilter.Bilinear);
        Raylib.SetTextureWrap(_light, TextureWrap.Clamp);
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
        _style = FloorStyleCatalog.For(themeId);
    }

    /// <summary>
    /// Tells the renderer which floor it is drawing, so the HUD can say its name and the cards can
    /// carry its emblem. Called once per floor, beside <see cref="SetTheme"/>.
    /// </summary>
    /// <param name="floor">The floor about to be played.</param>
    /// <param name="conditions">
    /// The descent conditions riding on this floor, or null for a plain floor. They ride the HUD
    /// as small marks beside the floor name, so the player always knows what is bending the rules.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public void SetFloor(FloorDefinition floor, IReadOnlyList<DescentCondition>? conditions = null)
    {
        ArgumentNullException.ThrowIfNull(floor);

        _floorChrome = FloorChrome.For(floor, conditions);
    }

    /// <summary>Forgets which cells have been explored, for the start of a new floor.</summary>
    public void Reset()
    {
        _explored.Clear();
        _levelsPrimed = false;
        _playerFacing = Direction.South;
        _enemyFacings.Clear();
        _chrome.Reset();
    }

    /// <inheritdoc />
    public void Draw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var atlas = Atlas;

        // Floors are not all the same size: the gallery is 21x21 and the warren is 11x11. The
        // viewport is sized from the lantern, but the maze bounds have to come from the floor
        // being drawn, not from the settings the renderer was constructed with.
        _rows = state.Maze.Rows;
        _columns = state.Maze.Columns;

        TrackFacing(state);
        Follow(state);
        TrackExploration(state);
        EaseLight(state);

        _view.Begin(CanvasColour);

        // The camera glides by the pixel, so the edge cells are half in view: everything of the
        // floor is clipped to the viewport rather than spilling into the canvas margin.
        BeginViewport();
        DrawCells(state, atlas);
        DrawProps(state, atlas);
        DrawSafeRoom(state);
        DrawEntities(state, atlas);
        DrawLight();
        DrawBloomMarks(state, atlas);
        DrawHunterMarks(state, atlas);
        DrawAmbience();
        Raylib.EndScissorMode();

        DrawRelicAnnouncement(state);
        PixelView.End();

        // The chrome is the existing HUD: pulled, glass status strip and hints. It draws in window
        // pixels, so it is applied after the canvas is scaled up, in the letterbox around it.
        var fit = _view.Present(RaylibLayout.HudHeight, RaylibLayout.FooterHeight, Margin, Backdrop());
        _chrome.DrawOverlay(state, _floorChrome);
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

    /// <summary>
    /// Points the camera at the player and resizes the canvas when a card changes how far the
    /// lantern reaches.
    /// </summary>
    /// <remarks>
    /// The zoom comes from the floor's <see cref="GameConfig.VisionRadius"/> rather than the
    /// effective radius, so lighting a wisp does not shove the camera in and out; a card that adds
    /// vision is a permanent, quieter zoom out. A viewport that changes means a new render target
    /// and a new light texture, which happens once per draft, not per frame.
    /// </remarks>
    private void Follow(GameState state)
    {
        var cells = PixelLayout.ViewCells(state.Config.VisionRadius);

        if (cells != _viewCells)
        {
            _view.Dispose();
            _viewCells = cells;
            _view = new PixelView(PixelLayout.CanvasWidth(cells), PixelLayout.CanvasHeight(cells));

            Raylib.UnloadImage(_lightImage);
            _lightImage = Raylib.GenImageColor(cells + 1, cells + 1, Color.Black);

            // The light is updated in place every frame, so the texture has to be the image's size.
            Raylib.UnloadTexture(_light);
            _light = Raylib.LoadTextureFromImage(_lightImage);
            Raylib.SetTextureFilter(_light, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(_light, TextureWrap.Clamp);
        }

        // The rules move the explorer a whole tile at the start of a step; the drawing glides
        // across that tile over the step's length, and the camera rides the drawn position, so
        // the floor scrolls under a walking figure instead of jumping when the step lands.
        _playerAt = state.IsPlayerStepping
            ? PixelLayout.Glide(state.PreviousPlayer, state.Player, state.PlayerStepFraction)
            : (state.Player.Row, state.Player.Column);

        _cameraX = PixelLayout.CameraOffset(_columns, _viewCells, _playerAt.Column);
        _cameraY = PixelLayout.CameraOffset(_rows, _viewCells, _playerAt.Row);
        _camera = new PixelCamera(
            (int)Math.Floor(_cameraX / (double)PixelLayout.CellSize),
            (int)Math.Floor(_cameraY / (double)PixelLayout.CellSize));
    }

    /// <summary>The canvas rectangle the maze is drawn in; nothing of the floor spills past it.</summary>
    private void BeginViewport() =>
        Raylib.BeginScissorMode(
            PixelLayout.Pad,
            PixelLayout.HeaderHeight + PixelLayout.Pad,
            _viewCells * PixelLayout.CellSize,
            _viewCells * PixelLayout.CellSize);

    /// <summary>How many cells a loop over the viewport visits: one more than the view, for the glide.</summary>
    private int SpanCells => _viewCells + 1;

    /// <summary>
    /// Returns the colour the letterbox is cleared to: the floor's accent, drowned in the cave's
    /// near-black. Each floor therefore sits in its own colour rather than the same black frame.
    /// </summary>
    private Color Backdrop() => _style.Fog;

    /// <summary>Returns the canvas x of a cell, as the camera sees it.</summary>
    private int CanvasX(int column) => PixelLayout.Pad + (column * PixelLayout.CellSize) - _cameraX;

    /// <summary>Returns the canvas y of a cell, as the camera sees it.</summary>
    private int CanvasY(int row) => PixelLayout.HeaderHeight + PixelLayout.Pad + (row * PixelLayout.CellSize) - _cameraY;

    /// <summary>Returns the canvas x of a fractional column, for a figure between two cells.</summary>
    private int CanvasX(double column) =>
        PixelLayout.Pad + (int)Math.Round(column * PixelLayout.CellSize) - _cameraX;

    /// <summary>Returns the canvas y of a fractional row, for a figure between two cells.</summary>
    private int CanvasY(double row) =>
        PixelLayout.HeaderHeight + PixelLayout.Pad + (int)Math.Round(row * PixelLayout.CellSize) - _cameraY;

    /// <summary>Returns whether any part of a cell is inside the current viewport.</summary>
    private bool OnScreen(Cell cell) => _camera.Shows(cell, SpanCells, SpanCells);

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

        // Only the viewport is drawn: the camera moves with the player, so a cell further away than
        // the viewport could not be seen even if it were painted.
        for (var row = _camera.Row; row < _camera.Row + SpanCells; row++)
        {
            if (row < 0 || row >= _rows)
            {
                continue;
            }

            for (var column = _camera.Column; column < _camera.Column + SpanCells; column++)
            {
                if (column < 0 || column >= _columns)
                {
                    continue;
                }

                var cell = state.Maze.GetCell(row, column);

                if (!Drawn(state, cell))
                {
                    continue;
                }

                var tint = CellTint(cell);
                var x = CanvasX(column);
                var y = CanvasY(row);

                atlas.Draw($"floor-{Variant(row, column, variants)}", x, y, tint);
                DrawWalls(state, atlas, cell, x, y, tint);
            }
        }
    }

    /// <summary>
    /// Draws the wall faces that belong to this cell: the north and west ones always, and the south
    /// and east ones when no neighbour will draw them.
    /// </summary>
    /// <remarks>
    /// A wall between two cells is both the eastern cell's west face and the western cell's east
    /// face. Drawing only the north and west faces paints each wall exactly once — but it also means
    /// a wall facing a cell that has never been seen is drawn by nobody, so the corridor shows a
    /// gap where its wall should be. When the southern or eastern neighbour is unknown this cell
    /// therefore draws that face itself; the two rules together paint every wall once and none
    /// not at all.
    /// </remarks>
    private void DrawWalls(GameState state, TileAtlas atlas, Cell cell, int x, int y, Color tint)
    {
        DrawFace(atlas, state, cell, Direction.North, x, y, tint);
        DrawFace(atlas, state, cell, Direction.West, x, y, tint);

        var southKnown = cell.Row + 1 < _rows &&
            Drawn(state, state.Maze.GetCell(cell.Row + 1, cell.Column));

        if (!southKnown)
        {
            DrawFace(atlas, state, cell, Direction.South, x, y, tint);
        }

        var eastKnown = cell.Column + 1 < _columns &&
            Drawn(state, state.Maze.GetCell(cell.Row, cell.Column + 1));

        if (!eastKnown)
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
        var props = atlas.SceneryPropCount;
        var hasFeature = atlas.Has("floor-feature");
        var emblem = atlas.PropCount > props ? $"prop-{props}" : null;

        for (var row = _camera.Row; row < _camera.Row + SpanCells; row++)
        {
            if (row < 0 || row >= _rows)
            {
                continue;
            }

            for (var column = _camera.Column; column < _camera.Column + SpanCells; column++)
            {
                if (column < 0 || column >= _columns)
                {
                    continue;
                }

                var cell = state.Maze.GetCell(row, column);

                if (!Drawn(state, cell) || IsBusy(state, cell))
                {
                    continue;
                }

                var hash = Hash(row, column);
                var tint = CellTint(cell);

                // The signature object is rare on purpose: it should be a landmark the player
                // remembers, not the most common thing on the floor.
                if (hasFeature && hash % FeatureSpacing == 0)
                {
                    atlas.Draw("floor-feature", CanvasX(column), CanvasY(row), tint);
                    continue;
                }

                // The emblem is rarer still: a carved mark that names the place.
                if (emblem is not null && hash % EmblemSpacing == 0)
                {
                    atlas.Draw(emblem, CanvasX(column), CanvasY(row), tint);
                    continue;
                }

                if (props == 0 || hash % PropSpacing != 0)
                {
                    continue;
                }

                atlas.Draw(
                    $"prop-{(hash / PropSpacing) % props}",
                    CanvasX(column),
                    CanvasY(row),
                    tint);
            }
        }
    }

    /// <summary>
    /// Draws the safe room as a place rather than as nine cells that happen to be open: pale
    /// flagstones with seams, a gold frame around the 3x3 block and a brazier in the middle.
    /// </summary>
    /// <param name="state">The floor being drawn.</param>
    /// <remarks>
    /// <para>
    /// The palette is the one scheme no floor uses: a bright ivory (hue 40, lightness 0.88) against
    /// deep umber seams, framed in gold. Every floor's walkable tile is over five hundred weighted
    /// units away, and the nearest palette slot of any kind — the Ossuary's bone near-white — is
    /// about seventy away, so the room reads at a glance even through the fog tint.
    /// </para>
    /// <para>
    /// It is deliberately drawn over the cave tile without replacing the wall faces: a room with
    /// its own walls missing would look like a hole in the maze. Only the interior is painted, and
    /// the frame is four corner brackets on the block's outer edge.
    /// </para>
    /// </remarks>
    private void DrawSafeRoom(GameState state)
    {
        if (state.SafeRoom is not { } room)
        {
            return;
        }

        const int Cell = PixelLayout.CellSize;
        var pulse = 0.6 + (0.4 * Math.Sin(Raylib.GetTime() * 1.7));
        var floor = new Color(RoomFloor.R, RoomFloor.G, RoomFloor.B, (byte)(state.IsInSafeRoom ? 238 : 208));

        // The entrance is the room's one tell. A warm threshold on the door cell, drawn whenever
        // the player has seen it, so a door in the rock is a find rather than an invisible seam
        // the player walks past until they happen to step on it.
        var door = room.Door;

        if (Known(state, door) && OnScreen(door))
        {
            var doorX = CanvasX(door.Column);
            var doorY = CanvasY(door.Row);
            var threshold = new Color(RoomFloor.R, RoomFloor.G, RoomFloor.B, (byte)(90 + (70 * pulse)));
            var gold = new Color(RoomFrame.R, RoomFrame.G, RoomFrame.B, (byte)(120 + (110 * pulse)));

            Raylib.DrawRectangle(doorX + 3, doorY + 3, Cell - 6, Cell - 6, threshold);
            Raylib.DrawRectangle(doorX + 3, doorY + 3, Cell - 6, 1, gold);
            Raylib.DrawRectangle(doorX + 3, doorY + Cell - 4, Cell - 6, 1, gold);
            Raylib.DrawRectangle(doorX + 3, doorY + 3, 1, Cell - 6, gold);
            Raylib.DrawRectangle(doorX + Cell - 4, doorY + 3, 1, Cell - 6, gold);
        }

        foreach (var cell in room.Cells)
        {
            if (!Known(state, cell) || !OnScreen(cell))
            {
                continue;
            }

            var x = CanvasX(cell.Column);
            var y = CanvasY(cell.Row);

            // Inset by a pixel so the room's own walls still draw along its edge.
            Raylib.DrawRectangle(x + 1, y + 1, Cell - 2, Cell - 2, floor);
            Raylib.DrawRectangle(x + 1, y + (Cell / 2), Cell - 2, 1, RoomSeam);
            Raylib.DrawRectangle(x + (Cell / 2), y + 1, 1, Cell - 2, RoomSeam);
            Raylib.DrawRectangle(x + 1, y + Cell - 2, Cell - 2, 1, RoomShade);
            Raylib.DrawRectangle(x + Cell - 2, y + 1, 1, Cell - 2, RoomShade);
        }

        // Four corner brackets around the block: the frame a carpentered room would have, without
        // a line drawn across the doorway.
        var corner = room.TopLeft;

        if (Known(state, corner) && OnScreen(corner))
        {
            var left = CanvasX(corner.Column);
            var top = CanvasY(corner.Row);
            var size = SecretRoom.Size * Cell;
            var gold = new Color(RoomFrame.R, RoomFrame.G, RoomFrame.B, (byte)(150 + (100 * pulse)));
            const int Arm = 6;
            const int Thick = 2;

            DrawBracket(left, top, Arm, Thick, 1, 1, gold);
            DrawBracket(left + size, top, Arm, Thick, -1, 1, gold);
            DrawBracket(left, top + size, Arm, Thick, 1, -1, gold);
            DrawBracket(left + size, top + size, Arm, Thick, -1, -1, gold);
        }

        // A brazier on the counter cell, so the room has a light of its own even before the player
        // walks in. The flame breathes on the same clock as the frame.
        var counter = room.Center;

        if (Known(state, counter) && OnScreen(counter))
        {
            var centreX = CanvasX(counter.Column) + (Cell / 2);
            var centreY = CanvasY(counter.Row) + (Cell / 2);
            var flame = new Color(RoomFrame.R, RoomFrame.G, RoomFrame.B, (byte)(160 + (95 * pulse)));

            Raylib.DrawRectangle(centreX - 4, centreY + 2, 8, 2, RoomShade);
            Raylib.DrawRectangle(centreX - 3, centreY + 3, 6, 1, RoomSeam);
            Raylib.DrawRectangle(centreX - 1, centreY - 3, 3, 5, flame);
            Raylib.DrawRectangle(centreX - 3, centreY - 1, 1, 3, flame);
            Raylib.DrawRectangle(centreX + 3, centreY - 1, 1, 3, flame);
        }
    }

    /// <summary>Draws one L-shaped corner bracket of the room's frame.</summary>
    /// <param name="x">The corner's x.</param>
    /// <param name="y">The corner's y.</param>
    /// <param name="arm">How long each arm is.</param>
    /// <param name="thickness">How thick each arm is.</param>
    /// <param name="horizontal">-1 to grow left, 1 to grow right.</param>
    /// <param name="vertical">-1 to grow up, 1 to grow down.</param>
    /// <param name="colour">The bracket's colour.</param>
    private static void DrawBracket(int x, int y, int arm, int thickness, int horizontal, int vertical, Color colour)
    {
        var left = horizontal > 0 ? x : x - arm;
        var top = vertical > 0 ? y : y - thickness;
        var side = horizontal > 0 ? x : x - thickness;
        var down = vertical > 0 ? y : y - arm;

        Raylib.DrawRectangle(left, top, arm, thickness, colour);
        Raylib.DrawRectangle(side, down, thickness, arm, colour);
    }

    /// <summary>Returns whether a cell holds something that must not be covered by scenery.</summary>
    private static bool IsBusy(GameState state, Cell cell) =>
        cell == state.Player ||
        HunterOn(state, cell) ||
        cell == state.Exit ||
        state.Keys.Contains(cell) ||
        state.EchoCell == cell ||
        state.CuriosityCell == cell ||
        state.RelicCell == cell ||
        state.Locks.Contains(cell);

    /// <summary>Returns a stable, positive hash of a cell, for variant and prop choices.</summary>
    private static int Hash(int row, int column) =>
        unchecked(((row * 73856093) ^ (column * 19349663)) & 0x7FFFFFFF);

    /// <summary>Returns <paramref name="value"/> wrapped into [0, <paramref name="span"/>).</summary>
    private static double Wrap(double value, double span) => value - (Math.Floor(value / span) * span);

    /// <summary>Returns the fractional part of a value, the basis of the weather hashes.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    /// <summary>Returns a stable positive hash of a string, used to seed a floor's weather.</summary>
    private static int TextHash(string text)
    {
        var hash = unchecked((int)2166136261);

        foreach (var character in text)
        {
            hash = unchecked((hash ^ character) * 16777619);
        }

        return hash & 0x7FFFFFFF;
    }

    /// <summary>Draws the keys, the arch, the wisp, the barriers and the two figures.</summary>
    private void DrawEntities(GameState state, TileAtlas atlas)
    {
        var time = Raylib.GetTime();
        var frame = (int)(time * 3.0) % 3;

        // The bloom glow a card grants after a catch: a warm halo behind every bloom the rules
        // have just revealed, pulsing so it reads as a glow rather than as a bright tile.
        var glowing = state.IsBloomGlowActive;
        var halo = 0.55 + (0.45 * Math.Sin(time * 7.0));

        foreach (var key in state.Keys)
        {
            if (!Known(state, key) || !OnScreen(key))
            {
                continue;
            }

            if (glowing)
            {
                var keyX = CanvasX(key.Column) + (PixelLayout.CellSize / 2);
                var keyY = CanvasY(key.Row) + (PixelLayout.CellSize / 2);

                Raylib.DrawCircle(keyX, keyY, 7.0f, new Color(255, 232, 158, 70 + (int)(90 * halo)));
                Raylib.DrawCircle(keyX, keyY, 4.5f, new Color(255, 246, 205, 110 + (int)(90 * halo)));
            }

            atlas.Draw($"key-{(frame + (key.Column + key.Row)) % 3}", CanvasX(key.Column), CanvasY(key.Row));
        }

        // The Echo: a standing memorial stone with a slit of cold light, drawn from primitives
        // rather than from a bloom tile. It used to borrow the key art, which made the one
        // story beat on the floor look like a pickup; a grave marker cannot be mistaken for one.
        // It keeps its place after it has been heard, greyed out.
        if (state.EchoCell is { } echo && Known(state, echo) && OnScreen(echo))
        {
            var cellX = CanvasX(echo.Column);
            var cellY = CanvasY(echo.Row);
            var pale = state.IsEchoRead ? EchoDim : EchoPale;
            var alpha = state.IsEchoRead
                ? (byte)140
                : (byte)(110 + (130 * (0.5 + (0.5 * Math.Sin(time * 2.2)))));
            var glow = new Color(pale.R, pale.G, pale.B, alpha);
            const int Cell = PixelLayout.CellSize;

            // The slab leans a little: a 6-wide body with the top row offset by one pixel.
            Raylib.DrawRectangle(cellX + 5, cellY + 4, 6, 9, RoomShade);
            Raylib.DrawRectangle(cellX + 6, cellY + 3, 4, 1, RoomShade);

            // The slit of light, and the crossbar that makes it a marker rather than a face.
            Raylib.DrawRectangle(cellX + 7, cellY + 5, 2, 7, glow);
            Raylib.DrawRectangle(cellX + 4, cellY + 7, 8, 2, glow);

            if (!state.IsEchoRead)
            {
                const int Dot = 2;
                var far = Cell - Dot - 2;

                Raylib.DrawRectangle(cellX + 2, cellY + 2, Dot, Dot, glow);
                Raylib.DrawRectangle(cellX + far, cellY + 2, Dot, Dot, glow);
                Raylib.DrawRectangle(cellX + 2, cellY + far, Dot, Dot, glow);
                Raylib.DrawRectangle(cellX + far, cellY + far, Dot, Dot, glow);
            }
        }

        DrawLocks(state);
        DrawCuriosity(state);
        DrawRelic(state);

        if (state.Exit is var exit && Known(state, exit) && OnScreen(exit))
        {
            atlas.Draw(
                state.IsExitUnlocked ? "arch-open" : "arch-locked",
                CanvasX(exit.Column),
                CanvasY(exit.Row));
        }

        var hunters = state.Enemies;

        for (var index = 0; index < hunters.Count; index++)
        {
            var hunter = hunters[index];

            if (!OnScreen(hunter.Cell) && !OnScreen(hunter.From))
            {
                continue;
            }

            var facing = index < _enemyFacings.Count ? _enemyFacings[index] : Direction.South;

            // A hunter's step is as long as its clock, so it slides the whole way between two
            // cells rather than appearing on the next one.
            var (hunterRow, hunterColumn) = PixelLayout.Glide(hunter.From, hunter.Cell, hunter.StepFraction);

            // A pool of dark red under its feet, as if the floor stains where it stands.
            Raylib.DrawEllipse(
                CanvasX(hunterColumn) + (PixelLayout.CellSize / 2),
                CanvasY(hunterRow) + PixelLayout.CellSize - 1,
                7f,
                2.5f,
                hunter.Stunned ? new Color(20, 30, 60, 110) : new Color(70, 0, 6, 130));

            atlas.Draw(
                $"killer-{NameOf(facing)}-{EnemyFrame(hunter)}",
                CanvasX(hunterColumn),
                CanvasY(hunterRow),
                hunter.Stunned ? Stunned : Color.White);
        }

        atlas.Draw(
            $"player-{NameOf(_playerFacing)}-{PlayerFrame(state)}",
            CanvasX(_playerAt.Column),
            CanvasY(_playerAt.Row));

        // The tide is a wash over the whole frame rather than a change to the tiles: the water does
        // not replace the hall, it sits over it, and the player can still read the floorplan the
        // lantern gives them.
        if (state.IsTideIn)
        {
            var breath = 0.5 + (0.5 * Math.Sin(time * 1.7));
            Raylib.DrawRectangle(
                0,
                0,
                CanvasWidth,
                CanvasHeight,
                new Color(36, 92, 132, 40 + (int)(22 * breath)));

            for (var line = 0; line < 5; line++)
            {
                var y = (int)((time * 9.0) + (line * 37)) % Math.Max(1, CanvasHeight);
                Raylib.DrawRectangle(0, y, CanvasWidth, 1, new Color(150, 210, 240, 20));
            }
        }
    }

    /// <summary>
    /// Marks every bloom the player has seen so it cannot be mistaken for scenery: a pulsing gold
    /// halo, a gold outline traced around the flower's own pixels, and a twinkle above it.
    /// </summary>
    /// <param name="state">The floor being drawn.</param>
    /// <param name="atlas">The floor's atlas, for the bloom tile and its outline.</param>
    /// <remarks>
    /// The floors are full of flowers — props, emblems, signature objects — and several themes
    /// paint them in the same hue as the bloom, so the bloom alone could not be told apart. This
    /// is drawn after the lightmap on purpose: the darkness multiplies everything under it, and
    /// the one thing the player is looking for has to read at the edge of the lantern too. The
    /// gold is <see cref="BloomGold"/>, which no floor uses for its scenery.
    /// </remarks>
    private void DrawBloomMarks(GameState state, TileAtlas atlas)
    {
        var time = Raylib.GetTime();
        var frame = (int)(time * 3.0) % 3;
        const int Half = PixelLayout.CellSize / 2;

        foreach (var key in state.Keys)
        {
            if (!Known(state, key) || !OnScreen(key))
            {
                continue;
            }

            var x = CanvasX(key.Column);
            var y = CanvasY(key.Row);

            // Each bloom breathes on its own phase, so a room with three of them shimmers
            // rather than blinking in unison.
            var phase = (key.Row * 1.3) + (key.Column * 0.7);
            var pulse = 0.5 + (0.5 * Math.Sin((time * 3.2) + phase));

            // A remembered bloom (out of the torch, still in memory) keeps its mark, a little
            // quieter, so the player can walk back to one they passed.
            var strength = state.IsVisible(key) ? 1.0 : 0.7;

            Raylib.DrawCircle(x + Half, y + Half, 10.0f, Alpha(BloomGold, (35 + (35 * pulse)) * strength));
            Raylib.DrawCircle(x + Half, y + Half, 6.5f, Alpha(BloomGlow, (45 + (45 * pulse)) * strength));

            var tile = $"key-{(frame + (key.Column + key.Row)) % 3}";

            atlas.DrawOutline(tile, x, y, Alpha(BloomGold, (190 + (65 * pulse)) * strength));
            atlas.Draw(tile, x, y);

            // A four-point twinkle over the flower's shoulder, flaring on the pulse.
            if (pulse > 0.35)
            {
                var sparkle = Alpha(BloomGlow, 255 * strength * pulse);
                var sx = x + PixelLayout.CellSize - 3;
                var sy = y + 1;

                Raylib.DrawRectangle(sx, sy - 1, 1, 3, sparkle);
                Raylib.DrawRectangle(sx - 1, sy, 3, 1, sparkle);

                if (pulse > 0.8)
                {
                    Raylib.DrawRectangle(sx, sy - 2, 1, 5, sparkle);
                    Raylib.DrawRectangle(sx - 2, sy, 5, 1, sparkle);
                }
            }
        }
    }

    /// <summary>
    /// Gives every hunter in the light a blood-red rim and burning eyes, drawn over the darkness.
    /// </summary>
    /// <param name="state">The floor being drawn.</param>
    /// <param name="atlas">The floor's atlas, for the hunter's outline.</param>
    /// <remarks>
    /// <para>
    /// The hunter's cloak is the darkest colour on the floor, so under the lightmap its body is
    /// close to black: what the player sees is a red outline around a shape, and two eyes. That
    /// is drawn here, after the light, so it does not sink into the dark with the body.
    /// </para>
    /// <para>
    /// The marks are scaled by the eased light where the hunter stands, so a hunter in the dark
    /// stays hidden exactly as before — this never shows the player something the torch does not
    /// — and one walking into the light fades in rather than popping. A stunned hunter's rim goes
    /// cold blue and its eyes go out; a sleeping one keeps its rim but shuts its eyes.
    /// </para>
    /// </remarks>
    private void DrawHunterMarks(GameState state, TileAtlas atlas)
    {
        var hunters = state.Enemies;
        var time = Raylib.GetTime();

        for (var index = 0; index < hunters.Count; index++)
        {
            var hunter = hunters[index];

            if (!OnScreen(hunter.Cell) && !OnScreen(hunter.From))
            {
                continue;
            }

            var (row, column) = PixelLayout.Glide(hunter.From, hunter.Cell, hunter.StepFraction);
            var lit = LevelAt((int)Math.Round(row), (int)Math.Round(column));

            if (lit <= LightFloor)
            {
                continue;
            }

            var strength = Math.Clamp(lit / 0.6, 0.0, 1.0);
            var facing = index < _enemyFacings.Count ? _enemyFacings[index] : Direction.South;
            var x = CanvasX(column);
            var y = CanvasY(row);
            var pulse = 0.5 + (0.5 * Math.Sin((time * 4.0) + (index * 2.1)));
            var tile = $"killer-{NameOf(facing)}-{EnemyFrame(hunter)}";

            atlas.DrawOutline(
                tile,
                x,
                y,
                Alpha(hunter.Stunned ? Stunned : HunterRim, (150 + (90 * pulse)) * strength));

            // The outline is the silhouette stamped around the figure, so the body is drawn back
            // over it — dimmed by the light it stands in, as the lightmap would have, so the
            // cloak stays a shadow inside a red rim.
            var shade = (byte)Math.Clamp((int)(lit * 255f), 0, 255);
            var body = hunter.Stunned
                ? new Color((byte)(Stunned.R * shade / 255), (byte)(Stunned.G * shade / 255), (byte)(Stunned.B * shade / 255), (byte)255)
                : new Color(shade, shade, shade, (byte)255);

            atlas.Draw(tile, x, y, body);

            if (hunter.Stunned || hunter.Dormant || facing == Direction.North)
            {
                continue;
            }

            // The eyes sit where the rig draws them: both face-on, the forward pair in profile.
            var (leftEye, rightEye) = facing switch
            {
                Direction.South => (7, 9),
                Direction.East => (9, 10),
                _ => (5, 6),
            };
            const int EyeRow = 4;

            foreach (var eye in (ReadOnlySpan<int>)[leftEye, rightEye])
            {
                Raylib.DrawCircle(x + eye, y + EyeRow, 2.5f, Alpha(HunterRim, (60 + (70 * pulse)) * strength));
                Raylib.DrawRectangle(x + eye, y + EyeRow, 1, 1, Alpha(HunterEye, 255 * strength));
            }
        }
    }

    /// <summary>Returns a colour with its alpha replaced, clamped to a byte.</summary>
    private static Color Alpha(Color colour, double alpha) =>
        new(colour.R, colour.G, colour.B, (byte)Math.Clamp((int)Math.Round(alpha), 0, 255));

    /// <summary>Draws the Gilded Cage's three locks, shut or thrown.</summary>
    /// <param name="state">The floor being drawn.</param>
    private void DrawLocks(GameState state)
    {
        if (state.Locks.Count == 0)
        {
            return;
        }

        var time = Raylib.GetTime();

        foreach (var cell in state.Locks)
        {
            if (!Known(state, cell) || !OnScreen(cell))
            {
                continue;
            }

            var x = CanvasX(cell.Column);
            var y = CanvasY(cell.Row);
            var thrown = state.IsLockThrown(cell);
            var gold = thrown
                ? new Color(112, 96, 58, 190)
                : new Color(236, 198, 104, 235);
            var light = thrown
                ? new Color(70, 62, 42, 120)
                : new Color(255, 240, 186, (int)(150 + (90 * (0.5 + (0.5 * Math.Sin(time * 3.0))))));

            // A gilded cage: a small barred box with a ring on top. Thrown, it hangs open and grey.
            Raylib.DrawRectangle(x + 3, y + 6, 10, 8, light);
            Raylib.DrawRectangle(x + 4, y + 7, 8, 6, gold);
            Raylib.DrawRectangle(x + 5, y + 3, 6, 1, gold);
            Raylib.DrawRectangle(x + 4, y + 4, 1, 2, gold);
            Raylib.DrawRectangle(x + 11, y + 4, 1, 2, gold);

            if (thrown)
            {
                // The door is swung out: one bar standing clear of the box.
                Raylib.DrawRectangle(x + 13, y + 6, 1, 6, gold);
            }
            else
            {
                Raylib.DrawRectangle(x + 7, y + 7, 1, 5, new Color(96, 74, 34, 220));
                Raylib.DrawRectangle(x + 9, y + 7, 1, 5, new Color(96, 74, 34, 220));
            }
        }
    }

    /// <summary>Draws the floor's one optional find, in its three forms.</summary>
    /// <param name="state">The floor being drawn.</param>
    /// <remarks>
    /// Each is drawn from primitives rather than from a tile because there are three of them and
    /// they have to be told apart at a glance in the dark: a bell, a box, a lit stone. Once spent
    /// they stay where they were, greyed out, so the player can see what they walked past.
    /// </remarks>
    private void DrawCuriosity(GameState state)
    {
        if (state.CuriosityCell is not { } cell || !Known(state, cell) || !OnScreen(cell))
        {
            return;
        }

        var x = CanvasX(cell.Column);
        var y = CanvasY(cell.Row);
        var time = Raylib.GetTime();
        var spent = state.IsCuriositySpent;
        var pulse = 0.5 + (0.5 * Math.Sin(time * 2.6));

        if (spent)
        {
            Raylib.DrawRectangle(x + 5, y + 6, 6, 5, new Color(74, 82, 84, 150));
            return;
        }

        switch (state.Curiosity)
        {
            case CuriosityKind.Bell:
            {
                var gold = new Color(240, 214, 138, (int)(190 + (60 * pulse)));

                Raylib.DrawRectangle(x + 6, y + 3, 4, 1, gold);
                Raylib.DrawRectangle(x + 4, y + 4, 8, 1, gold);
                Raylib.DrawRectangle(x + 5, y + 5, 6, 4, gold);
                Raylib.DrawRectangle(x + 7, y + 9, 2, 2, gold);
                Raylib.DrawCircle(
                    x + 8,
                    y + 12,
                    4.0f,
                    new Color((byte)255, (byte)226, (byte)150, (byte)(40 + (int)(50 * pulse))));
                break;
            }

            case CuriosityKind.Reliquary:
            {
                var bone = new Color(206, 198, 176, 230);
                var seal = new Color(196, 78, 88, (int)(170 + (70 * pulse)));

                Raylib.DrawRectangle(x + 3, y + 5, 10, 7, bone);
                Raylib.DrawRectangle(x + 3, y + 4, 10, 1, new Color(150, 142, 124, 230));
                Raylib.DrawRectangle(x + 7, y + 7, 2, 2, seal);
                Raylib.DrawCircle(
                    x + 8,
                    y + 9,
                    7.0f,
                    new Color((byte)210, (byte)90, (byte)100, (byte)(34 + (int)(46 * pulse))));
                break;
            }

            default:
            {
                var stone = new Color(120, 148, 128, 235);
                var flame = new Color(170, 246, 186, (int)(170 + (80 * pulse)));

                Raylib.DrawRectangle(x + 4, y + 9, 8, 3, stone);
                Raylib.DrawRectangle(x + 6, y + 6, 4, 3, stone);
                Raylib.DrawRectangle(x + 7, y + 3, 2, 3, flame);
                Raylib.DrawCircle(
                    x + 8,
                    y + 6,
                    6.0f,
                    new Color((byte)150, (byte)240, (byte)170, (byte)(36 + (int)(44 * pulse))));
                break;
            }
        }
    }

    /// <summary>
    /// Draws the altar: a low plinth holding one floating relic, colour-coded by what it does.
    /// </summary>
    /// <remarks>
    /// The altar has to read as a destination, not a pickup: it is a stone with something above
    /// it rather than a glint on the floor, and its colour is the effect's family — violet for
    /// power, gold for the run's economy, green for healing. Once taken, the plinth stays, empty.
    /// </remarks>
    private void DrawRelic(GameState state)
    {
        if (state.RelicCell is not { } cell || !Known(state, cell) || !OnScreen(cell))
        {
            return;
        }

        var x = CanvasX(cell.Column);
        var y = CanvasY(cell.Row);
        var time = Raylib.GetTime();
        var pulse = 0.5 + (0.5 * Math.Sin(time * 2.1));

        // The plinth is the same whether the relic is there or gone; only the gem changes.
        Raylib.DrawRectangle(x + 3, y + 10, 10, 2, new Color(96, 92, 112, 235));
        Raylib.DrawRectangle(x + 5, y + 8, 6, 2, new Color(118, 112, 138, 235));

        if (state.IsRelicTaken || state.Relic is not { } kind)
        {
            return;
        }

        var tint = RelicTint(kind);
        var glow = new Color(tint.R, tint.G, tint.B, (byte)(52 + (int)(70 * pulse)));
        var core = new Color(tint.R, tint.G, tint.B, (byte)(190 + (int)(60 * pulse)));

        Raylib.DrawCircle(x + 8, y + 5, 6.5f, glow);
        Raylib.DrawRectangle(x + 7, y + 2, 3, 3, core);
        Raylib.DrawRectangle(x + 6, y + 4, 5, 2, core);
        Raylib.DrawRectangle(x + 7, y + 6, 3, 1, core);
    }

    /// <summary>
    /// Floats the relic's name over the explorer for a few seconds after the altar is touched.
    /// </summary>
    /// <remarks>
    /// The relic's effect is not visible until the next floor, so without a word at the moment of
    /// taking it the biggest find in the run would pass with only a chime. The name is drawn over
    /// the player rather than in the HUD so it reads as something that happened to them.
    /// </remarks>
    private void DrawRelicAnnouncement(GameState state)
    {
        if (!state.IsRelicTaken || state.Relic is not { } kind)
        {
            return;
        }

        var time = Raylib.GetTime();

        if (_announcedRelicFloor != state.Config.FloorNumber)
        {
            _announcedRelicFloor = state.Config.FloorNumber;
            _relicAnnounceUntil = time + RelicAnnounceSeconds;
        }

        var left = _relicAnnounceUntil - time;

        if (left <= 0)
        {
            return;
        }

        var fade = (byte)(255 * Math.Clamp(left / 1.2, 0, 1));
        var centre = CanvasX(_playerAt.Column) + (PixelLayout.CellSize / 2);
        var name = RelicCatalog.CardFor(kind).Name.ToUpperInvariant();

        Font.DrawCentered(name, centre, CanvasY(_playerAt.Row) - 12, 1, new Color((byte)238, (byte)228, (byte)255, fade));
    }

    /// <summary>Returns the gem colour that names a relic's family.</summary>
    /// <remarks>
    /// The family comes from Core, so a gem and the run details card agree on what red, green and
    /// gold mean instead of each keeping its own list.
    /// </remarks>
    public static Color RelicTint(RelicKind kind) => RelicCatalog.FamilyOf(kind) switch
    {
        RelicFamily.Body => new Color(132, 214, 190, 255),
        RelicFamily.Light => new Color(140, 196, 240, 255),
        RelicFamily.Hunt => new Color(190, 168, 236, 255),
        RelicFamily.Garden => new Color(232, 196, 120, 255),
        _ => new Color(224, 118, 118, 255),
    };

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

    /// <summary>Returns the frame a hunter is on: its gait only moves while it is stepping.</summary>
    /// <param name="hunter">The hunter to draw.</param>
    private static string EnemyFrame(HunterView hunter) =>
        hunter.From == hunter.Cell
            ? "idle"
            : hunter.StepFraction < 0.5 ? "walk0" : "walk1";

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
        state.IsVisible(cell) || RememberedCell(state, cell);

    /// <summary>
    /// Returns whether a seen cell is close enough to the player to still be drawn.
    /// </summary>
    /// <param name="state">The floor being drawn.</param>
    /// <param name="cell">The cell to test.</param>
    /// <returns>
    /// <see langword="true"/> when the player has seen the cell and it lies within
    /// <see cref="MemoryRadius"/> of them.
    /// </returns>
    private bool RememberedCell(GameState state, Cell cell) =>
        _explored.Contains(cell) && WithinMemory(state, cell);

    /// <summary>Returns whether <paramref name="cell"/> lies within the torch's memory.</summary>
    /// <param name="state">The floor being drawn.</param>
    /// <param name="cell">The cell to test.</param>
    private static bool WithinMemory(GameState state, Cell cell) =>
        Math.Abs(cell.Row - state.Player.Row) <= MemoryRadius &&
        Math.Abs(cell.Column - state.Player.Column) <= MemoryRadius;

    /// <summary>Returns whether any hunter is standing on <paramref name="cell"/>.</summary>
    /// <param name="state">The floor being drawn.</param>
    /// <param name="cell">The cell to check.</param>
    private static bool HunterOn(GameState state, Cell cell)
    {
        foreach (var hunter in state.Enemies)
        {
            if (hunter.Cell == cell)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Multiplies the scene by one texel per cell: lantern falloff, remembered cells, and the
    /// killer's cold light.
    /// </summary>
    private void DrawLight()
    {
        UploadLight();

        // The flicker rides on the draw's tint instead of being baked into the texture.
        var flicker = Flicker();
        var light = _style.LightTint;
        var tint = new Color(
            (byte)Math.Clamp((flicker * light.R) / 255, 0, 255),
            (byte)Math.Clamp((flicker * light.G) / 255, 0, 255),
            (byte)Math.Clamp((flicker * light.B) / 255, 0, 255),
            (byte)255);

        Raylib.BeginBlendMode(BlendMode.Multiplied);
        // One texel per cell from the camera's first cell, drawn where that cell is: the light
        // scrolls with the floor, and the viewport's clip trims the extra row and column.
        Raylib.DrawTexturePro(
            _light,
            new Rectangle(0, 0, SpanCells, SpanCells),
            new Rectangle(
                CanvasX(_camera.Column),
                CanvasY(_camera.Row),
                SpanCells * PixelLayout.CellSize,
                SpanCells * PixelLayout.CellSize),
            Vector2.Zero,
            0f,
            tint);
        Raylib.EndBlendMode();
    }

    /// <summary>Returns the lantern's flicker as a multiplier, from 0.88 to 1.0.</summary>
    private static byte Flicker()
    {
        var breath = 0.88 + (0.12 * (0.5 + (0.5 * Math.Sin(Raylib.GetTime() * 6.3))));
        return (byte)Math.Clamp((int)Math.Round(breath * 255.0), 0, 255);
    }

    /// <summary>
    /// Records every cell the lantern reaches, over the whole maze.
    /// </summary>
    /// <remarks>
    /// A generous vision card can light cells further out than the viewport shows, and the map must
    /// remember them for when the camera gets there; the viewport only decides what is painted, not
    /// what has been seen.
    /// </remarks>
    private void TrackExploration(GameState state)
    {
        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);

                if (state.IsVisible(cell))
                {
                    _explored.Add(cell);
                }
            }
        }
    }

    /// <summary>
    /// Eases every cell's light toward what the floor says it should be this frame.
    /// </summary>
    /// <param name="state">The floor being drawn.</param>
    /// <remarks>
    /// <para>
    /// The rules decide what is lit a whole tile at a time, at the moment a step starts, so a
    /// lightmap read straight from them jumps a cell with every step. Here each cell keeps its own
    /// level and slides toward its target over about a step, and the lantern's falloff is measured
    /// from where the explorer is drawn — between two cells while walking — rather than from the
    /// tile the rules have already moved them to. The edge of the light rolls forward with the
    /// walk, cells ahead brighten as they come into the torch, and the room behind dims away
    /// instead of blinking out.
    /// </para>
    /// <para>
    /// It runs over the whole floor, not only the viewport, so a cell that scrolls into view has
    /// been easing all along and does not arrive with a stale level. The largest floor is a few
    /// hundred cells, so this is cheap.
    /// </para>
    /// </remarks>
    private void EaseLight(GameState state)
    {
        var size = (_rows, _columns);

        if (size != _levelSize || _levels.Length != _rows * _columns)
        {
            _levelSize = size;
            _levels = new float[_rows * _columns];
            _seen = new float[_rows * _columns];
            _glow = new float[_rows * _columns];
            _levelsPrimed = false;
        }

        // A new floor starts at its targets: fading in from black would hide the first second.
        var step = _levelsPrimed
            ? (float)(1.0 - Math.Exp(-Math.Min(Raylib.GetFrameTime(), 0.25) / LightEaseSeconds))
            : 1f;
        _levelsPrimed = true;

        var radius = Math.Max(1, state.EffectiveVisionRadius);
        var (playerRow, playerColumn) = _playerAt;
        var glowing = state.EnemyGlow.Count > 0;

        for (var row = 0; row < _rows; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);
                var index = (row * _columns) + column;
                var visible = state.IsVisible(cell);
                double level;

                if (visible)
                {
                    var distance = Math.Max(Math.Abs(row - playerRow), Math.Abs(column - playerColumn));
                    var falloff = 1.0 - (distance / (radius + 1.0));
                    level = 0.70 + (Math.Clamp(falloff, 0.0, 1.0) * 0.30);
                }
                else if (_explored.Contains(cell) && WithinMemory(state, cell))
                {
                    level = 0.55;
                }
                else
                {
                    level = 0.0;
                }

                _levels[index] += ((float)level - _levels[index]) * step;
                _seen[index] += ((visible ? 1f : 0f) - _seen[index]) * step;
                _glow[index] += ((glowing && state.EnemyGlow.Contains(cell) ? 1f : 0f) - _glow[index]) * step;
            }
        }
    }

    /// <summary>Writes the viewport's eased light into the light texture, one texel per cell.</summary>
    /// <remarks>
    /// The texture is updated in place rather than recreated, since it now changes every frame
    /// something is moving. It covers the camera's first cell and one more row and column than
    /// the view, because a gliding camera straddles two cells.
    /// </remarks>
    private void UploadLight()
    {
        var light = _style.LightTint;

        for (var row = 0; row < SpanCells; row++)
        {
            var mazeRow = _camera.Row + row;

            for (var column = 0; column < SpanCells; column++)
            {
                var mazeColumn = _camera.Column + column;
                var colour = Color.Black;

                if (mazeRow >= 0 && mazeRow < _rows && mazeColumn >= 0 && mazeColumn < _columns)
                {
                    var index = (mazeRow * _columns) + mazeColumn;
                    var brightness = Math.Clamp(_levels[index], 0f, 1f) * 255f;

                    // The hunter's light is colder: red and green give way a little to blue.
                    var cold = _glow[index];
                    var red = brightness * (1f - (0.20f * cold));
                    var green = brightness * (1f - (0.06f * cold));

                    colour = new Color(
                        (byte)Math.Clamp((int)(red * light.R / 255f), 0, 255),
                        (byte)Math.Clamp((int)(green * light.G / 255f), 0, 255),
                        (byte)Math.Clamp((int)(brightness * light.B / 255f), 0, 255),
                        (byte)255);
                }

                Raylib.ImageDrawPixel(ref _lightImage, column, row, colour);
            }
        }

        unsafe
        {
            Raylib.UpdateTexture(_light, _lightImage.Data);
        }
    }

    /// <summary>Returns a cell's eased light, 0 to 1; 0 off the floor.</summary>
    private float LevelAt(int row, int column) =>
        row < 0 || row >= _rows || column < 0 || column >= _columns || _levels.Length != _rows * _columns
            ? 0f
            : _levels[(row * _columns) + column];

    /// <summary>
    /// Returns whether a cell's floor and walls are drawn: it is seen or remembered, or it is
    /// still fading out of the light.
    /// </summary>
    private bool Drawn(GameState state, Cell cell) =>
        Known(state, cell) || LevelAt(cell.Row, cell.Column) > LightFloor;

    /// <summary>
    /// Returns the tint a cell's tiles are drawn with: white in the torch, the remembered grey
    /// out of it, and in between while it crosses from one to the other.
    /// </summary>
    private Color CellTint(Cell cell)
    {
        var seen = _seen.Length == _rows * _columns
            ? Math.Clamp(_seen[(cell.Row * _columns) + cell.Column], 0f, 1f)
            : 1f;

        return new Color(
            (byte)(Remembered.R + ((255 - Remembered.R) * seen)),
            (byte)(Remembered.G + ((255 - Remembered.G) * seen)),
            (byte)(Remembered.B + ((255 - Remembered.B) * seen)),
            (byte)255);
    }

    /// <summary>
    /// Draws the floor's weather: a handful of pixel particles whose motion and colour come from
    /// <see cref="FloorStyle.Ambient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layer is drawn after the lightmap, so it glows in the dark rather than being dimmed by
    /// it. Its particles are small and sparse: this is atmosphere, and the player still has to
    /// read a key at the edge of the lantern.
    /// </para>
    /// <para>
    /// Every position is a hash of the particle index and a per-floor seed, so the same floor's
    /// weather is the same on every replay without storing any particle state.
    /// </para>
    /// </remarks>
    private void DrawAmbience()
    {
        var time = Raylib.GetTime();
        var seed = TextHash(_themeId);
        var areaWidth = _viewCells * PixelLayout.CellSize;
        var areaHeight = _viewCells * PixelLayout.CellSize;
        var left = (float)PixelLayout.Pad;
        var top = (float)PixelLayout.HeaderHeight + PixelLayout.Pad;
        var colour = _style.AmbientColor;

        for (var index = 0; index < AmbientCount; index++)
        {
            var a = Frac(Math.Sin((index * 12.9898) + (seed * 0.0001) + 4.1414) * 43758.5453);
            var b = Frac(Math.Sin((index * 78.233) + (seed * 0.0002) + 1.618) * 12345.6789);
            var phase = (time * 0.7) + (index * 1.7);
            double x;
            double y;
            byte alpha = 150;
            var width = 1;
            var height = 1;

            switch (_style.Ambient)
            {
                case FloorAmbient.Motes:
                    x = left + (Frac(a + (time * 0.012)) * areaWidth);
                    y = top + (Frac(b - (time * 0.010)) * areaHeight);
                    alpha = (byte)(55 + (115 * (0.5 + (0.5 * Math.Sin(phase * 1.3)))));
                    break;

                case FloorAmbient.Bubbles:
                    x = left + (a * areaWidth) + (Math.Sin(phase * 1.4) * 3);
                    y = top + (Frac(b - (time * 0.045)) * areaHeight);
                    alpha = (byte)(80 + (70 * (0.5 + (0.5 * Math.Sin(phase)))));
                    width = 2;
                    height = 2;
                    break;

                case FloorAmbient.Spores:
                    x = left + (Frac(a + (time * 0.006) + (Math.Sin(phase * 0.6) * 0.02)) * areaWidth);
                    y = top + (Frac(b + (Math.Cos(phase * 0.5) * 0.02)) * areaHeight);
                    alpha = (byte)(50 + (110 * (0.5 + (0.5 * Math.Sin(phase * 1.7)))));
                    break;

                case FloorAmbient.Embers:
                    x = left + (Frac(a + (time * 0.02) + (Math.Sin(phase * 2.1) * 0.01)) * areaWidth);
                    y = top + (Frac(b - (time * 0.075)) * areaHeight);
                    alpha = (byte)(170 * (1.0 - Frac(b - (time * 0.075))));
                    break;

                case FloorAmbient.Drips:
                    var drop = Frac(b + (time * 0.45));
                    x = left + (a * areaWidth);
                    y = top + (drop * areaHeight);
                    alpha = (byte)(drop < 0.25 ? 190 : 0);
                    height = 3;
                    break;

                case FloorAmbient.Dust:
                    x = left + (Frac(a + (time * 0.007)) * areaWidth);
                    y = top + (Frac(b + (time * 0.004)) * areaHeight);
                    alpha = (byte)(35 + (55 * (0.5 + (0.5 * Math.Sin(phase * 0.8)))));
                    break;

                case FloorAmbient.Moths:
                    x = left + ((a * areaWidth) + (Math.Cos(phase * 1.9) * 14));
                    y = top + ((b * areaHeight) + (Math.Sin(phase * 2.6) * 10));
                    alpha = (byte)(100 + (90 * (0.5 + (0.5 * Math.Sin(phase * 3.1)))));
                    width = 2;
                    break;

                case FloorAmbient.Petals:
                    x = left + (Frac(a + (time * 0.015) + (Math.Sin(phase) * 0.02)) * areaWidth);
                    y = top + (Frac(b + (time * 0.030)) * areaHeight);
                    alpha = (byte)(80 + (80 * (0.5 + (0.5 * Math.Sin(phase * 1.1)))));
                    width = index % 2 == 0 ? 2 : 1;
                    height = index % 2 == 0 ? 1 : 2;
                    break;

                case FloorAmbient.Ash:
                    x = left + (Frac(a + (time * 0.018) + (Math.Sin(phase * 0.7) * 0.015)) * areaWidth);
                    y = top + (Frac(b + (time * 0.024)) * areaHeight);
                    alpha = (byte)(40 + (70 * (0.5 + (0.5 * Math.Sin(phase)))));
                    break;

                case FloorAmbient.Rain:
                    x = left + (Frac(a + (time * 0.10)) * areaWidth);
                    y = top + (Frac(b + (time * 0.55)) * areaHeight);
                    alpha = (byte)110;
                    height = 4;
                    break;

                case FloorAmbient.Static:
                    x = left + (a * areaWidth);
                    y = top + (b * areaHeight);
                    alpha = (byte)(Math.Sin((time * 8.0) + (index * 2.3)) > 0.25 ? 170 : 0);
                    break;

                default:
                    x = left + (Frac(a + (time * 0.01) + (Math.Cos(phase * 0.7) * 0.03)) * areaWidth);
                    y = top + (Frac(b + (time * 0.012) + (Math.Sin(phase * 0.5) * 0.03)) * areaHeight);
                    alpha = (byte)(70 + (130 * (0.5 + (0.5 * Math.Sin(phase * 2.0)))));
                    width = 2;
                    height = 2;
                    break;
            }

            if (alpha == 0)
            {
                continue;
            }

            // The weather hangs in the cave, not on the lens: it is shifted by the camera and
            // wrapped, so it scrolls with the floor as the explorer walks.
            x = left + Wrap(x - left - _cameraX, areaWidth);
            y = top + Wrap(y - top - _cameraY, areaHeight);

            Raylib.DrawRectangle(
                (int)Math.Round(x),
                (int)Math.Round(y),
                width,
                height,
                new Color(colour.R, colour.G, colour.B, alpha));
        }
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

        var hunters = state.Enemies;

        while (_enemyFacings.Count < hunters.Count)
        {
            _enemyFacings.Add(Direction.South);
        }

        for (var index = 0; index < hunters.Count; index++)
        {
            var hunter = hunters[index];
            _enemyFacings[index] = Facing(hunter.From, hunter.Cell, _enemyFacings[index]);
        }
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

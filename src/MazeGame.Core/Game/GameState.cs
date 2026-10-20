using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// The whole game as one object: where everything is, what the player may do, and how the
/// killer chases.
/// </summary>
/// <remarks>
/// <para>
/// This is the only type the UI needs. It owns the maze, the player, the killer, the exit,
/// the uncollected keys and the placed barriers, and it decides every rule. It never draws
/// anything and never reads the clock: time arrives through <see cref="Tick"/>.
/// </para>
/// <para>
/// <b>Nothing here can soft-lock the game.</b> Three invariants are enforced by design and
/// covered by tests:
/// </para>
/// <list type="bullet">
///   <item><description>The exit can be walked over while locked, so it can never trap the
///   player in a corridor. It simply does nothing until every key is collected.</description></item>
///   <item><description>Barriers raise the killer's cost and make it wait proportionally
///   longer, but never block a cell, so a route to the player always exists.</description></item>
///   <item><description>Keys, the exit and the killer are placed on distinct cells after the
///   maze exists, and the maze is connected, so everything is reachable.</description></item>
///   <item><description>The killer starts on a dead end. Since a perfect maze is a tree, a
///   killer in a corridor would cut off everything behind it; a dead end is a leaf, so it
///   never does.</description></item>
/// </list>
/// <para>
/// All randomness comes from the injected <see cref="IRandom"/>, so a seed reproduces the
/// same game exactly.
/// </para>
/// </remarks>
public sealed class GameState
{
    // The four directions, stored once so the visibility search does not allocate a fresh
    // array for every cell it visits.
    private static readonly Direction[] NeighbourDirections =
        [Direction.North, Direction.East, Direction.South, Direction.West];

    private readonly GameConfig _config;
    private readonly Grid _maze;
    private readonly IPathfinder _pathfinder;
    private readonly HashSet<Cell> _keys;
    private readonly HashSet<Cell> _barriers = [];
    private readonly HashSet<Cell> _visible = [];
    private readonly Cell _exit;

    private Cell _player;
    private Cell _enemy;
    private Cell _barrierCursor;
    private int _barriersRemaining;
    private GameStatus _status = GameStatus.InProgress;
    private double _elapsedSeconds;
    private bool _placingBarrier;

    // The killer's route to the player, cached so it is only recomputed when something
    // invalidates it (the player moves, or a barrier is placed).
    private IReadOnlyList<Cell>? _enemyRoute;

    // Index into _enemyRoute of the next cell the killer will step onto. Starts at 1
    // because index 0 is the cell the killer is already standing on.
    private int _nextStepIndex;

    /// <summary>
    /// Builds a game over <paramref name="maze"/>, placing the player, the killer, the exit
    /// and the keys on distinct cells.
    /// </summary>
    /// <param name="config">The tuning values for this game.</param>
    /// <param name="maze">
    /// An already generated maze whose size matches <paramref name="config"/>. It is passed
    /// in rather than generated here so tests can use small hand-built mazes.
    /// </param>
    /// <param name="pathfinder">Used by the killer to find its route to the player.</param>
    /// <param name="random">
    /// Source of randomness for placement. Pass the same instance that generated the maze
    /// to make the whole game reproducible from one seed.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the maze size does not match the config, when the config is invalid, or
    /// when the maze cannot hold the player, killer, exit and keys.
    /// </exception>
    public GameState(GameConfig config, Grid maze, IPathfinder pathfinder, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(maze);
        ArgumentNullException.ThrowIfNull(pathfinder);
        ArgumentNullException.ThrowIfNull(random);

        config.Validate();

        if (maze.Rows != config.Rows || maze.Columns != config.Columns)
        {
            throw new ArgumentException(
                $"The maze is {maze.Rows}x{maze.Columns} but the config describes {config.Rows}x{config.Columns}.",
                nameof(maze));
        }

        _config = config;
        _maze = maze;
        _pathfinder = pathfinder;
        _barriersRemaining = config.BarrierBudget;

        // Shuffle every cell once, then take the first unused ones. Because the list holds
        // each cell exactly once, the player, killer, exit and keys are guaranteed to be on
        // different cells without any retry loop.
        var cells = new List<Cell>(maze.CellCount);
        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                cells.Add(maze.GetCell(row, column));
            }
        }

        random.Shuffle(cells);

        _player = cells[0];
        _enemy = ChooseKillerStart(maze, _player, pathfinder);

        var available = new List<Cell>(cells.Count - 2);
        foreach (var cell in cells)
        {
            if (cell != _player && cell != _enemy)
            {
                available.Add(cell);
            }
        }

        // Config.Validate guarantees at least 3 + KeyCount cells, so the exit plus every key
        // fits in this list. The slicing below therefore cannot run out.
        _exit = available[0];
        _keys = new HashSet<Cell>(available.GetRange(1, config.KeyCount));

        RecomputeVisibility();
    }

    /// <summary>
    /// Chooses where the killer starts: the dead end that is furthest from the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dead-end rule matters for playability, not just atmosphere. A perfect maze is a
    /// tree, so a killer standing in a corridor sits on the only route between the player
    /// and everything behind it — and since the player cannot walk through the killer, those
    /// cells would be permanently unreachable. A dead-end cell has exactly one opening, so
    /// removing it from a tree leaves the rest connected: every key and the exit stay
    /// reachable without ever stepping on the killer.
    /// </para>
    /// <para>
    /// Distance is measured through the maze (a path search), not as the crow flies, so the
    /// killer starts genuinely far away. The scan is row-major, so ties resolve the same way
    /// on every run.
    /// </para>
    /// </remarks>
    private static Cell ChooseKillerStart(Grid maze, Cell player, IPathfinder pathfinder)
    {
        Cell? furthestDeadEnd = null;
        var furthestDistance = -1;

        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                var cell = maze.GetCell(row, column);

                // Three walls means exactly one opening: a dead end, and so a leaf.
                if (cell == player || cell.WallCount != 3)
                {
                    continue;
                }

                var distance = pathfinder.FindPath(maze, player, cell, _ => 1).Count;
                if (distance > furthestDistance)
                {
                    furthestDistance = distance;
                    furthestDeadEnd = cell;
                }
            }
        }

        // A maze with no dead ends (for example a closed loop, which the generator never
        // makes but a hand-built test maze can) has no safe leaf to use. Fall back to the
        // diagonally opposite corner: still never the player's own cell, because a playable
        // maze has at least three cells.
        return furthestDeadEnd ?? maze.GetCell(
            maze.Rows - 1 - player.Row,
            maze.Columns - 1 - player.Column);
    }

    /// <summary>
    /// Gets the settings this game was built with.
    /// </summary>
    public GameConfig Config => _config;

    /// <summary>
    /// Gets the maze being played. Read it for walls; do not expect it to change.
    /// </summary>
    public Grid Maze => _maze;

    /// <summary>
    /// Gets the cell the player currently occupies.
    /// </summary>
    public Cell Player => _player;

    /// <summary>
    /// Gets the cell the killer currently occupies.
    /// </summary>
    public Cell Enemy => _enemy;

    /// <summary>
    /// Gets the cell that wins the game once every key is collected.
    /// </summary>
    public Cell Exit => _exit;

    /// <summary>
    /// Gets the keys the player has not collected yet. The set is read-only; collect a key
    /// by walking onto its cell.
    /// </summary>
    public IReadOnlySet<Cell> Keys => _keys;

    /// <summary>
    /// Gets the cells holding a player-placed barrier.
    /// </summary>
    public IReadOnlySet<Cell> Barriers => _barriers;

    /// <summary>
    /// Gets how many keys are still to be collected.
    /// </summary>
    public int KeysRemaining => _keys.Count;

    /// <summary>
    /// Gets how many keys the player has collected so far.
    /// </summary>
    public int KeysCollected => _config.KeyCount - _keys.Count;

    /// <summary>
    /// Gets how many barriers the player may still place.
    /// </summary>
    public int BarriersRemaining => _barriersRemaining;

    /// <summary>
    /// Gets a value indicating whether the exit can be used. It opens once every key is
    /// collected, and it can still be walked over while locked.
    /// </summary>
    public bool IsExitUnlocked => _keys.Count == 0;

    /// <summary>
    /// Gets the current outcome of the game.
    /// </summary>
    public GameStatus Status => _status;

    /// <summary>
    /// Gets a value indicating whether the player is currently aiming a barrier. While this
    /// is true the game clock is paused, the player cannot walk, and the directional keys
    /// move <see cref="BarrierCursor"/> instead.
    /// </summary>
    public bool IsPlacingBarrier => _placingBarrier;

    /// <summary>
    /// Gets the cell the barrier would be placed on, or the player's cell when not aiming.
    /// </summary>
    public Cell BarrierCursor => _barrierCursor;

    /// <summary>
    /// Determines whether the player can currently see <paramref name="cell"/>.
    /// </summary>
    /// <param name="cell">The cell to test.</param>
    /// <returns>
    /// <see langword="true"/> when the cell is the player's own cell or lies within
    /// <see cref="GameConfig.VisionRadius"/> steps through open passages.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Visibility is a <b>rule</b>, not a drawing decision: it says what the player is
    /// allowed to know, so it lives here and the renderers only decide how to darken what
    /// they are told. That is also why it is a query rather than a stored per-cell flag the
    /// UI could set.
    /// </para>
    /// <para>
    /// Sight travels along open passages, so a wall really does block it: a cell on the far
    /// side of a wall stays hidden even when it is one step away. Distance is counted in
    /// steps of walking, which makes the radius a torch range rather than a circle drawn
    /// through rock.
    /// </para>
    /// </remarks>
    public bool IsVisible(Cell cell) => _visible.Contains(cell);

    /// <summary>
    /// Tries to move the player one cell in <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">The direction to step in.</param>
    /// <returns>
    /// <see langword="true"/> when the player moved (even if that ended the game);
    /// <see langword="false"/> when a wall blocked the step or the game is already over.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Walls are the only thing that can block the player: the outer boundary is walled by
    /// construction, so there is no way to walk out of the maze. The player is not slowed by
    /// their own barriers — barriers exist to delay the killer.
    /// </para>
    /// <para>
    /// A step onto the exit wins only when every key is collected; while locked the exit is
    /// an ordinary cell, which is what keeps a corridor from being blocked by it.
    /// Stepping onto the killer loses.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="direction"/> is not one of the four directions.
    /// </exception>
    public bool TryMove(Direction direction)
    {
        // While aiming a barrier the directional keys move the cursor, not the player, and
        // the game clock is frozen, so a move here would be ambiguous.
        if (_status != GameStatus.InProgress || _placingBarrier)
        {
            return false;
        }

        // HasWall validates the position and the direction, and an outer wall always reports
        // true, so a step out of the maze is rejected here rather than indexing off the grid.
        if (_maze.HasWall(_player.Row, _player.Column, direction))
        {
            return false;
        }

        var (rowDelta, columnDelta) = direction.Delta();
        _player = _maze.GetCell(_player.Row + rowDelta, _player.Column + columnDelta);

        // The killer was chasing the old destination; its cached route is now stale.
        InvalidateEnemyRoute();

        // What the player can see is anchored to their cell, so it moves with them.
        RecomputeVisibility();

        // Collecting is idempotent: stepping on an already collected key cell changes nothing.
        _keys.Remove(_player);

        if (_player == _enemy)
        {
            _status = GameStatus.Caught;
            return true;
        }

        if (_player == _exit && IsExitUnlocked)
        {
            _status = GameStatus.Won;
        }

        return true;
    }

    /// <summary>
    /// Starts aiming a barrier. The game clock pauses until the placement is confirmed or
    /// cancelled.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when aiming started; <see langword="false"/> when there is no
    /// barrier left, the game is over, or an aim is already in progress.
    /// </returns>
    /// <remarks>
    /// Aiming is a rule, not a UI mode: while it is active <see cref="Tick(double)"/> does
    /// nothing, so the killer is frozen and the player can study the map without being
    /// punished for the time spent deciding where to put the trap.
    /// </remarks>
    public bool BeginBarrierPlacement()
    {
        if (_status != GameStatus.InProgress || _placingBarrier || _barriersRemaining <= 0)
        {
            return false;
        }

        _placingBarrier = true;
        _barrierCursor = _player;
        return true;
    }

    /// <summary>
    /// Moves the barrier cursor one cell in <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">The direction to move the aim in.</param>
    /// <returns>
    /// <see langword="true"/> when the cursor moved; <see langword="false"/> when it would
    /// leave the maze or no aim is in progress.
    /// </returns>
    /// <remarks>
    /// The cursor ignores walls: it is a targeting reticle, not the player, so a barrier can
    /// be dropped anywhere the player has decided to put it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="direction"/> is not one of the four directions.
    /// </exception>
    public bool MoveBarrierCursor(Direction direction)
    {
        if (!_placingBarrier)
        {
            return false;
        }

        var (rowDelta, columnDelta) = direction.Delta();
        var row = _barrierCursor.Row + rowDelta;
        var column = _barrierCursor.Column + columnDelta;

        if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns)
        {
            return false;
        }

        _barrierCursor = _maze.GetCell(row, column);
        return true;
    }

    /// <summary>
    /// Places a barrier on the cell the cursor is on and ends the aim.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the barrier was placed; <see langword="false"/> when no
    /// aim is in progress or that cell already holds a barrier.
    /// </returns>
    /// <remarks>
    /// A refusal spends nothing and leaves the aim active, so a player who aims at an
    /// occupied cell can simply move the cursor and try again.
    /// </remarks>
    public bool ConfirmBarrierPlacement()
    {
        if (!_placingBarrier || _barriersRemaining <= 0)
        {
            return false;
        }

        // Add returns false when the cell already has a barrier; the budget must not be
        // spent twice on the same cell.
        if (!_barriers.Add(_barrierCursor))
        {
            return false;
        }

        _barriersRemaining--;
        _placingBarrier = false;
        InvalidateEnemyRoute();
        return true;
    }

    /// <summary>
    /// Stops aiming without placing anything or spending a barrier.
    /// </summary>
    /// <remarks>Does nothing when no aim is in progress.</remarks>
    public void CancelBarrierPlacement() => _placingBarrier = false;

    /// <summary>
    /// Advances the game clock by <paramref name="deltaSeconds"/> and lets the killer move
    /// when a step is due.
    /// </summary>
    /// <param name="deltaSeconds">
    /// Seconds since the previous call. Must be finite and non-negative.
    /// </param>
    /// <remarks>
    /// <para>
    /// At most one killer step happens per call, and at most one step's worth of time is
    /// banked. A long frame (a dragged window, a suspended laptop) therefore cannot teleport
    /// the killer across the maze.
    /// </para>
    /// <para>
    /// Entering a normal cell takes <see cref="GameConfig.EnemyStepSeconds"/>. Entering a
    /// barriered cell takes that long multiplied by <see cref="GameConfig.ObstacleCost"/>, so
    /// a barrier is a real delay and not just a different route. While the player is aiming
    /// a barrier the clock is frozen. After the game is over this method does nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="deltaSeconds"/> is negative, NaN or infinite.
    /// </exception>
    public void Tick(double deltaSeconds)
    {
        if (!(deltaSeconds >= 0) || double.IsInfinity(deltaSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaSeconds),
                deltaSeconds,
                "Elapsed time must be a finite, non-negative number of seconds.");
        }

        if (_status != GameStatus.InProgress || _placingBarrier)
        {
            return;
        }

        var interval = SecondsUntilNextStep();

        _elapsedSeconds = Math.Min(_elapsedSeconds + deltaSeconds, interval);
        if (_elapsedSeconds < interval)
        {
            return;
        }

        _elapsedSeconds -= interval;
        AdvanceEnemy();
    }

    /// <summary>
    /// Returns how long the killer needs before its next step: one normal step, times the
    /// cost of the cell it is about to enter.
    /// </summary>
    /// <remarks>
    /// Charging the delay before the move (rather than after) is what makes a barrier read
    /// correctly: the killer stalls at the barrier, then forces its way through, instead of
    /// appearing to pass it instantly and then idling on the other side.
    /// </remarks>
    private double SecondsUntilNextStep()
    {
        var route = EnemyRouteToPlayer();

        if (route.Count <= _nextStepIndex)
        {
            return _config.EnemyStepSeconds;
        }

        return _config.EnemyStepSeconds * CostOfEntering(route[_nextStepIndex]);
    }

    /// <summary>
    /// Moves the killer one cell along its route to the player, ending the game if that
    /// brings it onto the player's cell.
    /// </summary>
    private void AdvanceEnemy()
    {
        var route = EnemyRouteToPlayer();

        // A single-cell route means the killer is already on the player's cell, which has
        // already ended the game; nothing left to do.
        if (route.Count <= 1 || _nextStepIndex >= route.Count)
        {
            return;
        }

        _enemy = route[_nextStepIndex];
        _nextStepIndex++;

        // The rest of the cached route is still the best way to a player who has not moved,
        // so it is reused rather than recomputed every step.
        if (_enemy == _player)
        {
            _status = GameStatus.Caught;
        }
    }

    /// <summary>
    /// Returns the killer's route to the player, recomputing it only when it was invalidated.
    /// </summary>
    private IReadOnlyList<Cell> EnemyRouteToPlayer()
    {
        if (_enemyRoute is null)
        {
            _enemyRoute = _pathfinder.FindPath(_maze, _enemy, _player, CostOfEntering);
            _nextStepIndex = 1;
        }

        return _enemyRoute;
    }

    /// <summary>
    /// Rebuilds the set of cells the player can see: a breadth-first walk out from the
    /// player's cell through open passages, stopping at <see cref="GameConfig.VisionRadius"/>.
    /// </summary>
    /// <remarks>
    /// Breadth-first search is the right fit because it visits cells in order of walking
    /// distance, so the radius is an exact cut-off: every cell reached is visible and no
    /// cell beyond it is explored. Walls are respected simply by only crossing passages,
    /// which is what keeps sight from leaking through the maze's walls.
    /// </remarks>
    private void RecomputeVisibility()
    {
        _visible.Clear();
        _visible.Add(_player);

        if (_config.VisionRadius == 0)
        {
            return;
        }

        var frontier = new Queue<(Cell Cell, int Distance)>();
        frontier.Enqueue((_player, 0));

        while (frontier.Count > 0)
        {
            var (cell, distance) = frontier.Dequeue();
            if (distance == _config.VisionRadius)
            {
                continue;
            }

            foreach (var direction in NeighbourDirections)
            {
                if (_maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbour = _maze.GetCell(cell.Row + rowDelta, cell.Column + columnDelta);

                // Add returns false for a cell already found on a shorter or equal route.
                if (!_visible.Add(neighbour))
                {
                    continue;
                }

                frontier.Enqueue((neighbour, distance + 1));
            }
        }
    }

    /// <summary>
    /// The killer's cost to enter a cell: expensive for a barriered cell, normal otherwise.
    /// The value is always finite, which is what guarantees a route always exists.
    /// </summary>
    private int CostOfEntering(Cell cell) =>
        _barriers.Contains(cell) ? _config.ObstacleCost : 1;

    /// <summary>
    /// Marks the cached killer route as out of date; the next step recomputes it.
    /// </summary>
    private void InvalidateEnemyRoute()
    {
        _enemyRoute = null;
        _nextStepIndex = 0;
    }
}

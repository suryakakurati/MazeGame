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
/// A catch costs a heart rather than ending the run: the killer is thrown to a distant dead
/// end, the player gets a short window of invulnerability, and every key, barrier and wisp on
/// the floor is kept. Only the last heart ends the run.
/// </para>
/// <para>
/// <b>Nothing here can soft-lock the game.</b> Three invariants are enforced by design and
/// covered by tests:
/// </para>
/// <list type="bullet">
///   <item><description>The exit can be walked over while locked, so it can never trap the
///   player in a corridor. It simply does nothing until every key is collected.</description></item>
///   <item><description>A barrier is sealed for a few seconds, which makes the killer treat
///   that cell as a wall and look for another way, and afterwards a speed bump that is
///   expensive but passable. A sealed barrier is never a permanent wall: when it is the only
///   route the killer tears it apart, so a route always exists.</description></item>
///   <item><description>A wisp only changes <i>where</i> the killer walks, never whether it
///   can reach the player, so it cannot affect reachability at all.</description></item>
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

    /// <summary>How many steps of cave a glowing wisp lights up around itself.</summary>
    private const int LureGlowRadius = 1;

    /// <summary>
    /// How full the stamina bar must be before sprinting is available again after running dry.
    /// Without this, an empty bar would let the player sprint one step at a time.
    /// </summary>
    private const double SprintRecoveryThreshold = 0.25;

    private readonly GameConfig _config;
    private readonly Grid _maze;
    private readonly IPathfinder _pathfinder;
    private readonly HashSet<Cell> _keys;
    private readonly HashSet<Cell> _barriers = [];
    private readonly Dictionary<Cell, double> _barrierSeals = [];
    private readonly HashSet<Cell> _visible = [];
    private readonly HashSet<Cell> _enemyGlow = [];
    private readonly Cell _exit;

    private Cell _player;
    private Cell _enemy;
    private Cell _aimCursor;
    private Cell? _lure;
    private double _lureSecondsLeft;
    private int _barriersRemaining;
    private int _luresRemaining;
    private int _hearts;
    private readonly int _maxHearts;
    private double _invulnerableSeconds;
    private double _stunSecondsLeft;
    private GameStatus _status = GameStatus.InProgress;
    private double _elapsedSeconds;
    private PlacementMode _aiming;

    // Timed steps. The player's cell changes the instant a step is accepted; this cooldown only
    // decides when the next step may start, and the renderer slides the figure across.
    private Cell _playerFrom;
    private double _playerStepLeft;
    private double _playerStepTotal;
    private Direction? _bufferedDirection;

    // Sprint: a stamina bar, not a card. Exhausting it requires a partial refill before it can be
    // used again, so the bar reads as a resource rather than a flickering toggle.
    private bool _sprintRequested;
    private bool _sprintExhausted;
    private double _sprintStaminaLeft;

    // Momentum: a run rule a card switches on. Straight steps earn speed up to the cap, and the
    // first step after a turn is slower for a moment afterwards. With the defaults (all zero) none
    // of this runs and no timing changes, which is what keeps the rule tests' feel identical.
    private Direction? _lastStepDirection;
    private int _straightSteps;
    private double _turnPenaltyLeft;

    // The killer's last step, kept for the same visual slide. Its step length varies (a barriered
    // cell costs several steps' worth of time), so the duration is recorded per step.
    private Cell _enemyFrom;
    private double _enemyStepLeft;
    private double _enemyStepTotal;

    // The killer's route to its chosen target, cached so it is only recomputed when something
    // invalidates it (the player moves, an item is placed, a seal cracks, or a wisp goes).
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
        _luresRemaining = config.LureCount;
        _hearts = config.StartingHearts;
        _maxHearts = config.MaxHearts;
        _sprintStaminaLeft = config.SprintStaminaSeconds;

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

        // Nothing is mid-step at the start, so both figures are drawn exactly on their cells.
        _playerFrom = _player;
        _enemyFrom = _enemy;

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
    /// Gets the cells holding a player-placed barrier, whether it is still sealed or already a
    /// spent husk.
    /// </summary>
    public IReadOnlySet<Cell> Barriers => _barriers;

    /// <summary>
    /// Gets the barriers that are still sealed, mapped to the seconds the seal has left.
    /// </summary>
    /// <remarks>
    /// A sealed barrier counts as a wall for the killer, so a renderer draws these differently
    /// from the cracked ones and can show how much of the seal is left. A cell in
    /// <see cref="Barriers"/> that is missing here is spent: passable, but expensive.
    /// </remarks>
    public IReadOnlyDictionary<Cell, double> BarrierSeals => _barrierSeals;

    /// <summary>
    /// Gets the cell holding the glowing wisp, or <see langword="null"/> when none is active.
    /// </summary>
    /// <remarks>
    /// The wisp is a decoy, not a trap: while it is the cheaper destination by Dijkstra's
    /// measure, the killer walks towards it instead of the player. It never blocks anything and
    /// never hides the player, so the game stays winnable and losable exactly as before.
    /// </remarks>
    public Cell? Lure => _lure;

    /// <summary>
    /// Gets how many seconds the active wisp has left to glow, or 0 when none is active.
    /// </summary>
    public double LureSecondsRemaining => _lure is null ? 0.0 : _lureSecondsLeft;

    /// <summary>
    /// Gets how many wisps the player may still place.
    /// </summary>
    public int LuresRemaining => _luresRemaining;

    /// <summary>
    /// Gets a value indicating whether a wisp is currently glowing.
    /// </summary>
    public bool IsLureActive => _lure is not null;

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
    /// Gets how many hearts the player has left on this floor.
    /// </summary>
    /// <remarks>
    /// A catch spends one heart and resets the chase; reaching zero ends the run. Hearts are
    /// never regenerated by the floor itself, only by cards and by finishing a floor with
    /// hearts to spare.
    /// </remarks>
    public int Hearts => _hearts;

    /// <summary>
    /// Gets the most hearts this floor allows, which cards may raise later.
    /// </summary>
    public int MaxHearts => _maxHearts;

    /// <summary>
    /// Gets a value indicating whether the player is still inside the post-catch grace window.
    /// </summary>
    /// <remarks>
    /// While this is true another catch cannot happen, and the killer waits at the player's cell
    /// instead of moving onto it. It exists so one mistake costs exactly one heart.
    /// </remarks>
    public bool IsInvulnerable => _invulnerableSeconds > 0;

    /// <summary>
    /// Gets how many seconds of post-catch invulnerability remain, or 0 when there is none.
    /// </summary>
    public double InvulnerabilitySecondsRemaining => _invulnerableSeconds;

    /// <summary>
    /// Gets a value indicating whether the killer is still stunned by the last catch.
    /// </summary>
    /// <remarks>
    /// A stunned killer neither moves nor builds up its next step, so the stun is a genuine
    /// window in which the player can put distance between the two of them.
    /// </remarks>
    public bool IsEnemyStunned => _stunSecondsLeft > 0;

    /// <summary>
    /// Gets how many seconds of the killer's post-catch stun remain, or 0 when it is awake.
    /// </summary>
    public double EnemyStunSecondsRemaining => _stunSecondsLeft;

    /// <summary>
    /// Gets what the player is aiming, or <see cref="PlacementMode.None"/> when they are just
    /// walking.
    /// </summary>
    public PlacementMode AimMode => _aiming;

    /// <summary>
    /// Gets a value indicating whether the player is aiming anything. While this is true the
    /// game clock is paused, the player cannot walk, and the directional keys move
    /// <see cref="AimCursor"/> instead.
    /// </summary>
    public bool IsAiming => _aiming != PlacementMode.None;

    /// <summary>
    /// Gets a value indicating whether the player is currently aiming a barrier.
    /// </summary>
    public bool IsPlacingBarrier => _aiming == PlacementMode.Barrier;

    /// <summary>
    /// Gets a value indicating whether the player is currently aiming a wisp.
    /// </summary>
    public bool IsPlacingLure => _aiming == PlacementMode.Lure;

    /// <summary>
    /// Gets the cell the aimed item would be placed on, or the player's cell when not aiming.
    /// </summary>
    public Cell AimCursor => _aimCursor;

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
    /// <para>
    /// The killer carries its own light. Once it is within
    /// <see cref="GameConfig.EnemyAuraDistance"/> steps, the cells within
    /// <see cref="GameConfig.EnemyAuraRadius"/> of it also count as visible — even when the
    /// corridor between the two of you is pitch black — so its approach can be seen coming.
    /// </para>
    /// </remarks>
    public bool IsVisible(Cell cell) => _visible.Contains(cell);

    /// <summary>
    /// Gets the vision radius in force right now: the floor's radius, less what a burning wisp
    /// costs.
    /// </summary>
    /// <remarks>
    /// The wisp is a beacon, and a card may make watching it expensive. The penalty is subtracted
    /// only while the wisp is actually burning, and the visible set is rebuilt when it starts and
    /// when it clears, so the cost comes and goes with the glow. It never reduces sight below the
    /// player's own cell.
    /// </remarks>
    public int EffectiveVisionRadius =>
        Math.Max(0, _config.VisionRadius - (_lure is not null ? _config.LureVisionPenalty : 0));

    /// <summary>Gets a value indicating whether a step's cooldown is still running.</summary>
    public bool IsPlayerStepping => _playerStepLeft > 0;

    /// <summary>Gets the cell the player stepped from.</summary>
    /// <remarks>
    /// The rule-side position is <see cref="Player"/>, which changes at the start of a step. The
    /// window deliberately does <b>not</b> draw between the two cells: the explorer snaps to its
    /// tile so one press is one tile and the player can count them. These two members exist for a
    /// walk-cycle animation and for tests that measure the cadence.
    /// </remarks>
    public Cell PreviousPlayer => _playerFrom;

    /// <summary>Gets how far through its step the player is, from 0 to 1.</summary>
    /// <remarks>1 means "standing still on a cell". This is the step's clock, not a position: the
    /// window draws the explorer on its cell and uses this only to pick a walk frame.</remarks>
    public double PlayerStepFraction => StepFraction(_playerStepLeft, _playerStepTotal);

    /// <summary>Gets the cell the killer stepped from, for the renderer's slide.</summary>
    public Cell PreviousEnemy => _enemyFrom;

    /// <summary>Gets how far through its step the killer's figure is, from 0 to 1.</summary>
    public double EnemyStepFraction => StepFraction(_enemyStepLeft, _enemyStepTotal);

    /// <summary>Gets a value indicating whether the player is sprinting right now.</summary>
    /// <remarks>
    /// Sprinting needs a request from the input, stamina left, a timed game (an instant-step floor
    /// cannot sprint), and a live, un-aimed run.
    /// </remarks>
    public bool IsSprinting =>
        _sprintRequested &&
        !_sprintExhausted &&
        _sprintStaminaLeft > 0 &&
        _config.PlayerStepSeconds > 0 &&
        _config.SprintStaminaSeconds > 0 &&
        _status == GameStatus.InProgress &&
        _aiming == PlacementMode.None;

    /// <summary>Gets the fraction of the stamina bar that is left, from 0 to 1.</summary>
    /// <remarks>One when sprinting is disabled for the floor, so a HUD can hide the bar.</remarks>
    public double SprintStaminaFraction =>
        _config.SprintStaminaSeconds <= 0
            ? 1.0
            : Math.Clamp(_sprintStaminaLeft / _config.SprintStaminaSeconds, 0, 1);

    /// <summary>
    /// Asks for sprinting to be on or off. The input calls this every frame while Shift is held.
    /// </summary>
    /// <param name="sprinting">Whether the player is holding sprint.</param>
    /// <remarks>
    /// This is a request, not a command: <see cref="IsSprinting"/> decides whether it is granted, so
    /// stamina, aiming and an instant-step floor cannot be bypassed by the input layer.
    /// </remarks>
    public void SetSprinting(bool sprinting) => _sprintRequested = sprinting;

    /// <summary>
    /// Gets the cells that are visible only because the killer is lighting them.
    /// </summary>
    /// <remarks>
    /// A renderer uses this to tint the killer's aura differently from the player's own torch;
    /// the game itself only cares that those cells are visible. The set is empty while the
    /// killer is further away than <see cref="GameConfig.EnemyAuraDistance"/>.
    /// </remarks>
    public IReadOnlySet<Cell> EnemyGlow => _enemyGlow;

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
        // While aiming, the directional keys move the cursor, not the player, and the game
        // clock is frozen, so a move here would be ambiguous.
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None)
        {
            return false;
        }

        // Timed steps: while the last step is still cooling down, this press is remembered for the
        // end of it instead of being queued. One input only, so mashing cannot outrun the cadence,
        // and a hold reads as continuous walking.
        if (_playerStepLeft > 0)
        {
            _bufferedDirection = direction;
            return false;
        }

        return Step(direction);
    }

    /// <summary>
    /// Performs one step: the whole of the movement rule, unchanged, plus the cooldown that gates
    /// the next one.
    /// </summary>
    /// <param name="direction">The direction to step in.</param>
    /// <returns><see langword="true"/> when the player moved, even if that ended the game.</returns>
    private bool Step(Direction direction)
    {
        // HasWall validates the position and the direction, and an outer wall always reports
        // true, so a step out of the maze is rejected here rather than indexing off the grid.
        if (_maze.HasWall(_player.Row, _player.Column, direction))
        {
            return false;
        }

        var (rowDelta, columnDelta) = direction.Delta();
        var destination = _maze.GetCell(_player.Row + rowDelta, _player.Column + columnDelta);

        // Walking into the killer is a mistake, not a free pass. During the post-catch window the
        // creature cannot be shoved through, so the step is simply refused rather than dealing
        // a second hit for the same mistake.
        var ontoKiller = destination == _enemy;
        if (ontoKiller && _invulnerableSeconds > 0)
        {
            return false;
        }

        _playerFrom = _player;
        _player = destination;

        // The step is accepted now, so the next one waits. A zero step time leaves the cooldown at
        // zero, which is exactly the instant movement the rule tests were written against.
        TrackMomentum(direction);
        _playerStepLeft = NextPlayerStepSeconds();
        _playerStepTotal = _playerStepLeft;

        // The killer was chasing the old destination; its cached route is now stale.
        InvalidateEnemyRoute();

        // What the player can see is anchored to their cell, so it moves with them.
        RecomputeVisibility();

        // Collecting is idempotent: stepping on an already collected key cell changes nothing.
        _keys.Remove(_player);

        if (ontoKiller)
        {
            TakeHit();
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
    public bool BeginBarrierPlacement() => BeginAim(PlacementMode.Barrier);

    /// <summary>
    /// Starts aiming a wisp. The game clock pauses until the placement is confirmed or
    /// cancelled, exactly as it does for a barrier.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when aiming started; <see langword="false"/> when no wisp is
    /// left, one is already glowing, the game is over, or an aim is already in progress.
    /// </returns>
    /// <remarks>
    /// Only one wisp may glow at a time. That is a deliberate limit: two decoys would let the
    /// player split the killer's attention without ever being chased, whereas one turns into a
    /// decision about <i>when</i> to spend it.
    /// </remarks>
    public bool BeginLurePlacement() => BeginAim(PlacementMode.Lure);

    /// <summary>
    /// Starts aiming <paramref name="mode"/>, placing the cursor on the player.
    /// </summary>
    /// <returns><see langword="true"/> when the aim started.</returns>
    /// <remarks>
    /// The single gate for both placeables, so the "game over", "already aiming" and
    /// "nothing left to spend" rules cannot drift apart.
    /// </remarks>
    private bool BeginAim(PlacementMode mode)
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None)
        {
            return false;
        }

        var available = mode switch
        {
            PlacementMode.Barrier => _barriersRemaining > 0,
            PlacementMode.Lure => _luresRemaining > 0 && _lure is null,
            _ => false,
        };

        if (!available)
        {
            return false;
        }

        _aiming = mode;
        _aimCursor = _player;
        return true;
    }

    /// <summary>
    /// Moves the aiming cursor one cell in <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">The direction to move the aim in.</param>
    /// <returns>
    /// <see langword="true"/> when the cursor moved; <see langword="false"/> when it would
    /// leave the maze or no aim is in progress.
    /// </returns>
    /// <remarks>
    /// The cursor ignores walls: it is a targeting reticle, not the player, so a barrier or a
    /// wisp can be dropped anywhere the player has decided to put it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="direction"/> is not one of the four directions.
    /// </exception>
    public bool MoveAimCursor(Direction direction)
    {
        if (_aiming == PlacementMode.None)
        {
            return false;
        }

        var (rowDelta, columnDelta) = direction.Delta();
        var row = _aimCursor.Row + rowDelta;
        var column = _aimCursor.Column + columnDelta;

        if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns)
        {
            return false;
        }

        _aimCursor = _maze.GetCell(row, column);
        return true;
    }

    /// <summary>
    /// Places whatever is being aimed on the cursor's cell and ends the aim.
    /// </summary>
    /// <returns><see langword="true"/> when it was placed; <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// A refusal spends nothing and leaves the aim active, so a player who aims at an
    /// occupied cell can simply move the cursor and try again.
    /// </remarks>
    public bool ConfirmPlacement() => _aiming switch
    {
        PlacementMode.Barrier => ConfirmBarrier(),
        PlacementMode.Lure => ConfirmLure(),
        _ => false,
    };

    /// <summary>
    /// Places a barrier on the cursor's cell at the start of its seal, and ends the aim.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the barrier was placed; <see langword="false"/> when no
    /// barrier is being aimed, the budget is gone, or that cell already holds a barrier.
    /// </returns>
    /// <remarks>
    /// A refusal spends nothing and leaves the aim active, so a player who aims at an
    /// occupied cell can simply move the cursor and try again.
    /// </remarks>
    private bool ConfirmBarrier()
    {
        if (!IsPlacingBarrier || _barriersRemaining <= 0)
        {
            return false;
        }

        // Add returns false when the cell already has a barrier; the budget must not be
        // spent twice on the same cell.
        if (!_barriers.Add(_aimCursor))
        {
            return false;
        }

        // The seal is what makes a barrier a wall rather than a toll. A configured seal of
        // zero means "never sealed", so the cell goes straight to its spent price.
        if (_config.BarrierSealSeconds > 0)
        {
            _barrierSeals[_aimCursor] = _config.BarrierSealSeconds;
        }

        _barriersRemaining--;
        _aiming = PlacementMode.None;
        InvalidateEnemyRoute();
        return true;
    }

    /// <summary>
    /// Lights a wisp on the cursor's cell and ends the aim.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the wisp was placed; <see langword="false"/> when no wisp
    /// is being aimed, one is already glowing, or the cursor is on the player's or the killer's
    /// own cell.
    /// </returns>
    /// <remarks>
    /// A wisp on the player's own cell would be a decoy at the exact place the killer is
    /// already heading, so it is refused rather than silently spending a charge for nothing. A
    /// wisp under the killer's feet is refused for a sharper reason: the killer's route to it
    /// would be zero steps long, so it would sit still for the wisp's whole lifetime. That is a
    /// six-second freeze for one charge, which the barrier does not even offer.
    /// </remarks>
    private bool ConfirmLure()
    {
        if (!IsPlacingLure || _luresRemaining <= 0 || _lure is not null ||
            _aimCursor == _player || _aimCursor == _enemy)
        {
            return false;
        }

        _lure = _aimCursor;
        _lureSecondsLeft = _config.LureSeconds;
        _luresRemaining--;
        _aiming = PlacementMode.None;
        InvalidateEnemyRoute();

        // The wisp lights its own cell, so what the player can see changes at once.
        RecomputeVisibility();
        return true;
    }

    /// <summary>
    /// Stops aiming without placing anything or spending a charge.
    /// </summary>
    /// <remarks>Does nothing when no aim is in progress.</remarks>
    public void CancelPlacement() => _aiming = PlacementMode.None;

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
    /// Entering a normal cell takes <see cref="GameConfig.EnemyStepSeconds"/>. Entering a spent
    /// barriered cell takes that long multiplied by <see cref="GameConfig.ObstacleCost"/>, and
    /// tearing a sealed one apart takes <see cref="GameConfig.BarrierBreakSeconds"/> on its own,
    /// so an obstacle is a real delay and not just a different route. Barrier seals and the
    /// wisp age in the same wall-clock time (a long frame still ages them fully, even though it
    /// cannot bank more than one step), and everything is frozen while the player is aiming.
    /// After the game is over this method does nothing.
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

        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None)
        {
            return;
        }

        // Seals and the wisp age in wall-clock time, so a frame long enough to be clamped below
        // still counts fully against them.
        AdvanceTimers(deltaSeconds);

        // A press that arrived during the last step is spent here, before the killer acts. It can
        // never chain: the buffered step starts its own cooldown, so one input is one step.
        ResolveBufferedStep();

        // That step may have won the floor or lost the last heart; either way the killer has no
        // business taking another step in the same frame.
        if (_status != GameStatus.InProgress)
        {
            return;
        }

        // A stunned killer neither moves nor banks progress towards its next step: the stun has
        // to be a real window, not a step that lands the instant the player stops running.
        if (_stunSecondsLeft > 0)
        {
            _elapsedSeconds = 0;
            return;
        }

        var interval = SecondsUntilNextStep();

        _elapsedSeconds = Math.Min(_elapsedSeconds + deltaSeconds, interval);
        if (_elapsedSeconds < interval)
        {
            return;
        }

        _elapsedSeconds -= interval;
        AdvanceEnemy(interval);
    }

    /// <summary>
    /// Spends a buffered direction once the last step's cooldown has run out.
    /// </summary>
    /// <remarks>
    /// The buffer is cleared whether or not the step is legal: a press aimed at a wall should not
    /// sit there and fire later when the player happens to face an opening.
    /// </remarks>
    private void ResolveBufferedStep()
    {
        if (_playerStepLeft > 0 || _bufferedDirection is not { } direction)
        {
            return;
        }

        _bufferedDirection = null;
        Step(direction);
    }

    /// <summary>
    /// Returns how long the next player step must wait: the floor's step time, shortened by sprint
    /// when it is granted.
    /// </summary>
    private double NextPlayerStepSeconds()
    {
        if (_config.PlayerStepSeconds <= 0)
        {
            return 0;
        }

        var step = _config.PlayerStepSeconds;

        if (_config.MomentumMaxBonus > 0 && _config.MomentumRampPerStep > 0)
        {
            var bonus = Math.Min(_config.MomentumMaxBonus, _straightSteps * _config.MomentumRampPerStep);
            step /= 1.0 + bonus;

            if (_turnPenaltyLeft > 0)
            {
                step *= 1.0 + _config.TurnPenaltyScale;
            }
        }

        return IsSprinting ? step / Math.Max(1.0, _config.SprintMultiplier) : step;
    }

    /// <summary>
    /// Updates the straight-line run counter and starts the turn penalty when the player turns.
    /// </summary>
    /// <param name="direction">The direction of the step that was just accepted.</param>
    /// <remarks>
    /// This is a rule about cadence only: the cell has already changed by the time it runs, and
    /// with momentum switched off (the default) it does nothing at all.
    /// </remarks>
    private void TrackMomentum(Direction direction)
    {
        if (_config.MomentumMaxBonus <= 0 || _config.MomentumRampPerStep <= 0)
        {
            return;
        }

        if (_lastStepDirection == direction)
        {
            _straightSteps++;
        }
        else
        {
            // The step that starts a new line is the turn, so it is the one that pays.
            _straightSteps = 0;

            if (_lastStepDirection is not null)
            {
                _turnPenaltyLeft = _config.TurnPenaltySeconds;
            }
        }

        _lastStepDirection = direction;
    }

    /// <summary>
    /// Maps a step's remaining time to how far through it the figure is, from 0 to 1.
    /// </summary>
    /// <param name="left">Seconds left in the step.</param>
    /// <param name="total">The step's full length.</param>
    /// <returns>1 when no step is running, so "standing still" needs no special case.</returns>
    private static double StepFraction(double left, double total) =>
        total <= 0 ? 1.0 : Math.Clamp(1.0 - (left / total), 0, 1);

    /// <summary>
    /// Ages the seals and the wisp by <paramref name="deltaSeconds"/>, expiring whatever runs
    /// out, and marks the killer's route stale when a cost changed.
    /// </summary>
    /// <remarks>
    /// A cracked seal and an expired wisp both change what the killer should do, so both
    /// invalidate the cached route. Only the seal leaves: the barrier itself stays on the map
    /// as a husk that is passable at <see cref="GameConfig.ObstacleCost"/>.
    /// </remarks>
    private void AdvanceTimers(double deltaSeconds)
    {
        if (_barrierSeals.Count > 0)
        {
            // Snapshot the keys before touching the dictionary: the values are updated in the
            // loop, and removing an entry while enumerating would invalidate the enumerator.
            var sealedCells = new List<Cell>(_barrierSeals.Keys);
            var cracked = false;

            foreach (var cell in sealedCells)
            {
                var remaining = _barrierSeals[cell] - deltaSeconds;

                if (remaining > 0)
                {
                    _barrierSeals[cell] = remaining;
                    continue;
                }

                _barrierSeals.Remove(cell);
                cracked = true;
            }

            if (cracked)
            {
                InvalidateEnemyRoute();
            }
        }

        if (_lure is not null)
        {
            _lureSecondsLeft -= deltaSeconds;
            if (_lureSecondsLeft <= 0)
            {
                ClearLure();
            }
        }

        if (_invulnerableSeconds > 0)
        {
            _invulnerableSeconds = Math.Max(0, _invulnerableSeconds - deltaSeconds);
        }

        if (_stunSecondsLeft > 0)
        {
            _stunSecondsLeft = Math.Max(0, _stunSecondsLeft - deltaSeconds);
        }

        if (_turnPenaltyLeft > 0)
        {
            _turnPenaltyLeft = Math.Max(0, _turnPenaltyLeft - deltaSeconds);
        }

        // The two slides finish in wall-clock time even when the game is otherwise paused for a
        // stun; they are presentation, not a rule window.
        var stepping = _playerStepLeft > 0;

        if (stepping)
        {
            _playerStepLeft = Math.Max(0, _playerStepLeft - deltaSeconds);
        }

        if (_enemyStepLeft > 0)
        {
            _enemyStepLeft = Math.Max(0, _enemyStepLeft - deltaSeconds);
        }

        UpdateSprint(deltaSeconds, stepping);
    }

    /// <summary>
    /// Drains or refills the stamina bar for <paramref name="deltaSeconds"/>, and re-enables
    /// sprinting once the bar is back above the recovery threshold.
    /// </summary>
    /// <param name="deltaSeconds">The frame's length.</param>
    /// <param name="stepping">Whether a player step was running at the start of the frame.</param>
    /// <remarks>
    /// <para>
    /// The bar only drains <b>while a step is running</b>. Holding Shift on the spot is a player
    /// getting ready, not a sprint, and charging for it would empty the bar before they set off.
    /// Reading the flag from the start of the frame keeps the last sliver of a step honest: the
    /// frame that finishes a step still pays for it.
    /// </para>
    /// <para>
    /// Running dry has to cost something, or sprint would be free: an empty bar needs a quarter
    /// refill before it can be spent again.
    /// </para>
    /// </remarks>
    private void UpdateSprint(double deltaSeconds, bool stepping)
    {
        var capacity = _config.SprintStaminaSeconds;

        if (capacity <= 0)
        {
            _sprintStaminaLeft = 0;
            _sprintExhausted = false;
            return;
        }

        if (IsSprinting && stepping)
        {
            _sprintStaminaLeft = Math.Max(0, _sprintStaminaLeft - deltaSeconds);

            if (_sprintStaminaLeft <= 0)
            {
                _sprintExhausted = true;
            }

            return;
        }

        if (IsSprinting)
        {
            // Standing still with Shift held: nothing spent, nothing regained.
            return;
        }

        var recoveryRate = capacity / Math.Max(0.0001, _config.SprintRecoverySeconds);
        _sprintStaminaLeft = Math.Min(capacity, _sprintStaminaLeft + (deltaSeconds * recoveryRate));

        if (_sprintExhausted && _sprintStaminaLeft >= capacity * SprintRecoveryThreshold)
        {
            _sprintExhausted = false;
        }
    }

    /// <summary>
    /// Removes the glowing wisp and marks the killer's route stale.
    /// </summary>
    /// <remarks>
    /// The wisp is spent whether it was reached or timed out, so the killer always falls back
    /// to the player on the very next route.
    /// </remarks>
    private void ClearLure()
    {
        if (_lure is null)
        {
            return;
        }

        _lure = null;
        _lureSecondsLeft = 0;
        InvalidateEnemyRoute();

        // The wisp's pool of light goes with it, and a card's vision penalty ends with the glow.
        // Without this the cells it lit stayed visible until the next move.
        RecomputeVisibility();
    }

    /// <summary>
    /// Returns how long the killer needs before its next step: one normal step, times the
    /// cost of the cell it is about to enter, or the full tear-down time when that cell holds
    /// a sealed barrier.
    /// </summary>
    /// <remarks>
    /// Charging the delay before the move (rather than after) is what makes a barrier read
    /// correctly: the killer stalls at the barrier, then forces its way through, instead of
    /// appearing to pass it instantly and then idling on the other side. A sealed barrier is
    /// priced at <see cref="GameConfig.BarrierBreakSeconds"/> rather than a multiple of the step
    /// time because tearing it apart is a different action from walking.
    /// </remarks>
    private double SecondsUntilNextStep()
    {
        var route = EnemyRouteToTarget();

        if (route.Count <= _nextStepIndex)
        {
            return _config.EnemyStepSeconds;
        }

        var next = route[_nextStepIndex];

        return _barrierSeals.ContainsKey(next)
            ? _config.BarrierBreakSeconds
            : _config.EnemyStepSeconds * CostOfEntering(next);
    }

    /// <summary>
    /// Moves the killer one cell along its route, ending the game if that brings it onto the
    /// player's cell, or tears apart a sealed barrier that stands in the way.
    /// </summary>
    /// <param name="interval">
    /// How long the step that just completed took. Recorded so the renderer can slide the killer
    /// over the same time, whatever cell it walked into.
    /// </param>
    /// <remarks>
    /// Tearing the seal is a step of its own: the barrier shatters, nothing moves, and the next
    /// route is computed over plain floor. That is what makes a sealed barrier read as an
    /// obstacle the killer deals with, rather than something it walks through after a pause.
    /// </remarks>
    private void AdvanceEnemy(double interval)
    {
        var route = EnemyRouteToTarget();

        // A single-cell route means the killer is already on its destination, which either has
        // already ended the game or is the wisp it just consumed; nothing left to do.
        if (route.Count <= 1 || _nextStepIndex >= route.Count)
        {
            return;
        }

        var next = route[_nextStepIndex];

        if (_barrierSeals.ContainsKey(next))
        {
            BreakSeal(next);
            return;
        }

        // The creature cannot share the player's cell during the post-catch window, so it waits
        // outside and strikes when the window closes. Letting it step on would either overlap the
        // two or silently extend the invulnerability, both of which misrepresent the rule.
        if (next == _player && _invulnerableSeconds > 0)
        {
            return;
        }

        _enemyFrom = _enemy;
        _enemy = next;
        _enemyStepLeft = interval;
        _enemyStepTotal = interval;
        _nextStepIndex++;

        // The killer carries its own light, so what the player can see changes as it moves
        // even when the player stands still.
        RecomputeVisibility();

        // The rest of the cached route is still the best way to a player who has not moved,
        // so it is reused rather than recomputed every step.
        if (_enemy == _player)
        {
            TakeHit();
            return;
        }

        // Reaching the wisp spends it: the decoy has done its job and the chase resumes.
        if (_enemy == _lure)
        {
            ClearLure();

            // A card may make the wisp cost the killer real time. The stun is the same window a
            // catch buys, so it cannot be shortened by a frame landing mid-step.
            if (_config.LureStunSeconds > 0)
            {
                _stunSecondsLeft = Math.Max(_stunSecondsLeft, _config.LureStunSeconds);
                _elapsedSeconds = 0;
            }
        }
    }

    /// <summary>
    /// Spends one heart on a catch and resets the chase, or ends the run when that was the last
    /// heart.
    /// </summary>
    /// <remarks>
    /// The player keeps their cell and everything they have collected; only the killer is moved.
    /// That is what makes a mistake cost tempo instead of progress, which is the whole point of
    /// hearts in a 20-floor run. The invulnerability guard belongs here as well as at the two
    /// call sites, so a future third way to be caught cannot deal two hearts at once.
    /// </remarks>
    private void TakeHit()
    {
        if (_invulnerableSeconds > 0)
        {
            return;
        }

        _hearts--;

        if (_hearts <= 0)
        {
            _hearts = 0;
            _status = GameStatus.Caught;
            return;
        }

        _enemy = ChooseRespawnCell();

        // A respawn is a throw, not a walk: snap the figure instead of sliding it across the map.
        _enemyFrom = _enemy;
        _enemyStepLeft = 0;
        _enemyStepTotal = 0;

        _invulnerableSeconds = _config.InvulnerabilitySeconds;
        _stunSecondsLeft = _config.RespawnStunSeconds;
        _elapsedSeconds = 0;
        InvalidateEnemyRoute();
        RecomputeVisibility();
    }

    /// <summary>
    /// Picks the cell the killer is thrown to after a catch: the furthest reachable cell, with
    /// dead ends and non-objective cells preferred.
    /// </summary>
    /// <remarks>
    /// Reachability is measured through the maze, so "far" means far to walk rather than far on
    /// screen. Avoiding keys, the exit, the wisp and existing barriers keeps a respawn from
    /// parking the killer on something the player still needs; the second pass drops that
    /// preference so an extremely small or cluttered floor still produces a legal cell.
    /// </remarks>
    private Cell ChooseRespawnCell()
    {
        var reachable = DistancesFrom(_player, _maze.CellCount);

        return FindFurthest(reachable, avoidObjectives: true)
            ?? FindFurthest(reachable, avoidObjectives: false)
            ?? _player;
    }

    /// <summary>
    /// Returns the best respawn candidate among <paramref name="distances"/>, ranking by walking
    /// distance, then dead end, then row-major order for reproducibility.
    /// </summary>
    /// <param name="distances">Reachable cells mapped to their walking distance from the player.</param>
    /// <param name="avoidObjectives">
    /// When true, keys, the exit, the wisp and barriers are skipped.
    /// </param>
    private Cell? FindFurthest(Dictionary<Cell, int> distances, bool avoidObjectives)
    {
        Cell? best = null;
        var bestDistance = -1;
        var bestIsDeadEnd = false;

        foreach (var (cell, distance) in distances)
        {
            if (cell == _player || (avoidObjectives && IsObjectiveCell(cell)))
            {
                continue;
            }

            var isDeadEnd = cell.WallCount == 3;
            var better =
                best is null ||
                distance > bestDistance ||
                (distance == bestDistance && isDeadEnd && !bestIsDeadEnd) ||
                (distance == bestDistance && isDeadEnd == bestIsDeadEnd && Precedes(cell, best.Value));

            if (!better)
            {
                continue;
            }

            best = cell;
            bestDistance = distance;
            bestIsDeadEnd = isDeadEnd;
        }

        return best;
    }

    /// <summary>
    /// Returns whether <paramref name="cell"/> is something the player still needs or has
    /// deliberately placed, and so a poor place to drop the killer.
    /// </summary>
    private bool IsObjectiveCell(Cell cell) =>
        _keys.Contains(cell) || cell == _exit || cell == _lure || _barriers.Contains(cell);

    /// <summary>Row-major ordering, used only to break respawn ties reproducibly.</summary>
    private static bool Precedes(Cell left, Cell right) =>
        left.Row < right.Row || (left.Row == right.Row && left.Column < right.Column);

    /// <summary>
    /// Tears a sealed barrier apart: the seal and the barrier both leave the map, so the cell
    /// is plain floor from the next step on, and the killer's route is recomputed.
    /// </summary>
    /// <param name="cell">The sealed cell the killer has just spent its time on.</param>
    private void BreakSeal(Cell cell)
    {
        _barrierSeals.Remove(cell);
        _barriers.Remove(cell);
        InvalidateEnemyRoute();
    }

    /// <summary>
    /// Returns the killer's route to whichever target it currently wants, recomputing it only
    /// when it was invalidated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target is chosen by the same Dijkstra search that does the walking: the killer runs
    /// to the player, and to the wisp as well when one is glowing, and follows whichever route
    /// is genuinely cheaper by the current costs. That is why a wisp thrown into a side branch
    /// <i>works</i> — it is not a scripted "distracted" state, it is the shortest path simply
    /// pointing somewhere else.
    /// </para>
    /// <para>
    /// Ties go to the player, so a wisp can never make the killer dither between two equal
    /// options.
    /// </para>
    /// </remarks>
    private IReadOnlyList<Cell> EnemyRouteToTarget()
    {
        if (_enemyRoute is not null)
        {
            return _enemyRoute;
        }

        _enemyRoute = _pathfinder.FindPath(_maze, _enemy, _player, CostOfEntering);

        // A wisp under the killer's own feet is not a target: its route would be a single
        // cell, which would stall the hunt instead of redirecting it. Placement refuses that
        // cell as well; this is the belt-and-braces half of the same rule.
        if (_lure is { } lure && lure != _enemy)
        {
            var toLure = _pathfinder.FindPath(_maze, _enemy, lure, CostOfEntering);

            if (toLure.Count > 0 && RouteCost(toLure) < RouteCost(_enemyRoute))
            {
                _enemyRoute = toLure;
            }
        }

        _nextStepIndex = 1;
        return _enemyRoute;
    }

    /// <summary>
    /// Sums what the killer pays to walk <paramref name="route"/>, which is the cost of every
    /// cell entered (the cell it starts on is free).
    /// </summary>
    /// <remarks>
    /// This is what makes two Dijkstra searches comparable: a route's length in cells says
    /// nothing when one of those cells is a barriered one.
    /// </remarks>
    private int RouteCost(IReadOnlyList<Cell> route)
    {
        var total = 0;

        for (var index = 1; index < route.Count; index++)
        {
            total += CostOfEntering(route[index]);
        }

        return total;
    }

    /// <summary>
    /// Rebuilds the set of cells the player can see: their own torch range, the light around
    /// an active wisp, plus the killer's aura once the killer is close enough.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One breadth-first walk out from the player answers both questions at once, because it
    /// yields walking distances: cells within <see cref="GameConfig.VisionRadius"/> are visible,
    /// and the killer's own distance is read out of the same map. The walk is done to the
    /// larger of the two radii so the distance is known even when the killer is outside the
    /// torch.
    /// </para>
    /// <para>
    /// Walls are respected simply by only crossing passages, which is what keeps sight from
    /// leaking through them. The killer's aura then walks out from the killer itself, which is
    /// why its surroundings can be seen even when the route between player and killer is dark:
    /// it is the killer that is lighting them, not the player.
    /// </para>
    /// </remarks>
    private void RecomputeVisibility()
    {
        _visible.Clear();
        _enemyGlow.Clear();

        // A burning wisp can shrink the torch, so read the radius in force rather than the
        // floor's own number.
        var visionRadius = EffectiveVisionRadius;
        var searchRadius = Math.Max(visionRadius, _config.EnemyAuraDistance);
        var playerDistances = DistancesFrom(_player, searchRadius);

        foreach (var (cell, distance) in playerDistances)
        {
            if (distance <= visionRadius)
            {
                _visible.Add(cell);
            }
        }

        // A wisp is a beacon the player placed, so its own pool of light is visible even where
        // the torch does not reach. It is deliberately not part of EnemyGlow: it reveals the
        // wisp, nothing about where the killer is.
        if (_lure is { } lure)
        {
            foreach (var cell in DistancesFrom(lure, LureGlowRadius).Keys)
            {
                _visible.Add(cell);
            }
        }

        // Far away the killer is invisible, and so is the light it carries.
        if (!playerDistances.TryGetValue(_enemy, out var enemyDistance) ||
            enemyDistance > _config.EnemyAuraDistance)
        {
            return;
        }

        foreach (var cell in DistancesFrom(_enemy, _config.EnemyAuraRadius).Keys)
        {
            _enemyGlow.Add(cell);
            _visible.Add(cell);
        }
    }

    /// <summary>
    /// Walks out from <paramref name="origin"/> through open passages, returning every cell
    /// reached together with the number of steps needed to reach it.
    /// </summary>
    /// <remarks>
    /// Breadth-first search visits cells in order of walking distance, so cutting the walk at
    /// <paramref name="radius"/> is exact: nothing beyond the radius is explored, and every
    /// cell that is reached is genuinely that many steps away.
    /// </remarks>
    private Dictionary<Cell, int> DistancesFrom(Cell origin, int radius)
    {
        var distances = new Dictionary<Cell, int> { [origin] = 0 };

        if (radius == 0)
        {
            return distances;
        }

        var frontier = new Queue<Cell>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();
            var distance = distances[cell];

            if (distance == radius)
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

                // Already reached means it was found on a shorter or equal route.
                if (distances.ContainsKey(neighbour))
                {
                    continue;
                }

                distances[neighbour] = distance + 1;
                frontier.Enqueue(neighbour);
            }
        }

        return distances;
    }

    /// <summary>
    /// The killer's cost to enter a cell: prohibitive for a sealed barrier, expensive for a
    /// spent one, normal otherwise. The value is always finite, which is what guarantees a
    /// route always exists and keeps the maze free of soft-locks.
    /// </summary>
    /// <remarks>
    /// The sealed price is finite on purpose. A truly impassable cell would let the player wall
    /// the killer in, so a sealed barrier is instead priced so high that any real detour wins —
    /// and when there is no detour, the killer pays it by tearing the barrier apart.
    /// </remarks>
    private int CostOfEntering(Cell cell)
    {
        if (_barrierSeals.ContainsKey(cell))
        {
            return _config.SealedBarrierCost;
        }

        return _barriers.Contains(cell) ? _config.ObstacleCost : 1;
    }

    /// <summary>
    /// Marks the cached killer route as out of date; the next step recomputes it.
    /// </summary>
    private void InvalidateEnemyRoute()
    {
        _enemyRoute = null;
        _nextStepIndex = 0;
    }
}

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
    /// <summary>How long the player must stand still to complete a veil.</summary>
    private const double VeilChannelSeconds = 0.6;

    /// <summary>How long a thrown lantern burns before it returns.</summary>
    private const double LanternSeconds = 5.0;

    /// <summary>How often Regrowth seals a dead end.</summary>
    private const double RegrowthIntervalSeconds = 18.0;

    /// <summary>How often Fracture moves a wall.</summary>
    private const double FractureIntervalSeconds = 22.0;

    /// <summary>How long the lit half of an Eclipse lasts.</summary>
    private const double EclipseLitSeconds = 10.0;

    /// <summary>How long the dark half of an Eclipse lasts.</summary>
    private const double EclipseDarkSeconds = 6.0;

    /// <summary>How long a shove stuns the hunter it moves.</summary>
    private const double ShoveStunSeconds = 2.0;

    /// <summary>How many cells a shove pushes a hunter.</summary>
    private const int ShovePushCells = 2;
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

    /// <summary>
    /// How many undrained events are kept. A frontend drains them every frame, so this only
    /// matters when nothing is listening (tests, the console view): the queue then holds the most
    /// recent moments instead of growing with the length of the run.
    /// </summary>
    private const int MaxQueuedEvents = 32;

    private readonly GameConfig _config;
    private readonly Grid _maze;
    private readonly IPathfinder _pathfinder;
    private readonly IRandom _random;
    private readonly HashSet<Cell> _keys;
    private readonly HashSet<Cell> _barriers = [];
    private readonly Dictionary<Cell, double> _barrierSeals = [];
    private readonly HashSet<Cell> _visible = [];
    private readonly HashSet<Cell> _enemyGlow = [];
    private Cell _exit;
    private readonly Queue<GameEvent> _events = new();

    private Cell _player;
    private Cell _aimCursor;
    private Cell? _lure;
    private double _lureSecondsLeft;
    private int _barriersRemaining;
    private int _luresRemaining;

    // The new kit. Charges are per floor; the state is the rule, the HUD only reads it.
    private PowerupCharges _powerupsRemaining;
    private Direction _facing = Direction.South;
    private double _veilChannelLeft;
    private double _veilSecondsLeft;
    private double _dragSecondsLeft;
    private double _dragPenaltyLeft;
    private Cell? _lantern;
    private double _lanternSecondsLeft;

    // Descent conditions. Each timer is zero unless its condition is in force.
    private double _regrowthSecondsLeft;
    private double _fractureSecondsLeft;
    private double _eclipseSecondsLeft;
    private bool _eclipseDark;
    private double _packElapsed;

    private int _hearts;
    private readonly int _maxHearts;
    private double _invulnerableSeconds;

    // Card-granted reprieves. Both are consumed once per floor, so a new floor is a new chance.
    private bool _freeCatchUsed;
    private bool _reviveUsed;
    private GameStatus _status = GameStatus.InProgress;
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

    // The hunters. Most floors have one and the deep floors have two or three; the rule is the
    // same for each, so everything below loops rather than special-casing a second monster.
    private readonly List<Hunter> _hunters = [];

    // Reused by Enemies, so a renderer reading the hunters every frame allocates nothing.
    private readonly HunterView[] _enemyViews;

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
        _random = random;
        _barriersRemaining = config.BarrierBudget;
        _luresRemaining = config.LureCount;
        _powerupsRemaining = config.MutedPowerup is { } muted
            ? config.Powerups.With(muted, 0)
            : config.Powerups;
        _regrowthSecondsLeft = RegrowthIntervalSeconds;
        _fractureSecondsLeft = FractureIntervalSeconds;
        _eclipseSecondsLeft = EclipseLitSeconds;
        _hearts = config.StartingHearts;
        _maxHearts = config.MaxHearts;
        _sprintStaminaLeft = config.SprintStaminaSeconds;

        // Shuffle every floor cell once, then take the first unused ones. Because the list holds
        // each cell exactly once, the player, killer, exit and keys are guaranteed to be on
        // different cells without any retry loop. Solid rock is not a cell anything can stand on:
        // on a shaped floor the maze's rectangle is bigger than the cave inside it.
        var cells = new List<Cell>(maze.OpenCellCount);
        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (!maze.IsSolid(row, column))
                {
                    cells.Add(maze.GetCell(row, column));
                }
            }
        }

        var requiredCells = 2 + config.HunterCount + config.KeyCount;

        if (cells.Count < requiredCells)
        {
            throw new ArgumentException(
                $"The floor has {cells.Count} walkable cells, but the player, {config.HunterCount} " +
                $"hunter(s), the exit and {config.KeyCount} key(s) need {requiredCells} different ones.",
                nameof(maze));
        }

        random.Shuffle(cells);

        _player = cells[0];

        var hunterCells = ChooseHunterStarts(maze, _player, pathfinder, config.HunterCount);
        _enemyViews = new HunterView[config.HunterCount];

        foreach (var cell in hunterCells)
        {
            _hunters.Add(new Hunter(cell));
        }

        var available = new List<Cell>(cells.Count - hunterCells.Count);
        foreach (var cell in cells)
        {
            if (cell != _player && !hunterCells.Contains(cell))
            {
                available.Add(cell);
            }
        }

        // Config.Validate guarantees at least 3 + KeyCount cells, so the exit plus every key
        // fits in this list. The slicing below therefore cannot run out.
        _exit = available[0];
        _keys = new HashSet<Cell>(available.GetRange(1, config.KeyCount));

        // Nothing is mid-step at the start, so every figure is drawn exactly on its cell.
        _playerFrom = _player;

        RecomputeVisibility();
    }

    /// <summary>
    /// Chooses where the hunters start: the furthest dead ends from the player, one per hunter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dead-end rule matters for playability, not just atmosphere. A perfect maze is a
    /// tree, so a killer standing in a corridor sits on the only route between the player
    /// and everything behind it — and since the player cannot walk through the killers, those
    /// cells would be permanently unreachable. A dead-end cell has exactly one opening, so
    /// removing it from a tree leaves the rest connected: every key and the exit stay
    /// reachable without ever stepping on a hunter.
    /// </para>
    /// <para>
    /// Distance is measured through the maze (a breadth-first walk), not as the crow flies, so
    /// the hunters start genuinely far away. Ties go to dead ends, then to row-major order, so
    /// the same seed always places them on the same cells. Later hunters are measured from the
    /// player as well, so two of them are never stacked in the same corner.
    /// </para>
    /// </remarks>
    private static List<Cell> ChooseHunterStarts(
        Grid maze,
        Cell player,
        IPathfinder pathfinder,
        int count)
    {
        var starts = new List<Cell>(count);
        var distances = DistancesFrom(maze, player, maze.CellCount);

        for (var index = 0; index < count; index++)
        {
            Cell? best = null;
            var bestDistance = -1;
            var bestIsDeadEnd = false;

            for (var row = 0; row < maze.Rows; row++)
            {
                for (var column = 0; column < maze.Columns; column++)
                {
                    var cell = maze.GetCell(row, column);

                    if (cell == player || starts.Contains(cell) ||
                        !distances.TryGetValue(cell, out var distance))
                    {
                        continue;
                    }

                    // Three walls means exactly one opening: a dead end, and so a leaf. The first
                    // hunter must take one, or it can wall the player off; a second may take any
                    // far cell once the good leaves are gone.
                    var isDeadEnd = cell.WallCount == 3;
                    var better = best is null || distance > bestDistance ||
                        (distance == bestDistance && isDeadEnd && !bestIsDeadEnd);

                    if (!better)
                    {
                        continue;
                    }

                    best = cell;
                    bestDistance = distance;
                    bestIsDeadEnd = isDeadEnd;
                }
            }

            // A maze with no dead ends (for example a closed loop, which the generator never
            // makes but a hand-built test maze can) has no safe leaf to use. Fall back to the
            // furthest unused cell instead, so two hunters can never be placed on one cell — a
            // corridor of bare cells is exactly where that would happen.
            if (best is null)
            {
                foreach (var (cell, _) in distances)
                {
                    if (cell == player || starts.Contains(cell))
                    {
                        continue;
                    }

                    if (best is null || distances[cell] > distances[best.Value])
                    {
                        best = cell;
                    }
                }
            }

            // A hand-built floor can be disconnected, in which case the walk above never reaches the
            // cells behind the break and there is no "furthest" cell to pick. Any floor cell that is
            // not already taken keeps the constructor total; the original rule (the mirrored cell)
            // stays as the last resort for a maze with exactly as many cells as figures.
            if (best is null)
            {
                best = FindUnusedCell(maze, player, starts);
            }

            starts.Add(best ?? maze.GetCell(
                Math.Clamp(maze.Rows - 1 - player.Row, 0, maze.Rows - 1),
                Math.Clamp(maze.Columns - 1 - player.Column, 0, maze.Columns - 1)));
        }

        return starts;
    }

    /// <summary>
    /// Returns the first floor cell that is neither the player nor an already placed hunter.
    /// </summary>
    /// <param name="maze">The floor to search.</param>
    /// <param name="player">The cell to skip.</param>
    /// <param name="starts">The cells the earlier hunters took.</param>
    /// <returns>An unused cell, or null when every cell is taken.</returns>
    private static Cell? FindUnusedCell(Grid maze, Cell player, List<Cell> starts)
    {
        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (maze.IsSolid(row, column))
                {
                    continue;
                }

                var cell = maze.GetCell(row, column);

                if (cell != player && !starts.Contains(cell))
                {
                    return cell;
                }
            }
        }

        return null;
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
    /// Gets the cell the first hunter currently occupies.
    /// </summary>
    /// <remarks>
    /// Kept as the single-hunter spelling of <see cref="Enemies"/>: every screen still draws the
    /// first hunter, and a one-hunter floor reads exactly as it did before there were more.
    /// </remarks>
    public Cell Enemy => _hunters[0].Cell;

    /// <summary>
    /// Gets every hunter on the floor, in the order they were placed.
    /// </summary>
    /// <remarks>
    /// The list is a snapshot of the current frame: each entry has the hunter's cell, the cell it
    /// stepped from and how far through that step it is, so a renderer can slide every one of them
    /// without touching the rule. The array behind it is reused, so reading this every frame costs
    /// no allocation.
    /// </remarks>
    public IReadOnlyList<HunterView> Enemies
    {
        get
        {
            for (var index = 0; index < _hunters.Count; index++)
            {
                var hunter = _hunters[index];

                _enemyViews[index] = new HunterView(
                    hunter.Cell,
                    hunter.From,
                    StepFraction(hunter.StepLeft, hunter.StepTotal),
                    hunter.StunSeconds > 0);
            }

            return _enemyViews;
        }
    }

    /// <summary>Gets how many hunters are on the floor.</summary>
    public int HunterCount => _hunters.Count;

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

    /// <summary>Gets the powerup charges left on this floor.</summary>
    /// <remarks>
    /// The HUD reads this every frame. A charge is spent when the verb succeeds, not when it is
    /// requested, so a blink into rock or a shove at empty air costs nothing.
    /// </remarks>
    public PowerupCharges PowerupsRemaining => _powerupsRemaining;

    /// <summary>Gets the direction the player is facing, which the one-press verbs use.</summary>
    /// <remarks>
    /// Facing is updated by a direction press even when the step is blocked, so a player can turn
    /// to a wall and blink through it without first finding an open route.
    /// </remarks>
    public Direction Facing => _facing;

    /// <summary>Gets a value indicating whether the player is unseen.</summary>
    public bool IsVeiled => _veilSecondsLeft > 0;

    /// <summary>Gets a value indicating whether the player is standing still to complete a veil.</summary>
    public bool IsVeilCharging => _veilChannelLeft > 0;

    /// <summary>Gets how much of the veil is left, in seconds.</summary>
    public double VeilSecondsRemaining => _veilSecondsLeft;

    /// <summary>Gets a value indicating whether the hunt is currently slowed.</summary>
    public bool IsDragActive => _dragSecondsLeft > 0;

    /// <summary>Gets a value indicating whether the hunt is faster after a Drag ended.</summary>
    public bool IsDragPenalty => _dragPenaltyLeft > 0;

    /// <summary>Gets a value indicating whether a lantern is burning away from the player.</summary>
    public bool IsLanternActive => _lantern is not null;

    /// <summary>Gets the cell the thrown lantern is on, or null when it is in the player's hand.</summary>
    public Cell? LanternCell => _lantern;

    /// <summary>Gets how many seconds the thrown lantern has left.</summary>
    public double LanternSecondsRemaining => _lanternSecondsLeft;

    /// <summary>
    /// Gets where the player's light comes from: the thrown lantern while it burns, otherwise the
    /// player's own cell.
    /// </summary>
    public Cell LightOrigin => _lantern ?? _player;

    /// <summary>Gets the descent conditions in force on this floor.</summary>
    public IReadOnlyList<DescentCondition> Conditions => _config.Conditions;

    /// <summary>Gets a value indicating whether the dark half of an Eclipse is running.</summary>
    public bool IsEclipseDark => _eclipseDark;

    /// <summary>Tests whether <paramref name="condition"/> is in force on this floor.</summary>
    /// <param name="condition">The condition to test.</param>
    /// <returns><see langword="true"/> when it is in the floor's config.</returns>
    public bool HasCondition(DescentCondition condition) => _config.Conditions.Contains(condition);

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
    /// Takes every event raised since the last drain, oldest first, and empties the queue.
    /// </summary>
    /// <returns>
    /// The events, or an empty list when nothing happened. The returned list is a snapshot: the
    /// game keeps playing with a fresh queue from the moment this returns.
    /// </returns>
    /// <remarks>
    /// Draining is how a frontend turns rules into sound without inspecting the state twice. A
    /// caller that never drains is harmless — the queue keeps only the most recent moments, so a
    /// long headless run cannot grow without bound.
    /// </remarks>
    public IReadOnlyList<GameEvent> DrainEvents()
    {
        if (_events.Count == 0)
        {
            return Array.Empty<GameEvent>();
        }

        var drained = _events.ToArray();
        _events.Clear();
        return drained;
    }

    /// <summary>
    /// Appends <paramref name="gameEvent"/> to the queue the UI drains.
    /// </summary>
    /// <param name="gameEvent">The moment that just happened.</param>
    private void Raise(GameEvent gameEvent)
    {
        // A frontend that does not listen (the console view, a long test) must not accumulate a
        // queue the size of the run. The oldest moment is the one worth dropping.
        if (_events.Count >= MaxQueuedEvents)
        {
            _events.Dequeue();
        }

        _events.Enqueue(gameEvent);
    }

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
    /// Gets a value indicating whether every hunter is still stunned by the last catch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stunned hunter neither moves nor builds up its next step, so the stun is a genuine
    /// window in which the player can put distance between the two of them. A catch stuns all of
    /// them for that reason: a reprieve that leaves one hunter walking is not a reprieve.
    /// </para>
    /// <para>
    /// With more than one hunter the quiet reads as false the moment any of them recovers, which
    /// is exactly what the music should say.
    /// </para>
    /// </remarks>
    public bool IsEnemyStunned
    {
        get
        {
            foreach (var hunter in _hunters)
            {
                if (hunter.StunSeconds <= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Gets how many seconds of the longest post-catch stun remain, or 0 when every hunter is
    /// awake.
    /// </summary>
    public double EnemyStunSecondsRemaining
    {
        get
        {
            var longest = 0.0;

            foreach (var hunter in _hunters)
            {
                longest = Math.Max(longest, hunter.StunSeconds);
            }

            return longest;
        }
    }

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
    /// <see cref="GameConfig.VisionRadius"/> steps through open passages, or within
    /// <see cref="NearSightRadius"/> of the player's own cell.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Visibility is a <b>rule</b>, not a drawing decision: it says what the player is
    /// allowed to know, so it lives here and the renderers only decide how to darken what
    /// they are told. That is also why it is a query rather than a stored per-cell flag the
    /// UI could set.
    /// </para>
    /// <para>
    /// Sight travels along open passages, so a wall really does block it: the corridor on the
    /// far side of a wall stays hidden until there is a way round. Distance is counted in steps
    /// of walking, which makes the radius a torch range rather than a circle drawn through rock.
    /// </para>
    /// <para>
    /// The one exception is <see cref="NearSightRadius"/>: the lantern also spills onto the stone
    /// within two cells of the player, whether or not a wall stands between them. That is what
    /// draws the near side of a wall you are standing against — without it the wall's face belongs
    /// to a cell you have never seen and the corridor looks like it has a gap in it.
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
    public int EffectiveVisionRadius
    {
        get
        {
            var radius = _config.VisionRadius;

            if (_lure is not null)
            {
                radius -= _config.LureVisionPenalty;
            }

            if (_eclipseDark)
            {
                radius--;
            }

            if (IsVeiled)
            {
                radius--;
            }

            return Math.Max(0, radius);
        }
    }

    /// <summary>
    /// Gets the radius of the lantern spill that ignores walls: the configured
    /// <see cref="GameConfig.NearSightRadius"/>, capped so it can never see further than the
    /// torch itself in a straight line.
    /// </summary>
    /// <remarks>
    /// One cell is the floor when the torch is down to a single step (The Hush), and nothing when
    /// the torch is out entirely, so the rule cannot be used as a way to keep seeing on a blind
    /// floor.
    /// </remarks>
    public int NearSightRadius
    {
        get
        {
            var torch = EffectiveVisionRadius;
            return torch <= 0 ? 0 : Math.Min(_config.NearSightRadius, torch);
        }
    }

    /// <summary>Gets a value indicating whether a step's cooldown is still running.</summary>
    public bool IsPlayerStepping => _playerStepLeft > 0;

    /// <summary>
    /// Gets how long the player's last step took, in seconds; zero before the first step.
    /// </summary>
    /// <remarks>
    /// This is the cadence the input layer should repeat a held direction at. It is the step that
    /// actually happened, after sprint, momentum and any turn penalty, so holding walks exactly as
    /// fast as the floor allows instead of at a rate the input invents.
    /// </remarks>
    public double LastPlayerStepSeconds => _playerStepTotal;

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

    /// <summary>Gets the cell the first hunter stepped from, for the renderer's slide.</summary>
    public Cell PreviousEnemy => _hunters[0].From;

    /// <summary>Gets how far through its step the first hunter's figure is, from 0 to 1.</summary>
    public double EnemyStepFraction => StepFraction(_hunters[0].StepLeft, _hunters[0].StepTotal);

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

        // Facing is a rule because Blink and Shove read it: a press toward a wall must turn the
        // player even though the step itself is refused.
        _facing = direction;

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
        _facing = direction;

        // HasWall validates the position and the direction, and an outer wall always reports
        // true, so a step out of the maze is rejected here rather than indexing off the grid.
        if (_maze.HasWall(_player.Row, _player.Column, direction))
        {
            return false;
        }

        // A step is movement, and movement gives the player away: a veil, charged or active,
        // ends the moment the player commits to a cell.
        BreakVeil();

        var sprinting = IsSprinting;
        var (rowDelta, columnDelta) = direction.Delta();
        var destination = _maze.GetCell(_player.Row + rowDelta, _player.Column + columnDelta);

        // Walking into the killer is a mistake, not a free pass. During the post-catch window the
        // creature cannot be shoved through, so the step is simply refused rather than dealing
        // a second hit for the same mistake.
        var caughtBy = HunterAt(destination);
        if (caughtBy is not null && _invulnerableSeconds > 0)
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

        // Collecting is idempotent: stepping on an already collected key cell changes nothing,
        // which is also why the pickup event is raised from the removal rather than the cell.
        Raise(GameEvent.Step);

        CollectKeyAt(_player);

        if (_config.MagneticKeys)
        {
            CollectAdjacentKeys();
        }

        // Walking onto the lantern picks it back up; it is the only way to end the effect early.
        if (_lantern is { } lantern && _player == lantern)
        {
            ClearLantern();
        }

        if (caughtBy is not null)
        {
            TakeHit(caughtBy);
            return true;
        }

        if (_player == _exit && IsExitUnlocked)
        {
            _status = GameStatus.Won;
            Raise(GameEvent.FloorCleared);
            return true;
        }

        // The Listener turns the hunt into a turn-based exchange: every player step is the only
        // thing that winds the hunters forward. Sprinting is loud enough to be worth two.
        if (HasCondition(DescentCondition.Listener))
        {
            CreditListenerStep(sprinting);
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
        Raise(GameEvent.SealPlaced);
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
            _aimCursor == _player || HunterAt(_aimCursor) is not null)
        {
            return false;
        }

        _lure = _aimCursor;
        _lureSecondsLeft = _config.LureSeconds;
        _luresRemaining--;
        _aiming = PlacementMode.None;
        InvalidateEnemyRoute();
        Raise(GameEvent.WispLit);

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
    /// Blinks through the wall the player faces, leaving the breach open.
    /// </summary>
    /// <returns><see langword="true"/> when the player moved.</returns>
    /// <remarks>
    /// The breach is a real change to the maze: the wall is removed for the rest of the floor and
    /// the hunters can use it, which is what makes Blink a shortcut with a bill. It cannot leave
    /// the floor or enter rock, and it costs a charge only when it succeeds.
    /// </remarks>
    public bool TryBlink()
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None ||
            _powerupsRemaining.Blink <= 0)
        {
            return false;
        }

        var (rowDelta, columnDelta) = _facing.Delta();
        var row = _player.Row + rowDelta;
        var column = _player.Column + columnDelta;

        if (!_maze.HasWall(_player.Row, _player.Column, _facing) ||
            row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns ||
            _maze.IsSolid(row, column))
        {
            return false;
        }

        var destination = _maze.GetCell(row, column);
        var caughtBy = HunterAt(destination);

        if (caughtBy is not null && _invulnerableSeconds > 0)
        {
            return false;
        }

        _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Blink, -1);
        _maze.RemoveWall(_player.Row, _player.Column, _facing);

        BreakVeil();
        _playerFrom = _player;
        _player = destination;
        _playerStepLeft = NextPlayerStepSeconds();
        _playerStepTotal = _playerStepLeft;
        InvalidateEnemyRoute();
        RecomputeVisibility();
        Raise(GameEvent.Blink);

        CollectKeyAt(_player);

        if (_config.MagneticKeys)
        {
            CollectAdjacentKeys();
        }

        if (_lantern is { } lantern && _player == lantern)
        {
            ClearLantern();
        }

        if (caughtBy is not null)
        {
            TakeHit(caughtBy);
            return true;
        }

        if (_player == _exit && IsExitUnlocked)
        {
            _status = GameStatus.Won;
            Raise(GameEvent.FloorCleared);
        }

        return true;
    }

    /// <summary>
    /// Starts a veil: the player must stand still for half a second, then goes unseen for three.
    /// </summary>
    /// <returns><see langword="true"/> when the channel started.</returns>
    /// <remarks>
    /// The charge is spent when the veil completes, not when the channel starts, so a player who
    /// moves during the wind-up loses nothing but the moment. Moving during the veil breaks it.
    /// </remarks>
    public bool TryVeil()
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None ||
            IsVeiled || IsVeilCharging || _powerupsRemaining.Veil <= 0)
        {
            return false;
        }

        _veilChannelLeft = VeilChannelSeconds;
        return true;
    }

    /// <summary>
    /// Slows the hunt to 40% for three seconds, then leaves it 10% faster for five.
    /// </summary>
    /// <returns><see langword="true"/> when the drag started.</returns>
    public bool TryDrag()
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None ||
            IsDragActive || _powerupsRemaining.Drag <= 0)
        {
            return false;
        }

        _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Drag, -1);
        _dragSecondsLeft = _config.DragSeconds;
        _dragPenaltyLeft = 0;
        InvalidateEnemyRoute();
        Raise(GameEvent.Drag);
        return true;
    }

    /// <summary>
    /// Throws the lantern up to four cells ahead and lets the hunters follow it.
    /// </summary>
    /// <returns><see langword="true"/> when the lantern left the player's hand.</returns>
    /// <remarks>
    /// While it burns, <see cref="LightOrigin"/> is the lantern and the player's own cell is dark.
    /// Walking onto it, waiting it out, or a hunter reaching it ends the effect. The throw stops at
    /// the first wall and refuses a cell a hunter already stands on, so a charge is never wasted on
    /// a lantern that is consumed immediately.
    /// </remarks>
    public bool TryThrowLantern()
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None ||
            IsLanternActive || _powerupsRemaining.Lantern <= 0)
        {
            return false;
        }

        var (rowDelta, columnDelta) = _facing.Delta();
        var cell = _player;

        for (var distance = 0; distance < _config.LanternRange; distance++)
        {
            if (_maze.HasWall(cell.Row, cell.Column, _facing))
            {
                break;
            }

            var row = cell.Row + rowDelta;
            var column = cell.Column + columnDelta;

            if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns ||
                _maze.IsSolid(row, column))
            {
                break;
            }

            cell = _maze.GetCell(row, column);
        }

        if (cell == _player || HunterAt(cell) is not null)
        {
            return false;
        }

        _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Lantern, -1);
        _lantern = cell;
        _lanternSecondsLeft = LanternSeconds;
        InvalidateEnemyRoute();
        RecomputeVisibility();
        Raise(GameEvent.LanternThrown);
        return true;
    }

    /// <summary>
    /// Shoves the hunter in the cell the player faces two cells back and stuns it.
    /// </summary>
    /// <returns><see langword="true"/> when a hunter was moved.</returns>
    /// <remarks>
    /// The shove is loud: every other hunter is given its next step immediately. That is the price
    /// of the only melee answer in the kit, and it keeps a shove from being a free reset.
    /// </remarks>
    public bool TryShove()
    {
        if (_status != GameStatus.InProgress || _aiming != PlacementMode.None ||
            _powerupsRemaining.Shove <= 0)
        {
            return false;
        }

        var (rowDelta, columnDelta) = _facing.Delta();

        if (_maze.HasWall(_player.Row, _player.Column, _facing))
        {
            return false;
        }

        var target = _maze.GetCell(_player.Row + rowDelta, _player.Column + columnDelta);
        var hunter = HunterAt(target);

        if (hunter is null)
        {
            return false;
        }

        _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Shove, -1);

        // Push until two cells are gained or the next cell is a wall, rock or another hunter.
        var landing = target;

        for (var pushed = 0; pushed < ShovePushCells; pushed++)
        {
            if (_maze.HasWall(landing.Row, landing.Column, _facing))
            {
                break;
            }

            var row = landing.Row + rowDelta;
            var column = landing.Column + columnDelta;

            if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns ||
                _maze.IsSolid(row, column))
            {
                break;
            }

            var next = _maze.GetCell(row, column);

            if (HunterAt(next) is { } occupant && occupant != hunter)
            {
                break;
            }

            landing = next;
        }

        hunter.From = hunter.Cell;
        hunter.Cell = landing;
        hunter.StepLeft = 0;
        hunter.StepTotal = 0;
        hunter.Elapsed = 0;
        hunter.StunSeconds = Math.Max(hunter.StunSeconds, ShoveStunSeconds);
        hunter.Route = null;
        hunter.NextStep = 0;

        foreach (var other in _hunters)
        {
            if (other != hunter && other.StunSeconds <= 0)
            {
                other.Elapsed = SecondsUntilNextStep(other);
            }
        }

        InvalidateEnemyRoute();
        RecomputeVisibility();
        Raise(GameEvent.Shoved);
        return true;
    }

    /// <summary>Ends a charged or active veil and returns the hunters to the chase.</summary>
    private void BreakVeil()
    {
        if (_veilChannelLeft <= 0 && _veilSecondsLeft <= 0)
        {
            return;
        }

        _veilChannelLeft = 0;
        _veilSecondsLeft = 0;

        foreach (var hunter in _hunters)
        {
            hunter.KnownPlayer = null;
        }

        InvalidateEnemyRoute();
        RecomputeVisibility();
    }

    /// <summary>Completes a veil once the channel has run out.</summary>
    private void CompleteVeil()
    {
        _veilChannelLeft = 0;

        if (_powerupsRemaining.Veil <= 0)
        {
            return;
        }

        _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Veil, -1);
        _veilSecondsLeft = _config.VeilDurationSeconds;

        // The hunters keep walking to the cell they last saw the player on; the veil is not a
        // teleport, it is a moment of being unobserved.
        foreach (var hunter in _hunters)
        {
            hunter.KnownPlayer = _player;
            hunter.Route = null;
            hunter.NextStep = 0;
        }

        RecomputeVisibility();
        Raise(GameEvent.Veiled);
    }

    /// <summary>Collects the key on <paramref name="cell"/>, if there is one.</summary>
    /// <param name="cell">The cell just entered.</param>
    /// <remarks>
    /// The pickup is the one place that pays a card's per-key bonuses, so a magnetic key and a key
    /// under the player's feet behave identically: blink credit, then Drift's exit move.
    /// </remarks>
    private void CollectKeyAt(Cell cell)
    {
        if (!_keys.Remove(cell))
        {
            return;
        }

        Raise(GameEvent.KeyPicked);

        if (_config.BlinkRefundOnKey)
        {
            _powerupsRemaining = _powerupsRemaining.Adjust(PowerupKind.Blink, 1);
        }

        // Drift moves the last leg of the floor as the hunt tightens. It happens after the pickup
        // so the key that opened the arch is the one that moves it.
        if (HasCondition(DescentCondition.Drift))
        {
            RelocateExit();
        }
    }

    /// <summary>Collects any key touching the player's cell through an open passage.</summary>
    /// <remarks>
    /// Used by the magnetic-key card. Walls still block the pull, so the card is a reach of one
    /// step rather than a vacuum through rock, and a floor with no keys costs nothing.
    /// </remarks>
    private void CollectAdjacentKeys()
    {
        foreach (var direction in NeighbourDirections)
        {
            if (_maze.HasWall(_player.Row, _player.Column, direction))
            {
                continue;
            }

            var (rowDelta, columnDelta) = direction.Delta();
            var row = _player.Row + rowDelta;
            var column = _player.Column + columnDelta;

            if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns ||
                _maze.IsSolid(row, column))
            {
                continue;
            }

            CollectKeyAt(_maze.GetCell(row, column));
        }
    }

    /// <summary>Puts the lantern back in the player's hand and restores the light to them.</summary>
    private void ClearLantern()
    {
        if (_lantern is null)
        {
            return;
        }

        _lantern = null;
        _lanternSecondsLeft = 0;
        InvalidateEnemyRoute();
        RecomputeVisibility();
        Raise(GameEvent.LanternReturned);
    }

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

        AdvanceConditions(deltaSeconds);

        if (_status != GameStatus.InProgress)
        {
            return;
        }

        // Pack replaces the individual clocks with one: every hunter that is not stunned steps on
        // the same beat, so the group commits instead of arriving in a trickle.
        if (HasCondition(DescentCondition.Pack))
        {
            _packElapsed += deltaSeconds * HunterTimeScale;
            var interval = _config.EnemyStepSeconds;

            if (_packElapsed >= interval)
            {
                _packElapsed -= interval;

                foreach (var hunter in _hunters)
                {
                    if (hunter.StunSeconds > 0)
                    {
                        hunter.Elapsed = 0;
                        continue;
                    }

                    AdvanceEnemy(hunter, interval);

                    if (_status != GameStatus.InProgress)
                    {
                        return;
                    }
                }
            }

            return;
        }

        // The Listener has no clock of its own: the player's steps are the only thing that winds
        // the hunt, so this loop is skipped entirely when that condition is in force.
        if (HasCondition(DescentCondition.Listener))
        {
            return;
        }

        // Every hunter keeps its own clock, so two of them with different step times interleave
        // instead of moving in lockstep. A stunned one neither moves nor banks progress towards
        // its next step: the stun has to be a real window, not a step that lands the instant the
        // player stops running.
        foreach (var hunter in _hunters)
        {
            if (hunter.StunSeconds > 0)
            {
                hunter.Elapsed = 0;
                continue;
            }

            var interval = SecondsUntilNextStep(hunter);

            hunter.Elapsed = Math.Min(hunter.Elapsed + (deltaSeconds * HunterTimeScale), interval);
            if (hunter.Elapsed < interval)
            {
                continue;
            }

            hunter.Elapsed -= interval;
            AdvanceEnemy(hunter, interval);

            // A catch or a cleared floor stops the rest of the loop: the remaining hunters have no
            // business taking a step in the same frame.
            if (_status != GameStatus.InProgress)
            {
                return;
            }
        }
    }

    /// <summary>Gets the current multiplier on how fast the hunters' clocks run.</summary>
    /// <remarks>
    /// Drag slows the clock to 40%; its hangover speeds it up by 10%. Both are one number so the
    /// individual-hunter and Pack clocks cannot disagree about what time it is.
    /// </remarks>
    private double HunterTimeScale =>
        _dragSecondsLeft > 0 ? 0.4 : _dragPenaltyLeft > 0 ? 1.1 : 1.0;

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
                Raise(GameEvent.SealCracked);
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

        foreach (var hunter in _hunters)
        {
            if (hunter.StunSeconds > 0)
            {
                hunter.StunSeconds = Math.Max(0, hunter.StunSeconds - deltaSeconds);
            }
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

        foreach (var hunter in _hunters)
        {
            if (hunter.StepLeft > 0)
            {
                hunter.StepLeft = Math.Max(0, hunter.StepLeft - deltaSeconds);
            }
        }

        if (_veilChannelLeft > 0)
        {
            _veilChannelLeft = Math.Max(0, _veilChannelLeft - deltaSeconds);

            if (_veilChannelLeft <= 0)
            {
                CompleteVeil();
            }
        }

        if (_veilSecondsLeft > 0)
        {
            _veilSecondsLeft = Math.Max(0, _veilSecondsLeft - deltaSeconds);

            if (_veilSecondsLeft <= 0)
            {
                BreakVeil();
            }
        }

        if (_dragSecondsLeft > 0)
        {
            _dragSecondsLeft = Math.Max(0, _dragSecondsLeft - deltaSeconds);

            if (_dragSecondsLeft <= 0)
            {
                _dragPenaltyLeft = _config.DragPenaltySeconds;
                InvalidateEnemyRoute();
            }
        }
        else if (_dragPenaltyLeft > 0)
        {
            _dragPenaltyLeft = Math.Max(0, _dragPenaltyLeft - deltaSeconds);
        }

        if (_lantern is not null)
        {
            _lanternSecondsLeft = Math.Max(0, _lanternSecondsLeft - deltaSeconds);

            if (_lanternSecondsLeft <= 0)
            {
                ClearLantern();
            }
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
    /// Returns how long <paramref name="hunter"/> needs before its next step: one normal step,
    /// times the cost of the cell it is about to enter, or the full tear-down time when that cell
    /// holds a sealed barrier.
    /// </summary>
    /// <param name="hunter">The hunter whose next step is being timed.</param>
    /// <remarks>
    /// Charging the delay before the move (rather than after) is what makes a barrier read
    /// correctly: the killer stalls at the barrier, then forces its way through, instead of
    /// appearing to pass it instantly and then idling on the other side. A sealed barrier is
    /// priced at <see cref="GameConfig.BarrierBreakSeconds"/> rather than a multiple of the step
    /// time because tearing it apart is a different action from walking.
    /// </remarks>
    private double SecondsUntilNextStep(Hunter hunter)
    {
        var route = EnemyRouteToTarget(hunter);
        var hunted = HasCondition(DescentCondition.Hunted) && hunter == _hunters[0];

        if (route.Count <= hunter.NextStep)
        {
            return hunted ? _config.EnemyStepSeconds * 0.8 : _config.EnemyStepSeconds;
        }

        var next = route[hunter.NextStep];
        var interval = _barrierSeals.ContainsKey(next)
            ? _config.BarrierBreakSeconds
            : _config.EnemyStepSeconds * CostOfEntering(next);

        // Hunted is one relentless pursuer, not a faster pack: only the first hunter gets the
        // 20% edge, so the other hunters stay readable and dodgeable.
        return hunted ? interval * 0.8 : interval;
    }

    /// <summary>
    /// Moves one hunter a cell along its route, ending the game if that brings it onto the
    /// player's cell, or tears apart a sealed barrier that stands in the way.
    /// </summary>
    /// <param name="hunter">The hunter that is stepping.</param>
    /// <param name="interval">
    /// How long the step that just completed took. Recorded so the renderer can slide the hunter
    /// over the same time, whatever cell it walked into.
    /// </param>
    /// <remarks>
    /// Tearing the seal is a step of its own: the barrier shatters, nothing moves, and the next
    /// route is computed over plain floor. That is what makes a sealed barrier read as an
    /// obstacle the killer deals with, rather than something it walks through after a pause.
    /// </remarks>
    private void AdvanceEnemy(Hunter hunter, double interval)
    {
        var route = EnemyRouteToTarget(hunter);

        // A single-cell route means the hunter is already on its destination, which either has
        // already ended the game or is the wisp it just consumed; nothing left to do.
        if (route.Count <= 1 || hunter.NextStep >= route.Count)
        {
            return;
        }

        var next = route[hunter.NextStep];

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

        hunter.From = hunter.Cell;
        hunter.Cell = next;
        hunter.StepLeft = interval;
        hunter.StepTotal = interval;
        hunter.NextStep++;
        Raise(GameEvent.KillerStep);

        // The hunters carry their own light, so what the player can see changes as they move
        // even when the player stands still.
        RecomputeVisibility();

        // The rest of the cached route is still the best way to a player who has not moved,
        // so it is reused rather than recomputed every step.
        if (hunter.Cell == _player)
        {
            TakeHit(hunter);
            return;
        }

        // The lantern is what the hunters were walking toward, so reaching it puts the light
        // out: the throw buys time, but it is not a safe place to leave the only light.
        if (_lantern is { } lantern && hunter.Cell == lantern)
        {
            ClearLantern();
        }

        // Reaching the wisp spends it: the decoy has done its job and the chase resumes.
        if (hunter.Cell == _lure)
        {
            Raise(GameEvent.WispConsumed);
            ClearLure();

            // A card may make the wisp cost the hunter real time. The stun is the same window a
            // catch buys, so it cannot be shortened by a frame landing mid-step.
            if (_config.LureStunSeconds > 0)
            {
                hunter.StunSeconds = Math.Max(hunter.StunSeconds, _config.LureStunSeconds);
                hunter.Elapsed = 0;
            }
        }
    }

    /// <summary>
    /// Spends one heart on a catch and resets the chase, or ends the run when that was the last
    /// heart.
    /// </summary>
    /// <param name="culprit">The hunter that reached the player.</param>
    /// <remarks>
    /// The player keeps their cell and everything they have collected; only the culprit is thrown.
    /// That is what makes a mistake cost tempo instead of progress, which is the whole point of
    /// hearts in a 20-floor run. The invulnerability guard belongs here as well as at the two
    /// call sites, so a future third way to be caught cannot deal two hearts at once. Every hunter
    /// is stunned, not just the one that fed: a reprieve that leaves a second monster walking is
    /// not a reprieve, and the deep floors would otherwise be unwinnable after one mistake.
    /// </remarks>
    private void TakeHit(Hunter culprit)
    {
        ArgumentNullException.ThrowIfNull(culprit);

        if (_invulnerableSeconds > 0)
        {
            return;
        }

        // Featherfall spends a whole miss: no heart, but the hunter is still thrown back and the
        // grace window still starts, so the catch is survived rather than ignored.
        if (_config.FirstCatchFree && !_freeCatchUsed)
        {
            _freeCatchUsed = true;
            Raise(GameEvent.Graced);
        }
        else
        {
            _hearts -= _config.CatchCost;

            // Bloom turns the fatal catch into a heart, once per floor. The chase still resets
            // below, so it is a fresh attempt rather than a reprieve in place.
            if (_hearts <= 0 && _config.ReviveOnce && !_reviveUsed)
            {
                _reviveUsed = true;
                _hearts = 1;
                Raise(GameEvent.Revived);
            }
            else if (_hearts <= 0)
            {
                _hearts = 0;
                _status = GameStatus.Caught;
                Raise(GameEvent.HeartLostLast);
                Raise(GameEvent.RunLost);
                return;
            }
            else
            {
                Raise(GameEvent.HeartLost);
            }
        }

        // Being caught is the opposite of being unseen. Clear the veil before the throw so the
        // reprieve cannot be spent while the player is already invisible.
        BreakVeil();

        // The search skips the other hunters: a throw has to be a throw, not a stack of two
        // monsters on one cell.
        var landing = ChooseRespawnCell(culprit);
        culprit.From = landing;
        culprit.Cell = landing;
        culprit.StepLeft = 0;
        culprit.StepTotal = 0;

        _invulnerableSeconds = _config.InvulnerabilitySeconds;

        foreach (var hunter in _hunters)
        {
            hunter.StunSeconds = Math.Max(hunter.StunSeconds, _config.RespawnStunSeconds);
            hunter.Elapsed = 0;
        }

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
    private Cell ChooseRespawnCell(Hunter culprit)
    {
        var reachable = DistancesFrom(_maze, _player, _maze.CellCount);

        return FindFurthest(reachable, avoidObjectives: true, culprit)
            ?? FindFurthest(reachable, avoidObjectives: false, culprit)
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
    /// <param name="culprit">The hunter being thrown; every other hunter's cell is skipped.</param>
    private Cell? FindFurthest(Dictionary<Cell, int> distances, bool avoidObjectives, Hunter culprit)
    {
        Cell? best = null;
        var bestDistance = -1;
        var bestIsDeadEnd = false;

        foreach (var (cell, distance) in distances)
        {
            if (cell == _player ||
                (avoidObjectives && IsObjectiveCell(cell)) ||
                (HunterAt(cell) is { } occupant && occupant != culprit))
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
        Raise(GameEvent.SealBroken);
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
    private IReadOnlyList<Cell> EnemyRouteToTarget(Hunter hunter)
    {
        if (hunter.Route is not null)
        {
            return hunter.Route;
        }

        // While the lantern burns, it is the target: the hunters see the light, not the player.
        // A veil freezes the target at the cell they last saw, so the player can step away while
        // the hunt searches the room they left.
        var destination = _lantern
            ?? (IsVeiled ? hunter.KnownPlayer ?? _player : _player);

        var toPlayer = _pathfinder.FindPath(_maze, hunter.Cell, destination, cell => CostOfEnteringFor(hunter, cell));
        hunter.Route = toPlayer;

        // A wisp under the hunter's own feet is not a target: its route would be a single
        // cell, which would stall the hunt instead of redirecting it. Placement refuses that
        // cell as well; this is the belt-and-braces half of the same rule.
        if (_lure is { } lure && lure != hunter.Cell)
        {
            var toLure = _pathfinder.FindPath(_maze, hunter.Cell, lure, cell => CostOfEnteringFor(hunter, cell));

            if (toLure.Count > 0 && RouteCost(toLure) < RouteCost(toPlayer))
            {
                hunter.Route = toLure;
            }
        }

        hunter.NextStep = 1;
        return hunter.Route;
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

        // A burning wisp, an Eclipse or a veil can shrink the torch, so read the radius in force
        // rather than the floor's own number. The light comes from the lantern when it is thrown.
        var visionRadius = EffectiveVisionRadius;
        var searchRadius = Math.Max(visionRadius, _config.EnemyAuraDistance);
        var origin = LightOrigin;
        var lightDistances = DistancesFrom(_maze, origin, searchRadius);
        var playerDistances = origin == _player
            ? lightDistances
            : DistancesFrom(_maze, _player, _config.EnemyAuraDistance);

        foreach (var (cell, distance) in lightDistances)
        {
            if (distance <= visionRadius)
            {
                _visible.Add(cell);
            }
        }

        // The lantern spills onto the rock around the light, walls or no walls. Walking distance
        // alone leaves the far side of a wall undrawn, which reads as a hole in the corridor
        // rather than as stone; this is the one place sight is allowed to ignore a wall.
        foreach (var cell in CellsWithin(origin, NearSightRadius))
        {
            _visible.Add(cell);
        }

        // A wisp is a beacon the player placed, so its own pool of light is visible even where
        // the torch does not reach. It is deliberately not part of EnemyGlow: it reveals the
        // wisp, nothing about where the killer is.
        if (_lure is { } lure)
        {
            foreach (var cell in DistancesFrom(_maze, lure, LureGlowRadius).Keys)
            {
                _visible.Add(cell);
            }
        }

        // A hunter further away than its aura is invisible, and so is the light it carries. Each
        // hunter is asked separately, so one that has just been thrown into the dark does not dim
        // the one standing three cells behind you.
        for (var index = 0; index < _hunters.Count; index++)
        {
            var hunter = _hunters[index];
            var alwaysVisible = HasCondition(DescentCondition.Hunted) && index == 0;

            if (!alwaysVisible &&
                (!playerDistances.TryGetValue(hunter.Cell, out var enemyDistance) ||
                 enemyDistance > _config.EnemyAuraDistance))
            {
                continue;
            }

            foreach (var cell in DistancesFrom(_maze, hunter.Cell, _config.EnemyAuraRadius).Keys)
            {
                _enemyGlow.Add(cell);
                _visible.Add(cell);
            }
        }
    }

    /// <summary>
    /// Returns every cell whose straight-line distance from <paramref name="origin"/> is at most
    /// <paramref name="radius"/>, clipped to the maze.
    /// </summary>
    /// <param name="origin">The centre of the circle.</param>
    /// <param name="radius">The circle's radius in cells; zero yields the origin alone.</param>
    /// <returns>The cells in no particular order.</returns>
    private IEnumerable<Cell> CellsWithin(Cell origin, int radius)
    {
        if (radius <= 0)
        {
            yield return origin;
            yield break;
        }

        var squared = radius * radius;

        for (var rowDelta = -radius; rowDelta <= radius; rowDelta++)
        {
            for (var columnDelta = -radius; columnDelta <= radius; columnDelta++)
            {
                if ((rowDelta * rowDelta) + (columnDelta * columnDelta) > squared)
                {
                    continue;
                }

                var row = origin.Row + rowDelta;
                var column = origin.Column + columnDelta;

                if (row < 0 || row >= _maze.Rows || column < 0 || column >= _maze.Columns)
                {
                    continue;
                }

                yield return _maze.GetCell(row, column);
            }
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
    private static Dictionary<Cell, int> DistancesFrom(Grid maze, Cell origin, int radius)
    {
        ArgumentNullException.ThrowIfNull(maze);

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
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbour = maze.GetCell(cell.Row + rowDelta, cell.Column + columnDelta);

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

    /// <summary>How many normal steps a hunter will pay to avoid standing on another one.</summary>
    private const int HunterStackCost = 4;

    /// <summary>
    /// The cost for <paramref name="hunter"/> to enter <paramref name="cell"/>: the normal price,
    /// plus a surcharge for a cell another hunter is standing on.
    /// </summary>
    /// <param name="hunter">The hunter that is routing.</param>
    /// <param name="cell">The cell it would enter.</param>
    /// <returns>The price in normal steps.</returns>
    /// <remarks>
    /// The surcharge is finite on purpose. Two hunters may share a cell when the maze leaves them
    /// no alternative, but they should look for a way around first: a stack of two monsters reads
    /// as one, and the player cannot tell there are two of them in the corridor.
    /// </remarks>
    private int CostOfEnteringFor(Hunter hunter, Cell cell) =>
        CostOfEntering(cell) + (HunterAt(cell) is { } occupant && occupant != hunter ? HunterStackCost : 0);

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

    /// <summary>Runs the timed part of every descent condition in force.</summary>
    /// <param name="deltaSeconds">The frame's length.</param>
    /// <remarks>
    /// Regrowth and Fracture mutate the maze, so both invalidate the hunters' cached routes and
    /// rebuild visibility. Eclipse toggles the torch and repaints what the player knows.
    /// </remarks>
    private void AdvanceConditions(double deltaSeconds)
    {
        if (HasCondition(DescentCondition.Regrowth))
        {
            _regrowthSecondsLeft -= deltaSeconds;

            if (_regrowthSecondsLeft <= 0)
            {
                SealDeadEnd();
                _regrowthSecondsLeft = RegrowthIntervalSeconds;
            }
        }

        if (HasCondition(DescentCondition.Fracture))
        {
            _fractureSecondsLeft -= deltaSeconds;

            if (_fractureSecondsLeft <= 0)
            {
                FractureMaze();
                _fractureSecondsLeft = FractureIntervalSeconds;
            }
        }

        if (HasCondition(DescentCondition.Eclipse))
        {
            _eclipseSecondsLeft -= deltaSeconds;

            if (_eclipseSecondsLeft <= 0)
            {
                _eclipseDark = !_eclipseDark;
                _eclipseSecondsLeft = _eclipseDark ? EclipseDarkSeconds : EclipseLitSeconds;
                RecomputeVisibility();
                Raise(_eclipseDark ? GameEvent.EclipseFell : GameEvent.EclipseLifted);
            }
        }
    }

    /// <summary>
    /// Seals the mouth of one dead end, never one holding the player, a key, the exit, a hunter
    /// or the lantern.
    /// </summary>
    /// <remarks>
    /// A dead end has one passage, so closing it cannot disconnect anything the player needs; it
    /// just makes the maze surrender a hiding place. If every dead end is occupied there is
    /// nothing to do, and the next interval tries again.
    /// </remarks>
    private void SealDeadEnd()
    {
        var candidates = new List<(Cell Cell, Direction Direction)>();

        for (var row = 0; row < _maze.Rows; row++)
        {
            for (var column = 0; column < _maze.Columns; column++)
            {
                if (_maze.IsSolid(row, column))
                {
                    continue;
                }

                var open = _maze.OpenDirections(row, column);
                if (open.Count != 1)
                {
                    continue;
                }

                var cell = _maze.GetCell(row, column);

                if (cell == _player ||
                    _keys.Contains(cell) ||
                    cell == _exit ||
                    HunterAt(cell) is not null ||
                    (_lantern is { } lantern && cell == lantern))
                {
                    continue;
                }

                candidates.Add((cell, open[0]));
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var (deadEnd, passage) = candidates[_random.NextInt(candidates.Count)];
        _maze.AddWall(deadEnd.Row, deadEnd.Column, passage);
        Raise(GameEvent.WallSealed);
        InvalidateEnemyRoute();
        RecomputeVisibility();
    }

    /// <summary>
    /// Opens one wall and closes another, skipping any close that would disconnect the exit, a
    /// key or a hunter from the player.
    /// </summary>
    /// <remarks>
    /// The opening is always safe: removing a wall can only add routes. The close is tried on
    /// passages between two cells that both have another way out, then checked against the
    /// things the floor still needs; a candidate that fails is put back and the next is tried.
    /// </remarks>
    private void FractureMaze()
    {
        (Cell Cell, Direction Direction)? opened = OpenRandomWall();
        CloseRandomPassage(opened);
        InvalidateEnemyRoute();
        RecomputeVisibility();
    }

    /// <summary>Removes one interior wall at random and raises the event for it.</summary>
    /// <returns>The wall that was opened, for the close step to avoid undoing it.</returns>
    private (Cell Cell, Direction Direction)? OpenRandomWall()
    {
        var walls = new List<(Cell Cell, Direction Direction)>();

        for (var row = 0; row < _maze.Rows; row++)
        {
            for (var column = 0; column < _maze.Columns; column++)
            {
                if (_maze.IsSolid(row, column))
                {
                    continue;
                }

                foreach (var direction in new[] { Direction.East, Direction.South })
                {
                    var (rowDelta, columnDelta) = direction.Delta();
                    var neighbourRow = row + rowDelta;
                    var neighbourColumn = column + columnDelta;

                    if (neighbourRow < 0 || neighbourRow >= _maze.Rows ||
                        neighbourColumn < 0 || neighbourColumn >= _maze.Columns ||
                        _maze.IsSolid(neighbourRow, neighbourColumn) ||
                        !_maze.HasWall(row, column, direction))
                    {
                        continue;
                    }

                    walls.Add((_maze.GetCell(row, column), direction));
                }
            }
        }

        if (walls.Count == 0)
        {
            return null;
        }

        var chosen = walls[_random.NextInt(walls.Count)];
        _maze.RemoveWall(chosen.Cell.Row, chosen.Cell.Column, chosen.Direction);
        Raise(GameEvent.WallOpened);
        return chosen;
    }

    /// <summary>
    /// Closes one passage that keeps every key, the exit and every hunter reachable.
    /// </summary>
    /// <param name="opened">The wall just opened, excluded so Fracture cannot simply undo itself.</param>
    private void CloseRandomPassage((Cell Cell, Direction Direction)? opened)
    {
        var passages = new List<(Cell Cell, Direction Direction)>();

        for (var row = 0; row < _maze.Rows; row++)
        {
            for (var column = 0; column < _maze.Columns; column++)
            {
                if (_maze.IsSolid(row, column) || _maze.OpenDirections(row, column).Count < 2)
                {
                    continue;
                }

                foreach (var direction in new[] { Direction.East, Direction.South })
                {
                    var (rowDelta, columnDelta) = direction.Delta();
                    var neighbourRow = row + rowDelta;
                    var neighbourColumn = column + columnDelta;

                    if (neighbourRow < 0 || neighbourRow >= _maze.Rows ||
                        neighbourColumn < 0 || neighbourColumn >= _maze.Columns ||
                        _maze.IsSolid(neighbourRow, neighbourColumn) ||
                        _maze.HasWall(row, column, direction) ||
                        _maze.OpenDirections(neighbourRow, neighbourColumn).Count < 2)
                    {
                        continue;
                    }

                    var candidate = (_maze.GetCell(row, column), direction);

                    if (opened is { } open &&
                        open.Cell == candidate.Item1 && open.Direction == candidate.Item2)
                    {
                        continue;
                    }

                    passages.Add(candidate);
                }
            }
        }

        if (passages.Count == 0)
        {
            return;
        }

        _random.Shuffle(passages);

        foreach (var (cell, direction) in passages)
        {
            _maze.AddWall(cell.Row, cell.Column, direction);

            if (ConnectivityPreserved())
            {
                Raise(GameEvent.WallClosed);
                return;
            }

            _maze.RemoveWall(cell.Row, cell.Column, direction);
        }
    }

    /// <summary>
    /// Returns whether the player can still reach the exit, every key and every hunter.
    /// </summary>
    /// <remarks>
    /// Empty branches are allowed to be cut off: Regrowth deliberately seals dead ends, and a
    /// closed corridor that holds nothing the floor needs is a legitimate new shape.
    /// </remarks>
    private bool ConnectivityPreserved()
    {
        var reachable = DistancesFrom(_maze, _player, _maze.CellCount);

        if (!reachable.ContainsKey(_exit))
        {
            return false;
        }

        foreach (var key in _keys)
        {
            if (!reachable.ContainsKey(key))
            {
                return false;
            }
        }

        foreach (var hunter in _hunters)
        {
            if (!reachable.ContainsKey(hunter.Cell))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Moves the exit to a far cell, away from the player and the things still on the floor.</summary>
    /// <remarks>
    /// Drift is announced by the sound layer and by the arch itself changing cells. The new cell
    /// is the furthest reachable one that holds nothing, so it is always findable.
    /// </remarks>
    private void RelocateExit()
    {
        var distances = DistancesFrom(_maze, _player, _maze.CellCount);
        Cell? best = null;
        var bestDistance = -1;

        foreach (var (cell, distance) in distances)
        {
            if (cell == _player ||
                _keys.Contains(cell) ||
                cell == _exit ||
                HunterAt(cell) is not null ||
                (_lantern is { } lantern && cell == lantern))
            {
                continue;
            }

            if (distance > bestDistance ||
                (distance == bestDistance && best is { } current && Precedes(cell, current)))
            {
                best = cell;
                bestDistance = distance;
            }
        }

        if (best is not { } newExit || newExit == _exit)
        {
            return;
        }

        _exit = newExit;
        Raise(GameEvent.ExitMoved);
    }

    /// <summary>
    /// Winds the hunters forward by one step after a player step under The Listener.
    /// </summary>
    /// <param name="sprinting">Whether the step was a sprint, which is worth a second step.</param>
    private void CreditListenerStep(bool sprinting)
    {
        foreach (var hunter in _hunters)
        {
            if (hunter.StunSeconds > 0)
            {
                continue;
            }

            var interval = SecondsUntilNextStep(hunter);
            AdvanceEnemy(hunter, interval);

            if (_status != GameStatus.InProgress)
            {
                return;
            }

            if (sprinting)
            {
                AdvanceEnemy(hunter, interval);

                if (_status != GameStatus.InProgress)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Returns the hunter standing on <paramref name="cell"/>, or <see langword="null"/> when
    /// the cell is empty.
    /// </summary>
    /// <param name="cell">The cell to inspect.</param>
    /// <returns>The hunter on that cell, or null.</returns>
    private Hunter? HunterAt(Cell cell)
    {
        foreach (var hunter in _hunters)
        {
            if (hunter.Cell == cell)
            {
                return hunter;
            }
        }

        return null;
    }

    /// <summary>
    /// Marks every cached hunter route as out of date; the next step recomputes them.
    /// </summary>
    private void InvalidateEnemyRoute()
    {
        foreach (var hunter in _hunters)
        {
            hunter.Route = null;
            hunter.NextStep = 0;
        }
    }

    /// <summary>
    /// One hunter's private state: where it is, where it stepped from, how long that step is
    /// taking, and the route it is following. Kept as a mutable class rather than a record so the
    /// rules can update it in place, and private so the only way to read it is <see cref="Enemies"/>.
    /// </summary>
    private sealed class Hunter
    {
        /// <summary>Creates a hunter standing on <paramref name="cell"/>, going nowhere yet.</summary>
        /// <param name="cell">The cell it starts on.</param>
        public Hunter(Cell cell)
        {
            Cell = cell;
            From = cell;
        }

        /// <summary>Gets or sets the cell it is on.</summary>
        public Cell Cell { get; set; }

        /// <summary>Gets or sets the cell it stepped from, for the renderer's slide.</summary>
        public Cell From { get; set; }

        /// <summary>Gets or sets the seconds left in its current step.</summary>
        public double StepLeft { get; set; }

        /// <summary>Gets or sets how long that step was, so the slide can be a fraction.</summary>
        public double StepTotal { get; set; }

        /// <summary>Gets or sets how far it has banked towards its next step.</summary>
        public double Elapsed { get; set; }

        /// <summary>Gets or sets the seconds of stun left, during which it neither moves nor banks.</summary>
        public double StunSeconds { get; set; }

        /// <summary>Gets or sets the cached route to its chosen target.</summary>
        public IReadOnlyList<Cell>? Route { get; set; }

        /// <summary>Gets or sets the index of the next cell on that route.</summary>
        public int NextStep { get; set; }

        /// <summary>
        /// Gets or sets the cell this hunter last saw the player on, used while a veil is up.
        /// Null means it is not searching a remembered cell.
        /// </summary>
        public Cell? KnownPlayer { get; set; }
    }
}

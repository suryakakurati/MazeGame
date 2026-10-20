using System;

namespace MazeGame.Core.Game;

/// <summary>
/// Every tunable number in the game, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Game logic reads its numbers from here instead of hard-coding them, so difficulty can be
/// changed (or tested) without touching rules. Defaults describe the intended game:
/// a 15x15 maze with three keys, three barriers, two lures and a killer that takes a step
/// every six tenths of a second.
/// </para>
/// <para>
/// Construct with an object initializer and call <see cref="Validate"/> before use
/// (a <see cref="GameState"/> does that for you):
/// <c>new GameConfig { Rows = 21, Columns = 21, Seed = 7 }</c>.
/// </para>
/// <para>
/// The values are init-only, so a config can be shared safely once built — nothing can
/// change it mid-game.
/// </para>
/// </remarks>
public sealed class GameConfig
{
    /// <summary>
    /// Gets the number of maze rows. Defaults to 15.
    /// </summary>
    public int Rows { get; init; } = 15;

    /// <summary>
    /// Gets the number of maze columns. Defaults to 15.
    /// </summary>
    public int Columns { get; init; } = 15;

    /// <summary>
    /// Gets how many keys must be collected before the exit opens. Defaults to 3.
    /// Zero is allowed and means the exit is open from the start.
    /// </summary>
    public int KeyCount { get; init; } = 3;

    /// <summary>
    /// Gets how many normal cells a spent (already cracked) barriered cell is worth to the
    /// killer. Defaults to 5, against a normal cell's cost of 1. It is both the Dijkstra weight
    /// of that cell and the number of normal steps the killer needs to enter it, so a spent
    /// barrier always costs real time: with the default step time that is a 3-second pause.
    /// Must be at least 1: barriers slow the killer down, they never make a cell impassable.
    /// </summary>
    /// <remarks>
    /// Fresh barriers are sealed instead and are governed by <see cref="BarrierSealSeconds"/>,
    /// <see cref="BarrierBreakSeconds"/> and <see cref="SealedBarrierCost"/>; this value is what
    /// a barrier is worth once it has cracked.
    /// </remarks>
    public int ObstacleCost { get; init; } = 5;

    /// <summary>
    /// Gets how many barriers the player may place in one game. Defaults to 3.
    /// </summary>
    public int BarrierBudget { get; init; } = 3;

    /// <summary>
    /// Gets how long a freshly placed barrier stays sealed: the killer treats a sealed barrier
    /// as a wall and looks for another way round. Defaults to 10 seconds, deliberately longer
    /// than the walk from the killer's usual distance so a barrier put on its route is still
    /// sealed when it arrives. Zero means barriers are never sealed and only ever cost
    /// <see cref="ObstacleCost"/>. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// After the seal lapses the barrier cracks into a spent husk that is still expensive to
    /// cross, so a barrier has two phases: a short wall, then a permanent speed bump.
    /// </remarks>
    public double BarrierSealSeconds { get; init; } = 10.0;

    /// <summary>
    /// Gets how long the killer takes to tear a sealed barrier apart when the seal is the only
    /// way to reach the player. Defaults to 3 seconds, so a barrier that truly cuts the route
    /// is still broken through rather than soft-locking the game. Must be finite and greater
    /// than zero.
    /// </summary>
    public double BarrierBreakSeconds { get; init; } = 3.0;

    /// <summary>
    /// Gets what a sealed barrier is worth to the killer's pathfinding, in normal cells.
    /// Defaults to 40, which is far more than any detour a 15x15 maze offers, so the killer
    /// takes another route while the seal holds and only attacks the barrier when there is
    /// none. Must be at least 1.
    /// </summary>
    public int SealedBarrierCost { get; init; } = 40;

    /// <summary>
    /// Gets how many wisps the player may place in one game. Defaults to 2.
    /// </summary>
    public int LureCount { get; init; } = 2;

    /// <summary>
    /// Gets how many seconds a placed wisp keeps glowing. Defaults to 6, which is roughly ten
    /// killer steps: long enough to pull it down a side branch, short enough that it cannot be
    /// parked. Must be finite and greater than zero.
    /// </summary>
    public double LureSeconds { get; init; } = 6.0;

    /// <summary>
    /// Gets the seconds the killer needs to enter a normal cell. Defaults to 0.6, which is
    /// slow enough for the player to keep walking away. A barriered cell multiplies this by
    /// <see cref="ObstacleCost"/>. Must be positive.
    /// </summary>
    public double EnemyStepSeconds { get; init; } = 0.6;

    /// <summary>
    /// Gets the radius, in walking steps, that the player can see. Defaults to 3. Zero means
    /// the player sees only their own cell, and walls block sight, so this is a torch range
    /// down corridors rather than a circle. Must not be negative.
    /// </summary>
    public int VisionRadius { get; init; } = 3;

    /// <summary>
    /// Gets how many extra interior walls the generator removes to create loops. Defaults to
    /// 8, which gives a 15x15 maze a handful of alternate routes rather than a warren. Zero
    /// keeps the maze perfect (exactly one route between any two cells). Must not be negative.
    /// </summary>
    /// <remarks>
    /// Loops matter for play, not just for looks: a perfect maze leaves barriers with nothing
    /// to redirect the killer onto, while a braided one lets a well-placed barrier cost the
    /// killer real distance.
    /// </remarks>
    public int ExtraLoops { get; init; } = 8;

    /// <summary>
    /// Gets the share of the maze's dead ends that are braided open, as a percentage. Defaults
    /// to 55, which leaves roughly 22 dead ends on a 15x15 maze instead of 57 and turns most
    /// barrier placements into real detours. Zero keeps every dead end. Must be between 0 and
    /// 100 inclusive.
    /// </summary>
    /// <remarks>
    /// This is the setting that decides how often a chase ends in a corner. It is separate from
    /// <see cref="ExtraLoops"/> because the two do different jobs: braiding removes traps, extra
    /// loops add shortcuts across the map.
    /// </remarks>
    public int DeadEndBraidPercent { get; init; } = 55;

    /// <summary>
    /// Gets the radius, in cells, lit by the killer itself. Defaults to 2. Zero means only the
    /// killer's own cell glows. Must not be negative.
    /// </summary>
    public int EnemyAuraRadius { get; init; } = 2;

    /// <summary>
    /// Gets how many walking steps away the killer may be before its own light becomes visible
    /// to the player. Defaults to 6, roughly three and a half seconds of running. Zero means
    /// the glow appears only once the killer reaches the player's cell. Must not be negative.
    /// </summary>
    public int EnemyAuraDistance { get; init; } = 6;

    /// <summary>
    /// Gets the seed for maze generation and entity placement, or <see langword="null"/>
    /// for a different game on every run. The same seed always produces the same game.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Checks that the settings describe a game that can actually be played.
    /// </summary>
    /// <remarks>
    /// The size rule is the interesting one: the player, the killer, the exit and every key
    /// need their own cell, so the maze must have at least <c>3 + KeyCount</c> cells. Catching
    /// that here turns a confusing "no free cell" failure at placement time into one clear
    /// error before anything is built.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any individual setting is outside its allowed range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the maze is too small to hold the player, killer, exit and keys.
    /// </exception>
    public void Validate()
    {
        if (Rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows), Rows, "A game needs at least one row.");
        }

        if (Columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Columns), Columns, "A game needs at least one column.");
        }

        if (KeyCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KeyCount), KeyCount, "The key count cannot be negative.");
        }

        if (ObstacleCost < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ObstacleCost),
                ObstacleCost,
                "A barrier must cost at least 1 to enter; it can slow the killer but never block it.");
        }

        if (BarrierBudget < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierBudget),
                BarrierBudget,
                "The barrier budget cannot be negative.");
        }

        if (SealedBarrierCost < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SealedBarrierCost),
                SealedBarrierCost,
                "A sealed barrier must cost at least 1 to enter; it is a detour, never a hole in the map.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(BarrierSealSeconds >= 0) || double.IsInfinity(BarrierSealSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierSealSeconds),
                BarrierSealSeconds,
                "The seal time must be a finite, non-negative number of seconds.");
        }

        if (!(BarrierBreakSeconds > 0) || double.IsInfinity(BarrierBreakSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierBreakSeconds),
                BarrierBreakSeconds,
                "The time to break a sealed barrier must be a finite, positive number of seconds.");
        }

        if (LureCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureCount),
                LureCount,
                "The wisp count cannot be negative.");
        }

        if (!(LureSeconds > 0) || double.IsInfinity(LureSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureSeconds),
                LureSeconds,
                "A wisp must last a finite, positive number of seconds.");
        }

        if (DeadEndBraidPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DeadEndBraidPercent),
                DeadEndBraidPercent,
                "The braided share must be a percentage between 0 and 100.");
        }

        // Written as "not greater than zero" so NaN is rejected as well as 0 and negatives.
        if (!(EnemyStepSeconds > 0) || double.IsInfinity(EnemyStepSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyStepSeconds),
                EnemyStepSeconds,
                "The killer's step time must be a finite, positive number of seconds.");
        }

        if (VisionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VisionRadius),
                VisionRadius,
                "The vision radius cannot be negative.");
        }

        if (ExtraLoops < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExtraLoops),
                ExtraLoops,
                "The number of extra loops cannot be negative.");
        }

        if (EnemyAuraRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyAuraRadius),
                EnemyAuraRadius,
                "The killer's glow radius cannot be negative.");
        }

        if (EnemyAuraDistance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyAuraDistance),
                EnemyAuraDistance,
                "The distance at which the killer's glow appears cannot be negative.");
        }

        // long arithmetic: a huge row count must not overflow into a "big enough" answer.
        var requiredCells = 3L + KeyCount;
        if ((long)Rows * Columns < requiredCells)
        {
            throw new ArgumentException(
                $"A {Rows}x{Columns} maze has {Rows * (long)Columns} cells, but the player, killer, exit and " +
                $"{KeyCount} key(s) need {requiredCells} different cells.",
                nameof(KeyCount));
        }
    }
}

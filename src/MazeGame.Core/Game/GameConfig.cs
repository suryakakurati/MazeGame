using System;

namespace MazeGame.Core.Game;

/// <summary>
/// Every tunable number in the game, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Game logic reads its numbers from here instead of hard-coding them, so difficulty can be
/// changed (or tested) without touching rules. Defaults describe the intended game:
/// a 15x15 maze with three keys, three barriers and a killer that takes a step every
/// six tenths of a second.
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
    /// Gets how many normal cells a barriered cell is worth to the killer. Defaults to 5,
    /// against a normal cell's cost of 1. It is both the Dijkstra weight of that cell and
    /// the number of normal steps the killer needs to enter it, so a barrier always costs
    /// real time: with the default step time that is a 3-second pause. Must be at least 1:
    /// barriers slow the killer down, they never make a cell impassable.
    /// </summary>
    public int ObstacleCost { get; init; } = 5;

    /// <summary>
    /// Gets how many barriers the player may place in one game. Defaults to 3.
    /// </summary>
    public int BarrierBudget { get; init; } = 3;

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

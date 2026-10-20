using System;

namespace MazeGame.Core.Game;

/// <summary>
/// The data for one floor of a run: its name and theme, its maze shape, and the numbers that
/// make it harder than the floor before it.
/// </summary>
/// <remarks>
/// <para>
/// A floor is data, not a subclass. Everything the game needs to build one is here, and
/// <see cref="GameConfig.ForFloor"/> composes it over the run's base settings, so adding a
/// fourth floor is a new record in <see cref="FloorCatalog"/> rather than a new code path.
/// </para>
/// <para>
/// The theme id is deliberately a string rather than a colour: Core does not know how the cave
/// is drawn. The renderer maps the id to a palette, and a missing id is a failing test rather
/// than a silent default.
/// </para>
/// </remarks>
public sealed record FloorDefinition
{
    /// <summary>
    /// Gets the floor's number in the run, starting at 1. The catalog is ordered by it.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the floor's display name, shown on its intro card (for example "The Shallows").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the theme id the renderer maps to a palette and ambience (for example "shallows").
    /// </summary>
    public required string ThemeId { get; init; }

    /// <summary>Gets the number of maze rows. Defaults to 15.</summary>
    public int Rows { get; init; } = 15;

    /// <summary>Gets the number of maze columns. Defaults to 15.</summary>
    public int Columns { get; init; } = 15;

    /// <summary>Gets how many keys the floor's exit needs. Defaults to 3.</summary>
    public int KeyCount { get; init; } = 3;

    /// <summary>
    /// Gets the seconds the killer needs per step on this floor. Defaults to 0.6, the same as
    /// the base config; later floors lower it.
    /// </summary>
    public double EnemyStepSeconds { get; init; } = 0.6;

    /// <summary>
    /// Gets the most hearts the player may hold on this floor. Defaults to 3. Hearts carried in
    /// from the previous floor start below this ceiling.
    /// </summary>
    public int MaxHearts { get; init; } = 3;

    /// <summary>
    /// Checks that the floor describes something buildable.
    /// </summary>
    /// <remarks>
    /// Capacity (the maze holding the player, killer, exit and keys) is checked by
    /// <see cref="GameConfig.Validate"/> when the floor is built, so it is not duplicated here.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a number is outside its range, or the name or theme is empty.
    /// </exception>
    public void Validate()
    {
        if (Number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Number), Number, "Floors are numbered from 1.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A floor needs a name for its intro card.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(ThemeId))
        {
            throw new ArgumentException("A floor needs a theme id for the renderer.", nameof(ThemeId));
        }

        if (Rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows), Rows, "A floor needs at least one row.");
        }

        if (Columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Columns), Columns, "A floor needs at least one column.");
        }

        if (KeyCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KeyCount), KeyCount, "The key count cannot be negative.");
        }

        if (!(EnemyStepSeconds > 0) || double.IsInfinity(EnemyStepSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyStepSeconds),
                EnemyStepSeconds,
                "The killer's step time must be a finite, positive number of seconds.");
        }

        if (MaxHearts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxHearts),
                MaxHearts,
                "A floor must allow at least one heart.");
        }
    }
}

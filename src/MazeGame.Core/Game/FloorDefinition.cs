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
    /// Gets the seconds the player waits between steps on this floor. Defaults to 0.11, about five
    /// and a half times the killer's 0.6 s pace on the first floor; later floors tighten it so the
    /// chase keeps the same shape. Zero is allowed and means instant movement, which is what hand-
    /// built test floors use. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// This is the per-floor base that cards may multiply (Light Feet, Momentum, Heavy). It lives
    /// on the floor rather than the run so the descent can tighten without a card.
    /// </remarks>
    public double PlayerStepSeconds { get; init; } = 0.11;

    /// <summary>
    /// Gets the most hearts the player may hold on this floor. Defaults to 3. Hearts carried in
    /// from the previous floor start below this ceiling.
    /// </summary>
    public int MaxHearts { get; init; } = 3;

    /// <summary>
    /// Gets the one-line gimmick shown under the floor's name on its card, for example
    /// "the lantern reaches one step here". Defaults to none.
    /// </summary>
    /// <remarks>
    /// Content, not a rule: it is the sentence that tells the player what this floor does
    /// differently, so the descent is read as well as felt.
    /// </remarks>
    public string? Gimmick { get; init; }

    /// <summary>
    /// Gets how far the lantern reaches on this floor, or null to inherit the run's setting.
    /// </summary>
    /// <remarks>
    /// The knob behind a floor that is about light rather than speed: a blind floor hunts by
    /// sound of the walls, a lit one shows you the whole room and lets the killer come at you
    /// from off screen. It also sets the camera's zoom — more vision is a wider view.
    /// </remarks>
    public int? VisionRadius { get; init; }

    /// <summary>Gets how many barriers this floor allows, or null to inherit the run's budget.</summary>
    public int? BarrierBudget { get; init; }

    /// <summary>Gets how many wisps this floor allows, or null to inherit the run's count.</summary>
    public int? LureCount { get; init; }

    /// <summary>Gets how long a wisp burns on this floor, or null to inherit the run's seconds.</summary>
    public double? LureSeconds { get; init; }

    /// <summary>Gets the extra walls the braider removes for loops, or null to inherit.</summary>
    public int? ExtraLoops { get; init; }

    /// <summary>Gets the percentage of dead ends the braider opens on this floor, or null to inherit.</summary>
    public int? DeadEndBraidPercent { get; init; }

    /// <summary>
    /// Gets how far the killer's cold glow reaches on this floor, or null to inherit. Zero hides
    /// the killer until it is in sight, which is what a stalking floor is made of.
    /// </summary>
    public int? EnemyAuraRadius { get; init; }

    /// <summary>
    /// Gets the distance at which the killer's glow appears, or null to inherit. Lower means you
    /// hear it later.
    /// </summary>
    public int? EnemyAuraDistance { get; init; }

    /// <summary>Gets how far a catch throws the killer, or null to inherit the run's distance.</summary>
    public int? RespawnDistance { get; init; }

    /// <summary>Gets the grace period after a catch, in seconds, or null to inherit.</summary>
    public double? InvulnerabilitySeconds { get; init; }

    /// <summary>Gets how long the killer is stunned after a catch, or null to inherit.</summary>
    public double? RespawnStunSeconds { get; init; }

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

        if (!(PlayerStepSeconds >= 0) || double.IsInfinity(PlayerStepSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(PlayerStepSeconds),
                PlayerStepSeconds,
                "The player's step time must be a finite, non-negative number of seconds.");
        }

        if (MaxHearts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxHearts),
                MaxHearts,
                "A floor must allow at least one heart.");
        }

        // The optional knobs are validated where they land (the composed GameConfig), but a
        // negative one is a content typo worth naming here: the floor, not the run, is wrong.
        if (VisionRadius is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(VisionRadius), VisionRadius, "Vision cannot be negative.");
        }

        if (BarrierBudget is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BarrierBudget), BarrierBudget, "A barrier budget cannot be negative.");
        }

        if (LureCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LureCount), LureCount, "A wisp count cannot be negative.");
        }

        if (ExtraLoops is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ExtraLoops), ExtraLoops, "Extra loops cannot be negative.");
        }

        if (DeadEndBraidPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DeadEndBraidPercent),
                DeadEndBraidPercent,
                "A braid percentage is between 0 and 100.");
        }

        if (EnemyAuraRadius is < 0 || EnemyAuraDistance is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EnemyAuraRadius), "The killer's glow cannot be negative.");
        }

        if (RespawnDistance is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RespawnDistance), RespawnDistance, "The respawn distance cannot be negative.");
        }

        foreach (var (value, name) in new (double? Value, string Name)[]
                 {
                     (LureSeconds, nameof(LureSeconds)),
                     (InvulnerabilitySeconds, nameof(InvulnerabilitySeconds)),
                     (RespawnStunSeconds, nameof(RespawnStunSeconds)),
                 })
        {
            if (value is { } seconds && (!(seconds >= 0) || double.IsInfinity(seconds)))
            {
                throw new ArgumentOutOfRangeException(name, seconds, "A duration must be finite and non-negative.");
            }
        }
    }
}

using System;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// Builds complete, playable games: one floor from a definition, a whole campaign, or a single
/// game from the base config.
/// </summary>
/// <remarks>
/// <para>
/// Nothing about mazes, placement or decoration belongs in an event loop. The frontends ask this
/// factory for a campaign, and a replay is simply another <see cref="CreateCampaign"/> call — so
/// "start a run" is a tested rule rather than glue duplicated in two frontends.
/// </para>
/// <para>
/// Each floor gets its own <see cref="IRandom"/> derived from the run seed and the floor number,
/// so a seeded campaign is reproducible while its floors are genuinely different layouts. An
/// unseeded run passes <see langword="null"/> and gets a new cave on every floor.
/// </para>
/// <para>
/// Floors are shaped by the two decorators the game ships with — <see cref="DeadEndBraider"/> for
/// the dead ends and <see cref="BraidedMazeGenerator"/> for the extra loops — over the untouched
/// graded <see cref="MazeGenerator"/>.
/// </para>
/// </remarks>
public sealed class GameFactory
{
    /// <summary>
    /// Distance between consecutive floors' seeds. A prime large enough that neighbouring floors
    /// do not share a random stream prefix; it only has to be stable, not special.
    /// </summary>
    private const int FloorSeedStride = 7919;

    private readonly GameConfig _config;
    private readonly Func<int?, IRandom> _createRandom;
    private readonly IPathfinder _pathfinder;

    /// <summary>
    /// Creates a factory for <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The shared rules every floor of this factory uses.</param>
    /// <param name="createRandom">
    /// Makes the random source for one game from a seed, or from <see langword="null"/> for a
    /// fresh one. Called once per floor, which is what keeps a seeded run reproducible.
    /// </param>
    /// <param name="pathfinder">The search the killer uses to chase.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="config"/> is invalid.</exception>
    public GameFactory(GameConfig config, Func<int?, IRandom> createRandom, IPathfinder pathfinder)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(createRandom);
        ArgumentNullException.ThrowIfNull(pathfinder);

        // Fail before the first run rather than in the middle of a title screen.
        config.Validate();

        _config = config;
        _createRandom = createRandom;
        _pathfinder = pathfinder;
    }

    /// <summary>
    /// Gets the settings every game from this factory uses, for window geometry and HUD counts.
    /// </summary>
    public GameConfig Config => _config;

    /// <summary>
    /// Builds the shipped campaign: three floors, in order, starting with full hearts.
    /// </summary>
    /// <returns>A run that starts on floor 1.</returns>
    /// <remarks>
    /// This is what the frontends call on the title screen and on "descend again", so a new run
    /// is always a fresh campaign with fresh floors. The campaign is given the run's seed, so a
    /// seeded run reproduces its draft offers as well as its mazes; an unseeded run passes null
    /// and gets a fresh stream of offers.
    /// </remarks>
    public Campaign CreateCampaign() => new(FloorCatalog.Shipped, CreateFloor, _config.Seed);

    /// <summary>
    /// Builds one floor of a run from its definition, the hearts the player brings and the cards
    /// they hold.
    /// </summary>
    /// <param name="floor">The floor's data.</param>
    /// <param name="startingHearts">
    /// Hearts the player has left. They become the floor's <see cref="GameConfig.StartingHearts"/>,
    /// which is how damage carries between floors.
    /// </param>
    /// <param name="modifiers">The cards taken this run, applied over the floor's config.</param>
    /// <returns>A playable floor that starts <see cref="GameStatus.InProgress"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="floor"/> or <paramref name="modifiers"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the floor is invalid, the carried hearts do not fit its ceiling, or the seeded
    /// random source is missing.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the floor's maze is too small for its player, killer, exit and keys — including
    /// the extra keys a card may have added.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the random factory returns <see langword="null"/>.
    /// </exception>
    public GameState CreateFloor(FloorDefinition floor, int startingHearts, RunModifiers modifiers) =>
        CreateFloor(floor, startingHearts, modifiers, Array.Empty<DescentCondition>());

    /// <summary>
    /// Builds one floor with the run-scoped conditions in force.
    /// </summary>
    /// <param name="floor">The floor's data.</param>
    /// <param name="startingHearts">Hearts the player has left.</param>
    /// <param name="modifiers">The cards taken this run.</param>
    /// <param name="conditions">The descent conditions active from this floor on.</param>
    /// <returns>A playable floor that starts <see cref="GameStatus.InProgress"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is null.
    /// </exception>
    public GameState CreateFloor(
        FloorDefinition floor,
        int startingHearts,
        RunModifiers modifiers,
        IReadOnlyList<DescentCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(conditions);

        // The floor's own numbers first, so a bad floor definition fails with its own message,
        // then the cards over them, which may change the key count and the heart ceiling.
        //
        // The hearts are checked against the *composed* floor: a card can raise a floor's ceiling
        // (Blood Pact, Hearth), so the floor definition's own ceiling cannot be the last word on
        // how many hearts it accepts. ForFloor is given the floor's own ceiling, then the lifted
        // hearts are restored over the composed maximum once the cards have run.
        var floorCeiling = floor.MaxHearts + (conditions.Contains(DescentCondition.Mute) ? 1 : 0);
        var config = modifiers.Apply(
            _config.ForFloor(floor, Math.Min(startingHearts, floorCeiling), conditions));

        if (startingHearts > config.StartingHearts)
        {
            if (startingHearts > config.MaxHearts)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingHearts),
                    startingHearts,
                    $"Floor {floor.Number} accepts at most {config.MaxHearts} hearts with the run's cards.");
            }

            config = config with { StartingHearts = startingHearts };
        }

        var seed = FloorSeed(floor);
        var random = _createRandom(seed)
            ?? throw new InvalidOperationException("The random factory returned null.");

        var generator = new BraidedMazeGenerator(
            new DeadEndBraider(new MazeGenerator(random), random, config.DeadEndBraidPercent),
            random,
            config.ExtraLoops);

        var maze = generator.Generate(new MazeOptions(config.Rows, config.Columns, seed)
        {
            Shape = config.Shape == ShapeKind.Rect
                ? null
                : MazeShape.Create(config.Shape, config.Rows, config.Columns, random),
        });

        // The same random source drives placement, so one seed reproduces the whole floor.
        return new GameState(config, maze, _pathfinder, random);
    }

    /// <summary>
    /// Builds a single game from the base config, without a campaign.
    /// </summary>
    /// <returns>A game that starts <see cref="GameStatus.InProgress"/>.</returns>
    /// <remarks>
    /// Kept as the plain "one game" seam for tests and tools; the shipped frontends use
    /// <see cref="CreateCampaign"/>. Calling this again after a game has ended gives a completely
    /// independent game, which is what makes replaying a single call rather than a special mode.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the random factory returns <see langword="null"/>.
    /// </exception>
    public GameState Create()
    {
        var random = _createRandom(_config.Seed)
            ?? throw new InvalidOperationException("The random factory returned null.");

        var generator = new BraidedMazeGenerator(
            new DeadEndBraider(new MazeGenerator(random), random, _config.DeadEndBraidPercent),
            random,
            _config.ExtraLoops);

        var maze = generator.Generate(new MazeOptions(_config.Rows, _config.Columns, _config.Seed)
        {
            Shape = _config.Shape == ShapeKind.Rect
                ? null
                : MazeShape.Create(_config.Shape, _config.Rows, _config.Columns, random),
        });

        // The same random source drives placement, so one seed reproduces the whole game.
        return new GameState(_config, maze, _pathfinder, random);
    }

    /// <summary>
    /// Returns the seed for <paramref name="floor"/>, offset from the run seed by its number.
    /// </summary>
    /// <param name="floor">The floor being built.</param>
    /// <returns>The floor's seed, or <see langword="null"/> for an unseeded run.</returns>
    /// <remarks>
    /// Floor 1 uses the run seed unchanged, so a fixed seed keeps reproducing the same first
    /// floor it always did; each later floor shifts by <see cref="FloorSeedStride"/>.
    /// </remarks>
    private int? FloorSeed(FloorDefinition floor) =>
        _config.Seed is { } seed ? seed + ((floor.Number - 1) * FloorSeedStride) : null;
}

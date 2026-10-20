using System;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// Builds complete, playable games from one <see cref="GameConfig"/>: the shipped maze pipeline
/// plus a fresh <see cref="GameState"/>.
/// </summary>
/// <remarks>
/// <para>
/// A game is more than a config: it needs a generated maze, a seeded random source shared by
/// generation and placement, and a pathfinder. Doing that in the frontends would have meant the
/// "start a new run" rule living in two event loops, so it lives here instead: the UI asks for a
/// game, and a restart is simply another <see cref="Create"/> call.
/// </para>
/// <para>
/// Each call builds its own <see cref="IRandom"/>, so a seeded config replays exactly and a
/// <see langword="null"/> seed gives a genuinely different maze every run. The maze is shaped by
/// the two decorators the game ships with — <see cref="DeadEndBraider"/> for the dead ends and
/// <see cref="BraidedMazeGenerator"/> for the extra loops — over the untouched graded
/// <see cref="MazeGenerator"/>.
/// </para>
/// </remarks>
public sealed class GameFactory
{
    private readonly GameConfig _config;
    private readonly Func<IRandom> _createRandom;
    private readonly IPathfinder _pathfinder;

    /// <summary>
    /// Creates a factory for <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The tuning values every game from this factory uses.</param>
    /// <param name="createRandom">
    /// Makes the random source for one game. It is called once per <see cref="Create"/>, which is
    /// what keeps a seeded game reproducible and an unseeded one fresh.
    /// </param>
    /// <param name="pathfinder">The search the killer uses to chase.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="config"/> is invalid.</exception>
    public GameFactory(GameConfig config, Func<IRandom> createRandom, IPathfinder pathfinder)
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
    /// Builds a new game: a freshly generated, braided maze with the player, killer, exit and keys
    /// placed on distinct cells.
    /// </summary>
    /// <returns>A game that starts <see cref="GameStatus.InProgress"/>.</returns>
    /// <remarks>
    /// Calling this again after a game has ended gives a completely independent game, which is
    /// what makes replaying a single call rather than a special mode.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the random factory returns <see langword="null"/>.
    /// </exception>
    public GameState Create()
    {
        var random = _createRandom()
            ?? throw new InvalidOperationException("The random factory returned null.");

        var generator = new BraidedMazeGenerator(
            new DeadEndBraider(new MazeGenerator(random), random, _config.DeadEndBraidPercent),
            random,
            _config.ExtraLoops);

        var maze = generator.Generate(new MazeOptions(_config.Rows, _config.Columns, _config.Seed));

        // The same random source drives placement, so one seed reproduces the whole game.
        return new GameState(_config, maze, _pathfinder, random);
    }
}

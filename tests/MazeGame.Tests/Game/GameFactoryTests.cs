using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the factory that the frontends call to start a game — including on a replay after
/// death, where the contract is simply "a brand-new, independent, playable game".
/// </summary>
public sealed class GameFactoryTests
{
    private const int Seed = 7;

    [Fact]
    public void Create_ReturnsAPlayableGame()
    {
        var state = Factory().Create();

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(15, state.Maze.Rows);
        Assert.Equal(15, state.Maze.Columns);
        Assert.Equal(3, state.KeysRemaining);
        Assert.Equal(3, state.BarriersRemaining);
        Assert.Equal(2, state.LuresRemaining);
        Assert.False(state.IsExitUnlocked);
        Assert.NotEqual(state.Player, state.Enemy);
        Assert.DoesNotContain(state.Player, state.Keys);
    }

    [Fact]
    public void Create_ShapesTheMazeWithTheBraidAndTheLoops()
    {
        // The factory owns the shipped pipeline, so this proves the config reaches both
        // decorators rather than leaving the player on a bare perfect maze.
        var state = Factory().Create();

        var perfectPassages = state.Maze.CellCount - 1;
        Assert.True(
            state.Maze.CountPassages() >= perfectPassages + state.Config.ExtraLoops + 1,
            "the braid must open at least one dead end on a 15x15 maze");
    }

    [Fact]
    public void Create_WithBothShapersOff_LeavesAPerfectMaze()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed, DeadEndBraidPercent = 0, ExtraLoops = 0 },
            () => new SeededRandom(Seed),
            new DijkstraPathfinder());

        var state = factory.Create();

        Assert.Equal(state.Maze.CellCount - 1, state.Maze.CountPassages());
    }

    [Fact]
    public void Create_WithTheSameSeed_ProducesTheSameGame()
    {
        var first = Factory().Create();
        var second = Factory().Create();

        for (var row = 0; row < first.Maze.Rows; row++)
        {
            for (var column = 0; column < first.Maze.Columns; column++)
            {
                Assert.Equal(first.Maze.GetCell(row, column), second.Maze.GetCell(row, column));
            }
        }

        Assert.Equal(first.Player, second.Player);
        Assert.Equal(first.Enemy, second.Enemy);
        Assert.Equal(first.Exit, second.Exit);
        Assert.Equal(first.Keys, second.Keys);
    }

    [Fact]
    public void Create_WithANullSeed_ProducesDifferentGames()
    {
        // A replay should be a new cave, not the same one again.
        var factory = new GameFactory(
            new GameConfig { Seed = null },
            () => new SeededRandom(null),
            new DijkstraPathfinder());

        var openings = new HashSet<(Cell Player, Cell Enemy, Cell Exit)>();

        for (var run = 0; run < 3; run++)
        {
            var state = factory.Create();
            openings.Add((state.Player, state.Enemy, state.Exit));
        }

        Assert.True(openings.Count > 1, "three unseeded games must not all start identically");
    }

    [Fact]
    public void Create_AfterAGameEnds_StartsAFreshIndependentGame()
    {
        // This is the replay contract: after the killer wins, asking the factory again must give
        // a game that starts from the beginning with every objective and budget restored.
        var state = Factory().Create();

        for (var tick = 0; tick < 400 && state.Status == GameStatus.InProgress; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, state.Status);

        var replay = Factory().Create();

        Assert.Equal(GameStatus.InProgress, replay.Status);
        Assert.Equal(3, replay.KeysRemaining);
        Assert.Equal(3, replay.BarriersRemaining);
        Assert.Equal(2, replay.LuresRemaining);
        Assert.Empty(replay.Barriers);
        Assert.Empty(replay.BarrierSeals);
        Assert.Null(replay.Lure);

        // The old game must not be resurrected by starting a new one.
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Create_AsksForAFreshRandomSourceEveryGame()
    {
        var calls = 0;
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            () =>
            {
                calls++;
                return new SeededRandom(Seed);
            },
            new DijkstraPathfinder());

        factory.Create();
        factory.Create();

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Constructor_WithAnInvalidConfig_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameFactory(new GameConfig { Rows = 0 }, () => new SeededRandom(Seed), new DijkstraPathfinder()));
    }

    [Fact]
    public void Constructor_WithNullArguments_Throws()
    {
        var random = new SeededRandom(Seed);
        var pathfinder = new DijkstraPathfinder();

        Assert.Throws<ArgumentNullException>(() => new GameFactory(null!, () => random, pathfinder));
        Assert.Throws<ArgumentNullException>(() => new GameFactory(new GameConfig(), null!, pathfinder));
        Assert.Throws<ArgumentNullException>(() => new GameFactory(new GameConfig(), () => random, null!));
    }

    [Fact]
    public void Create_WhenTheRandomFactoryReturnsNull_Throws()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            () => null!,
            new DijkstraPathfinder());

        Assert.Throws<InvalidOperationException>(() => factory.Create());
    }

    /// <summary>
    /// The factory wired exactly as `Program` wires it: one seeded source per game.
    /// </summary>
    private static GameFactory Factory(int? seed = Seed) =>
        new(
            new GameConfig { Seed = seed },
            () => new SeededRandom(seed),
            new DijkstraPathfinder());
}

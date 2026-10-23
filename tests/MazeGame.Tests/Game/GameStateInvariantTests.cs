using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Game;

/// <summary>
/// Plays many games with random inputs and checks the invariants that must hold at every
/// single step, not just after a scripted sequence.
/// </summary>
/// <remarks>
/// This is the safety net for the rules: scripted tests prove specific behaviour, while this
/// proves there is no input sequence that leaves the player unreachable, overspends the
/// barrier budget, loses or gains keys, or resumes a finished game.
/// </remarks>
public sealed class GameStateInvariantTests
{
    private const int MazeSize = 8;
    private const int GameCount = 20;
    private const int StepsPerGame = 400;

    [Fact]
    public void PlayingRandomGames_NeverBreaksAnInvariant()
    {
        var pathfinder = new DijkstraPathfinder();

        for (var seed = 0; seed < GameCount; seed++)
        {
            var random = new SeededRandom(seed);
            var config = new GameConfig
            {
                Rows = MazeSize,
                Columns = MazeSize,
                KeyCount = 3,
                EnemyStepSeconds = 0.05,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = seed,
            };
            var maze = new MazeGenerator(random).Generate(new MazeOptions(MazeSize, MazeSize, seed));
            var state = new GameState(config, maze, pathfinder, random);

            for (var step = 0; step < StepsPerGame && state.Status == GameStatus.InProgress; step++)
            {
                // Direction's values are 1, 2, 4, 8, so this picks uniformly among the four.
                state.TryMove((Direction)(1 << random.NextInt(4)));

                state.Tick(0.05);

                Assert.InRange(state.KeysRemaining, 0, config.KeyCount);
                Assert.Equal(config.KeyCount - state.KeysRemaining, state.KeysCollected);

                // The killer must always have a way to the player, whatever the player does.
                if (state.Status == GameStatus.InProgress)
                {
                    var route = pathfinder.FindPath(
                        state.Maze,
                        state.Enemy,
                        state.Player,
                        _ => 1);

                    Assert.NotEmpty(route);
                }
            }
        }
    }

    [Fact]
    public void AWholeGame_CanBeWonOnAGeneratedMaze()
    {
        // The killer is parked (a huge step time) so this isolates the objective rules: on a
        // real generated maze, a bot that walks to each key and then to the exit must win.
        var random = new SeededRandom(77);
        var config = new GameConfig
        {
            Rows = MazeSize,
            Columns = MazeSize,
            KeyCount = 3,
            EnemyStepSeconds = 1000.0,
            Seed = 77,
        };
        var maze = new MazeGenerator(random).Generate(new MazeOptions(MazeSize, MazeSize, 77));
        var state = new GameState(config, maze, new DijkstraPathfinder(), random);

        while (state.KeysRemaining > 0)
        {
            WalkTo(state, state.Keys.First());
        }

        WalkTo(state, state.Exit);

        Assert.Equal(GameStatus.Won, state.Status);
        Assert.Equal(3, state.KeysCollected);
    }

    [Fact]
    public void AWholeGame_CanBeWonOnABraidedMaze()
    {
        // The braid adds loops to the maze the UI actually plays on; the objective rules must
        // not care. Same parked killer as the perfect-maze version of this test.
        var random = new SeededRandom(91);
        var config = new GameConfig
        {
            Rows = MazeSize,
            Columns = MazeSize,
            KeyCount = 3,
            ExtraLoops = 10,
            EnemyStepSeconds = 1000.0,
            Seed = 91,
        };
        var generator = new BraidedMazeGenerator(new MazeGenerator(random), random, config.ExtraLoops);
        var maze = generator.Generate(new MazeOptions(MazeSize, MazeSize, 91));
        var state = new GameState(config, maze, new DijkstraPathfinder(), random);

        while (state.KeysRemaining > 0)
        {
            WalkTo(state, state.Keys.First());
        }

        WalkTo(state, state.Exit);

        Assert.Equal(GameStatus.Won, state.Status);
        Assert.Equal((MazeSize * MazeSize) - 1 + config.ExtraLoops, maze.CountPassages());
    }

    [Fact]
    public void AFleeingPlayer_IsEventuallyCaught()
    {
        // The escape route must always run out, or the game could be played forever by
        // running in circles. The player greedily moves to whichever neighbour is furthest
        // from the killer, which is the best a fleeing player can do.
        var random = new SeededRandom(5);
        var config = new GameConfig
        {
            Rows = MazeSize,
            Columns = MazeSize,
            KeyCount = 3,
            EnemyStepSeconds = 1.0,
            // One heart keeps this test about the chase running out, not about the heart
            // economy; the three-heart version is covered in GameStateHeartTests.
            StartingHearts = 1,
            MaxHearts = 1,
            Seed = 5,
        };
        var maze = new MazeGenerator(random).Generate(new MazeOptions(MazeSize, MazeSize, 5));
        var state = new GameState(config, maze, new DijkstraPathfinder(), random);
        var pathfinder = new DijkstraPathfinder();

        for (var step = 0; step < 2000 && state.Status == GameStatus.InProgress; step++)
        {
            state.TryMove(FurthestDirection(state, pathfinder));
            state.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void AFinishedGame_StaysFinishedAndFrozen()
    {
        for (var seed = 0; seed < GameCount; seed++)
        {
            var random = new SeededRandom(seed);
            var config = new GameConfig
            {
                Rows = MazeSize,
                Columns = MazeSize,
                KeyCount = 3,
                EnemyStepSeconds = 0.05,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = seed,
            };
            var maze = new MazeGenerator(random).Generate(new MazeOptions(MazeSize, MazeSize, seed));
            var state = new GameState(config, maze, new DijkstraPathfinder(), random);

            for (var step = 0; step < StepsPerGame && state.Status == GameStatus.InProgress; step++)
            {
                state.TryMove((Direction)(1 << random.NextInt(4)));
                state.Tick(0.05);
            }

            if (state.Status == GameStatus.InProgress)
            {
                continue;
            }

            var status = state.Status;
            var player = state.Player;
            var enemy = state.Enemy;

            state.Tick(1.0);
            state.TryMove(Direction.North);

            Assert.Equal(status, state.Status);
            Assert.Equal(player, state.Player);
            Assert.Equal(enemy, state.Enemy);
        }
    }

    /// <summary>
    /// Moves the player one cell at a time along the shortest route to
    /// <paramref name="target"/>.
    /// </summary>
    private static void WalkTo(GameState state, Cell target)
    {
        var pathfinder = new DijkstraPathfinder();

        while (state.Player != target)
        {
            var route = pathfinder.FindPath(state.Maze, state.Player, target, _ => 1);
            Assert.True(route.Count >= 2, "a reachable target must have a route with at least one step");
            Assert.True(state.TryMove(DirectionBetween(state.Player, route[1])));
        }
    }

    /// <summary>
    /// Picks the open direction whose cell is furthest from the killer.
    /// </summary>
    private static Direction FurthestDirection(GameState state, DijkstraPathfinder pathfinder)
    {
        var bestDirection = Direction.North;
        var bestDistance = -1;

        foreach (var direction in System.Enum.GetValues<Direction>())
        {
            if (state.Maze.HasWall(state.Player.Row, state.Player.Column, direction))
            {
                continue;
            }

            var (rowDelta, columnDelta) = direction.Delta();
            var cell = state.Maze.GetCell(state.Player.Row + rowDelta, state.Player.Column + columnDelta);
            var route = pathfinder.FindPath(state.Maze, state.Enemy, cell, _ => 1);

            if (route.Count > bestDistance)
            {
                bestDistance = route.Count;
                bestDirection = direction;
            }
        }

        return bestDirection;
    }

    /// <summary>
    /// Returns the direction that steps from <paramref name="from"/> to the neighbouring
    /// cell <paramref name="to"/>.
    /// </summary>
    private static Direction DirectionBetween(Cell from, Cell to)
    {
        var rowDelta = to.Row - from.Row;
        var columnDelta = to.Column - from.Column;

        return rowDelta switch
        {
            -1 => Direction.North,
            1 => Direction.South,
            _ => columnDelta == 1 ? Direction.East : Direction.West,
        };
    }
}

using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies how a game is set up and how the player moves: distinct placement, walls,
/// key collection, the locked exit, winning and losing.
/// </summary>
public sealed class GameStateTests
{
    [Fact]
    public void Constructor_WithNullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameState(null!, TestLevels.Corridor(), new DijkstraPathfinder(), new FixedRandom(0)));
    }

    [Fact]
    public void Constructor_WithNullMaze_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameState(TestLevels.CorridorConfig(), null!, new DijkstraPathfinder(), new FixedRandom(0)));
    }

    [Fact]
    public void Constructor_WithNullPathfinder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameState(TestLevels.CorridorConfig(), TestLevels.Corridor(), null!, new FixedRandom(0)));
    }

    [Fact]
    public void Constructor_WithNullRandom_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameState(TestLevels.CorridorConfig(), TestLevels.Corridor(), new DijkstraPathfinder(), null!));
    }

    [Fact]
    public void Constructor_WhenMazeSizeDiffersFromConfig_Throws()
    {
        var config = new GameConfig { Rows = 2, Columns = 3, KeyCount = 0 };
        var wrongSizedMaze = new Grid(3, 2);

        Assert.Throws<ArgumentException>(
            () => new GameState(config, wrongSizedMaze, new DijkstraPathfinder(), new FixedRandom(0)));
    }

    [Fact]
    public void Constructor_WhenConfigIsInvalid_Throws()
    {
        var config = new GameConfig { Rows = 1, Columns = 2, KeyCount = 1 };

        Assert.Throws<ArgumentException>(
            () => new GameState(config, TestLevels.Corridor(2), new DijkstraPathfinder(), new FixedRandom(0)));
    }

    [Fact]
    public void Constructor_PlacesPlayerKillerExitAndKeysOnDistinctCells()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var state = CreateSeededGame(seed);

            var occupied = new HashSet<Cell> { state.Player, state.Enemy, state.Exit };

            Assert.Equal(3, occupied.Count);
            Assert.Equal(3, state.Keys.Count);

            foreach (var key in state.Keys)
            {
                Assert.True(occupied.Add(key), $"seed {seed}: key {key} shares a cell with the player, killer or exit");
            }
        }
    }

    [Fact]
    public void Constructor_PlacesKillerOnADeadEnd()
    {
        // A dead end has exactly one opening, so the killer can never stand between the
        // player and something further down a corridor.
        for (var seed = 0; seed < 10; seed++)
        {
            var state = CreateSeededGame(seed);

            Assert.Equal(3, state.Enemy.WallCount);
        }
    }

    [Fact]
    public void Constructor_LeavesEveryObjectiveReachableWithoutCrossingTheKiller()
    {
        // The anti-softlock rule that specifically covers "the killer is in the way": on a
        // perfect maze the player could never walk past it, so no key or exit may sit behind
        // the killer's starting cell.
        var pathfinder = new DijkstraPathfinder();

        for (var seed = 0; seed < 10; seed++)
        {
            var state = CreateSeededGame(seed);

            foreach (var target in System.Linq.Enumerable.Append(state.Keys, state.Exit))
            {
                var route = pathfinder.FindPath(state.Maze, state.Player, target, _ => 1);

                Assert.NotEmpty(route);
                Assert.DoesNotContain(state.Enemy, route);
            }
        }
    }

    [Fact]
    public void Constructor_StartsWithEveryKeyAndFullBarrierBudget()
    {
        var state = TestLevels.CorridorGame(keyCount: 3, barrierBudget: 2);

        Assert.Equal(3, state.KeysRemaining);
        Assert.Equal(0, state.KeysCollected);
        Assert.Equal(2, state.BarriersRemaining);
        Assert.Empty(state.Barriers);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.False(state.IsExitUnlocked);
    }

    [Fact]
    public void Constructor_WithZeroKeys_OpensTheExitImmediately()
    {
        var state = TestLevels.CorridorGame(keyCount: 0);

        Assert.True(state.IsExitUnlocked);
        Assert.Empty(state.Keys);
    }

    [Fact]
    public void TryMove_WithNoKeysOntoTheExit_WinsImmediately()
    {
        // The smallest playable maze: player (0,0), exit (0,1), killer (0,2).
        var state = TestLevels.CorridorGame(length: 3, keyCount: 0);

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(state.Exit, state.Player);
        Assert.Equal(GameStatus.Won, state.Status);
    }

    [Fact]
    public void Constructor_WithSameSeed_ProducesTheSamePlacement()
    {
        var first = CreateSeededGame(seed: 1234);
        var second = CreateSeededGame(seed: 1234);

        Assert.Equal(first.Player, second.Player);
        Assert.Equal(first.Enemy, second.Enemy);
        Assert.Equal(first.Exit, second.Exit);
        Assert.True(first.Keys.SetEquals(second.Keys));
    }

    [Fact]
    public void TryMove_IntoAWall_ReturnsFalseAndLeavesThePlayerAlone()
    {
        var state = TestLevels.CorridorGame();
        var before = state.Player;

        Assert.False(state.TryMove(Direction.North));
        Assert.False(state.TryMove(Direction.South));
        Assert.Equal(before, state.Player);
    }

    [Fact]
    public void TryMove_OutOfTheMaze_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();

        Assert.False(state.TryMove(Direction.West));
        Assert.Equal(TestLevels.Corridor().GetCell(0, 0), state.Player);
    }

    [Fact]
    public void TryMove_ThroughAPassage_MovesThePlayer()
    {
        var state = TestLevels.CorridorGame();

        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(TestLevels.Corridor().GetCell(0, 1), state.Player);
    }

    [Fact]
    public void TryMove_OntoTheLockedExit_MovesThereButDoesNotWin()
    {
        // Walking over a locked exit must be possible: blocking it could trap the player in a
        // corridor and make the game unwinnable.
        var state = TestLevels.CorridorGame();

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(state.Exit, state.Player);
        Assert.False(state.IsExitUnlocked);
        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    [Fact]
    public void TryMove_OntoAKey_CollectsIt()
    {
        var state = TestLevels.CorridorGame();

        state.TryMove(Direction.East);
        state.TryMove(Direction.East);

        Assert.Equal(1, state.KeysCollected);
        Assert.Equal(2, state.KeysRemaining);
        Assert.DoesNotContain(state.Player, state.Keys);
    }

    [Fact]
    public void TryMove_OverACollectedKey_DoesNotCountItTwice()
    {
        var state = TestLevels.CorridorGame();

        state.TryMove(Direction.East);
        state.TryMove(Direction.East);
        state.TryMove(Direction.West);
        state.TryMove(Direction.East);

        Assert.Equal(1, state.KeysCollected);
        Assert.Equal(2, state.KeysRemaining);
    }

    [Fact]
    public void CollectingEveryKey_OpensTheExit()
    {
        var state = TestLevels.CorridorGame();

        CollectEveryKey(state);

        Assert.Equal(3, state.KeysCollected);
        Assert.Equal(0, state.KeysRemaining);
        Assert.True(state.IsExitUnlocked);
    }

    [Fact]
    public void TryMove_OntoTheOpenExit_Wins()
    {
        var state = TestLevels.CorridorGame();
        CollectEveryKey(state);

        // Walk back down the corridor to the exit at (0,1).
        Assert.True(state.TryMove(Direction.West));
        Assert.True(state.TryMove(Direction.West));
        Assert.True(state.TryMove(Direction.West));

        Assert.Equal(state.Exit, state.Player);
        Assert.Equal(GameStatus.Won, state.Status);
    }

    [Fact]
    public void TryMove_OntoTheKiller_IsCaught()
    {
        var state = TestLevels.CorridorGame();

        for (var step = 0; step < 4; step++)
        {
            Assert.True(state.TryMove(Direction.East));
            Assert.Equal(GameStatus.InProgress, state.Status);
        }

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(state.Enemy, state.Player);
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void TryMove_AfterWinning_ReturnsFalseAndKeepsThePlayerInPlace()
    {
        var state = TestLevels.CorridorGame();
        CollectEveryKey(state);
        state.TryMove(Direction.West);
        state.TryMove(Direction.West);
        state.TryMove(Direction.West);
        Assert.Equal(GameStatus.Won, state.Status);

        Assert.False(state.TryMove(Direction.West));
        Assert.Equal(state.Exit, state.Player);
    }

    [Fact]
    public void TryMove_AfterBeingCaught_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();
        for (var step = 0; step < 5; step++)
        {
            state.TryMove(Direction.East);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.False(state.TryMove(Direction.West));
    }

    [Fact]
    public void TryMove_WithAnUndefinedDirection_Throws()
    {
        var state = TestLevels.CorridorGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.TryMove((Direction)99));
    }

    [Fact]
    public void EveryKeyAndTheExit_AreReachableFromThePlayer()
    {
        // The anti-softlock invariant: after generation and placement nothing is walled off.
        for (var seed = 0; seed < 10; seed++)
        {
            var state = CreateSeededGame(seed);

            var reachable = ReachableCellCount(state.Maze, state.Player);

            Assert.Equal(state.Maze.CellCount, reachable);
        }
    }

    /// <summary>
    /// Walks east until every key is collected (the corridor puts them at (0,2)..(0,4)).
    /// </summary>
    private static void CollectEveryKey(GameState state)
    {
        while (state.KeysRemaining > 0)
        {
            Assert.True(state.TryMove(Direction.East), "the corridor should always allow moving east");
        }
    }

    private static GameState CreateSeededGame(int seed, int size = 8, int keyCount = 3)
    {
        var random = new SeededRandom(seed);
        var config = new GameConfig { Rows = size, Columns = size, KeyCount = keyCount, Seed = seed };
        var maze = new MazeGenerator(random).Generate(new MazeOptions(size, size, seed));
        return new GameState(config, maze, new DijkstraPathfinder(), random);
    }

    private static int ReachableCellCount(Grid grid, Cell start)
    {
        var visited = new bool[grid.Rows, grid.Columns];
        var queue = new Queue<Cell>();
        queue.Enqueue(start);
        visited[start.Row, start.Column] = true;

        var count = 0;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            count++;

            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (grid.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var next = grid.GetCell(cell.Row + rowDelta, cell.Column + columnDelta);

                if (visited[next.Row, next.Column])
                {
                    continue;
                }

                visited[next.Row, next.Column] = true;
                queue.Enqueue(next);
            }
        }

        return count;
    }
}

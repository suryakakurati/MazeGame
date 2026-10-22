using System;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the killer's behaviour: it advances on a timer, only through open passages,
/// follows the player when they move, and stops once the game is over.
/// </summary>
public sealed class GameStateEnemyTests
{
    [Fact]
    public void Tick_BeforeAStepIsDue_LeavesTheKillerInPlace()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var start = state.Enemy;

        state.Tick(0.9);

        Assert.Equal(start, state.Enemy);
    }

    [Fact]
    public void Tick_WhenAStepIsDue_MovesTheKillerOneCell()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var start = state.Enemy;

        state.Tick(1.0);

        Assert.NotEqual(start, state.Enemy);
        AssertOpenStep(state.Maze, start, state.Enemy);
    }

    [Fact]
    public void Tick_AccumulatesTimeAcrossCalls()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var start = state.Enemy;

        state.Tick(0.6);
        Assert.Equal(start, state.Enemy);

        state.Tick(0.4);
        Assert.NotEqual(start, state.Enemy);
    }

    [Fact]
    public void Tick_WithAHugeDelta_MovesTheKillerOnlyOneStep()
    {
        // A stalled frame (dragged window, sleeping laptop) must not teleport the killer.
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var start = state.Enemy;

        state.Tick(100.0);

        AssertOpenStep(state.Maze, start, state.Enemy);
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
    }

    [Fact]
    public void Tick_MovesTheKillerOnlyBetweenCellsWithoutWalls()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var previous = state.Enemy;

        for (var step = 0; step < 4; step++)
        {
            state.Tick(1.0);
            AssertOpenStep(state.Maze, previous, state.Enemy);
            previous = state.Enemy;
        }
    }

    [Fact]
    public void Tick_WhenTheKillerReachesThePlayer_IsCaught()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);

        for (var step = 0; step < 4; step++)
        {
            state.Tick(1.0);
            Assert.Equal(GameStatus.InProgress, state.Status);
        }

        state.Tick(1.0);

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.Equal(state.Player, state.Enemy);
    }

    [Fact]
    public void Tick_AfterWinning_DoesNotMoveTheKiller()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        CollectEveryKey(state);
        state.TryMove(Direction.West);
        state.TryMove(Direction.West);
        state.TryMove(Direction.West);
        Assert.Equal(GameStatus.Won, state.Status);
        var killer = state.Enemy;

        state.Tick(10.0);

        Assert.Equal(killer, state.Enemy);
        Assert.Equal(GameStatus.Won, state.Status);
    }

    [Fact]
    public void Tick_AfterBeingCaught_KeepsTheStatusAndPositions()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        for (var step = 0; step < 5; step++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        var killer = state.Enemy;

        state.Tick(10.0);

        Assert.Equal(killer, state.Enemy);
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Tick_AfterThePlayerMoves_RecomputesTheRoute()
    {
        // Step 1: the player stands on (0,1) with a barrier under them, so the killer's
        // cached route runs along the top and it steps to (0,2).
        var state = TestLevels.TwoRouteGame();
        Assert.True(state.TryMove(Direction.East));
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 1));
        state.Tick(TestLevels.OneSecondPerStep);
        Assert.Equal(state.Maze.GetCell(0, 2), state.Enemy);

        // Step 2: the player steps back to (0,0). The cached route would continue to (0,1),
        // but the barriers make that expensive, so a recomputed route goes back round the
        // bottom: the killer must move to (1,2), not (0,1).
        Assert.True(state.TryMove(Direction.West));
        state.Tick(TestLevels.OneSecondPerStep);

        Assert.Equal(state.Maze.GetCell(1, 2), state.Enemy);
    }

    [Fact]
    public void Tick_InTheSmallestMaze_CatchesThePlayerInTwoSteps()
    {
        // 1x3 with no keys: player (0,0), exit (0,1), killer (0,2).
        var state = TestLevels.CorridorGame(length: 3, keyCount: 0, enemyStepSeconds: 1.0);

        state.Tick(1.0);
        Assert.Equal(state.Maze.GetCell(0, 1), state.Enemy);
        Assert.Equal(GameStatus.InProgress, state.Status);

        state.Tick(1.0);
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Senses_ReachThePlayerOnlyInsideTheAura()
    {
        // A long L: the hunter is eleven steps from the player, well outside the six-step aura.
        var maze = new Grid(7, 7);

        for (var column = 0; column < 6; column++)
        {
            maze.RemoveWall(6, column, Direction.East);
        }

        for (var row = 0; row < 6; row++)
        {
            maze.RemoveWall(row, 0, Direction.South);
        }

        var far = new GameState(
            new GameConfig
            {
                Rows = 7,
                Columns = 7,
                KeyCount = 1,
                EnemyStepSeconds = 1.0,
                StartingHearts = 3,
                MaxHearts = 3,
                Seed = null,
            },
            maze,
            new DijkstraPathfinder(),
            new FixedRandom(0));

        Assert.Equal(maze.GetCell(0, 0), far.Player);
        far.Tick(0.1);
        Assert.False(far.IsPlayerSensed);

        // The same rule inside the aura: a corridor six cells long starts the hunter five steps
        // away, so it knows where the player is from the first frame.
        var near = TestLevels.CorridorGame();

        near.Tick(0.1);

        Assert.True(near.IsPlayerSensed);
    }

    [Fact]
    public void Tick_WithNegativeDelta_Throws()
    {
        var state = TestLevels.CorridorGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-0.1));
    }

    [Fact]
    public void Tick_WithNaNDelta_Throws()
    {
        var state = TestLevels.CorridorGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(double.NaN));
    }

    [Fact]
    public void Tick_WithInfiniteDelta_Throws()
    {
        var state = TestLevels.CorridorGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(double.PositiveInfinity));
    }

    /// <summary>
    /// Walks east until every key is collected (the corridor puts them at (0,2)..(0,4)).
    /// </summary>
    private static void CollectEveryKey(GameState state)
    {
        while (state.KeysRemaining > 0)
        {
            Assert.True(state.TryMove(Direction.East));
        }
    }

    /// <summary>
    /// Asserts that two cells are neighbours with no wall between them.
    /// </summary>
    private static void AssertOpenStep(Grid maze, Cell from, Cell to)
    {
        var rowDelta = to.Row - from.Row;
        var columnDelta = to.Column - from.Column;

        Assert.Equal(1, Math.Abs(rowDelta) + Math.Abs(columnDelta));

        var direction = rowDelta switch
        {
            -1 => Direction.North,
            1 => Direction.South,
            _ => columnDelta == 1 ? Direction.East : Direction.West,
        };

        Assert.False(maze.HasWall(from.Row, from.Column, direction));
    }
}

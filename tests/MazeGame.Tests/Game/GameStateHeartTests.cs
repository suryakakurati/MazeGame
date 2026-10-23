using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the roguelike health rule: a catch costs exactly one heart, throws the killer to a
/// distant cell, and only the last heart ends the run. Every test uses a hand-built corridor so
/// the cells are known rather than discovered.
/// </summary>
public sealed class GameStateHeartTests
{
    private const double Step = 1.0;

    [Fact]
    public void Catch_CostsOneHeartAndKeepsTheFloorRunning()
    {
        var state = Corridor(startingHearts: 3);

        // Player (0,0), keys (0,2)-(0,4), killer (0,5): five steps to the player.
        for (var step = 0; step < 5; step++)
        {
            state.Tick(Step);
        }

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(2, state.Hearts);
        Assert.Equal(3, state.KeysRemaining);
        Assert.NotEqual(state.Player, state.Enemy);
    }

    [Fact]
    public void Catch_ThrowsTheKillerToACellAtLeastTheRespawnDistanceAway()
    {
        var state = Corridor(length: 14, keyCount: 1, startingHearts: 3, respawnDistance: 5);

        for (var step = 0; step < 13; step++)
        {
            state.Tick(Step);
        }

        Assert.Equal(2, state.Hearts);

        var steps = new DijkstraPathfinder()
            .FindPath(state.Maze, state.Player, state.Enemy, _ => 1)
            .Count - 1;

        Assert.True(steps >= 5, $"the killer respawned only {steps} steps away");
    }

    [Fact]
    public void Catch_DoesNotParkTheKillerOnAKeyOrTheExit()
    {
        var state = Corridor(length: 14, keyCount: 1, startingHearts: 3, respawnDistance: 5);

        for (var step = 0; step < 13; step++)
        {
            state.Tick(Step);
        }

        Assert.Equal(2, state.Hearts);
        Assert.DoesNotContain(state.Enemy, state.Keys);
        Assert.NotEqual(state.Exit, state.Enemy);
    }

    [Fact]
    public void Catch_StunsTheKiller()
    {
        var state = Corridor(startingHearts: 3, respawnStunSeconds: 2.0);

        for (var step = 0; step < 5; step++)
        {
            state.Tick(Step);
        }

        var respawned = state.Enemy;
        Assert.True(state.IsEnemyStunned);

        state.Tick(0.5);

        Assert.Equal(respawned, state.Enemy);
        Assert.True(state.IsEnemyStunned);
    }

    [Fact]
    public void Catch_StartsTheInvulnerabilityWindow()
    {
        var state = Corridor(startingHearts: 3, invulnerabilitySeconds: 2.0, respawnStunSeconds: 0);

        for (var step = 0; step < 5; step++)
        {
            state.Tick(Step);
        }

        Assert.True(state.IsInvulnerable);

        state.Tick(1.0);
        Assert.True(state.IsInvulnerable);

        state.Tick(1.0);
        Assert.False(state.IsInvulnerable);
        Assert.Equal(2, state.Hearts);
    }

    [Fact]
    public void DuringInvulnerability_TheKillerWaitsInsteadOfCatchingAgain()
    {
        // A 1x3 floor: the only respawn cell is right next to the player, so without the
        // invulnerability rule the killer would deal a second hit almost immediately.
        var state = Corridor(length: 3, keyCount: 0, startingHearts: 3, invulnerabilitySeconds: 10.0, respawnStunSeconds: 0);

        state.Tick(Step);
        state.Tick(Step);

        Assert.Equal(2, state.Hearts);

        // The killer walks back to the cell next to the player, then has to wait there while
        // the invulnerability window is open instead of stepping onto the player.
        state.Tick(Step);
        var waitingCell = state.Enemy;
        state.Tick(Step);

        Assert.Equal(2, state.Hearts);
        Assert.Equal(waitingCell, state.Enemy);
        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    [Fact]
    public void DuringInvulnerability_SteppingOntoTheKiller_IsRefused()
    {
        // Player (0,0), exit (0,1), key (0,2), killer (0,3).
        var state = Corridor(length: 4, keyCount: 1, startingHearts: 3, invulnerabilitySeconds: 10.0);

        for (var step = 0; step < 3; step++)
        {
            state.Tick(Step);
        }

        Assert.Equal(2, state.Hearts);
        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);

        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryMove(Direction.East));
        Assert.False(state.TryMove(Direction.East));

        Assert.Equal(2, state.Hearts);
        Assert.Equal(state.Maze.GetCell(0, 2), state.Player);
    }

    [Fact]
    public void LastHeart_EndsTheRun()
    {
        var state = Corridor(length: 3, keyCount: 0, startingHearts: 1);

        state.Tick(Step);
        state.Tick(Step);

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.Equal(0, state.Hearts);

        // A finished game is frozen: no further hearts are spent and nothing moves.
        state.Tick(10.0);
        Assert.Equal(0, state.Hearts);
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void SteppingOntoTheKiller_CostsAHeart()
    {
        // A key keeps the exit locked, so walking east is a normal step rather than a win.
        // Player (0,0), exit (0,1), key (0,2), killer (0,3).
        var state = Corridor(length: 4, keyCount: 1, startingHearts: 3);

        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(2, state.Hearts);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(state.Maze.GetCell(0, 3), state.Player);

        // The respawn avoids the exit at (0,1), so the far end is the corridor's start.
        Assert.Equal(state.Maze.GetCell(0, 0), state.Enemy);
    }

    [Fact]
    public void Catch_KeepsKeys()
    {
        var config = new GameConfig
        {
            Rows = 2,
            Columns = 3,
            KeyCount = 1,
            StartingHearts = 3,
            MaxHearts = 3,
            EnemyStepSeconds = Step,
            Seed = null,
        };
        var state = new GameState(config, TestLevels.TwoRoutes(), new DijkstraPathfinder(), new FixedRandom(0));

        // The killer takes the short top route: (0,2), (0,1), then the player at (0,0).
        state.Tick(Step);
        state.Tick(Step);
        state.Tick(Step);

        Assert.Equal(2, state.Hearts);
        Assert.Equal(1, state.KeysRemaining);
    }

    [Fact]
    public void HeartsAndTheirCeiling_ComeFromTheConfig()
    {
        var config = new GameConfig
        {
            Rows = 1,
            Columns = 4,
            KeyCount = 0,
            StartingHearts = 2,
            MaxHearts = 5,
            Seed = null,
        };
        var state = new GameState(config, TestLevels.Corridor(4), new DijkstraPathfinder(), new FixedRandom(0));

        Assert.Equal(2, state.Hearts);
        Assert.Equal(5, state.MaxHearts);
    }

    /// <summary>
    /// Builds a corridor game with explicit heart rules. Placement stays the documented one:
    /// player (0,0), exit (0,1), then keys, with the killer in the far dead end.
    /// </summary>
    private static GameState Corridor(
        int length = 6,
        int keyCount = 3,
        int startingHearts = 3,
        int respawnDistance = 8,
        double invulnerabilitySeconds = 1.5,
        double respawnStunSeconds = 1.5)
    {
        var config = new GameConfig
        {
            Rows = 1,
            Columns = length,
            KeyCount = keyCount,
            EnemyStepSeconds = Step,
            StartingHearts = startingHearts,
            MaxHearts = startingHearts,
            RespawnDistance = respawnDistance,
            InvulnerabilitySeconds = invulnerabilitySeconds,
            RespawnStunSeconds = respawnStunSeconds,
            Seed = null,
        };

        return new GameState(
            config,
            TestLevels.Corridor(length),
            new DijkstraPathfinder(),
            new FixedRandom(0));
    }
}

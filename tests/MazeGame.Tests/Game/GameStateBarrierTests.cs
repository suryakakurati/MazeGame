using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the barrier mechanics: the player aims with a cursor while the game is paused,
/// placement spends the budget once per cell, and a barrier delays the killer without ever
/// blocking it.
/// </summary>
public sealed class GameStateBarrierTests
{
    [Fact]
    public void BeginBarrierPlacement_StartsTheCursorOnThePlayer()
    {
        var state = TestLevels.CorridorGame();

        Assert.True(state.BeginBarrierPlacement());

        Assert.True(state.IsPlacingBarrier);
        Assert.Equal(state.Player, state.AimCursor);
    }

    [Fact]
    public void BeginBarrierPlacement_WithNoBudget_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame(barrierBudget: 0);

        Assert.False(state.BeginBarrierPlacement());
        Assert.False(state.IsPlacingBarrier);
    }

    [Fact]
    public void BeginBarrierPlacement_WhileAlreadyAiming_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();
        state.BeginBarrierPlacement();

        Assert.False(state.BeginBarrierPlacement());
    }

    [Fact]
    public void BeginBarrierPlacement_AfterTheGameIsOver_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();
        for (var step = 0; step < 5; step++)
        {
            state.TryMove(Direction.East);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.False(state.BeginBarrierPlacement());
    }

    [Fact]
    public void MoveAimCursor_MovesOneCell()
    {
        var state = TestLevels.CorridorGame();
        state.BeginBarrierPlacement();

        Assert.True(state.MoveAimCursor(Direction.East));

        Assert.Equal(state.Maze.GetCell(0, 1), state.AimCursor);
    }

    [Fact]
    public void MoveAimCursor_AtTheEdge_ReturnsFalseAndStaysPut()
    {
        var state = TestLevels.CorridorGame();
        state.BeginBarrierPlacement();

        Assert.False(state.MoveAimCursor(Direction.West));
        Assert.False(state.MoveAimCursor(Direction.North));

        Assert.Equal(state.Player, state.AimCursor);
    }

    [Fact]
    public void MoveAimCursor_WhenNotAiming_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();

        Assert.False(state.MoveAimCursor(Direction.East));
    }

    [Fact]
    public void MoveAimCursor_IgnoresWalls()
    {
        // The cursor is a targeting reticle: it can cross a wall to reach any cell.
        var config = new GameConfig { Rows = 2, Columns = 2, KeyCount = 0, EnemyStepSeconds = 1.0 };
        var maze = new Grid(2, 2);
        var state = new GameState(config, maze, new DijkstraPathfinder(), new FixedRandom(0));
        state.BeginBarrierPlacement();

        Assert.True(maze.HasWall(0, 0, Direction.East));
        Assert.True(state.MoveAimCursor(Direction.East));
        Assert.Equal(maze.GetCell(0, 1), state.AimCursor);
    }

    [Fact]
    public void ConfirmPlacement_PlacesOnTheCursorCellAndSpendsOne()
    {
        var state = TestLevels.CorridorGame(barrierBudget: 2);
        state.BeginBarrierPlacement();
        state.MoveAimCursor(Direction.East);
        state.MoveAimCursor(Direction.East);

        Assert.True(state.ConfirmPlacement());

        Assert.Contains(state.Maze.GetCell(0, 2), state.Barriers);
        Assert.Equal(1, state.BarriersRemaining);
    }

    [Fact]
    public void ConfirmPlacement_StopsAiming()
    {
        var state = TestLevels.CorridorGame();
        state.BeginBarrierPlacement();

        Assert.True(state.ConfirmPlacement());

        Assert.False(state.IsPlacingBarrier);
    }

    [Fact]
    public void ConfirmPlacement_OnAnOccupiedCell_RefusesAndKeepsAiming()
    {
        var state = TestLevels.CorridorGame(barrierBudget: 2);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 0));

        Assert.True(state.BeginBarrierPlacement());
        Assert.False(state.ConfirmPlacement());

        Assert.True(state.IsPlacingBarrier);
        Assert.Equal(1, state.BarriersRemaining);
        Assert.Single(state.Barriers);
    }

    [Fact]
    public void ConfirmPlacement_WhenNotAiming_ReturnsFalse()
    {
        var state = TestLevels.CorridorGame();

        Assert.False(state.ConfirmPlacement());
        Assert.Empty(state.Barriers);
    }

    [Fact]
    public void CancelPlacement_LeavesAimingWithoutPlacing()
    {
        var state = TestLevels.CorridorGame(barrierBudget: 2);
        state.BeginBarrierPlacement();

        state.CancelPlacement();

        Assert.False(state.IsPlacingBarrier);
        Assert.Empty(state.Barriers);
        Assert.Equal(2, state.BarriersRemaining);
    }

    [Fact]
    public void CancelPlacement_WhenNotAiming_DoesNothing()
    {
        var state = TestLevels.CorridorGame();

        state.CancelPlacement();

        Assert.False(state.IsPlacingBarrier);
        Assert.Equal(2, state.BarriersRemaining);
    }

    [Fact]
    public void PlaceBarrier_CostsTheBudgetOncePerCell()
    {
        var state = TestLevels.CorridorGame(barrierBudget: 2);

        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 0));
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        Assert.Equal(2, state.Barriers.Count);
        Assert.Equal(0, state.BarriersRemaining);
        Assert.False(state.BeginBarrierPlacement());
    }

    [Fact]
    public void Aiming_FreezesTheKiller()
    {
        // The clock stops while aiming, so studying the map costs no time.
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var killer = state.Enemy;
        state.BeginBarrierPlacement();

        for (var tick = 0; tick < 10; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(killer, state.Enemy);
    }

    [Fact]
    public void Aiming_BlocksPlayerMovement()
    {
        var state = TestLevels.CorridorGame();
        state.BeginBarrierPlacement();

        Assert.False(state.TryMove(Direction.East));
        Assert.Equal(state.Maze.GetCell(0, 0), state.Player);
    }

    [Fact]
    public void Aiming_ResumesTheKillerAfterCancelling()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0);
        var killer = state.Enemy;
        state.BeginBarrierPlacement();
        state.Tick(5.0);

        state.CancelPlacement();
        state.Tick(1.0);

        Assert.NotEqual(killer, state.Enemy);
    }

    [Fact]
    public void Barrier_DoesNotStopThePlayer()
    {
        var state = TestLevels.CorridorGame();
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 0));

        Assert.True(state.TryMove(Direction.East));
    }

    [Fact]
    public void Barrier_NeverBlocksTheKiller()
    {
        // Entering the barriered cell costs 5 normal steps, but it must still arrive:
        // a player can never make the game unwinnable.
        var state = TestLevels.CorridorGame(barrierBudget: 1, enemyStepSeconds: 1.0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 0));

        for (var tick = 0; tick < 29; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Barrier_SteersTheKillerOntoTheCheaperDetour()
    {
        // Two routes to the player: a short top route through (0,1) and a long bottom route.
        // A barrier on the short route makes the long one cheaper, so the killer goes round.
        var state = TestLevels.TwoRouteGame();
        var barrierCell = state.Maze.GetCell(0, 1);

        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 1));

        state.Tick(TestLevels.OneSecondPerStep);

        Assert.Equal(state.Maze.GetCell(1, 1), state.Enemy);
        Assert.Contains(barrierCell, state.Barriers);
    }

    [Fact]
    public void Barrier_StillLetsTheKillerReachThePlayerOnTheDetour()
    {
        var state = TestLevels.TwoRouteGame();
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 1));

        // Three steps along the bottom route: (1,1), (1,0), then the player's cell.
        state.Tick(TestLevels.OneSecondPerStep);
        state.Tick(TestLevels.OneSecondPerStep);
        state.Tick(TestLevels.OneSecondPerStep);

        Assert.Equal(GameStatus.Caught, state.Status);
    }
}

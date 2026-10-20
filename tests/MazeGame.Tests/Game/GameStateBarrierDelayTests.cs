using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the delay half of the barrier rule: entering a barriered cell takes
/// <c>EnemyStepSeconds * ObstacleCost</c>, charged before the killer moves, so the player can
/// actually see the killer stall.
/// </summary>
/// <remarks>
/// <para>
/// The corridor level is used with a step time of one second and the default obstacle cost
/// of 5, so a spent barriered cell must take exactly 5 ticks (3 seconds at the shipped 0.6 s
/// step). These tests failed before the delay existed: the killer used to cross a barrier at
/// its normal pace, making the barrier look like it did nothing.
/// </para>
/// <para>
/// Seals are switched off here (<c>barrierSealSeconds: 0</c>) so these tests pin the price of
/// a <i>spent</i> barrier alone. A fresh barrier is sealed instead, which is a different rule
/// with its own tests in <c>GameStateSealTests</c>.
/// </para>
/// </remarks>
public sealed class GameStateBarrierDelayTests
{
    private const int CostSteps = 5;

    [Fact]
    public void Tick_BeforeEnteringABarrier_WaitsForTheBarrier()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));
        var killer = state.Enemy;

        for (var tick = 0; tick < CostSteps - 1; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(killer, state.Enemy);
        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    [Fact]
    public void Tick_WhenTheBarrierWaitIsOver_StepsOntoIt()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));

        for (var tick = 0; tick < CostSteps; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
    }

    [Fact]
    public void Tick_AfterCrossingABarrier_ResumesTheNormalPace()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));
        for (var tick = 0; tick < CostSteps; tick++)
        {
            state.Tick(1.0);
        }

        state.Tick(1.0);

        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);
    }

    [Fact]
    public void Tick_WithABarrierOnTheRoute_StillCatchesThePlayer()
    {
        // Five ticks for the barriered cell, then one per normal cell.
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));

        for (var tick = 0; tick < CostSteps + 4; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Tick_WithABarrierOnThePlayersCell_DelaysTheFinalStep()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 0));

        // Four normal cells bring the killer next to the player, then five ticks at the barrier.
        for (var tick = 0; tick < 4 + CostSteps - 1; tick++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(state.Maze.GetCell(0, 1), state.Enemy);
        Assert.Equal(GameStatus.InProgress, state.Status);

        state.Tick(1.0);

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Tick_WithoutABarrier_UsesOneStepPerSecond()
    {
        var state = TestLevels.CorridorGame(enemyStepSeconds: 1.0, barrierSealSeconds: 0);

        state.Tick(1.0);

        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
    }

    [Fact]
    public void Tick_WithAScaledStepTime_ScalesTheBarrierWait()
    {
        // Half-second steps: 5 cost units must take 5 * 0.5 = 2.5 seconds.
        var state = TestLevels.CorridorGame(enemyStepSeconds: 0.5, barrierSealSeconds: 0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));

        for (var tick = 0; tick < CostSteps; tick++)
        {
            state.Tick(0.5);
        }

        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
    }
}

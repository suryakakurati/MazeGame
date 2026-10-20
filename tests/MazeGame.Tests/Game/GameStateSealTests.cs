using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the seal half of the barrier rule: a fresh barrier counts as a wall, so the killer
/// takes another route while the seal holds and only tears it apart when there is no other way.
/// </summary>
/// <remarks>
/// The corridor level is used with one-second steps and an explicit three-second tear-down, so
/// every delay in these tests is a whole number of ticks and the arithmetic is readable.
/// </remarks>
public sealed class GameStateSealTests
{
    private const double Step = 1.0;
    private const double BreakSeconds = 3.0;

    [Fact]
    public void PlacingABarrier_SealsItForTheConfiguredTime()
    {
        var state = Corridor(sealSeconds: 10.0);

        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        var cell = state.Maze.GetCell(0, 3);
        Assert.Contains(cell, state.Barriers);
        Assert.Equal(10.0, state.BarrierSeals[cell], precision: 9);
    }

    [Fact]
    public void WithNoSealTime_ABarrierIsSpentImmediately()
    {
        var state = Corridor(sealSeconds: 0.0);

        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        Assert.Contains(state.Maze.GetCell(0, 3), state.Barriers);
        Assert.Empty(state.BarrierSeals);
    }

    [Fact]
    public void ASeal_ExpiresByTheClockEvenThoughTheKillerIsFarAway()
    {
        // A parked killer isolates the timer: the seal must age with the clock, not with the
        // killer's steps, so a barrier cannot be saved for later by simply standing still.
        var state = Corridor(sealSeconds: 2.0, stepSeconds: 1000.0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        state.Tick(2.0);

        Assert.Empty(state.BarrierSeals);
        Assert.Contains(state.Maze.GetCell(0, 3), state.Barriers);
    }

    [Fact]
    public void ACrackedBarrier_IsAPassableSpeedBumpAgain()
    {
        // The seal lapses after two seconds; from then on the husk is priced at ObstacleCost,
        // so the killer pushes through it exactly as it did before seals existed.
        var state = Corridor(sealSeconds: 2.0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        state.Tick(Step);
        state.Tick(Step);

        Assert.Empty(state.BarrierSeals);
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);

        for (var tick = 0; tick < 4; tick++)
        {
            state.Tick(Step);
        }

        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);
    }

    [Fact]
    public void ASealedBarrier_OnTheOnlyRoute_IsTornApartNotWaitedOut()
    {
        // player (0,0) ... killer (0,5), with the only route blocked at (0,3).
        var state = Corridor();
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));
        var barrier = state.Maze.GetCell(0, 3);

        // One step to reach (0,4), then up to the tear-down time with nothing else happening.
        state.Tick(Step);
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
        Assert.Contains(barrier, state.Barriers);

        state.Tick(Step);
        state.Tick(Step);

        // Two of the three tear-down seconds are gone, so the seal is still holding and the
        // killer has not moved any closer.
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
        Assert.Contains(barrier, state.Barriers);
        Assert.Equal(7.0, state.BarrierSeals[barrier], precision: 6);
    }

    [Fact]
    public void TearingASeal_DestroysTheBarrierAndThenTheKillerWalksIn()
    {
        var state = Corridor();
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));
        var barrier = state.Maze.GetCell(0, 3);

        // Three steps to reach (0,4): one step in, then three seconds of tearing at the wall.
        for (var tick = 0; tick < BreakSeconds + 1; tick++)
        {
            state.Tick(Step);
        }

        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
        Assert.DoesNotContain(barrier, state.Barriers);
        Assert.Empty(state.BarrierSeals);

        // With the barrier gone the cell is plain floor, reached on the next normal step.
        state.Tick(Step);

        Assert.Equal(barrier, state.Enemy);
    }

    [Fact]
    public void ASealedBarrier_IsNeverWalkedThroughWhileADetourExists()
    {
        // Two routes to the player. The seal is what makes the killer commit to the long one
        // straight away instead of counting down in front of the barrier.
        var state = TestLevels.TwoRouteGame();
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 1));
        var barrier = state.Maze.GetCell(0, 1);

        for (var tick = 0; tick < 6 && state.Status == GameStatus.InProgress; tick++)
        {
            state.Tick(Step);
            Assert.NotEqual(barrier, state.Enemy);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.Contains(barrier, state.Barriers);
    }

    [Fact]
    public void AScaledTearDownTime_IsChargedInSecondsNotSteps()
    {
        // Half-second steps but a two-second tear-down: the wait must not scale with the step
        // time, because breaking a wall is its own action.
        var state = Corridor(breakSeconds: 2.0, stepSeconds: 0.5);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));

        // One half-second step brings the killer next to the barrier; the tear-down then runs
        // on its own two-second clock.
        state.Tick(0.5);
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);

        state.Tick(0.5);
        state.Tick(0.5);
        state.Tick(0.5);

        // 1.5 seconds of the two needed: still sealed, still not moving.
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);
        Assert.NotEmpty(state.BarrierSeals);

        state.Tick(0.5);

        Assert.Empty(state.BarrierSeals);
        Assert.Empty(state.Barriers);
    }

    [Fact]
    public void ABarrierBudgetCannotSealTheKillerAwayForever()
    {
        // Three seals across the only corridor the killer can use. Each one must eventually
        // break, which is the rule that keeps the game from becoming unwinnable for the killer.
        var state = TestLevels.CorridorGame(enemyStepSeconds: Step, barrierBudget: 3, barrierSealSeconds: 10.0);
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 2));
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 3));
        Assert.True(TestLevels.PlaceBarrierAt(state, 0, 4));

        for (var tick = 0; tick < 100 && state.Status == GameStatus.InProgress; tick++)
        {
            state.Tick(Step);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    /// <summary>
    /// A 1x6 corridor game with explicit seal settings: player (0,0), exit (0,1), keys
    /// (0,2)-(0,4), killer (0,5).
    /// </summary>
    private static GameState Corridor(
        double sealSeconds = 10.0,
        double breakSeconds = BreakSeconds,
        double stepSeconds = Step) =>
        TestLevels.GameOver(
            TestLevels.Corridor(),
            new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = 3,
                BarrierBudget = 3,
                EnemyStepSeconds = stepSeconds,
                VisionRadius = 3,
                BarrierSealSeconds = sealSeconds,
                BarrierBreakSeconds = breakSeconds,
                SealedBarrierCost = 40,
                Seed = null,
            });
}

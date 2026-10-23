using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Covers the floor's event stream: the moments the UI turns into sound. The stream is deliberately
/// small, so each test checks both that the right moment is raised and that it is raised only once.
/// </summary>
public sealed class GameEventTests
{
    [Fact]
    public void AStepOntoAKey_RaisesStepThenKeyPicked()
    {
        var state = TestLevels.CorridorGame(keyCount: 3);

        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryMove(Direction.East));

        var events = state.DrainEvents();

        Assert.Equal(new[] { GameEvent.Step, GameEvent.Step, GameEvent.KeyPicked }, events);
    }

    [Fact]
    public void Draining_EmptiesTheQueue()
    {
        var state = TestLevels.CorridorGame(keyCount: 3);
        Assert.True(state.TryMove(Direction.East));

        Assert.NotEmpty(state.DrainEvents());
        Assert.Empty(state.DrainEvents());
    }

    [Fact]
    public void AQueueNobodyDrains_KeepsOnlyTheRecentMoments()
    {
        var state = TestLevels.CorridorGame(keyCount: 3);

        for (var step = 0; step < 70; step++)
        {
            Assert.True(state.TryMove(step % 2 == 0 ? Direction.East : Direction.West));
        }

        // The cap is 32; a headless run must not grow a queue the length of the campaign.
        Assert.Equal(32, state.DrainEvents().Count);
    }

    [Fact]
    public void ASpentHeart_RaisesHeartLost()
    {
        var state = TestLevels.CorridorGame(startingHearts: 2, enemyStepSeconds: 0.2);

        for (var tick = 0; tick < 12; tick++)
        {
            state.Tick(0.2);
        }

        var events = state.DrainEvents();

        Assert.Equal(1, state.Hearts);
        Assert.Contains(GameEvent.HeartLost, events);
        Assert.DoesNotContain(GameEvent.RunLost, events);
    }

    [Fact]
    public void TheLastHeart_RaisesHeartLostLastThenRunLost()
    {
        var state = TestLevels.CorridorGame(startingHearts: 1, enemyStepSeconds: 0.2);

        for (var tick = 0; tick < 12; tick++)
        {
            state.Tick(0.2);
        }

        var events = state.DrainEvents();

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.Contains(GameEvent.HeartLostLast, events);
        Assert.Contains(GameEvent.RunLost, events);
        Assert.True(
            events.ToList().IndexOf(GameEvent.HeartLostLast) < events.ToList().IndexOf(GameEvent.RunLost),
            "the last heart is lost before the run is.");
    }

    [Fact]
    public void ReachingTheExit_RaisesFloorCleared()
    {
        var state = TestLevels.CorridorGame(keyCount: 0);

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(GameStatus.Won, state.Status);
        Assert.Contains(GameEvent.FloorCleared, state.DrainEvents());
    }
}

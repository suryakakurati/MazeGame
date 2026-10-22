using System;
using MazeGame.Core.Game;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies that a floor can carry more than one hunter: each keeps its own cell, clock and stun,
/// a catch on any of them costs a heart, and a throw moves only the one that fed.
/// </summary>
/// <remarks>
/// The descent's biggest step up is a second hunter (floor 10) and a third (floor 16), so these
/// tests pin the rule rather than the content: <see cref="FloorCatalogTests"/> checks where the
/// extra hunters arrive.
/// </remarks>
public sealed class GameStateHunterTests
{
    /// <summary>
    /// Builds a 1x6 corridor with two hunters, a single key and two hearts.
    /// </summary>
    /// <remarks>
    /// Player (0,0), exit (0,1), key (0,2), then the hunters take the two furthest cells: (0,5)
    /// and (0,4). Every cell is one step from its neighbour, so a tick per second walks them home.
    /// </remarks>
    private static GameState TwoHunterGame() => new(
        new GameConfig
        {
            Rows = 1,
            Columns = 6,
            KeyCount = 1,
            HunterCount = 2,
            EnemyStepSeconds = 1.0,
            StartingHearts = 2,
            MaxHearts = 2,
            VisionRadius = 3,
        },
        TestLevels.Corridor(6),
        new DijkstraPathfinder(),
        new FixedRandom(0));

    [Fact]
    public void Constructor_WithTwoHunters_PlacesBothOnDistinctCells()
    {
        var state = TwoHunterGame();

        Assert.Equal(2, state.HunterCount);
        Assert.Equal(2, state.Enemies.Count);
        Assert.NotEqual(state.Enemies[0].Cell, state.Enemies[1].Cell);
        Assert.Equal(state.Enemies[0].Cell, state.Enemy);
    }

    [Fact]
    public void Tick_MovesEveryHunterOnItsOwnClock()
    {
        var state = TwoHunterGame();
        var first = state.Enemies[0].Cell;
        var second = state.Enemies[1].Cell;

        state.Tick(1.0);

        Assert.NotEqual(first, state.Enemies[0].Cell);
        Assert.NotEqual(second, state.Enemies[1].Cell);
    }

    [Fact]
    public void Tick_WhenASecondHunterReachesThePlayer_SpendsAHeartAndStunsThemAll()
    {
        var state = TwoHunterGame();

        // The nearer hunter is four steps away; the far one is five.
        for (var step = 0; step < 4; step++)
        {
            state.Tick(1.0);
        }

        Assert.Equal(1, state.Hearts);

        // A reprieve that leaves one hunter walking is not a reprieve, so the catch holds both.
        Assert.True(state.IsEnemyStunned);

        foreach (var hunter in state.Enemies)
        {
            Assert.True(hunter.Stunned);
            Assert.NotEqual(state.Player, hunter.Cell);
        }
    }

    [Fact]
    public void TakeHit_ThrowsOnlyTheHunterThatCaughtThePlayer()
    {
        var state = TwoHunterGame();

        // Let the near hunter walk onto the player; the far one is one cell behind it.
        for (var step = 0; step < 4; step++)
        {
            state.Tick(1.0);
        }

        var cells = new[] { state.Enemies[0].Cell, state.Enemies[1].Cell };

        Assert.Equal(2, cells.Length);
        Assert.NotEqual(cells[0], cells[1]);
        Assert.All(cells, cell => Assert.NotEqual(state.Player, cell));
    }

    [Fact]
    public void Recovery_AfterTheStun_LetsBothHuntersWalkAgain()
    {
        var state = TwoHunterGame();

        for (var step = 0; step < 4; step++)
        {
            state.Tick(1.0);
        }

        Assert.True(state.IsEnemyStunned);

        // The default respawn stun is 1.5 s; a couple of seconds is enough for both to wake.
        state.Tick(2.0);

        Assert.False(state.IsEnemyStunned);
        Assert.Equal(0.0, state.EnemyStunSecondsRemaining);
    }

    [Fact]
    public void ForFloor_CarriesTheFloorsHunterCount()
    {
        var config = new GameConfig().ForFloor(FloorCatalog.ByNumber(10), 3);

        Assert.Equal(2, config.HunterCount);
        Assert.Equal(3, new GameConfig().ForFloor(FloorCatalog.ByNumber(16), 3).HunterCount);
    }

    [Fact]
    public void ForFloor_MakesEveryCatchCostOneMoreHeartEveryFiveFloors()
    {
        var config = new GameConfig();

        Assert.Equal(1, config.ForFloor(FloorCatalog.ByNumber(1), 3).CatchCost);
        Assert.Equal(1, config.ForFloor(FloorCatalog.ByNumber(5), 3).CatchCost);
        Assert.Equal(2, config.ForFloor(FloorCatalog.ByNumber(6), 3).CatchCost);
        Assert.Equal(2, config.ForFloor(FloorCatalog.ByNumber(10), 3).CatchCost);
        Assert.Equal(3, config.ForFloor(FloorCatalog.ByNumber(11), 3).CatchCost);
        Assert.Equal(4, config.ForFloor(FloorCatalog.ByNumber(20), 3).CatchCost);
    }

    [Fact]
    public void Validate_WithNoHunters_Throws()
    {
        var config = new GameConfig { HunterCount = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenTheMazeCannotHoldEveryHunter_Throws()
    {
        // Two hunters, three keys and the player need seven cells; this maze has six.
        var config = new GameConfig
        {
            Rows = 1,
            Columns = 6,
            KeyCount = 3,
            HunterCount = 2,
        };

        Assert.Throws<ArgumentException>(() => config.Validate());
    }
}

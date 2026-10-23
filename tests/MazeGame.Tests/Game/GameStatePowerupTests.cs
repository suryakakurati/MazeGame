using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the five powerup verbs: Blink, Veil, Drag, Lantern and Shove.
/// </summary>
/// <remarks>
/// Each test names the price as well as the effect, because a powerup without its cost would be
/// a free tool and the kit is built around every verb having one. The levels are hand-built so
/// the cell a blink lands on and the cell a shove pushes to are exact, not searched for.
/// </remarks>
public sealed class GameStatePowerupTests
{
    [Fact]
    public void Blink_StepsThroughTheWallAndLeavesTheBreachOpen()
    {
        var state = BlinkLevel();
        var start = state.Player;
        var destination = state.Maze.GetCell(1, 0);

        Assert.True(state.TryBlink());

        Assert.Equal(destination, state.Player);
        Assert.Equal(start, state.PreviousPlayer);
        Assert.False(state.Maze.HasWall(0, 0, Direction.South));
        Assert.Equal(PowerupCharges.ForFloor(8).Blink - 1, state.PowerupsRemaining.Blink);
        Assert.Contains(GameEvent.Blink, state.DrainEvents());
    }

    [Fact]
    public void Blink_AtAnOpenPassageOrIntoRock_RefusesAndCostsNothing()
    {
        var state = BlinkLevel();

        // Turn east by stepping onto the locked exit; the passage ahead is open, so a blink has
        // nothing to go through and must not spend a charge.
        Assert.True(state.TryMove(Direction.East));
        Assert.False(state.TryBlink());
        Assert.Equal(PowerupCharges.ForFloor(8).Blink, state.PowerupsRemaining.Blink);
    }

    [Fact]
    public void Blink_WithNoCharges_Refuses()
    {
        var state = BlinkLevel(PowerupCharges.None);

        Assert.False(state.TryBlink());
        Assert.Equal(PowerupCharges.None.Blink, state.PowerupsRemaining.Blink);
    }

    [Fact]
    public void Veil_StandingStillForTheChannel_SpendsAChargeAndHidesThePlayer()
    {
        var state = CorridorGame();
        var veil = state.PowerupsRemaining.Veil;

        Assert.True(state.TryVeil());
        Assert.True(state.IsVeilCharging);
        Assert.False(state.IsVeiled);

        state.Tick(0.6);

        Assert.True(state.IsVeiled);
        Assert.False(state.IsVeilCharging);
        Assert.Equal(veil - 1, state.PowerupsRemaining.Veil);
        Assert.Contains(GameEvent.Veiled, state.DrainEvents());
    }

    [Fact]
    public void Veil_EndsAfterThreeSeconds()
    {
        var state = CorridorGame();
        Assert.True(state.TryVeil());

        state.Tick(0.6);
        Assert.True(state.IsVeiled);

        state.Tick(3.0);
        Assert.False(state.IsVeiled);
    }

    [Fact]
    public void Veil_ABrokenChannelCostsNothingAndAStepKeepsThePlayerHidden()
    {
        var state = CorridorGame();
        var veil = state.PowerupsRemaining.Veil;

        Assert.True(state.TryVeil());
        Assert.True(state.TryMove(Direction.East));
        Assert.False(state.IsVeilCharging);
        Assert.Equal(veil, state.PowerupsRemaining.Veil);

        // The broken channel costs nothing, but it can be started again.
        Assert.True(state.TryVeil());
        state.Tick(0.6);
        Assert.True(state.IsVeiled);

        // Walking is what a veil is for: it must not end the moment the player slips away.
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.IsVeiled);
        Assert.Equal(veil - 1, state.PowerupsRemaining.Veil);

        state.Tick(3.0);
        Assert.False(state.IsVeiled);
    }

    [Fact]
    public void Veil_LetsThePlayerWalkAwayWhileTheHuntSearchesTheOldCell()
    {
        // Two rows joined at both ends: the hunt's shortest route to the player is one way, and
        // its route to the cell it last saw them on is the other. That is what makes the veil's
        // promise visible — a frozen memory sends it back down the row it came from.
        var state = TestLevels.GameOver(TwoRowCorridor(), new GameConfig
        {
            Rows = 2,
            Columns = 6,
            KeyCount = 3,
            EnemyStepSeconds = 1.0,
            StartingHearts = 1,
            MaxHearts = 1,
            VisionRadius = 3,

            // Longer than the walk, so the test measures the veil rather than its clock.
            VeilDurationSeconds = 10.0,
            Seed = null,
        });

        Assert.Equal(state.Maze.GetCell(0, 0), state.Player);
        Assert.Equal(state.Maze.GetCell(1, 5), state.Enemy);

        // Half a second first, so the hunt has seen the player and taken a step down the row
        // toward them before the veil is drawn.
        state.Tick(0.5);

        Assert.True(state.TryVeil());
        state.Tick(0.6);
        Assert.True(state.IsVeiled);
        Assert.Equal(state.Maze.GetCell(1, 4), state.Enemy);

        for (var step = 0; step < 5; step++)
        {
            Assert.True(state.TryMove(Direction.East));
        }

        Assert.Equal(state.Maze.GetCell(0, 5), state.Player);
        Assert.True(state.IsVeiled);

        for (var second = 0; second < 4; second++)
        {
            state.Tick(1.0);
        }

        // Four steps after the walk: the hunt came up into the top row and kept going west,
        // toward the cell it last saw. A hunt that had followed the player would be three cells
        // to the east instead, which is where the player is standing.
        Assert.Equal(state.Maze.GetCell(0, 1), state.Enemy);
        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    /// <summary>
    /// Builds a two-row corridor joined once, at column two — an unequal pair of routes, so the
    /// hunt's target actually shows in which way it walks.
    /// </summary>
    private static Grid TwoRowCorridor()
    {
        var grid = new Grid(2, 6);

        for (var column = 0; column < 5; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
            grid.RemoveWall(1, column, Direction.East);
        }

        grid.RemoveWall(0, 2, Direction.South);

        return grid;
    }

    [Fact]
    public void Drag_SlowsTheHuntThenLeavesItFaster()
    {
        var state = CorridorGame();
        var drag = state.PowerupsRemaining.Drag;

        Assert.True(state.TryDrag());
        Assert.True(state.IsDragActive);
        Assert.Equal(drag - 1, state.PowerupsRemaining.Drag);

        state.Tick(3.0);

        Assert.False(state.IsDragActive);
        Assert.True(state.IsDragPenalty);

        state.Tick(5.0);

        Assert.False(state.IsDragPenalty);
        Assert.Equal(0, state.PowerupsRemaining.Drag);
        Assert.False(state.TryDrag());
    }

    [Fact]
    public void Lantern_LandsAheadAndMovesTheLightToItsCell()
    {
        var state = CorridorGame(length: 7);
        Assert.True(state.TryMove(Direction.East));

        Assert.True(state.TryThrowLantern());

        var lantern = state.Maze.GetCell(0, 5);
        Assert.Equal(lantern, state.LanternCell);
        Assert.Equal(lantern, state.LightOrigin);
        Assert.True(state.IsLanternActive);
        Assert.True(state.IsVisible(lantern));
        Assert.Equal(PowerupCharges.ForFloor(8).Lantern - 1, state.PowerupsRemaining.Lantern);
    }

    [Fact]
    public void Lantern_ReturnsAfterItsTimeRunsOut()
    {
        var state = CorridorGame(length: 9);
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryThrowLantern());

        state.Tick(8.0);

        Assert.False(state.IsLanternActive);
        Assert.Null(state.LanternCell);
        Assert.Equal(state.Player, state.LightOrigin);
        Assert.Contains(GameEvent.LanternReturned, state.DrainEvents());
    }

    [Fact]
    public void Lantern_WithNoChargesOrAPassageInTheWay_Refuses()
    {
        var state = CorridorGame(length: 7, PowerupCharges.None);

        Assert.False(state.TryThrowLantern());
    }

    [Fact]
    public void Shove_PushesTheHunterTwoCellsBackAndStunsIt()
    {
        var state = ShoveLevel();
        Assert.True(state.TryMove(Direction.East));
        state.Tick(1.0);

        var hunter = state.Maze.GetCell(0, 2);
        Assert.Equal(hunter, state.Enemy);

        Assert.True(state.TryShove());

        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);
        Assert.Equal(PowerupCharges.ForFloor(8).Shove - 1, state.PowerupsRemaining.Shove);
        Assert.Contains(GameEvent.Shoved, state.DrainEvents());

        // The stun is a real window: half a second later it has not moved again.
        state.Tick(0.5);
        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);
    }

    [Fact]
    public void Shove_WorksWithoutFacingTheHunter()
    {
        var state = ShoveLevel();
        Assert.True(state.TryMove(Direction.East));
        state.Tick(1.0);

        // Turn away: a press into the north wall only changes facing, it does not move.
        Assert.False(state.TryMove(Direction.North));

        Assert.True(state.TryShove());
        Assert.Equal(state.Maze.GetCell(0, 3), state.Enemy);
    }

    [Fact]
    public void Lantern_ReachesFiveCellsAhead()
    {
        // From (0,1) the throw crosses five cells and lands on (0,6). The old range stopped one
        // cell shorter, which made a room-wide throw impossible.
        var state = CorridorGame(length: 9);
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryThrowLantern());

        Assert.Equal(state.Maze.GetCell(0, 6), state.LanternCell);
    }

    [Fact]
    public void Lantern_BurnsForEightSeconds()
    {
        var state = CorridorGame(length: 9);
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryThrowLantern());

        state.Tick(7.9);
        Assert.True(state.IsLanternActive);

        state.Tick(0.2);
        Assert.False(state.IsLanternActive);
    }

    [Fact]
    public void Lantern_StopsShortOfAHunterInsteadOfBeingRefused()
    {
        // Player (0,1), hunter at (0,6): the throw stops on (0,5) rather than refusing the whole
        // charge because the furthest reachable cell is occupied.
        var state = CorridorGame(length: 7);
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryThrowLantern());

        Assert.Equal(state.Maze.GetCell(0, 5), state.LanternCell);
    }

    [Fact]
    public void Shove_AtAnEmptyCell_RefusesAndCostsNothing()
    {
        var state = CorridorGame();

        Assert.False(state.TryShove());
        Assert.Equal(PowerupCharges.ForFloor(8).Shove, state.PowerupsRemaining.Shove);
    }

    [Fact]
    public void Powerups_AreRefusedOnceTheFloorIsOver()
    {
        var state = CorridorGame(keyCount: 0);
        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(GameStatus.Won, state.Status);
        var blink = state.PowerupsRemaining.Blink;

        Assert.False(state.TryBlink());
        Assert.False(state.TryVeil());
        Assert.False(state.TryDrag());
        Assert.False(state.TryThrowLantern());
        Assert.False(state.TryShove());
        Assert.Equal(blink, state.PowerupsRemaining.Blink);
    }

    /// <summary>
    /// A 2x3 floor with the player's east side walled, so a blink has something to go through:
    /// player (0,0), exit (0,1), key (0,2), killer (1,2).
    /// </summary>
    private static GameState BlinkLevel(PowerupCharges? powerups = null)
    {
        var grid = new Grid(2, 3);
        grid.RemoveWall(0, 0, Direction.East);
        grid.RemoveWall(0, 1, Direction.East);
        grid.RemoveWall(0, 1, Direction.South);
        grid.RemoveWall(1, 0, Direction.East);
        grid.RemoveWall(1, 1, Direction.East);
        grid.RemoveWall(1, 2, Direction.North);

        return TestLevels.GameOver(
            grid,
            new GameConfig
            {
                Rows = 2,
                Columns = 3,
                KeyCount = 1,
                EnemyStepSeconds = 1.0,
                PlayerStepSeconds = 0.1,
                VisionRadius = 3,
                StartingHearts = 3,
                MaxHearts = 3,
                Powerups = powerups ?? PowerupCharges.ForFloor(8),
                Seed = null,
            });
    }

    /// <summary>Player (0,0), exit (0,1), keys (0,2)-(0,4), killer (0,5).</summary>
    private static GameState CorridorGame(
        int length = 6,
        PowerupCharges? powerups = null,
        int keyCount = 3) =>
        TestLevels.GameOver(
            TestLevels.Corridor(length),
            new GameConfig
            {
                Rows = 1,
                Columns = length,
                KeyCount = keyCount,
                EnemyStepSeconds = 1.0,
                PlayerStepSeconds = 0.1,
                VisionRadius = 3,
                StartingHearts = 3,
                MaxHearts = 3,
                Powerups = powerups ?? PowerupCharges.ForFloor(8),
                Seed = null,
            });

    /// <summary>
    /// A 1x4 corridor: player (0,0), exit (0,1), key (0,2), killer (0,3). After one killer step
    /// it stands on the key cell, next to the player on the exit.
    /// </summary>
    private static GameState ShoveLevel() =>
        TestLevels.GameOver(
            TestLevels.Corridor(4),
            new GameConfig
            {
                Rows = 1,
                Columns = 4,
                KeyCount = 1,
                EnemyStepSeconds = 1.0,
                PlayerStepSeconds = 0.1,
                VisionRadius = 3,
                StartingHearts = 3,
                MaxHearts = 3,
                Powerups = PowerupCharges.ForFloor(8),
                Seed = null,
            });
}

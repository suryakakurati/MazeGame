using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the late floors' exclusive rules: each is one switch, each is legible in a single
/// encounter, and none of them can make the arch unreachable.
/// </summary>
/// <remarks>
/// The floors are hand-built corridors, so the rule under test is the only thing that can decide
/// an assertion. The hunter's step time is deliberately huge where the hunt is not the subject, so
/// a long <c>Tick</c> winds the rule's clock and nothing else.
/// </remarks>
public sealed class FloorRuleTests
{
    [Fact]
    public void TheCatalog_AssignsOneRuleToTheFloorsWhoseIdentityIsOne()
    {
        var ruled = FloorCatalog.Shipped.Where(floor => floor.Rule is not null).ToList();

        Assert.Equal(6, ruled.Count);
        Assert.Equal(6, ruled.Select(floor => floor.Rule!.Value).Distinct().Count());

        Assert.Equal(FloorRule.Tide, ByNumber(3).Rule);
        Assert.Equal(FloorRule.ThreeLocks, ByNumber(6).Rule);
        Assert.Equal(FloorRule.Sleeper, ByNumber(14).Rule);
        Assert.Equal(FloorRule.ThreadGates, ByNumber(18).Rule);
        Assert.Equal(FloorRule.BurningLantern, ByNumber(19).Rule);
        Assert.Equal(FloorRule.LongDoor, ByNumber(20).Rule);
    }

    [Fact]
    public void AFloorWithoutARule_IsUnchanged()
    {
        var state = Game(rule: null);

        Assert.Null(state.Rule);
        Assert.Null(state.ThreadGateA);
        Assert.Null(state.ThreadGateB);
        Assert.Equal(0, state.LanternLoss);
        Assert.Equal(0, state.ExitHoldFraction);
        Assert.DoesNotContain(state.Enemies, hunter => hunter.Dormant);
    }

    [Fact]
    public void ThreadGates_AreTwoFreeCellsFarApart()
    {
        var state = Game(FloorRule.ThreadGates);

        Assert.NotNull(state.ThreadGateA);
        Assert.NotNull(state.ThreadGateB);

        var first = state.ThreadGateA!.Value;
        var second = state.ThreadGateB!.Value;

        Assert.NotEqual((first.Row, first.Column), (second.Row, second.Column));

        foreach (var taken in new[] { state.Player, state.Exit }.Concat(state.Keys).Concat(state.Enemies.Select(e => e.Cell)))
        {
            Assert.NotEqual((taken.Row, taken.Column), (first.Row, first.Column));
            Assert.NotEqual((taken.Row, taken.Column), (second.Row, second.Column));
        }
    }

    [Fact]
    public void ThreadGates_CarryThePlayerAcross()
    {
        var state = Game(FloorRule.ThreadGates);
        var (near, far) = Gates(state);

        Assert.True(WalkTo(state, state.Maze.GetCell(0, near.Column - 1)));
        state.Tick(2.0);
        state.DrainEvents();

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal((far.Row, far.Column), (state.Player.Row, state.Player.Column));
        Assert.Contains(GameEvent.Teleported, state.DrainEvents());
    }

    [Fact]
    public void ThreadGates_HaveACooldown()
    {
        var state = Game(FloorRule.ThreadGates);
        var (near, far) = Gates(state);

        Assert.True(WalkTo(state, state.Maze.GetCell(0, near.Column - 1)));
        state.Tick(2.0);
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(far.Column, state.Player.Column);

        // Stepping off the far pad and straight back on during the cooldown is an ordinary step.
        Assert.True(state.TryMove(Direction.West));
        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(far.Column, state.Player.Column);

        // After the two seconds the pad works again.
        state.Tick(2.0);
        Assert.True(state.TryMove(Direction.West));
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(near.Column, state.Player.Column);
    }

    [Fact]
    public void ASleeper_DoesNotMoveUntilItWakes()
    {
        var state = Game(FloorRule.Sleeper, length: 10);

        Assert.Single(state.Enemies);
        Assert.True(state.Enemies[0].Dormant);

        var sleeper = state.Enemies[0].Cell;

        state.Tick(5.0);

        Assert.Equal(sleeper, state.Enemies[0].Cell);
        Assert.True(state.Enemies[0].Dormant);
    }

    [Fact]
    public void ASleeper_WakesWhenThePlayerComesClose()
    {
        // A four-cell corridor puts the hunter three cells from the start, so one step east is
        // enough to wake it without touching the bloom at (0,2).
        var state = Game(FloorRule.Sleeper, length: 4);

        Assert.True(state.Enemies[0].Dormant);
        Assert.True(state.TryMove(Direction.East));

        Assert.False(state.Enemies[0].Dormant);
    }

    [Fact]
    public void ASleeper_WakesWhenABloomIsTaken()
    {
        var state = Game(FloorRule.Sleeper, length: 10, enemyStepSeconds: 1.0);
        var sleeper = state.Enemies[0].Cell;

        Assert.True(WalkTo(state, state.Maze.GetCell(0, 2)));
        Assert.Empty(state.Keys);
        Assert.False(state.Enemies[0].Dormant);

        state.Tick(1.0);

        Assert.NotEqual(sleeper, state.Enemies[0].Cell);
    }

    [Fact]
    public void TheLongDoor_NeedsFiveContinuousSeconds()
    {
        var state = Game(FloorRule.LongDoor);

        // The bloom first, or the arch is still barred and there is nothing to hold.
        Assert.True(WalkTo(state, state.Maze.GetCell(0, 2)));
        Assert.True(WalkTo(state, state.Exit));

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(0.0, state.ExitHoldFraction);

        state.Tick(2.5);

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.InRange(state.ExitHoldFraction, 0.45, 0.55);

        state.Tick(2.5);

        Assert.Equal(GameStatus.Won, state.Status);
    }

    [Fact]
    public void TheLongDoor_ResetsWhenThePlayerLeaves()
    {
        var state = Game(FloorRule.LongDoor);

        Assert.True(WalkTo(state, state.Maze.GetCell(0, 2)));
        Assert.True(WalkTo(state, state.Exit));
        state.Tick(3.0);

        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(0.0, state.ExitHoldFraction);

        Assert.True(state.TryMove(Direction.West));
        state.Tick(3.0);

        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    [Fact]
    public void APlainArch_IsStillWonOnTouch()
    {
        var state = Game(rule: null);

        Assert.True(WalkTo(state, state.Maze.GetCell(0, 2)));
        Assert.True(WalkTo(state, state.Exit));

        Assert.Equal(GameStatus.Won, state.Status);
    }

    [Fact]
    public void TheBurningLantern_LosesAStepEveryFortySeconds()
    {
        var state = Game(FloorRule.BurningLantern, visionRadius: 3);

        Assert.Equal(3, state.EffectiveVisionRadius);

        state.Tick(40.0);
        Assert.Equal(2, state.EffectiveVisionRadius);

        state.Tick(40.0);
        Assert.Equal(1, state.EffectiveVisionRadius);

        // The floor is one: the lantern keeps burning, but it can never go out entirely.
        state.Tick(400.0);
        Assert.Equal(1, state.EffectiveVisionRadius);
    }

    [Fact]
    public void TheBurningLantern_IsRestoredByEveryBloom()
    {
        var state = Game(FloorRule.BurningLantern, visionRadius: 3);

        state.Tick(45.0);
        Assert.Equal(2, state.EffectiveVisionRadius);

        Assert.True(WalkTo(state, state.Maze.GetCell(0, 2)));
        Assert.Empty(state.Keys);

        Assert.Equal(3, state.EffectiveVisionRadius);
        Assert.Equal(0, state.LanternLoss);
    }

    /// <summary>Returns the two thread gates, ordered by column.</summary>
    private static (Cell Near, Cell Far) Gates(GameState state)
    {
        var first = state.ThreadGateA!.Value;
        var second = state.ThreadGateB!.Value;

        return first.Column < second.Column ? (first, second) : (second, first);
    }

    private static FloorDefinition ByNumber(int number) =>
        FloorCatalog.Shipped.Single(floor => floor.Number == number);

    /// <summary>A one-row corridor game with <paramref name="rule"/> switched on.</summary>
    private static GameState Game(
        FloorRule? rule,
        int length = 10,
        int keyCount = 1,
        int visionRadius = 3,
        double enemyStepSeconds = 100.0,
        int startingHearts = 3)
    {
        var config = TestLevels.CorridorConfig(
            length,
            keyCount,
            enemyStepSeconds: enemyStepSeconds,
            visionRadius: visionRadius,
            startingHearts: startingHearts) with
        {
            Rule = rule,
        };

        return TestLevels.GameOver(TestLevels.Corridor(length), config);
    }

    private static bool WalkTo(GameState state, Cell target)
    {
        while (state.Player.Row != target.Row || state.Player.Column != target.Column)
        {
            var direction = state.Player.Column < target.Column
                ? Direction.East
                : state.Player.Column > target.Column
                    ? Direction.West
                    : state.Player.Row < target.Row ? Direction.South : Direction.North;

            if (!state.TryMove(direction))
            {
                return false;
            }
        }

        return true;
    }
}

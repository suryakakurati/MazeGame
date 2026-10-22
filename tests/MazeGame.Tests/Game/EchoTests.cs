using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the Echo: one per shipped floor, off the critical path, read by walking onto it, with
/// the hunt frozen while it speaks and nothing to hear the second time.
/// </summary>
public sealed class EchoTests
{
    private static readonly GameFactory Factory =
        new(new GameConfig(), seed => new SeededRandom(seed), new DijkstraPathfinder());

    [Fact]
    public void EveryShippedFloor_HasOneEcho_OffTheCriticalPath()
    {
        var pathfinder = new DijkstraPathfinder();

        for (var number = 1; number <= FloorCatalog.ShippedFloorCount; number++)
        {
            var state = Factory.CreateFloor(
                FloorCatalog.ByNumber(number),
                3,
                RunModifiers.None,
                Array.Empty<DescentCondition>());

            var echo = state.EchoCell;

            Assert.True(echo is not null, $"floor {number} has no Echo");
            var cell = echo!.Value;

            Assert.False(state.Maze.IsSolid(cell.Row, cell.Column), $"floor {number}: the Echo is in rock");
            Assert.NotEqual(state.Player, cell);
            Assert.NotEqual(state.Exit, cell);
            Assert.DoesNotContain(cell, state.Keys);
            Assert.DoesNotContain(cell, state.Enemies.Select(hunter => hunter.Cell));

            // A thread gate and the Echo must not share a cell: the gate would carry the reader
            // across the floor while the card was still open.
            Assert.NotEqual(state.ThreadGateA, cell);
            Assert.NotEqual(state.ThreadGateB, cell);

            if (state.SafeRoom is { } room)
            {
                Assert.False(room.Contains(cell), $"floor {number}: the Echo is inside the safe room");
            }

            // Off the critical path: going through the Echo is at least two steps longer than the
            // direct route, so touching it is always a detour rather than a corridor the player
            // must sit through.
            var direct = pathfinder.FindPath(state.Maze, state.Player, state.Exit, _ => 1).Count;
            var viaEcho = pathfinder.FindPath(state.Maze, state.Player, cell, _ => 1).Count;
            var toArch = pathfinder.FindPath(state.Maze, cell, state.Exit, _ => 1).Count;

            Assert.True(viaEcho > 0 && toArch > 0, $"floor {number}: the Echo is unreachable");
            Assert.True(
                viaEcho + toArch - direct - 1 >= 2,
                $"floor {number}: the Echo is on the shortest route");
        }
    }

    [Fact]
    public void WalkingOntoTheEcho_OpensIt_AndTheHuntHolds()
    {
        var state = OpenAnEcho(out var echo);

        Assert.True(state.IsEchoOpen);
        Assert.False(state.IsEchoRead);
        Assert.InRange(state.EchoLines.Count, 2, StoryCatalog.EchoLineCount);

        var hunters = state.Enemies.Select(hunter => hunter.Cell).ToList();

        state.Tick(4.0);

        Assert.Equal(hunters, state.Enemies.Select(hunter => hunter.Cell));

        // Reading is not walking: no direction is accepted while the card is open.
        Assert.False(state.TryMove(Direction.North));

        Assert.Equal(echo, state.Player);
    }

    [Fact]
    public void LettingGo_ResumesTheFloor_AndSpendsTheEcho()
    {
        var state = OpenAnEcho(out var echo);

        var hunters = state.Enemies.Select(hunter => hunter.Cell).ToList();

        Assert.True(state.CloseEcho());
        Assert.False(state.CloseEcho());

        Assert.False(state.IsEchoOpen);
        Assert.True(state.IsEchoRead);

        // The hunt was where it was left, and it moves again afterwards.
        Assert.Equal(hunters, state.Enemies.Select(hunter => hunter.Cell));

        state.Tick(4.0);

        Assert.NotEqual(hunters, state.Enemies.Select(hunter => hunter.Cell));
    }

    [Fact]
    public void HearingTheEcho_RestoresExactlyOneHeart_Once()
    {
        var state = OpenAnEcho(out _);
        var before = state.Hearts;

        Assert.True(state.CloseEcho());
        Assert.Equal(before + 1, state.Hearts);

        // Nothing to hear twice, so nothing to heal twice.
        Assert.False(state.CloseEcho());
        Assert.Equal(before + 1, state.Hearts);
    }

    [Fact]
    public void ComingBackTheSecondTime_SaysNothing()
    {
        var state = OpenAnEcho(out var echo);

        state.CloseEcho();

        Settle(state);

        var step = FirstOpening(state.Maze, echo);

        Assert.True(state.TryMove(step));
        Assert.False(state.IsEchoOpen);

        Settle(state);

        Assert.True(state.TryMove(Opposite(step)));

        Settle(state);

        Assert.Equal(echo, state.Player);
        Assert.False(state.IsEchoOpen);
        Assert.True(state.IsEchoRead);
    }

    [Fact]
    public void AFloorWithoutAnEcho_HasNoneAndSaysNothing()
    {
        var state = TestLevels.CorridorGame();

        Assert.Null(state.EchoCell);
        Assert.False(state.IsEchoOpen);
        Assert.False(state.IsEchoRead);
        Assert.Empty(state.EchoLines);
        Assert.False(state.CloseEcho());
    }

    [Fact]
    public void AnEchoWithoutAFloorNumber_IsRefused()
    {
        var config = new GameConfig { EchoEnabled = true };

        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void AFloorNumberOutsideTheBook_IsRefused()
    {
        var config = new GameConfig { FloorNumber = FloorCatalog.ShippedFloorCount + 1 };

        Assert.Throws<ArgumentOutOfRangeException>(config.Validate);
    }

    /// <summary>
    /// Finds a seeded first floor whose Echo can be walked to without clearing or losing the
    /// floor on the way, and leaves that game with the Echo's card open.
    /// </summary>
    /// <param name="echo">The Echo's cell, once found.</param>
    /// <returns>The game, with <see cref="GameState.IsEchoOpen"/> true.</returns>
    /// <remarks>
    /// The walk collects blooms as it goes, so on some seeds the arch opens and the player steps
    /// out of the floor on the way; those seeds are skipped rather than special-cased, which keeps
    /// the test about the Echo and not about the route.
    /// </remarks>
    private static GameState OpenAnEcho(out Cell echo)
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var factory = new GameFactory(
                new GameConfig { Seed = seed },
                value => new SeededRandom(value),
                new DijkstraPathfinder());

            var state = factory.CreateFloor(
                FloorCatalog.ByNumber(1),
                3,
                RunModifiers.None,
                Array.Empty<DescentCondition>());

            if (state.EchoCell is not { } target || !WalkTo(state, target))
            {
                continue;
            }

            if (state.Status == GameStatus.InProgress && state.IsEchoOpen)
            {
                echo = target;
                return state;
            }
        }

        throw new InvalidOperationException("No seed reached the Echo on floor 1.");
    }

    /// <summary>Walks the shortest route to <paramref name="target"/>, winding the step clock.</summary>
    private static bool WalkTo(GameState state, Cell target)
    {
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, target, _ => 1);

        if (path.Count == 0)
        {
            return false;
        }

        for (var index = 1; index < path.Count; index++)
        {
            if (state.IsEchoOpen)
            {
                return false;
            }

            if (!state.TryMove(DirectionToward(state.Player, path[index])))
            {
                return false;
            }

            if (state.IsPlayerStepping)
            {
                state.Tick(state.LastPlayerStepSeconds);
            }

            if (state.Status != GameStatus.InProgress)
            {
                return false;
            }
        }

        return state.Player.Row == target.Row && state.Player.Column == target.Column;
    }

    /// <summary>Winds the step clock until the player is standing still.</summary>
    private static void Settle(GameState state)
    {
        for (var guard = 0; guard < 8 && state.IsPlayerStepping; guard++)
        {
            state.Tick(state.LastPlayerStepSeconds);
        }
    }

    /// <summary>Returns the first open direction out of <paramref name="cell"/>.</summary>
    private static Direction FirstOpening(Grid maze, Cell cell)
    {
        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            if (!maze.HasWall(cell.Row, cell.Column, direction))
            {
                return direction;
            }
        }

        throw new InvalidOperationException("The Echo stands in a sealed cell.");
    }

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        _ => Direction.East,
    };

    private static Direction DirectionToward(Cell from, Cell to)
    {
        if (to.Row < from.Row)
        {
            return Direction.North;
        }

        if (to.Row > from.Row)
        {
            return Direction.South;
        }

        return to.Column > from.Column ? Direction.East : Direction.West;
    }
}

using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the safe room's pause: while the player stands inside, the world outside holds still
/// and the player can still walk around; the door cell is ordinary floor and leaving resumes
/// everything.
/// </summary>
/// <remarks>
/// The room is carved by the real carver into a hand-sized maze, so these tests exercise the same
/// nine cells and the same single door the game builds, without depending on a room being rolled.
/// </remarks>
public sealed class GameStateSafeRoomTests
{
    /// <summary>A 7x7 perfect maze with a reserved 3x3 room at rows 1-3, columns 1-3.</summary>
    private static readonly SecretRoomSite RoomSite = new(1, 1, Reserved: true);

    [Fact]
    public void WalkingIn_ReportsTheRoomAndPausesTheWorld()
    {
        var state = BuildGame();

        Assert.NotNull(state.SafeRoom);
        Assert.False(state.IsInSafeRoom);
        Assert.False(state.SafeRoom!.Contains(state.Player));
        Assert.False(state.SafeRoom.Contains(state.Exit));

        foreach (var key in state.Keys)
        {
            Assert.False(state.SafeRoom.Contains(key));
        }

        Assert.True(WalkTo(state, state.SafeRoom.Center));
        Assert.True(state.IsInSafeRoom);
        Assert.Contains(GameEvent.SecretRoomEntered, state.DrainEvents());
    }

    [Fact]
    public void InsideTheRoom_NoHunterMoves()
    {
        var state = BuildGame(enemyStepSeconds: 0.5);

        Assert.True(WalkTo(state, state.SafeRoom!.Center));

        var hunter = state.Enemies[0].Cell;

        state.Tick(10.0);

        Assert.Equal(hunter, state.Enemies[0].Cell);
        Assert.True(state.IsInSafeRoom);
    }

    [Fact]
    public void InsideTheRoom_SealsDoNotCrack()
    {
        var state = BuildGame(enemyStepSeconds: 0.5);

        // A barrier placed before entering: its seal is the floor's clock in miniature.
        Assert.True(TestLevels.PlaceBarrierAt(state, 6, 6));
        Assert.True(state.BarrierSeals.Count == 1);
        var seal = state.BarrierSeals[state.Maze.GetCell(6, 6)];

        Assert.True(WalkTo(state, state.SafeRoom!.Center));
        state.Tick(10.0);

        Assert.True(state.BarrierSeals.TryGetValue(state.Maze.GetCell(6, 6), out var remaining));
        Assert.Equal(seal, remaining);
    }

    [Fact]
    public void InsideTheRoom_ThePlayerCanStillWalk()
    {
        var state = BuildGame();

        Assert.True(WalkTo(state, state.SafeRoom!.Center));

        var center = state.SafeRoom.Center;
        var neighbour = FirstRoomNeighbour(state, center);

        Assert.True(state.TryMove(DirectionToward(center, neighbour)));
        Assert.True(state.IsInSafeRoom);
        Assert.False(state.SafeRoom.Contains(center) && state.Player == center);
    }

    [Fact]
    public void InsideTheRoom_ThePlayersOwnCooldownStillTicks()
    {
        var state = BuildGame(playerStepSeconds: 1.0, enemyStepSeconds: 100.0);

        Assert.True(WalkTo(state, state.SafeRoom!.Center));

        // Take one more step inside the room, so its cooldown is running while the world is paused.
        var from = state.SafeRoom.Center;
        var first = FirstRoomNeighbour(state, from);

        Assert.True(state.TryMove(DirectionToward(from, first)));
        Assert.True(state.IsPlayerStepping);
        Assert.True(state.IsInSafeRoom);

        var second = FirstRoomNeighbour(state, first);

        // A press during the cooldown is buffered, not applied: the player is still on `first`.
        Assert.False(state.TryMove(DirectionToward(first, second)));
        Assert.Equal(first.Row, state.Player.Row);
        Assert.Equal(first.Column, state.Player.Column);

        // One second of Tick inside the room spends the cooldown and resolves the buffered press,
        // which is exactly what proves the player's own clock still runs while the world is paused.
        state.Tick(1.0);

        Assert.Equal(second.Row, state.Player.Row);
        Assert.Equal(second.Column, state.Player.Column);
        Assert.True(state.IsInSafeRoom);

        // The resolved step started its own cooldown, still inside the room.
        Assert.True(state.IsPlayerStepping);
    }

    [Fact]
    public void InsideTheRoom_EveryCellIsLit()
    {
        var state = BuildGame(visionRadius: 0);

        Assert.True(WalkTo(state, state.SafeRoom!.Center));

        foreach (var cell in state.SafeRoomCells)
        {
            Assert.True(state.IsVisible(cell), $"room cell ({cell.Row}, {cell.Column}) is dark");
        }
    }

    [Fact]
    public void TheDoorCell_IsOrdinaryFloor()
    {
        var state = BuildGame();
        var door = state.SafeRoom!.Door;

        Assert.True(WalkTo(state, door));

        Assert.False(state.IsInSafeRoom);
        Assert.Equal(door.Row, state.Player.Row);
        Assert.Equal(door.Column, state.Player.Column);
    }

    [Fact]
    public void Leaving_ResumesTheHunt()
    {
        var state = BuildGame(enemyStepSeconds: 0.5);

        Assert.True(WalkTo(state, state.SafeRoom!.Center));
        state.Tick(5.0);

        var frozen = state.Enemies[0].Cell;

        Assert.True(WalkTo(state, state.SafeRoom.Door));
        state.Tick(5.0);

        Assert.NotEqual(frozen, state.Enemies[0].Cell);
        Assert.False(state.IsInSafeRoom);
        Assert.Contains(GameEvent.SecretRoomLeft, state.DrainEvents());
    }

    [Fact]
    public void StandingStillInside_RaisesEnterOnce()
    {
        var state = BuildGame();

        Assert.True(WalkTo(state, state.SafeRoom!.Center));
        state.Tick(1.0);
        state.Tick(1.0);

        var events = state.DrainEvents();

        Assert.Single(events, gameEvent => gameEvent == GameEvent.SecretRoomEntered);
        Assert.DoesNotContain(GameEvent.SecretRoomLeft, events);
    }

    [Fact]
    public void AFloorWithNoRoom_BehavesExactlyAsBefore()
    {
        var state = TestLevels.CorridorGame();

        Assert.Null(state.SafeRoom);
        Assert.Empty(state.SafeRoomCells);
        Assert.False(state.IsInSafeRoom);

        state.Tick(1.0);

        Assert.DoesNotContain(GameEvent.SecretRoomEntered, state.DrainEvents());
    }

    /// <summary>Builds a game over a maze whose reserved pocket became a real room.</summary>
    private static GameState BuildGame(
        double playerStepSeconds = 0,
        double enemyStepSeconds = 1.0,
        int visionRadius = 5)
    {
        var random = new SeededRandom(4);
        var maze = new MazeGenerator(random).Generate(new MazeOptions(7, 7, 4)
        {
            ReservedCells = SecretRoomCarver.ReservedCells(RoomSite),
        });

        var room = SecretRoomCarver.TryCarve(maze, RoomSite, random);

        Assert.NotNull(room);

        return new GameState(
            new GameConfig
            {
                Rows = 7,
                Columns = 7,
                KeyCount = 1,
                BarrierBudget = 1,
                BarrierSealSeconds = 30.0,
                EnemyStepSeconds = enemyStepSeconds,
                PlayerStepSeconds = playerStepSeconds,
                VisionRadius = visionRadius,
                EnemyAuraRadius = 0,
                EnemyAuraDistance = 0,
                StartingHearts = 3,
                MaxHearts = 3,
                Seed = null,
            },
            maze,
            new DijkstraPathfinder(),
            new FixedRandom(0),
            room);
    }

    /// <summary>Walks the player to <paramref name="target"/> one step at a time.</summary>
    private static bool WalkTo(GameState state, Cell target)
    {
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, target, _ => 1);

        if (path.Count == 0)
        {
            return false;
        }

        for (var index = 1; index < path.Count; index++)
        {
            if (!state.TryMove(DirectionToward(state.Player, path[index])))
            {
                return false;
            }

            // A timed floor has to be wound forward past each step's cooldown. The test floors
            // that care give the hunter a very long step time, so this never moves it.
            if (state.IsPlayerStepping)
            {
                state.Tick(state.LastPlayerStepSeconds);
            }
        }

        return state.Player.Row == target.Row && state.Player.Column == target.Column;
    }

    /// <summary>The first room cell that shares an open passage with <paramref name="from"/>.</summary>
    private static Cell FirstRoomNeighbour(GameState state, Cell from)
    {
        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            if (state.Maze.HasWall(from.Row, from.Column, direction))
            {
                continue;
            }

            var (rowDelta, columnDelta) = direction.Delta();
            var neighbour = state.Maze.GetCell(from.Row + rowDelta, from.Column + columnDelta);

            if (state.SafeRoom!.Contains(neighbour))
            {
                return neighbour;
            }
        }

        throw new InvalidOperationException("The room has no neighbouring room cell to step into.");
    }

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

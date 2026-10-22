using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using Xunit;

namespace MazeGame.Tests.Maze;

/// <summary>
/// Verifies the one promise a secret room makes to the maze: it is nine cells with a single door,
/// cut from rock, and it leaves every corridor it did not own exactly as it was.
/// </summary>
/// <remarks>
/// The tests build floors the way <see cref="GameFactory"/> does — outline, room plan from its own
/// stream, generation with the pocket reserved, carve — so what they check is the real pipeline
/// and not a simplified copy of it.
/// </remarks>
public sealed class SecretRoomTests
{
    /// <summary>
    /// Every carved room, on every shipped floor and both site flavours, has nine cells, lies in
    /// the grid, and joins the maze through exactly one opening.
    /// </summary>
    [Fact]
    public void EveryCarvedRoom_HasNineCellsAndOneEntrance()
    {
        var roomsSeen = 0;

        foreach (var floor in FloorCatalog.Shipped)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var built = Build(seed, floor);

                if (built.Room is not { } room)
                {
                    continue;
                }

                roomsSeen++;

                Assert.Equal(SecretRoom.CellCount, room.Cells.Count);
                Assert.Equal(SecretRoom.CellCount, Distinct(room.Cells).Count);

                foreach (var cell in room.Cells)
                {
                    Assert.InRange(cell.Row, 0, built.Maze.Rows - 1);
                    Assert.InRange(cell.Column, 0, built.Maze.Columns - 1);
                    Assert.False(built.Maze.IsSolid(cell.Row, cell.Column));
                    Assert.True(room.Contains(cell));
                }

                Assert.Equal(1, OpeningsFrom(built.Maze, room));
                Assert.False(room.Contains(room.Door));
                Assert.True(room.Contains(room.Doorway));
                Assert.Equal(1, Distance(built.Maze, room.Doorway, room.Door));
            }
        }

        // The sweep has to actually see rooms, or it proves nothing.
        Assert.True(roomsSeen > 20, $"only {roomsSeen} rooms were rolled across the catalog");
    }

    /// <summary>
    /// Carving adds exactly thirteen passages (twelve inside the room plus the door) and removes
    /// none: every wall between two original cells is exactly as the generator left it.
    /// </summary>
    [Fact]
    public void Carving_LeavesOriginalPassagesUntouchedAndAddsExactlyThirteen()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var before = Generate(seed, floor);

                if (before.Site is not { } site)
                {
                    continue;
                }

                var passagesBefore = before.Maze.CountPassages();
                var snapshot = Snapshot(before.Maze, site);

                var room = SecretRoomCarver.TryCarve(before.Maze, site, new SeededRandom(seed + 1));

                Assert.NotNull(room);

                // Twelve internal edges become passages; the doorway adds the thirteenth. Nothing
                // else moves, which is what "does not disturb the maze" means as a number.
                Assert.Equal(passagesBefore + 13, before.Maze.CountPassages());

                foreach (var (row, column, direction, wasOpen) in snapshot)
                {
                    Assert.Equal(wasOpen, !before.Maze.HasWall(row, column, direction));
                }
            }
        }
    }

    /// <summary>
    /// Every original cell is still reachable after the carve, and the room itself is reachable
    /// through its one door.
    /// </summary>
    [Fact]
    public void Carving_KeepsTheMazeConnected()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var built = Build(seed, floor);

                if (built.Room is not { } room)
                {
                    continue;
                }

                var start = room.Door;
                var reached = Reachable(built.Maze, start);

                for (var row = 0; row < built.Maze.Rows; row++)
                {
                    for (var column = 0; column < built.Maze.Columns; column++)
                    {
                        if (built.Maze.IsSolid(row, column))
                        {
                            continue;
                        }

                        Assert.Contains((row, column), reached);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A floor with no rock of its own has its pocket reserved before generation, so the room the
    /// spec asks for does not depend on the silhouette the floor happens to wear.
    /// </summary>
    [Fact]
    public void ARectangularFloor_ReservesItsPocket()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var site = SecretRoomCarver.Plan(null, 15, 15, 20, new SeededRandom(seed));

            if (site is not { } planned)
            {
                continue;
            }

            Assert.True(planned.Reserved, "a rectangle has no rock to hide a room in");
            Assert.Equal(SecretRoom.CellCount, SecretRoomCarver.ReservedCells(planned).Count);
        }
    }

    /// <summary>
    /// Every floor after the teaching floor rolls the same chance, within a point over ten
    /// thousand rolls, and no floor is ever a certainty again: the rooms are a find.
    /// </summary>
    [Fact]
    public void TheChance_IsTheSameRollOnEveryFloorAfterTheTeachingFloor()
    {
        const int trials = 10000;

        for (var floor = SecretRoomCarver.TeachingFloors + 1; floor <= 20; floor++)
        {
            var hits = 0;

            for (var seed = 0; seed < trials; seed++)
            {
                if (SecretRoomCarver.Rolls(floor, new SeededRandom(seed)))
                {
                    hits++;
                }
            }

            var percent = hits * 100.0 / trials;

            Assert.InRange(
                percent,
                SecretRoomCarver.RoomChancePercent - 1,
                SecretRoomCarver.RoomChancePercent + 1);
        }
    }

    /// <summary>
    /// A twenty-floor run meets the guaranteed first floor plus about one room for every ten
    /// floors after it: roughly three rooms. Rooms are a find, and the room cards are how a run
    /// that wants more of them invests in the rate.
    /// </summary>
    [Fact]
    public void ARun_MeetsRoomsThroughoutTheDescent()
    {
        const int runs = 200;
        var total = 0;

        for (var run = 0; run < runs; run++)
        {
            for (var floor = 1; floor <= FloorCatalog.ShippedFloorCount; floor++)
            {
                if (SecretRoomCarver.Rolls(floor, new SeededRandom((run * 7919) + floor)))
                {
                    total++;
                }
            }
        }

        var average = total / (double)runs;

        Assert.InRange(average, 2.0, 4.5);
    }

    /// <summary>
    /// A room card raises the chance by ten points a copy, and the total can reach a certainty
    /// without ever going past it.
    /// </summary>
    [Fact]
    public void TheRoomChance_IsRaisedByCardsAndCappedAtCertainty()
    {
        Assert.Equal(
            SecretRoomCarver.RoomChancePercent + 10,
            SecretRoomCarver.ChancePercent(20, 10));
        Assert.Equal(100, SecretRoomCarver.ChancePercent(20, 95));
        Assert.Equal(100, SecretRoomCarver.ChancePercent(20, 1000));
    }

    /// <summary>
    /// A forced room is carved whatever the dice say: a card that promises one cannot be broken by
    /// the ten percent roll.
    /// </summary>
    [Fact]
    public void AForcedRoom_SkipsTheRollAndFindsASite()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            Assert.True(SecretRoomCarver.Rolls(20, new SeededRandom(seed), bonusPercent: 0, forceRoom: true));
            Assert.NotNull(SecretRoomCarver.Plan(
                null,
                15,
                15,
                20,
                new SeededRandom(seed),
                bonusPercent: 0,
                forceRoom: true));
        }
    }

    [Fact]
    public void TheChance_IsCappedOutsideTheRun()
    {
        Assert.Equal(0, SecretRoomCarver.ChancePercent(0));
        Assert.Equal(100, SecretRoomCarver.ChancePercent(1));
        Assert.Equal(10, SecretRoomCarver.ChancePercent(20));
        Assert.Equal(10, SecretRoomCarver.ChancePercent(140));
        Assert.False(SecretRoomCarver.Rolls(0, new SeededRandom(1)));
    }

    /// <summary>
    /// The opening floors always hold a room, whatever the seed: the safe room is taught rather
    /// than hoped for.
    /// </summary>
    [Fact]
    public void TheTeachingFloors_AlwaysHoldARoom()
    {
        for (var floor = 1; floor <= SecretRoomCarver.TeachingFloors; floor++)
        {
            Assert.Equal(100, SecretRoomCarver.ChancePercent(floor));

            for (var seed = 0; seed < 50; seed++)
            {
                Assert.True(SecretRoomCarver.Rolls(floor, new SeededRandom(seed)));
            }
        }
    }

    /// <summary>
    /// Whenever the roll hits on an ordinary fifteen-by-fifteen floor, a site exists to put the
    /// room in; a floor that rolled a room never silently drops it.
    /// </summary>
    [Fact]
    public void Plan_FindsASiteWheneverTheRollHits()
    {
        for (var seed = 0; seed < 400; seed++)
        {
            var roll = SecretRoomCarver.Rolls(20, new SeededRandom(seed));
            var site = SecretRoomCarver.Plan(null, 15, 15, 20, new SeededRandom(seed));

            Assert.Equal(roll, site is not null);
        }
    }

    [Fact]
    public void Plan_MissesWhenTheFloorIsTooSmall()
    {
        Assert.Null(SecretRoomCarver.Plan(null, 4, 15, 20, new SeededRandom(1)));
        Assert.Null(SecretRoomCarver.Plan(null, 15, 4, 20, new SeededRandom(1)));
    }

    /// <summary>
    /// The maze's own objects are never placed inside a room, and no hunter starts there. This is
    /// checked through the factory, on both the rock and the reserved path.
    /// </summary>
    [Fact]
    public void NothingThePlayerMustFind_IsEverPlacedInARoom()
    {
        var roomsSeen = 0;

        foreach (var number in new[] { 1, 7, 20 })
        {
            var floor = FloorCatalog.Shipped[number - 1];

            for (var seed = 0; seed < 200; seed++)
            {
                var factory = new GameFactory(
                    new GameConfig { Seed = seed },
                    stream => new SeededRandom(stream),
                    new DijkstraPathfinder());
                var state = factory.CreateFloor(floor, floor.MaxHearts, RunModifiers.None);

                if (state.SafeRoom is not { } room)
                {
                    continue;
                }

                roomsSeen++;
                Assert.Equal(SecretRoom.CellCount, state.SafeRoomCells.Count);
                Assert.False(room.Contains(state.Player));
                Assert.False(room.Contains(state.Exit));

                foreach (var key in state.Keys)
                {
                    Assert.False(room.Contains(key));
                }

                foreach (var hunter in state.Enemies)
                {
                    Assert.False(room.Contains(hunter.Cell));
                }
            }
        }

        Assert.True(roomsSeen > 10, $"only {roomsSeen} rooms were built");
    }

    [Fact]
    public void TryCarve_WithNoSite_ReturnsNothing()
    {
        var grid = new Grid(5, 5);

        Assert.Null(SecretRoomCarver.TryCarve(grid, null, new SeededRandom(1)));
    }

    /// <summary>Builds a floor exactly as the factory does, and carves its room.</summary>
    private static (Grid Maze, SecretRoom? Room, SecretRoomSite? Site) Build(int seed, FloorDefinition floor)
    {
        var generated = Generate(seed, floor);
        var room = SecretRoomCarver.TryCarve(generated.Maze, generated.Site, new SeededRandom(seed + 7));

        return (generated.Maze, room, generated.Site);
    }

    /// <summary>Generates a floor, reserves the pocket and stops before the room is carved.</summary>
    private static (Grid Maze, SecretRoomSite? Site) Generate(int seed, FloorDefinition floor)
    {
        var config = new GameConfig().ForFloor(floor, floor.MaxHearts, Array.Empty<DescentCondition>());
        var random = new SeededRandom((seed * 977) + floor.Number);
        var shape = config.Shape == ShapeKind.Rect
            ? null
            : MazeShape.Create(config.Shape, config.Rows, config.Columns, random);
        var roomRandom = new SeededRandom(unchecked(((seed * 977) + floor.Number) ^ 0x5EC2B00F));

        var site = SecretRoomCarver.Plan(shape, config.Rows, config.Columns, floor.Number, roomRandom);

        var generator = new BraidedMazeGenerator(
            new DeadEndBraider(new MazeGenerator(random), random, config.DeadEndBraidPercent),
            random,
            config.ExtraLoops);

        var maze = generator.Generate(new MazeOptions(config.Rows, config.Columns, seed)
        {
            Shape = shape,
            ReservedCells = site is { Reserved: true } pocket
                ? SecretRoomCarver.ReservedCells(pocket)
                : null,
        });

        return (maze, site);
    }

    /// <summary>
    /// Records the wall state of every edge between two cells that are not part of the room, so
    /// the carve can be checked for having left all of them alone.
    /// </summary>
    private static List<(int Row, int Column, Direction Direction, bool WasOpen)> Snapshot(
        Grid maze,
        SecretRoomSite site)
    {
        var edges = new List<(int Row, int Column, Direction Direction, bool WasOpen)>();
        var room = new HashSet<(int Row, int Column)>();

        for (var rowOffset = 0; rowOffset < SecretRoom.Size; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < SecretRoom.Size; columnOffset++)
            {
                room.Add((site.Row + rowOffset, site.Column + columnOffset));
            }
        }

        for (var row = 0; row < maze.Rows; row++)
        {
            for (var column = 0; column < maze.Columns; column++)
            {
                if (room.Contains((row, column)))
                {
                    continue;
                }

                if (row + 1 < maze.Rows && !room.Contains((row + 1, column)))
                {
                    edges.Add((row, column, Direction.South, !maze.HasWall(row, column, Direction.South)));
                }

                if (column + 1 < maze.Columns && !room.Contains((row, column + 1)))
                {
                    edges.Add((row, column, Direction.East, !maze.HasWall(row, column, Direction.East)));
                }
            }
        }

        return edges;
    }

    /// <summary>Counts the openings between the room and the rest of the floor.</summary>
    private static int OpeningsFrom(Grid maze, SecretRoom room)
    {
        var openings = 0;
        var inside = new HashSet<(int Row, int Column)>();

        foreach (var cell in room.Cells)
        {
            inside.Add((cell.Row, cell.Column));
        }

        foreach (var cell in room.Cells)
        {
            foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var neighbour = (cell.Row + rowDelta, cell.Column + columnDelta);

                if (!inside.Contains(neighbour))
                {
                    openings++;
                }
            }
        }

        return openings;
    }

    /// <summary>Every cell reachable from <paramref name="start"/> through open passages.</summary>
    private static HashSet<(int Row, int Column)> Reachable(Grid maze, Cell start)
    {
        var seen = new HashSet<(int Row, int Column)> { (start.Row, start.Column) };
        var pending = new Stack<Cell>();
        pending.Push(start);

        while (pending.Count > 0)
        {
            var cell = pending.Pop();

            foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var row = cell.Row + rowDelta;
                var column = cell.Column + columnDelta;

                if (!seen.Add((row, column)))
                {
                    continue;
                }

                pending.Push(maze.GetCell(row, column));
            }
        }

        return seen;
    }

    private static HashSet<(int Row, int Column)> Distinct(IReadOnlyList<Cell> cells)
    {
        var distinct = new HashSet<(int Row, int Column)>();

        foreach (var cell in cells)
        {
            distinct.Add((cell.Row, cell.Column));
        }

        return distinct;
    }

    private static int Distance(Grid maze, Cell from, Cell to)
    {
        var seen = new Dictionary<(int Row, int Column), int> { [(from.Row, from.Column)] = 0 };
        var pending = new Queue<Cell>();
        pending.Enqueue(from);

        while (pending.Count > 0)
        {
            var cell = pending.Dequeue();
            var distance = seen[(cell.Row, cell.Column)];

            if (cell.Row == to.Row && cell.Column == to.Column)
            {
                return distance;
            }

            foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                if (maze.HasWall(cell.Row, cell.Column, direction))
                {
                    continue;
                }

                var (rowDelta, columnDelta) = direction.Delta();
                var row = cell.Row + rowDelta;
                var column = cell.Column + columnDelta;

                if (seen.ContainsKey((row, column)))
                {
                    continue;
                }

                seen[(row, column)] = distance + 1;
                pending.Enqueue(maze.GetCell(row, column));
            }
        }

        return int.MaxValue;
    }
}

using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the factory that the frontends call to start a game — including on a replay after
/// death, where the contract is simply "a brand-new, independent, playable game".
/// </summary>
public sealed class GameFactoryTests
{
    private const int Seed = 7;

    [Fact]
    public void Create_ReturnsAPlayableGame()
    {
        var state = Factory().Create();

        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(15, state.Maze.Rows);
        Assert.Equal(15, state.Maze.Columns);
        Assert.Equal(3, state.KeysRemaining);
        Assert.Equal(3, state.BarriersRemaining);
        Assert.Equal(2, state.LuresRemaining);
        Assert.Equal(3, state.Hearts);
        Assert.Equal(3, state.MaxHearts);
        Assert.False(state.IsExitUnlocked);
        Assert.NotEqual(state.Player, state.Enemy);
        Assert.DoesNotContain(state.Player, state.Keys);
    }

    [Fact]
    public void Create_ShapesTheMazeWithTheBraidAndTheLoops()
    {
        // The factory owns the shipped pipeline, so this proves the config reaches both
        // decorators rather than leaving the player on a bare perfect maze.
        var state = Factory().Create();

        var perfectPassages = state.Maze.CellCount - 1;
        Assert.True(
            state.Maze.CountPassages() >= perfectPassages + state.Config.ExtraLoops + 1,
            "the braid must open at least one dead end on a 15x15 maze");
    }

    [Fact]
    public void Create_WithBothShapersOff_LeavesAPerfectMaze()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed, DeadEndBraidPercent = 0, ExtraLoops = 0 },
            _ => new SeededRandom(Seed),
            new DijkstraPathfinder());

        var state = factory.Create();

        Assert.Equal(state.Maze.CellCount - 1, state.Maze.CountPassages());
    }

    [Fact]
    public void Create_WithTheSameSeed_ProducesTheSameGame()
    {
        var first = Factory().Create();
        var second = Factory().Create();

        for (var row = 0; row < first.Maze.Rows; row++)
        {
            for (var column = 0; column < first.Maze.Columns; column++)
            {
                Assert.Equal(first.Maze.GetCell(row, column), second.Maze.GetCell(row, column));
            }
        }

        Assert.Equal(first.Player, second.Player);
        Assert.Equal(first.Enemy, second.Enemy);
        Assert.Equal(first.Exit, second.Exit);
        Assert.Equal(first.Keys, second.Keys);
    }

    [Fact]
    public void Create_WithANullSeed_ProducesDifferentGames()
    {
        // A replay should be a new cave, not the same one again.
        var factory = new GameFactory(
            new GameConfig { Seed = null },
            _ => new SeededRandom(null),
            new DijkstraPathfinder());

        var openings = new HashSet<(Cell Player, Cell Enemy, Cell Exit)>();

        for (var run = 0; run < 3; run++)
        {
            var state = factory.Create();
            openings.Add((state.Player, state.Enemy, state.Exit));
        }

        Assert.True(openings.Count > 1, "three unseeded games must not all start identically");
    }

    [Fact]
    public void Create_AfterAGameEnds_StartsAFreshIndependentGame()
    {
        // This is the replay contract: after the killer wins, asking the factory again must give
        // a game that starts from the beginning with every objective and budget restored.
        var state = Factory().Create();
        var pathfinder = new DijkstraPathfinder();

        // A hunter only tracks the player inside its aura now, so standing still for 400 seconds
        // is no longer a guaranteed death on a fifteen-by-fifteen floor. The test is about the
        // replay contract, not the length of a chase, so the player walks at the hunter instead.
        for (var attempt = 0; attempt < 1200 && state.Status == GameStatus.InProgress; attempt++)
        {
            var path = pathfinder.FindPath(state.Maze, state.Player, state.Enemy, _ => 1);

            if (path.Count > 1)
            {
                state.TryMove(DirectionToward(state.Player, path[1]));
            }

            state.Tick(0.1);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.Equal(0, state.Hearts);

        var replay = Factory().Create();

        Assert.Equal(GameStatus.InProgress, replay.Status);
        Assert.Equal(3, replay.Hearts);
        Assert.Equal(3, replay.KeysRemaining);
        Assert.Equal(3, replay.BarriersRemaining);
        Assert.Equal(2, replay.LuresRemaining);
        Assert.Empty(replay.Barriers);
        Assert.Empty(replay.BarrierSeals);
        Assert.Null(replay.Lure);

        // The old game must not be resurrected by starting a new one.
        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void Create_AsksForAFreshRandomSourceEveryGame()
    {
        var calls = 0;
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            _ =>
            {
                calls++;
                return new SeededRandom(Seed);
            },
            new DijkstraPathfinder());

        factory.Create();
        factory.Create();

        Assert.Equal(2, calls);
    }

    [Fact]
    public void CreateFloor_WithARoomCard_TurnsAMissedRollIntoARoom()
    {
        // Find a shipped floor whose ten percent roll misses, then raise the chance to a certainty
        // with a card. The room must exist on the second build whatever the dice said.
        var factory = Factory();
        FloorDefinition? roomless = null;

        for (var number = 2; number <= FloorCatalog.ShippedFloorCount && roomless is null; number++)
        {
            var candidate = FloorCatalog.ByNumber(number);

            if (factory.CreateFloor(candidate, 3, RunModifiers.None).SafeRoom is null)
            {
                roomless = candidate;
            }
        }

        var floor = Assert.IsType<FloorDefinition>(roomless);
        var roomCard = new Card
        {
            Id = "test-room-card",
            Name = "Test Room Card",
            Tier = CardTier.Common,
            FaceText = "Rooms are certain, but this card exists only for the test.",
            Effect = new CardEffect { RoomChancePercent = 100 },
        };

        var boosted = factory.CreateFloor(floor, 3, RunModifiers.None.With(roomCard));

        Assert.NotNull(boosted.SafeRoom);
    }

    [Fact]
    public void Constructor_WithAnInvalidConfig_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameFactory(new GameConfig { Rows = 0 }, _ => new SeededRandom(Seed), new DijkstraPathfinder()));
    }

    [Fact]
    public void Constructor_WithNullArguments_Throws()
    {
        var random = new SeededRandom(Seed);
        var pathfinder = new DijkstraPathfinder();

        Assert.Throws<ArgumentNullException>(() => new GameFactory(null!, _ => random, pathfinder));
        Assert.Throws<ArgumentNullException>(() => new GameFactory(new GameConfig(), null!, pathfinder));
        Assert.Throws<ArgumentNullException>(() => new GameFactory(new GameConfig(), _ => random, null!));
    }

    [Fact]
    public void Create_WhenTheRandomFactoryReturnsNull_Throws()
    {
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            _ => null!,
            new DijkstraPathfinder());

        Assert.Throws<InvalidOperationException>(() => factory.Create());
    }

    [Fact]
    public void CreateCampaign_StartsOnFloorOneWithFullHearts()
    {
        var campaign = Factory().CreateCampaign();

        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(FloorCatalog.ShippedFloorCount, campaign.FloorCount);
        Assert.Equal("The Shallows", campaign.CurrentFloor.Name);
        Assert.Equal(3, campaign.Hearts);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
        Assert.Equal(3, campaign.Floor.Config.KeyCount);
    }

    [Fact]
    public void CreateFloor_UsesTheFloorsOwnRules()
    {
        var floor = FloorCatalog.ByNumber(3);

        var state = Factory().CreateFloor(floor, startingHearts: 2, RunModifiers.None);

        Assert.Equal(floor.Rows, state.Maze.Rows);
        Assert.Equal(floor.Columns, state.Maze.Columns);
        Assert.Equal(floor.KeyCount, state.Config.KeyCount);
        Assert.Equal(floor.KeyCount, state.KeysRemaining);
        Assert.Equal(floor.EnemyStepSeconds, state.Config.EnemyStepSeconds);
        Assert.Equal(2, state.Hearts);
        Assert.Equal(floor.MaxHearts, state.MaxHearts);
    }

    [Fact]
    public void CreateFloor_WithOneHeartLeft_StartsWithOneHeart()
    {
        var state = Factory().CreateFloor(FloorCatalog.ByNumber(1), startingHearts: 1, RunModifiers.None);

        Assert.Equal(1, state.Hearts);
        Assert.Equal(3, state.MaxHearts);
    }

    [Fact]
    public void CreateFloor_WithMoreHeartsThanTheFloorWasDesignedFor_KeepsThem()
    {
        // A run has no heart ceiling: a healed run carries every heart it earned, so the floor's
        // own design number is a slot count rather than a limit.
        var floor = FloorCatalog.ByNumber(1);

        var state = Factory().CreateFloor(floor, startingHearts: floor.MaxHearts + 2, RunModifiers.None);

        Assert.Equal(floor.MaxHearts + 2, state.Hearts);
        Assert.Equal(floor.MaxHearts + 2, state.MaxHearts);
    }

    [Fact]
    public void CreateFloor_WhenACardRaisesTheCeiling_AcceptsHeartsAboveTheFloorsOwn()
    {
        var floor = FloorCatalog.ByNumber(1);
        var bloodPact = RunModifiers.None.With(CardCatalog.ById("blood-pact")!);

        var state = Factory().CreateFloor(floor, startingHearts: 4, bloodPact);

        Assert.Equal(4, state.Hearts);
        Assert.Equal(4, state.MaxHearts);
    }

    [Fact]
    public void CreateFloor_AppliesTheCardsToEachFloorsOwnNumbers()
    {
        var floor = FloorCatalog.ByNumber(3);
        var bloodPact = RunModifiers.None.With(CardCatalog.ById("blood-pact")!);

        var state = Factory().CreateFloor(floor, startingHearts: 3, bloodPact);

        // Floor 3 is faster than floor 1 on its own; the card's 12% multiplies that floor's own
        // pace instead of compounding on the pace of the floor before it.
        Assert.Equal(floor.EnemyStepSeconds * 0.88, state.Config.EnemyStepSeconds, precision: 9);
        Assert.Equal(floor.MaxHearts + 1, state.MaxHearts);
    }

    [Fact]
    public void CreateFloor_WithTheSameSeed_ProducesTheSameFloor()
    {
        var floor = FloorCatalog.ByNumber(2);
        var first = Factory().CreateFloor(floor, startingHearts: 3, RunModifiers.None);
        var second = Factory().CreateFloor(floor, startingHearts: 3, RunModifiers.None);

        Assert.Equal(first.Player, second.Player);
        Assert.Equal(first.Enemy, second.Enemy);
        Assert.Equal(first.Exit, second.Exit);
        Assert.Equal(MazeSignature(first), MazeSignature(second));
    }

    [Fact]
    public void CreateFloor_DifferentFloorsOfASeededRun_GetDifferentMazes()
    {
        var first = Factory().CreateFloor(FloorCatalog.ByNumber(1), startingHearts: 3, RunModifiers.None);
        var second = Factory().CreateFloor(FloorCatalog.ByNumber(2), startingHearts: 3, RunModifiers.None);

        Assert.NotEqual(MazeSignature(first), MazeSignature(second));
    }

    [Fact]
    public void CreateCampaign_Twice_StartsTwoFreshRuns()
    {
        var factory = Factory();

        var first = factory.CreateCampaign();
        var second = factory.CreateCampaign();

        Assert.Equal(1, first.FloorNumber);
        Assert.Equal(1, second.FloorNumber);
        Assert.Equal(3, first.Hearts);
        Assert.Equal(3, second.Hearts);
        Assert.NotSame(first.Floor, second.Floor);
    }

    /// <summary>Returns the cardinal direction from one neighbouring cell towards another.</summary>
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

    /// <summary>
    /// The factory wired exactly as `Program` wires it: one seeded source per game.
    /// </summary>
    private static GameFactory Factory(int? seed = Seed) =>
        new(
            new GameConfig { Seed = seed },
            // Mirrors Program: the factory supplies the per-floor seed, and a fixed run seed
            // still reproduces the same campaign.
            givenSeed => new SeededRandom(givenSeed ?? seed),
            new DijkstraPathfinder());

    /// <summary>
    /// Encodes every cell's walls so two mazes can be compared as one value.
    /// </summary>
    private static string MazeSignature(GameState state)
    {
        var signature = new System.Text.StringBuilder();

        for (var row = 0; row < state.Maze.Rows; row++)
        {
            for (var column = 0; column < state.Maze.Columns; column++)
            {
                var cell = state.Maze.GetCell(row, column);
                signature
                    .Append(cell.HasWall(Direction.North) ? '1' : '0')
                    .Append(cell.HasWall(Direction.East) ? '1' : '0')
                    .Append(cell.HasWall(Direction.South) ? '1' : '0')
                    .Append(cell.HasWall(Direction.West) ? '1' : '0')
                    .Append(':');
            }

            signature.Append('/');
        }

        return signature.ToString();
    }
}

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
/// The altar and its relics: the catalog's shape, where the altar stands, what touching it does,
/// and the run-level promise that a relic is kept and never held twice.
/// </summary>
public sealed class RelicTests
{
    [Fact]
    public void EveryRelic_IsARareCardWithAFaceAndAnEffect()
    {
        Assert.NotEmpty(RelicCatalog.Cards);
        Assert.Equal(RelicCatalog.Kinds.Count, RelicCatalog.Cards.Count);

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in RelicCatalog.Cards)
        {
            card.Validate();

            Assert.True(ids.Add(card.Id), $"Duplicate relic id: {card.Id}");
            Assert.Equal(CardTier.Rare, card.Tier);
            Assert.False(string.IsNullOrWhiteSpace(card.FaceText));
            Assert.NotEqual(new CardEffect(), card.Effect);
            Assert.Same(card, RelicCatalog.ById(card.Id));
        }

        // Relics are found, not drafted: none may claim the trade language of a curse.
        Assert.Null(RelicCatalog.ById("not-a-relic"));
        Assert.Null(RelicCatalog.ById(string.Empty));
    }

    [Fact]
    public void EveryKind_ResolvesToItsOwnCard()
    {
        foreach (var kind in RelicCatalog.Kinds)
        {
            var card = RelicCatalog.CardFor(kind);

            Assert.Same(card, RelicCatalog.Cards[(int)kind]);
        }
    }

    [Fact]
    public void EveryKind_HasAFamily_AndEveryFamilyIsUsed()
    {
        var families = new HashSet<RelicFamily>();

        foreach (var kind in RelicCatalog.Kinds)
        {
            families.Add(RelicCatalog.FamilyOf(kind));
        }

        Assert.Equal(Enum.GetValues<RelicFamily>().Length, families.Count);
    }

    [Fact]
    public void Pick_WalksTheWholePoolBeforeRepeating()
    {
        var seen = new HashSet<RelicKind>();

        for (var skip = 0; skip < RelicCatalog.Cards.Count; skip++)
        {
            Assert.True(seen.Add(RelicCatalog.Pick(runSeed: 5150, floorNumber: 9, skip)));
        }

        Assert.Equal(RelicCatalog.Cards.Count, seen.Count);
    }

    [Fact]
    public void AFreshFloor_HasNoAltar()
    {
        Assert.Null(TestLevels.TwoRouteGame().RelicCell);
        Assert.Null(TestLevels.TwoRouteGame().Relic);
    }

    [Fact]
    public void TheShippedCampaign_StocksARelicOnItsFirstFloor()
    {
        var campaign = new GameFactory(
            new GameConfig { Seed = 2024 },
            seed => new SeededRandom(seed),
            new DijkstraPathfinder()).CreateCampaign();

        Assert.NotNull(campaign.Floor.Relic);

        var relic = RelicCatalog.CardFor(campaign.Floor.Relic!.Value);

        Assert.Contains(RelicCatalog.Cards, card => card.Id == relic.Id);
        Assert.Equal(campaign.Floor.RelicCell is not null, campaign.Floor.Relic is not null);
    }

    [Fact]
    public void TheAltar_StandsOffTheShortestRoute()
    {
        var state = Build(RelicKind.EmberHeart);

        Assert.NotNull(state.RelicCell);

        var cell = state.RelicCell!.Value;

        Assert.Equal(RelicKind.EmberHeart, state.Relic);
        Assert.NotEqual(state.Player, cell);
        Assert.NotEqual(state.Exit, cell);
        Assert.NotEqual(state.CuriosityCell, cell);
        Assert.DoesNotContain(cell, state.Keys);
    }

    [Fact]
    public void TouchingTheAltar_TakesItOnce_AndWakesTheSleeper()
    {
        var state = Build(RelicKind.EmberHeart, rule: FloorRule.Sleeper);
        var cell = state.RelicCell!.Value;

        Assert.Contains(state.Enemies, hunter => hunter.Dormant);

        Assert.True(WalkTo(state, cell));

        Assert.True(state.IsRelicTaken);

        var events = Drain(state);
        Assert.Contains(GameEvent.RelicFound, events);

        Assert.DoesNotContain(state.Enemies, hunter => hunter.Dormant);

        // Standing on the emptied plinth again must not raise the find twice.
        state.Tick(0.0);

        Assert.DoesNotContain(GameEvent.RelicFound, Drain(state));
    }

    [Fact]
    public void TheRelicsEffect_IsNotAppliedToTheFloorThatHoldsIt()
    {
        var state = Build(RelicKind.LanternShard);
        var before = state.EffectiveVisionRadius;

        Assert.True(WalkTo(state, state.RelicCell!.Value));

        // The floor reports the find; the campaign is what makes it last, so the torch on this
        // floor is unchanged.
        Assert.Equal(before, state.EffectiveVisionRadius);
    }

    [Fact]
    public void TheCampaign_BanksTheRelic_Once()
    {
        var campaign = CampaignWithRelic(RelicKind.LanternShard);

        var relic = RelicCatalog.CardFor(RelicKind.LanternShard);

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.RelicCell!.Value));
        Assert.DoesNotContain(campaign.Modifiers.Cards, card => card.Id == relic.Id);

        campaign.Sync();
        campaign.Sync();

        Assert.Single(campaign.Modifiers.Cards, card => card.Id == relic.Id);
        Assert.Contains(relic.Id, campaign.Snapshot().CardIds);
    }

    [Fact]
    public void ARelic_SurvivesSavingAndRestoring()
    {
        var campaign = CampaignWithRelic(RelicKind.LanternShard);

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.RelicCell!.Value));
        campaign.Sync();

        var restored = Campaign.Restore(campaign.Floors, BuildHonoringRelic, campaign.Snapshot());

        Assert.Single(restored.Modifiers.Cards, card => card.Id == RelicCatalog.CardFor(RelicKind.LanternShard).Id);
    }

    [Fact]
    public void ARelic_CarriesItsEffectToTheNextFloor()
    {
        var campaign = CampaignWithRelic(RelicKind.LanternShard);
        var relic = RelicCatalog.CardFor(RelicKind.LanternShard);

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.RelicCell!.Value));
        campaign.Sync();

        var before = campaign.Floor.EffectiveVisionRadius;

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.Exit));
        Assert.True(campaign.IsFloorCleared);
        Assert.True(campaign.SkipDraft());
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(before + 1, campaign.Floor.EffectiveVisionRadius);
    }

    [Fact]
    public void TheCampaign_NeverStocksARelicTheRunAlreadyHolds()
    {
        var campaign = CampaignWithRelic(RelicKind.LanternShard);

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.RelicCell!.Value));
        campaign.Sync();

        Assert.True(WalkTo(campaign.Floor, campaign.Floor.Exit));
        Assert.True(campaign.IsFloorCleared);
        Assert.True(campaign.SkipDraft());
        Assert.True(campaign.TryAdvanceFloor());

        Assert.NotNull(campaign.Floor.RelicCell);
        Assert.NotEqual(RelicKind.LanternShard, campaign.Floor.Relic);
    }

    /// <summary>
    /// Builds a two-floor campaign of open nine-by-nine rooms with the named relic authored on its
    /// first floor, so the test is about the altar rather than about what the seed stocks.
    /// </summary>
    private static Campaign CampaignWithRelic(RelicKind kind)
    {
        var floors = new List<FloorDefinition>
        {
            Floor(1) with { Relic = kind },
            Floor(2),
        };

        return new Campaign(floors, BuildHonoringRelic, runSeed: 808);
    }

    /// <summary>
    /// Builds one of the campaign's rooms with the run's cards and the relic the campaign stocked.
    /// </summary>
    private static GameState BuildHonoringRelic(
        FloorDefinition floor,
        int hearts,
        RunModifiers modifiers,
        IReadOnlyList<DescentCondition> conditions)
    {
        var config = modifiers.Apply(new GameConfig
        {
            Rows = 9,
            Columns = 9,
            KeyCount = 0,
            EnemyStepSeconds = 60.0,
            StartingHearts = hearts,
            MaxHearts = floor.MaxHearts,
            Seed = floor.Number,
            FloorNumber = floor.Number,
            Relic = floor.Relic,
        });

        return TestLevels.GameOver(RoomWithAnExitLeaf(9), config);
    }

    private static FloorDefinition Floor(int number) => new()
    {
        Number = number,
        Name = $"Floor {number}",
        ThemeId = "test",
        Rows = 9,
        Columns = 9,
        KeyCount = 0,
        EnemyStepSeconds = 60.0,
        MaxHearts = 3,
    };

    /// <summary>
    /// Builds a nine-by-nine open room holding an altar, with the arch on a leaf so the player
    /// cannot win the floor by walking past it.
    /// </summary>
    private static GameState Build(RelicKind kind, FloorRule? rule = null) =>
        TestLevels.GameOver(
            RoomWithAnExitLeaf(9),
            new GameConfig
            {
                Rows = 9,
                Columns = 9,
                KeyCount = 0,
                EnemyStepSeconds = 60.0,
                StartingHearts = 3,
                MaxHearts = 3,
                Seed = 4,
                FloorNumber = 4,
                Rule = rule,
                Relic = kind,
            });

    /// <summary>A square room with every interior wall removed, and the arch on a leaf.</summary>
    private static Grid RoomWithAnExitLeaf(int size)
    {
        var grid = new Grid(size, size);

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size - 1; column++)
            {
                if (row == 0 && column is 0 or 1)
                {
                    continue;
                }

                grid.RemoveWall(row, column, Direction.East);
            }
        }

        for (var column = 0; column < size; column++)
        {
            for (var row = 0; row < size - 1; row++)
            {
                grid.RemoveWall(row, column, Direction.South);
            }
        }

        return grid;
    }

    /// <summary>Walks the player to <paramref name="target"/> along the shortest route.</summary>
    private static bool WalkTo(GameState state, Cell target)
    {
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, target, _ => 1);

        for (var index = 1; index < path.Count; index++)
        {
            if (!state.TryMove(DirectionToward(state.Player, path[index])))
            {
                return false;
            }
        }

        return state.Player.Row == target.Row && state.Player.Column == target.Column;
    }

    /// <summary>Drains the floor's events into a list, so several can be asserted at once.</summary>
    private static List<GameEvent> Drain(GameState state) => new(state.DrainEvents());

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

        return to.Column < from.Column ? Direction.West : Direction.East;
    }
}

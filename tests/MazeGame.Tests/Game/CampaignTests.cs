using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the run object: floor order, the fact that a cleared floor is progress rather than
/// victory, hearts carried between floors, and the two ways a run can end.
/// </summary>
/// <remarks>
/// The builder here makes a 1x6 corridor per floor — player (0,0), exit (0,1), killer (0,5) — so a
/// floor can be cleared by one step east and the player can be wounded deliberately by ticking.
/// That keeps these tests about the campaign, not about maze generation. A cleared floor opens a
/// draft, so every advance here resolves it first; the draft's own rules live in
/// <see cref="CampaignDraftTests"/>.
/// </remarks>
public sealed class CampaignTests
{
    /// <summary>The fixed run seed these tests draft from.</summary>
    private const int RunSeed = 7;

    [Fact]
    public void NewCampaign_StartsOnTheFirstFloorWithFullHearts()
    {
        var campaign = CampaignOf(3);

        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(3, campaign.FloorCount);
        Assert.Equal(3, campaign.Hearts);
        Assert.Equal(3, campaign.MaxHearts);
        Assert.Equal(0, campaign.FloorsCleared);
        Assert.False(campaign.IsFloorCleared);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
        Assert.Empty(campaign.Modifiers.Cards);
        Assert.Null(campaign.Draft);
    }

    [Fact]
    public void ClearingAFloor_IsProgressNotVictory()
    {
        var campaign = CampaignOf(3);

        ClearFloor(campaign);

        Assert.True(campaign.IsFloorCleared);
        Assert.Equal(1, campaign.FloorsCleared);
        Assert.Equal(3, campaign.MaxHearts);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void TryAdvanceFloor_CarriesTheHeartsLeft()
    {
        var campaign = CampaignOf(3);

        WoundTo(campaign, hearts: 2);

        ClearFloor(campaign);

        // Taking a card carries no hearts, so the damage survives the transition.
        ResolveDraftWithACard(campaign);

        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(2, campaign.FloorNumber);
        Assert.Equal(2, campaign.Hearts);
        Assert.Equal(3, campaign.MaxHearts);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void ASkipHeal_IsNotClippedByTheFloorsOwnCeiling()
    {
        // A run has no heart ceiling: a floor designed around one heart must not throw away a
        // heal the player earned, and the next floor starts with every heart carried in.
        var floors = new List<FloorDefinition>
        {
            Floor(1),
            Floor(2) with { MaxHearts = 1 },
            Floor(3),
        };

        var campaign = new Campaign(floors, BuildFloor, RunSeed);

        ClearFloor(campaign);
        ResolveDraftWithACard(campaign);
        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(2, campaign.FloorNumber);
        Assert.Equal(3, campaign.Hearts);

        ClearFloor(campaign);
        Assert.True(campaign.SkipDraft());

        // Skipping banks a heart for the next floor; the narrow design must not clip it.
        Assert.Equal(4, campaign.HeartsOnNextFloor);
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(4, campaign.Hearts);
    }

    [Fact]
    public void TryAdvanceFloor_KeepsTheCardsTheRunAlreadyHolds()
    {
        var campaign = CampaignOf(3);

        ClearFloor(campaign);
        var picked = ResolveDraftWithACard(campaign);
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Single(campaign.Modifiers.Cards);
        Assert.Equal(Assert.IsType<Card>(picked).Id, campaign.Modifiers.Cards[0].Id);
        Assert.Equal(2, campaign.FloorNumber);
    }

    [Fact]
    public void TryAdvanceFloor_BeforeClearingTheFloor_ReturnsFalse()
    {
        var campaign = CampaignOf(3);

        Assert.False(campaign.TryAdvanceFloor());
        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void ClearingTheLastFloor_WinsTheRun()
    {
        var campaign = CampaignOf(3);

        ClearFloor(campaign);
        ResolveDraftWithACard(campaign);
        Assert.True(campaign.TryAdvanceFloor());

        ClearFloor(campaign);
        ResolveDraftWithACard(campaign);
        Assert.True(campaign.TryAdvanceFloor());

        Assert.True(campaign.IsOnLastFloor);
        ClearFloor(campaign);

        Assert.Equal(3, campaign.FloorsCleared);
        Assert.Equal(CampaignStatus.Won, campaign.Status);
        Assert.False(campaign.TryAdvanceFloor());
        Assert.Null(campaign.Draft);
    }

    [Fact]
    public void LosingTheLastHeart_EndsTheRunImmediately()
    {
        var fragileFloor = Floor(1) with { MaxHearts = 1 };
        var campaign = new Campaign([fragileFloor], BuildFloor, RunSeed);

        for (var tick = 0; tick < 30 && campaign.Floor.Status == GameStatus.InProgress; tick++)
        {
            campaign.Floor.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, campaign.Floor.Status);
        Assert.Equal(0, campaign.Hearts);
        Assert.Equal(CampaignStatus.Lost, campaign.Status);
        Assert.False(campaign.TryAdvanceFloor());
    }

    [Fact]
    public void Campaign_SnapshotsItsFloorList()
    {
        var floors = new List<FloorDefinition>
        {
            Floor(1),
            Floor(2),
            Floor(3),
        };
        var campaign = new Campaign(floors, BuildFloor, RunSeed);

        floors.Clear();

        Assert.Equal(3, campaign.FloorCount);
        Assert.Equal(1, campaign.FloorNumber);
    }

    [Fact]
    public void Campaign_WithNoFloors_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Campaign(Array.Empty<FloorDefinition>(), BuildFloor, RunSeed));
    }

    [Fact]
    public void Campaign_WithFloorNumbersOutOfOrder_Throws()
    {
        var floors = new[] { Floor(1), Floor(3), Floor(2) };

        Assert.Throws<ArgumentException>(() => new Campaign(floors, BuildFloor, RunSeed));
    }

    [Fact]
    public void Campaign_WithAnInvalidFloor_Throws()
    {
        var invalid = Floor(1) with { Name = "  " };

        Assert.Throws<ArgumentException>(() => new Campaign([invalid], BuildFloor, RunSeed));
    }

    [Fact]
    public void Campaign_WhenTheBuilderReturnsNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new Campaign([Floor(1)], (_, _, _, _) => null!, RunSeed));
    }

    [Fact]
    public void Campaign_WithNullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Campaign(null!, BuildFloor, RunSeed));
        Assert.Throws<ArgumentNullException>(() => new Campaign([Floor(1)], null!, RunSeed));
    }

    /// <summary>A campaign of <paramref name="floorCount"/> corridor floors, each allowing 3 hearts.</summary>
    private static Campaign CampaignOf(int floorCount)
    {
        var floors = new List<FloorDefinition>();
        for (var number = 1; number <= floorCount; number++)
        {
            floors.Add(Floor(number));
        }

        return new Campaign(floors, BuildFloor, RunSeed);
    }

    /// <summary>
    /// A three-floor campaign whose first draft is guaranteed to offer <paramref name="cardId"/>,
    /// with that first floor already cleared so the draft is open.
    /// </summary>
    /// <remarks>
    /// Offers are seeded, so a card a test needs is found by trying seeds rather than by reaching
    /// into the draft. The floors are 1x6 corridors, so the search is cheap.
    /// </remarks>
    private static Campaign CampaignDrafting(string cardId)
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var campaign = new Campaign([Floor(1), Floor(2), Floor(3)], BuildFloor, seed);
            ClearFloor(campaign);

            if (campaign.Draft!.Offers.Any(offer => offer.Id == cardId))
            {
                return campaign;
            }
        }

        throw new InvalidOperationException($"No seed in range offered {cardId}.");
    }

    private static FloorDefinition Floor(int number) => new()
    {
        Number = number,
        Name = $"Floor {number}",
        ThemeId = "test",
        Rows = 1,
        Columns = 6,
        KeyCount = 0,
        EnemyStepSeconds = 1.0,
        MaxHearts = 3,
    };

    /// <summary>
    /// Builds the 1x3 floor described above with the requested hearts and the run's cards applied.
    /// The exit is open with no keys, so stepping east clears it.
    /// </summary>
    private static GameState BuildFloor(
        FloorDefinition floor,
        int hearts,
        RunModifiers modifiers,
        IReadOnlyList<DescentCondition> conditions) =>
        BuildFloor(floor, hearts, modifiers);

    private static GameState BuildFloor(FloorDefinition floor, int hearts, RunModifiers modifiers) =>
        new(
            modifiers.Apply(new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = 0,
                StartingHearts = hearts,
                MaxHearts = floor.MaxHearts,
                EnemyStepSeconds = 1.0,
                Seed = null,
            }),
            TestLevels.Corridor(6),
            new DijkstraPathfinder(),
            new FixedRandom(0));

    /// <summary>
    /// Walks the corridor to every key and then to the exit. A card can add keys, so the one-step
    /// clear the old test used is no longer enough once the run has taken one.
    /// </summary>
    private static void ClearFloor(Campaign campaign)
    {
        var floor = campaign.Floor;

        for (var guard = 0; guard < 64 && floor.Status == GameStatus.InProgress; guard++)
        {
            var target = NearestKey(floor) ?? floor.Exit;
            var direction = target.Column > floor.Player.Column ? Direction.East : Direction.West;

            Assert.True(floor.TryMove(direction));
            floor.Tick(0.12);
        }

        Assert.Equal(GameStatus.Won, floor.Status);
    }

    /// <summary>Returns the key closest to the player along the one-row corridor.</summary>
    private static Cell? NearestKey(GameState floor)
    {
        Cell? best = null;
        var bestDistance = int.MaxValue;

        foreach (var key in floor.Keys)
        {
            var distance = Math.Abs(key.Column - floor.Player.Column);

            if (distance < bestDistance)
            {
                best = key;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Lets the killer catch the player until <paramref name="hearts"/> remain, failing the test
    /// if the run ends first.
    /// </summary>
    private static void WoundTo(Campaign campaign, int hearts)
    {
        for (var tick = 0; tick < 30 && campaign.Hearts > hearts; tick++)
        {
            campaign.Floor.Tick(1.0);
        }

        Assert.Equal(hearts, campaign.Hearts);
        Assert.Equal(GameStatus.InProgress, campaign.Floor.Status);
    }

    [Fact]
    public void TheShippedRun_CanBeWalkedFromTheFirstFloorToTheLast()
    {
        // The shipped catalog is twenty floors long; this walks that many corridor floors through
        // the real campaign object, resolving a draft between every pair, and proves the run ends
        // won rather than looping, stalling or running off the end.
        var count = FloorCatalog.Shipped.Count;
        var campaign = CampaignOf(count);

        for (var floor = 1; floor <= count; floor++)
        {
            Assert.Equal(floor, campaign.FloorNumber);

            ClearFloor(campaign);

            if (campaign.Status == CampaignStatus.Won)
            {
                break;
            }

            Assert.NotNull(campaign.Draft);
            ResolveDraftWithACard(campaign);

            // Floors 8 and 14 are gates: the run will not descend until a rule is chosen.
            if (campaign.ConditionOffer is { IsResolved: false } gate)
            {
                Assert.True(campaign.TakeCondition(gate.Options[0]));
            }

            Assert.True(campaign.TryAdvanceFloor(), $"the run refused to descend from floor {floor}.");
        }

        Assert.Equal(CampaignStatus.Won, campaign.Status);
        Assert.Equal(count, campaign.FloorsCleared);
    }

    /// <summary>
    /// Resolves the open draft with a card that leaves the heart count and the key count alone, or
    /// skips when the seeded draft has no such offer.
    /// </summary>
    /// <remarks>
    /// Choosing a card that neither heals nor moves the ceiling keeps a test's attention on the
    /// hearts it wounded, rather than on whether the draft happened to offer <c>second-wind</c> or
    /// <c>blood-pact</c>.
    /// </remarks>
    /// <returns>The card taken, or null when the draft had to be skipped.</returns>
    private static Card? ResolveDraftWithACard(Campaign campaign)
    {
        var draft = Assert.IsType<Draft>(campaign.Draft);

        // Key changes are avoided too: these tests walk a deliberately tiny 1x6 corridor, and
        // three key cards would not fit in it. The shipped floors are large enough for them.
        var card = draft.Offers.FirstOrDefault(
            offer => offer.Effect.HealNow == 0 &&
                     offer.Effect.HealOnFloorClear == 0 &&
                     offer.Effect.KeyCount == 0);

        if (card is null)
        {
            Assert.True(campaign.SkipDraft());
            return null;
        }

        Assert.True(campaign.TakeCard(card));
        return card;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the beat between two floors: clearing a floor opens a draft, the run refuses to
/// advance until it is resolved, taking a card folds into every later floor, and skipping heals.
/// </summary>
/// <remarks>
/// The floors are 1x6 corridors: the exit is at (0,1) and the floor needs no keys, so one step
/// east clears it, while the killer starts far enough away that ticking can wound the player on
/// purpose. A floor allows 4 hearts in most of these, which makes a one-heart heal visible
/// instead of being swallowed by the ceiling.
/// </remarks>
public sealed class CampaignDraftTests
{
    private const int Seed = 7;

    [Fact]
    public void ClearingAFloor_OpensADraftOfThreeDistinctOffers()
    {
        var campaign = CampaignOf();

        ClearFloor(campaign);
        var draft = campaign.Draft;

        Assert.NotNull(draft);
        Assert.Equal(DraftFactory.DefaultOfferCount, draft!.Offers.Count);
        Assert.Equal(draft.Offers.Count, draft.Offers.Select(card => card.Id).Distinct().Count());
        Assert.False(draft.IsResolved);
        Assert.Null(draft.Taken);
        Assert.False(draft.WasSkipped);
    }

    [Fact]
    public void BeforeClearingTheFloor_ThereIsNoDraft()
    {
        var campaign = CampaignOf();

        Assert.Null(campaign.Draft);
    }

    [Fact]
    public void TheDraft_IsTheSameObjectEveryRead()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);

        var first = campaign.Draft;
        var second = campaign.Draft;

        Assert.Same(first, second);
    }

    [Fact]
    public void TwoCampaignsWithTheSameSeed_OfferTheSameCards()
    {
        var first = CampaignOf();
        var second = CampaignOf();
        ClearFloor(first);
        ClearFloor(second);

        Assert.Equal(
            first.Draft!.Offers.Select(card => card.Id),
            second.Draft!.Offers.Select(card => card.Id));
    }

    [Fact]
    public void TryAdvanceFloor_WithAnOpenDraft_IsRefused()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);

        // The choice is part of the run, not a screen the frontend may skip.
        Assert.False(campaign.TryAdvanceFloor());
        Assert.Equal(1, campaign.FloorNumber);
        Assert.False(campaign.Draft!.IsResolved);
    }

    [Fact]
    public void TakeCard_ThatIsNotOnTheTable_IsRefused()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);
        var offered = campaign.Draft!.Offers.Select(card => card.Id).ToHashSet();
        var notOffered = CardCatalog.All.First(card => !offered.Contains(card.Id));

        Assert.False(campaign.TakeCard(notOffered));
        Assert.False(campaign.Draft!.IsResolved);
        Assert.Empty(campaign.Modifiers.Cards);
    }

    [Fact]
    public void TakeCard_ResolvesTheDraftAndFoldsIntoTheRun()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);
        var card = campaign.Draft!.Offers[0];

        Assert.True(campaign.TakeCard(card));

        Assert.True(campaign.Draft!.IsResolved);
        Assert.Equal(card.Id, campaign.Draft.Taken!.Id);
        Assert.Equal(card.Id, campaign.Modifiers.Cards.Single().Id);
        Assert.True(campaign.TryAdvanceFloor());
    }

    [Fact]
    public void TakeCard_Twice_IsRefused()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);
        var draft = campaign.Draft!;

        Assert.True(campaign.TakeCard(draft.Offers[0]));

        if (draft.Offers.Count > 1)
        {
            Assert.False(campaign.TakeCard(draft.Offers[1]));
        }

        Assert.Single(campaign.Modifiers.Cards);
    }

    [Fact]
    public void TakeCard_WithNoOpenDraft_IsRefused()
    {
        var campaign = CampaignOf();

        Assert.False(campaign.TakeCard(CardCatalog.All[0]));
    }

    [Fact]
    public void SkipDraft_BanksOneHeartForTheNextFloor()
    {
        var campaign = CampaignOf();
        WoundTo(campaign, hearts: 2);
        ClearFloor(campaign);

        Assert.True(campaign.SkipDraft());
        Assert.Equal(1, campaign.PendingHeals);
        Assert.True(campaign.Draft!.WasSkipped);
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(2 + 1, campaign.Hearts);
    }

    [Fact]
    public void SkipDraft_IsCappedByTheNextFloorsCeiling()
    {
        var campaign = CampaignOf(maxHearts: 3);
        ClearFloor(campaign);

        Assert.True(campaign.SkipDraft());
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(3, campaign.Hearts);
        Assert.Equal(3, campaign.MaxHearts);
    }

    [Fact]
    public void SkipDraft_Twice_IsRefused()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);

        Assert.True(campaign.SkipDraft());
        Assert.False(campaign.SkipDraft());
        Assert.Equal(1, campaign.PendingHeals);
    }

    [Fact]
    public void SkipDraft_WithNoOpenDraft_IsRefused()
    {
        var campaign = CampaignOf();

        Assert.False(campaign.SkipDraft());
        Assert.Equal(0, campaign.PendingHeals);
    }

    [Fact]
    public void SecondWind_BanksItsHealForTheNextFloor()
    {
        var seed = FindSeedOffering("second-wind");
        var campaign = CampaignOf(seed: seed);
        WoundTo(campaign, hearts: 2);
        ClearFloor(campaign);

        var card = campaign.Draft!.Offers.Single(offer => offer.Id == "second-wind");
        Assert.True(campaign.TakeCard(card));
        Assert.Equal(1, campaign.PendingHeals);

        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(3, campaign.Hearts);
    }

    [Fact]
    public void TrophyHunter_HealsOnEveryLaterFloorClear()
    {
        var seed = FindSeedOffering("trophy-hunter");
        var campaign = CampaignOf(seed: seed);

        ClearFloor(campaign);
        var card = campaign.Draft!.Offers.Single(offer => offer.Id == "trophy-hunter");
        Assert.True(campaign.TakeCard(card));

        // The heal is an effect of holding the card, so the clear that bought it does not heal;
        // the next one does.
        Assert.Equal(0, campaign.PendingHeals);
        Assert.True(campaign.TryAdvanceFloor());

        // The drawback lands at once: the next floor now needs a key, so the arch the player is
        // standing next to is locked and has to be earned.
        Assert.Equal(1, campaign.Floor.KeysRemaining);

        WoundTo(campaign, hearts: 3);

        // Walk the new key and come back to the arch. No ticks, so the wound sticks.
        Assert.True(campaign.Floor.TryMove(Direction.East));
        Assert.True(campaign.Floor.TryMove(Direction.East));
        Assert.True(campaign.Floor.TryMove(Direction.West));
        Assert.True(campaign.IsFloorCleared);

        // Holding the card is worth a heart on every clear: 3 wounded hearts become 4.
        Assert.Equal(4, campaign.HeartsOnNextFloor);
    }

    [Fact]
    public void LuckyFind_GrowsTheNextDraftAndShrinksTheOneAfterIt()
    {
        var seed = FindSeedOffering("lucky-find", floorCount: 4);
        var campaign = CampaignOf(floorCount: 4, seed: seed);

        ClearFloor(campaign);
        var luckyFind = campaign.Draft!.Offers.Single(offer => offer.Id == "lucky-find");
        Assert.True(campaign.TakeCard(luckyFind));
        Assert.True(campaign.TryAdvanceFloor());

        // Four offers next, then two: the extra choice is paid for one floor later.
        ClearFloor(campaign);
        Assert.Equal(4, campaign.Draft!.Offers.Count);
        Assert.True(campaign.SkipDraft());
        Assert.True(campaign.TryAdvanceFloor());

        ClearFloor(campaign);
        Assert.Equal(2, campaign.Draft!.Offers.Count);
    }

    [Fact]
    public void TheLastFloor_ClearsWithoutADraft()
    {
        var campaign = CampaignOf(floorCount: 1);

        ClearFloor(campaign);

        Assert.True(campaign.IsFloorCleared);
        Assert.True(campaign.IsOnLastFloor);
        Assert.Null(campaign.Draft);
        Assert.Equal(CampaignStatus.Won, campaign.Status);
    }

    [Fact]
    public void ALostRun_OpensNoDraft()
    {
        var campaign = CampaignOf(floorCount: 3, maxHearts: 1);

        for (var tick = 0; tick < 30 && campaign.Floor.Status == GameStatus.InProgress; tick++)
        {
            campaign.Floor.Tick(1.0);
        }

        Assert.Equal(GameStatus.Caught, campaign.Floor.Status);
        Assert.Null(campaign.Draft);
        Assert.Equal(CampaignStatus.Lost, campaign.Status);
    }

    [Fact]
    public void AWholeRun_CanBeClearedWhileHoldingCards()
    {
        var campaign = CampaignOf();

        for (var floor = 1; floor < campaign.FloorCount; floor++)
        {
            ClearFloor(campaign);

            // Any card that does not add a key: a key would leave the next floor's exit locked
            // and this walk could never finish it. The run is still "holding cards" throughout.
            Assert.True(campaign.TakeCard(campaign.Draft!.Offers.First(offer => offer.Effect.KeyCount == 0)));
            Assert.True(campaign.TryAdvanceFloor());
        }

        ClearFloor(campaign);

        Assert.Equal(CampaignStatus.Won, campaign.Status);
        Assert.Equal(campaign.FloorCount - 1, campaign.Modifiers.Cards.Count);
    }

    /// <summary>
    /// A campaign of 1x3 corridor floors, each allowing <paramref name="maxHearts"/> hearts.
    /// </summary>
    private static Campaign CampaignOf(int floorCount = 3, int seed = Seed, int maxHearts = 4)
    {
        var floors = new List<FloorDefinition>();
        for (var number = 1; number <= floorCount; number++)
        {
            floors.Add(new FloorDefinition
            {
                Number = number,
                Name = $"Floor {number}",
                ThemeId = "test",
                Rows = 1,
                Columns = 6,
                KeyCount = 0,
                EnemyStepSeconds = 1.0,
                MaxHearts = maxHearts,
            });
        }

        return new Campaign(floors, BuildFloor, seed);
    }

    /// <summary>Builds the corridor floor with the run's cards applied, as the factory would.</summary>
    private static GameState BuildFloor(FloorDefinition floor, int hearts, RunModifiers modifiers) =>
        new(
            modifiers.Apply(new GameConfig
            {
                // Six cells, not three: a card may add a key, and a floor too small for its keys
                // is an invalid floor rather than a caught bug.
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

    private static void ClearFloor(Campaign campaign) =>
        Assert.True(campaign.Floor.TryMove(Direction.East));

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

    /// <summary>
    /// Finds a run seed whose first draft offers <paramref name="cardId"/>. Deterministic, so the
    /// test keeps its meaning without pinning a magic number in a comment.
    /// </summary>
    private static int FindSeedOffering(string cardId, int floorCount = 3)
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var campaign = CampaignOf(floorCount, seed);
            ClearFloor(campaign);

            if (campaign.Draft!.Offers.Any(card => card.Id == cardId))
            {
                return seed;
            }
        }

        throw new InvalidOperationException($"No seed in range offers {cardId}.");
    }
}

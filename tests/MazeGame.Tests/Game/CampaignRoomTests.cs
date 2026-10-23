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
/// Verifies the safe room as an economy: every room's price is visible, no purchase may take the
/// last heart, a room is spent once used, and the promises about later floors are kept.
/// </summary>
/// <remarks>
/// The floors are hand-built around a real carved room, so the tests walk into the room the same
/// way a player does. The room's <i>kind</i> is drawn from the run seed, so each test finds a seed
/// that rolls the kind it needs rather than bypassing the draw.
/// </remarks>
public sealed class CampaignRoomTests
{
    private static readonly SecretRoomSite RoomSite = new(1, 1, Reserved: true);

    private static readonly int[] FloorNumbers = [1, 2, 3, 4, 5, 6, 7, 9];

    [Fact]
    public void OpeningARoom_NeedsThePlayerInside()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Cartographer, 0));

        Assert.False(campaign.CanOpenRoom);
        Assert.False(campaign.OpenRoom());
        Assert.Null(campaign.Room);

        EnterRoom(campaign);

        Assert.True(campaign.CanOpenRoom);
        Assert.True(campaign.OpenRoom());
        Assert.Equal(RoomKind.Cartographer, campaign.Room!.Kind);
        Assert.Equal("The Cartographer", campaign.Room.Name);
        Assert.NotEmpty(campaign.Room.Options);
    }

    [Fact]
    public void ClosingARoom_LeavesItUnspent()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Cartographer, 0));

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        campaign.CloseRoom();

        Assert.Null(campaign.Room);
        Assert.True(campaign.CanOpenRoom);
        Assert.True(campaign.OpenRoom());
        Assert.False(campaign.Room!.IsResolved);
    }

    [Fact]
    public void LeavingTheRoom_ClearsTheCardAndLeavesItUnspent()
    {
        // The frontend draws the card from Campaign.Room every frame. Leaving clears that card,
        // so this null is the contract the room screen has to transition on: holding the screen
        // for one more frame is null-referenced in the renderer (it crashed the window once).
        var campaign = BuildCampaign(SeedFor(RoomKind.Stall, 0), hearts: 3);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        Assert.True(campaign.ResolveRoom(RoomOption.Leave));

        Assert.Null(campaign.Room);
        Assert.True(campaign.CanOpenRoom);
        Assert.True(campaign.OpenRoom());
        Assert.False(campaign.Room!.IsResolved);
    }

    [Fact]
    public void TheCartographer_RevealsTheMapAndSpendsTheRoom()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Cartographer, 0));

        EnterRoom(campaign);
        campaign.OpenRoom();

        Assert.False(campaign.Floor.IsMapRevealed);
        Assert.True(campaign.ResolveRoom(Option(campaign, "reveal")));
        Assert.True(campaign.Floor.IsMapRevealed);
        Assert.Equal("THE MAP IS YOURS", campaign.Room!.Outcome);

        // Both machines and the floor know the counter is bare now.
        campaign.CloseRoom();
        Assert.False(campaign.CanOpenRoom);
        Assert.False(campaign.OpenRoom());
    }

    [Fact]
    public void TheCartographer_KeepsTheMapLitForTheRestOfTheFloor()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Cartographer, 0));

        EnterRoom(campaign);
        campaign.OpenRoom();
        Assert.True(campaign.ResolveRoom(Option(campaign, "reveal")));

        // Step back out of the room, where the world clock runs, and let two minutes pass. The
        // card promises the whole floor; thirty seconds used to be what it actually gave.
        Assert.True(WalkTo(campaign.Floor, campaign.Floor.SafeRoom!.Door));
        campaign.Floor.Tick(120.0);

        Assert.True(campaign.Floor.IsMapRevealed);
    }

    [Fact]
    public void TheCasino_NeverOffersAStakeThatCouldKill()
    {
        var oneHeart = BuildCampaign(SeedFor(RoomKind.LongTable, 0), hearts: 1, maxHearts: 1);
        EnterRoom(oneHeart);
        oneHeart.OpenRoom();

        // The only thing on the table is leaving: the last heart is not a stake.
        Assert.Equal(["leave"], OptionIds(oneHeart));

        var threeHearts = BuildCampaign(SeedFor(RoomKind.LongTable, 0), hearts: 3);
        EnterRoom(threeHearts);
        threeHearts.OpenRoom();

        Assert.Equal(["stake-1", "stake-2", "leave"], OptionIds(threeHearts));
    }

    [Fact]
    public void TheCasino_CapsTheStakeAtTwiceTheFloorNumber()
    {
        // The table's limit deepens with the descent: a rich run on floor one may still only
        // stake two hearts, because the house has not seen enough of it yet.
        var campaign = BuildCampaign(SeedFor(RoomKind.LongTable, 0), hearts: 20, maxHearts: 20);

        EnterRoom(campaign);
        campaign.OpenRoom();

        var stakes = campaign.Room!.Options
            .Where(option => option.Id.StartsWith("stake-", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(RoomCatalog.MaxStakeFor(campaign.FloorNumber), stakes.Count);
        Assert.Equal("stake-1", stakes[0].Id);
        Assert.Equal($"stake-{RoomCatalog.MaxStakeFor(campaign.FloorNumber)}", stakes[^1].Id);

        // The example the rule is written from: floor nineteen takes a stake of thirty-eight.
        Assert.Equal(38, RoomCatalog.MaxStakeFor(19));
    }

    [Fact]
    public void TheCasino_DoublesOrTakesTheStake()
    {
        var campaign = BuildCampaign(FindSeed(7, RoomKind.LongTable, 0), hearts: 3);

        EnterRoom(campaign);
        campaign.OpenRoom();

        var heartsBefore = campaign.Hearts;
        var wins = RoomCatalog.GambleWins(campaign.RunSeed, campaign.FloorNumber);

        Assert.True(campaign.ResolveRoom(Option(campaign, "stake-1")));

        Assert.Equal(wins ? heartsBefore + 1 : heartsBefore - 1, campaign.Hearts);
        Assert.Equal(wins ? "WON 2 HEARTS" : "LOST 1", campaign.Room!.Outcome);
    }

    [Fact]
    public void TheCasino_TakesTheStakeOnALoss()
    {
        // The test above follows whichever outcome its seed rolls, so it can spend a whole pass on
        // the winning side. This one chooses a seed that loses on purpose: the deduction is the
        // half of the table that costs the player something, and it is easy to get wrong by
        // refunding the stake after the roll.
        var losing = -1;

        for (var seed = 0; seed < 40000 && losing < 0; seed++)
        {
            if (RoomCatalog.Pick(seed, FloorNumbers[0]) == RoomKind.LongTable &&
                !RoomCatalog.GambleWins(seed, FloorNumbers[0]))
            {
                losing = seed;
            }
        }

        Assert.True(losing >= 0, "no seed lost a stake on the casino floor");

        var campaign = BuildCampaign(losing);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        var heartsBefore = campaign.Hearts;

        Assert.True(campaign.ResolveRoom(Option(campaign, "stake-2")));

        Assert.Equal(heartsBefore - 2, campaign.Hearts);
        Assert.Equal("LOST 2", campaign.Room!.Outcome);
    }

    [Fact]
    public void TheCasino_CannotBeRerolled()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.LongTable, 0), hearts: 3);

        EnterRoom(campaign);
        campaign.OpenRoom();
        Assert.True(campaign.ResolveRoom(Option(campaign, "stake-1")));

        var hearts = campaign.Hearts;

        campaign.CloseRoom();
        Assert.False(campaign.OpenRoom());
        Assert.Equal(hearts, campaign.Hearts);
    }

    [Fact]
    public void AOneHeartRun_IsOfferedNothingToSpendItOn()
    {
        foreach (var kind in new[] { RoomKind.Stall, RoomKind.Confessional, RoomKind.Toll })
        {
            var campaign = BuildCampaign(SeedFor(kind, 0), hearts: 1, maxHearts: 1);

            EnterRoom(campaign);
            Assert.True(campaign.OpenRoom());
            Assert.Equal(kind, campaign.Room!.Kind);

            // Only leaving is on the card: the last heart is not currency, so it is not offered.
            Assert.Equal(["leave"], OptionIds(campaign));
        }
    }

    [Fact]
    public void TheStall_SellsOneCardForOneHeart()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Stall, 0), hearts: 3);

        EnterRoom(campaign);
        campaign.OpenRoom();

        var forSale = campaign.Room!.Options.Where(option => option.Id.StartsWith("buy-", StringComparison.Ordinal)).ToList();

        Assert.Equal(DraftFactory.DefaultOfferCount, forSale.Count);

        var heartsBefore = campaign.Hearts;
        var cardsBefore = campaign.Modifiers.Cards.Count;

        Assert.True(campaign.ResolveRoom(forSale[0]));

        Assert.Equal(heartsBefore - 1, campaign.Hearts);
        Assert.Equal(cardsBefore + 1, campaign.Modifiers.Cards.Count);
        Assert.StartsWith("BOUGHT ", campaign.Room!.Outcome);
    }

    [Fact]
    public void TheConfessional_RemovesACardAndAHeart()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Confessional, 1));

        TakeACard(campaign);

        Assert.Single(campaign.Modifiers.Cards);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());
        Assert.Equal(RoomKind.Confessional, campaign.Room!.Kind);

        var heartsBefore = campaign.Hearts;

        Assert.True(campaign.ResolveRoom(Option(campaign, "give-0")));

        Assert.Empty(campaign.Modifiers.Cards);
        Assert.Equal(heartsBefore - 1, campaign.Hearts);
    }

    [Fact]
    public void EveryCardNamedOnARoomCard_StatesWhatItDoes()
    {
        // The player is not expected to know the catalog by heart, so any option that names a
        // card prints that card's face under the price — the Confessional and the Mirror as well
        // as the Stall.
        var confessional = BuildCampaign(SeedFor(RoomKind.Confessional, 1));

        TakeACard(confessional);

        EnterRoom(confessional);
        Assert.True(confessional.OpenRoom());

        var give = Option(confessional, "give-0");

        Assert.Equal(confessional.Modifiers.Cards[0].FaceText, give.Preview);

        var mirror = BuildCampaign(SeedFor(RoomKind.Mirror, 1));

        TakeACard(mirror);

        EnterRoom(mirror);
        Assert.True(mirror.OpenRoom());

        var swap = Option(mirror, "swap-0");

        Assert.Equal(mirror.Modifiers.Cards[0].FaceText, swap.Preview);
    }

    [Fact]
    public void TheConfessional_OffersNoFreeExitWhenItHasADeal()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Confessional, 1));

        TakeACard(campaign);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        var ids = OptionIds(campaign);

        Assert.Contains("give-0", ids);
        Assert.Contains(Campaign.RefuseOptionId, ids);
        Assert.DoesNotContain(RoomOption.Leave.Id, ids);

        var refusal = Option(campaign, Campaign.RefuseOptionId);

        Assert.Equal("ONE HEART AND A CARD IT CHOOSES", refusal.Note);
    }

    [Fact]
    public void RefusingTheConfessional_TakesAHeartAndACardItChooses()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Confessional, 1));

        TakeACard(campaign);

        Assert.Single(campaign.Modifiers.Cards);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        var heartsBefore = campaign.Hearts;

        Assert.True(campaign.ResolveRoom(Option(campaign, Campaign.RefuseOptionId)));

        // The refusal is the same price as the confession; what it costs is the choice of card.
        Assert.Empty(campaign.Modifiers.Cards);
        Assert.Equal(heartsBefore - 1, campaign.Hearts);
        Assert.StartsWith("IT TOOK ", campaign.Room!.Outcome);
        Assert.EndsWith(" AND A HEART", campaign.Room.Outcome);
    }

    [Fact]
    public void TheMirror_SwapsACardWithoutCharging()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Mirror, 1));

        TakeACard(campaign);

        var before = campaign.Modifiers.Cards[0];

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());
        Assert.Equal(RoomKind.Mirror, campaign.Room!.Kind);

        var heartsBefore = campaign.Hearts;

        Assert.True(campaign.ResolveRoom(Option(campaign, "swap-0")));

        Assert.Equal(heartsBefore, campaign.Hearts);
        Assert.Single(campaign.Modifiers.Cards);
        Assert.NotEqual(before.Id, campaign.Modifiers.Cards[0].Id);
    }

    [Fact]
    public void TheToll_TakesOneHunterOffTheNextFloor()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Toll, 0));

        Assert.Equal(2, campaign.Floor.HunterCount);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());
        Assert.True(campaign.ResolveRoom(Option(campaign, "toll")));

        ClearFloor(campaign);
        ResolveDraft(campaign);

        Assert.True(campaign.TryAdvanceFloor());

        // The floor definition asked for two; the toll is what leaves one at home.
        Assert.Equal(1, campaign.Floor.HunterCount);
    }

    [Fact]
    public void TheSeedVault_PaysThreeFloorsDown()
    {
        // The last floor admits one more heart than the ones before it, so the bloom has room to
        // land without being swallowed by this floor's ceiling.
        var campaign = BuildCampaign(SeedFor(RoomKind.SeedVault, 0), hearts: 3, floors: 4, topFloorMaxHearts: 4);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());
        Assert.True(campaign.ResolveRoom(Option(campaign, "plant")));

        ClearFloor(campaign);
        ResolveDraft(campaign);

        var hearts = campaign.Hearts;

        // Floors two and three: the seed has not bloomed yet.
        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(hearts, campaign.Floor.Hearts);

        ClearFloor(campaign);
        ResolveDraft(campaign);
        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(hearts, campaign.Floor.Hearts);

        // Floor four: it blooms on top of whatever the run was already carrying, and nothing is
        // clipped now that the run has no heart ceiling.
        ClearFloor(campaign);
        ResolveDraft(campaign);

        var landing = campaign.HeartsOnNextFloor + 1;

        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(landing, campaign.Floor.Hearts);
    }

    [Fact]
    public void TheTurn_RewindsFiveFloorsAndKeepsEverything()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Turn, 7), floors: FloorNumbers.Length, hearts: 3);

        // Descend to floor 9 by clearing the eight floors before it.
        for (var cleared = 0; cleared < FloorNumbers.Length - 1; cleared++)
        {
            ClearFloor(campaign);
            ResolveDraft(campaign);
            Assert.True(campaign.TryAdvanceFloor());
        }

        Assert.Equal(9, campaign.FloorNumber);

        var hearts = campaign.Hearts;
        var cards = campaign.Modifiers.Cards.Count;

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());
        Assert.Equal(RoomKind.Turn, campaign.Room!.Kind);

        Assert.True(campaign.ResolveRoom(Option(campaign, "back-5")));

        // The custom run numbers its eighth floor 9, so five floors back is the third.
        Assert.Equal(3, campaign.FloorNumber);
        Assert.Equal(hearts, campaign.Floor.Hearts);
        Assert.Equal(cards, campaign.Modifiers.Cards.Count);

        // Once per run, even from the floor it landed on.
        Assert.False(campaign.TryReincarnate(false));
    }

    [Fact]
    public void TheTurn_CostsAHeartToStartOver()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Turn, 7), floors: FloorNumbers.Length, hearts: 3);

        for (var cleared = 0; cleared < FloorNumbers.Length - 1; cleared++)
        {
            ClearFloor(campaign);
            ResolveDraft(campaign);
            Assert.True(campaign.TryAdvanceFloor());
        }

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        var hearts = campaign.Hearts;

        Assert.True(campaign.ResolveRoom(Option(campaign, "back-1")));

        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(hearts - 1, campaign.Floor.Hearts);
    }

    [Fact]
    public void TheStall_ShowsEveryCardsFaceBeforeTakingAHeart()
    {
        var campaign = BuildCampaign(SeedFor(RoomKind.Stall, 0), hearts: 3);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        var buys = campaign.Room!.Options
            .Where(option => option.Id.StartsWith("buy-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(buys);

        foreach (var option in buys)
        {
            Assert.NotNull(option.Preview);
            Assert.Contains(" but ", option.Preview!, StringComparison.Ordinal);
            Assert.Contains(CardCatalog.All, card => card.FaceText == option.Preview);
        }
    }

    [Fact]
    public void HouseFavour_PromisesACasinoOnTheNextFloorAndPays66Once()
    {
        Campaign? campaign = null;
        Card? promise = null;

        for (var seed = 0; seed < 2000 && campaign is null; seed++)
        {
            var candidate = BuildCampaign(seed, floors: 3, onlyForcedRooms: true);
            ClearFloor(candidate);

            var offer = candidate.Draft!.Offers
                .FirstOrDefault(card => string.Equals(card.Id, "house-favour", StringComparison.Ordinal));

            if (offer is not null)
            {
                campaign = candidate;
                promise = offer;
            }
        }

        Assert.NotNull(campaign);
        Assert.NotNull(promise);

        Assert.True(campaign!.TakeCard(promise!));
        Assert.True(campaign.TryAdvanceFloor());

        // The promise wins over the ten percent roll; without it this builder would have made a
        // roomless floor.
        Assert.NotNull(campaign.Floor.SafeRoom);

        EnterRoom(campaign);
        Assert.True(campaign.OpenRoom());

        Assert.Equal(RoomKind.LongTable, campaign.Room!.Kind);
        Assert.Equal(66, campaign.CasinoWinPercent);
        Assert.Contains("66%", campaign.Room.Prompt, StringComparison.Ordinal);

        var stakes = campaign.Room.Options
            .Where(option => option.Id.StartsWith("stake-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(stakes);
        Assert.All(stakes, stake => Assert.Contains("66% DOUBLE", stake.Note!, StringComparison.Ordinal));

        // The favoured table is one play: afterwards the house is back to 45%.
        Assert.True(campaign.ResolveRoom(stakes[0]));
        Assert.Equal(45, campaign.CasinoWinPercent);
    }

    [Fact]
    public void TheFavouredTablePaysAboutSixtySixPercent()
    {
        const int trials = 10000;
        var normal = 0;
        var favoured = 0;

        for (var seed = 0; seed < trials; seed++)
        {
            if (RoomCatalog.GambleWins(seed, 5))
            {
                normal++;
            }

            if (RoomCatalog.GambleWins(seed, 5, RoomCatalog.FavouredGambleWinPercent))
            {
                favoured++;
            }
        }

        Assert.InRange(normal, 4300, 4700);
        Assert.InRange(favoured, 6400, 6800);
    }

    [Fact]
    public void EveryRoomKind_IsReachable_OnAFloorItBelongsTo()
    {
        var seen = new HashSet<RoomKind>();
        var kinds = Enum.GetValues<RoomKind>();

        for (var seed = 0; seed < 4000 && seen.Count < kinds.Length; seed++)
        {
            for (var floor = 1; floor <= FloorCatalog.ShippedFloorCount; floor++)
            {
                seen.Add(RoomCatalog.Pick(seed, floor));
            }
        }

        Assert.Equal(kinds.Length, seen.Count);
    }

    [Fact]
    public void TheTurn_IsNotRolledBeforeItsFloor()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            Assert.NotEqual(RoomKind.Turn, RoomCatalog.Pick(seed, RoomCatalog.TurnFloor - 1));
            Assert.Equal(0, RoomCatalog.Weight(RoomKind.Turn, RoomCatalog.TurnFloor - 1));
        }
    }

    [Fact]
    public void FromCards_EqualsAddingEachCardInTurn()
    {
        foreach (var card in CardCatalog.All)
        {
            var folded = RunModifiers.None.With(card);
            var rebuilt = RunModifiers.FromCards([card]);

            Assert.Equal(folded.VisionRadius, rebuilt.VisionRadius);
            Assert.Equal(folded.EnemyStepScale, rebuilt.EnemyStepScale, 6);
            Assert.Equal(folded.PlayerStepScale, rebuilt.PlayerStepScale, 6);
            Assert.Equal(folded.KeyCount, rebuilt.KeyCount);
            Assert.Equal(folded.CatchCost, rebuilt.CatchCost);
            Assert.Equal(folded.Describe(), rebuilt.Describe());
        }
    }

    /// <summary>Builds a campaign whose floors hold a real carved room.</summary>
    /// <param name="onlyForcedRooms">
    /// When true, floors only get their room if the definition carries <see cref="FloorDefinition.ForceRoom"/>;
    /// used to prove that a card's promise is what put the room there.
    /// </param>
    private static Campaign BuildCampaign(
        int seed,
        int? hearts = null,
        int floors = 2,
        int maxHearts = 3,
        int? topFloorMaxHearts = null,
        bool onlyForcedRooms = false)
    {
        var topHearts = topFloorMaxHearts ?? maxHearts;

        var definitions = Enumerable.Range(0, floors)
            .Select(index => new FloorDefinition
            {
                Number = index < FloorNumbers.Length
                    ? FloorNumbers[index]
                    : FloorNumbers[^1] + (index - FloorNumbers.Length) + 1,
                Name = $"Floor {index + 1}",
                ThemeId = "shallows",
                MaxHearts = index == floors - 1 ? topHearts : maxHearts,

                // Two on every floor, so a toll that leaves one at home is visible as a number.
                HunterCount = 2,
                KeyCount = 1,
            })
            .ToArray();

        return new Campaign(
            definitions,
            (floor, startingHearts, _, _) =>
            {
                var hasRoom = !onlyForcedRooms || floor.ForceRoom;
                var random = new SeededRandom(4);
                var maze = new MazeGenerator(random).Generate(new MazeOptions(7, 7, 4)
                {
                    ReservedCells = hasRoom ? SecretRoomCarver.ReservedCells(RoomSite) : null,
                });
                var room = hasRoom ? SecretRoomCarver.TryCarve(maze, RoomSite, random) : null;

                if (hasRoom)
                {
                    Assert.NotNull(room);
                }

                return new GameState(
                    new GameConfig
                    {
                        Rows = 7,
                        Columns = 7,
                        KeyCount = 1,
                        HunterCount = floor.HunterCount,
                        EnemyStepSeconds = 10.0,
                        PlayerStepSeconds = 0,
                        VisionRadius = 5,
                        EnemyAuraRadius = 0,
                        EnemyAuraDistance = 0,
                        StartingHearts = startingHearts,
                        MaxHearts = Math.Max(startingHearts, floor.MaxHearts),
                        Seed = null,
                    },
                    maze,
                    new DijkstraPathfinder(),
                    new FixedRandom(0),
                    room);
            },
            seed);
    }

    private static void EnterRoom(Campaign campaign)
    {
        Assert.True(WalkTo(campaign.Floor, campaign.Floor.SafeRoom!.Center));
    }

    /// <summary>Collects every key and walks onto the exit.</summary>
    private static void ClearFloor(Campaign campaign)
    {
        var state = campaign.Floor;

        foreach (var key in new List<Cell>(state.Keys))
        {
            Assert.True(WalkTo(state, key));
        }

        Assert.True(WalkTo(state, state.Exit));
        Assert.True(campaign.IsFloorCleared);
    }

    private static void TakeACard(Campaign campaign)
    {
        ClearFloor(campaign);
        ResolveDraft(campaign);
        Assert.True(campaign.TryAdvanceFloor());
    }

    private static void ResolveDraft(Campaign campaign)
    {
        var draft = campaign.Draft;
        Assert.NotNull(draft);

        if (draft!.Offers.Count > 0)
        {
            Assert.True(campaign.TakeCard(draft.Offers[0]));
            return;
        }

        Assert.True(campaign.SkipDraft());
    }

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

    private static RoomOption Option(Campaign campaign, string id)
    {
        foreach (var option in campaign.Room!.Options)
        {
            if (string.Equals(option.Id, id, StringComparison.Ordinal))
            {
                return option;
            }
        }

        throw new InvalidOperationException($"The room has no option '{id}'.");
    }

    private static List<string> OptionIds(Campaign campaign) =>
        campaign.Room!.Options.Select(option => option.Id).ToList();

    /// <summary>
    /// Finds a run seed whose room on the campaign's <paramref name="index"/>th floor is
    /// <paramref name="kind"/>.
    /// </summary>
    private static int SeedFor(RoomKind kind, int index) => FindSeed(1, kind, index);

    private static int FindSeed(int start, RoomKind kind, int index)
    {
        for (var seed = start; seed < start + 40000; seed++)
        {
            if (RoomCatalog.Pick(seed, FloorNumberAt(index)) == kind)
            {
                return seed;
            }
        }

        throw new InvalidOperationException($"No seed rolls {kind} on floor index {index}.");
    }

    /// <summary>The floor number the custom campaign uses for its <paramref name="index"/>th floor.</summary>
    private static int FloorNumberAt(int index) =>
        index < FloorNumbers.Length
            ? FloorNumbers[index]
            : FloorNumbers[^1] + (index - FloorNumbers.Length) + 1;
}

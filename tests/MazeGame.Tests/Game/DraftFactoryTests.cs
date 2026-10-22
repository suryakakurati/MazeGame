using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies how a draft's offers are chosen: reproducibility from the seed, and the guarantees
/// that keep a pick meaningful — no repeats, no contradictions, no vision runaway.
/// </summary>
/// <remarks>
/// The catalog is passed in, so most of these run over a two-card fake catalog where the rule
/// being tested is the only thing that can decide the answer.
/// </remarks>
public sealed class DraftFactoryTests
{
    [Fact]
    public void Offers_AreReproducibleFromTheSeedAndFloor()
    {
        var first = Ids(DraftFactory.Offers(11, 1, [], 3, draftsRemaining: 2));
        var second = Ids(DraftFactory.Offers(11, 1, [], 3, draftsRemaining: 2));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Offers_ChangeWithTheFloor()
    {
        // A seeded run still wants a different choice on every floor.
        var first = Ids(DraftFactory.Offers(11, 1, [], 3, draftsRemaining: 2));
        var second = Ids(DraftFactory.Offers(11, 2, [], 3, draftsRemaining: 2));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Offers_ChangeWithTheRunSeed()
    {
        var first = Ids(DraftFactory.Offers(11, 1, [], 3, draftsRemaining: 2));
        var second = Ids(DraftFactory.Offers(12, 1, [], 3, draftsRemaining: 2));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Offers_MakeTheRequestedNumberOfDistinctCards()
    {
        var offers = DraftFactory.Offers(3, 1, [], DraftFactory.DefaultOfferCount, draftsRemaining: 2);

        Assert.Equal(DraftFactory.DefaultOfferCount, offers.Count);
        Assert.Equal(offers.Count, offers.Select(card => card.Id).Distinct().Count());
    }

    [Fact]
    public void Offers_HonourABiggerAndASmallerDraft()
    {
        var big = DraftFactory.Offers(3, 1, [], DraftFactory.MaxOfferCount, draftsRemaining: 2);
        var small = DraftFactory.Offers(3, 1, [], DraftFactory.MinOfferCount, draftsRemaining: 2);

        Assert.Equal(DraftFactory.MaxOfferCount, big.Count);
        Assert.Single(small);
    }

    [Fact]
    public void Offers_NeverOfferACardAlreadyHeld()
    {
        var wideEyes = Card("wide-eyes");

        var offers = DraftFactory.Offers(5, 1, [wideEyes], DraftFactory.MaxOfferCount, draftsRemaining: 2);

        Assert.DoesNotContain(offers, card => card.Id == wideEyes.Id);
    }

    [Fact]
    public void Offers_NeverOfferACardThatContradictsAHeldOne()
    {
        // Single copies, so the only rule that can decide the answer is the contradiction.
        var left = Card("wide-eyes") with { Id = "test-left", MaxCopies = 1, IncompatibleWith = "test-right" };
        var right = Card("wide-eyes") with { Id = "test-right", MaxCopies = 1, IncompatibleWith = "test-left" };
        var catalog = new[] { left, right };

        var withLeft = DraftFactory.Offers(1, 1, [left], 2, draftsRemaining: 2, catalog);
        var withRight = DraftFactory.Offers(1, 1, [right], 2, draftsRemaining: 2, catalog);

        // The pair is refused at the draft rather than leaving one card silently doing nothing.
        Assert.Empty(withLeft);
        Assert.Empty(withRight);
    }

    [Fact]
    public void Offers_StopOfferingVisionCardsAfterTwo()
    {
        var lanternOil = Card("lantern-oil");
        var catalog = new[] { lanternOil };
        var twoVisionCards = new[] { Card("wide-eyes"), Card("second-wind") };

        var offers = DraftFactory.Offers(
            1,
            1,
            twoVisionCards,
            1,
            draftsRemaining: 2,
            catalog);

        Assert.Empty(offers);
    }

    [Fact]
    public void Offers_AllowOneMoreVisionCardWhenOneIsHeld()
    {
        var offers = DraftFactory.Offers(
            1,
            1,
            [Card("wide-eyes")],
            1,
            draftsRemaining: 2);

        Assert.Single(offers);
    }

    [Fact]
    public void Offers_WithdrawLuckyFindWhenTooFewDraftsRemain()
    {
        var luckyFind = Card("lucky-find");
        var catalog = new[] { luckyFind };

        var oneDraftLeft = DraftFactory.Offers(1, 1, [], 1, draftsRemaining: 1, catalog);
        var twoDraftsLeft = DraftFactory.Offers(1, 1, [], 1, draftsRemaining: 2, catalog);

        // Its bonus lands in the next draft and its price in the one after, so with a single
        // draft left it would be pure upside and is not offered at all.
        Assert.Empty(oneDraftLeft);
        Assert.Single(twoDraftsLeft);
    }

    [Fact]
    public void Offers_TreatANegativeRemainderAsTheLastDraft()
    {
        var luckyFind = Card("lucky-find");

        var offers = DraftFactory.Offers(1, 1, [], 1, draftsRemaining: -3, [luckyFind]);

        Assert.Empty(offers);
    }

    [Fact]
    public void Offers_WithATinyCatalog_ReturnWhatExists()
    {
        var catalog = new[] { Card("wide-eyes") };

        var offers = DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, catalog);

        Assert.Single(offers);
    }

    [Fact]
    public void Offers_WithOnlyACurse_ReturnNothing()
    {
        // Curses are never offered alone; they exist as the price of a rare.
        var curse = Card("wide-eyes") with { Id = "quiet", Tier = CardTier.Curse };

        var offers = DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, [curse]);

        Assert.Empty(offers);
    }

    [Fact]
    public void Offers_RepeatAStackableCard()
    {
        var stackable = Card("wide-eyes") with { MaxCopies = 2 };

        var offers = DraftFactory.Offers(1, 1, [stackable], 1, draftsRemaining: 2, [stackable]);

        Assert.Single(offers);
        Assert.Equal(stackable.Id, offers[0].Id);
    }

    [Fact]
    public void Offers_StopOfferingAStackableCardAtItsCopyCap()
    {
        var stackable = Card("wide-eyes") with { MaxCopies = 2 };

        var oneHeld = DraftFactory.Offers(1, 1, [stackable], 1, draftsRemaining: 2, [stackable]);
        var twoHeld = DraftFactory.Offers(1, 1, [stackable, stackable], 1, draftsRemaining: 2, [stackable]);

        Assert.Single(oneHeld);
        Assert.Empty(twoHeld);
    }

    [Fact]
    public void Offers_StillFillADraftOnTheLastFloorOfALongRun()
    {
        // The whole point of copies: a twenty-three card catalog has to keep feeding a
        // nineteen-draft run, so a greedy player who always takes the first offer never reaches
        // a draft with fewer than three eligible cards to choose from.
        const int drafts = 19;
        var held = new List<Card>();

        for (var floor = 1; floor <= drafts; floor++)
        {
            var offers = DraftFactory.Offers(
                4242,
                floor,
                held,
                DraftFactory.DefaultOfferCount,
                draftsRemaining: drafts - floor);

            Assert.Equal(DraftFactory.DefaultOfferCount, offers.Count);
            held.Add(offers[0]);
        }
    }

    [Fact]
    public void Offers_EveryFifthFloorIncludesARare()
    {
        foreach (var floor in new[] { 5, 10, 15 })
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var offers = DraftFactory.Offers(seed, floor, [], 3, draftsRemaining: 2);

                Assert.Contains(offers, card => card.Tier == CardTier.Rare);
            }
        }
    }

    [Fact]
    public void Offers_LateFloorsDrawMoreRaresThanEarlyOnes()
    {
        var early = 0;
        var late = 0;

        for (var seed = 0; seed < 200; seed++)
        {
            early += DraftFactory.Offers(seed, 1, [], 3, draftsRemaining: 2).Count(card => card.Tier == CardTier.Rare);
            late += DraftFactory.Offers(seed, 18, [], 3, draftsRemaining: 2).Count(card => card.Tier == CardTier.Rare);
        }

        Assert.True(late > early, $"late floors drew {late} rares against {early} early");
    }

    [Fact]
    public void Offers_OnARewoundFloor_UseTheDepthsRarityCurve()
    {
        // A rewind to floor four must not hand out the rares of floor eighteen, so the draw reads
        // the deepest floor the run ever reached rather than the floor it is standing on. Neither
        // floor is a milestone, so the guaranteed rare cannot decide the answer.
        var shallow = 0;
        var deep = 0;

        for (var seed = 0; seed < 200; seed++)
        {
            shallow += DraftFactory.Offers(seed, 4, [], 3, draftsRemaining: 2, effectiveFloor: 4)
                .Count(card => card.Tier == CardTier.Rare);
            deep += DraftFactory.Offers(seed, 4, [], 3, draftsRemaining: 2, effectiveFloor: 18)
                .Count(card => card.Tier == CardTier.Rare);
        }

        Assert.True(deep > shallow, $"the rewound draft drew {deep} rares against {shallow}");
    }

    [Fact]
    public void Offers_WithASalt_DifferFromThePlainDraft()
    {
        // A shop and a mirror salt their draw, so their cards are not the next draft's cards.
        var plain = DraftFactory.Offers(3, 1, [], 3, draftsRemaining: 2);
        var salted = DraftFactory.Offers(3, 1, [], 3, draftsRemaining: 2, salt: 0x1234);

        Assert.NotEqual(Ids(plain), Ids(salted));
    }

    [Fact]
    public void Offers_WithNoCatalog_ReturnNothing()
    {
        var offers = DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, []);

        Assert.Empty(offers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(DraftFactory.MaxOfferCount + 1)]
    public void Offers_WithAnImpossibleCount_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DraftFactory.Offers(1, 1, [], count, draftsRemaining: 2));
    }

    [Fact]
    public void Offers_WithNullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => DraftFactory.Offers(1, 1, null!, 3, draftsRemaining: 2));
        Assert.Throws<ArgumentNullException>(
            () => DraftFactory.Offers(1, 1, [null!], 3, draftsRemaining: 2));
        Assert.Throws<ArgumentNullException>(
            () => DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, [null!]));
    }

    private static Card Card(string id) => CardCatalog.ById(id)!;

    private static List<string> Ids(IReadOnlyList<Card> cards) =>
        cards.Select(card => card.Id).ToList();
}

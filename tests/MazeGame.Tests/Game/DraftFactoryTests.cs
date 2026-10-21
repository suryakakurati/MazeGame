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
        var deepRoots = Card("deep-roots");
        var thornwright = Card("thornwright");
        var catalog = new[] { deepRoots, thornwright };

        var withRoots = DraftFactory.Offers(1, 1, [deepRoots], 2, draftsRemaining: 2, catalog);
        var withThorns = DraftFactory.Offers(1, 1, [thornwright], 2, draftsRemaining: 2, catalog);

        // Both absolutes set the seal lifetime, so the pair is refused at the draft rather than
        // leaving one card silently doing nothing.
        Assert.Empty(withRoots);
        Assert.Empty(withThorns);
    }

    [Fact]
    public void Offers_StopOfferingVisionCardsAfterTwo()
    {
        var smokeWisp = Card("smoke-wisp");
        var catalog = new[] { smokeWisp };
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
        var catalog = new[] { Card("bait") };

        var offers = DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, catalog);

        Assert.Single(offers);
    }

    [Fact]
    public void Offers_WithOnlyACurse_ReturnNothing()
    {
        // Curses are never offered alone; they exist as the price of a rare.
        var curse = Card("deep-roots") with { Id = "quiet", Tier = CardTier.Curse };

        var offers = DraftFactory.Offers(1, 1, [], 3, draftsRemaining: 2, [curse]);

        Assert.Empty(offers);
    }

    [Fact]
    public void Offers_RepeatAStackableCard()
    {
        var stackable = Card("bait") with { IsStackable = true };

        var offers = DraftFactory.Offers(1, 1, [stackable], 1, draftsRemaining: 2, [stackable]);

        Assert.Single(offers);
        Assert.Equal(stackable.Id, offers[0].Id);
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

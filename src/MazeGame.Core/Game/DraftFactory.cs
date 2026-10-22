using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Game;

/// <summary>
/// Builds a draft's offers from the run's seed, the floor number and the cards already held.
/// </summary>
/// <remarks>
/// <para>
/// The offers are a pure function of the seed and the floor, which is what makes a seeded run
/// reproducible and lets a save file store only the seed: the draft is regenerated, not
/// recorded. Reality comes from <see cref="IRandom"/> like everything else in the project.
/// </para>
/// <para>
/// Four guards keep the draft honest: a card is not offered again once the run holds its
/// <see cref="Card.MaxCopies"/> copies, a card that contradicts a held one is not offered, no card
/// may change vision once the run already holds two that do, and no card is offered when the part
/// of the run that pays for it does not exist. Rares are drawn less often than commons early in
/// the run and more often late, and every fifth floor's draft guarantees one when one is still
/// eligible, so a rare feels like an event without being a lottery.
/// </para>
/// </remarks>
public static class DraftFactory
{
    /// <summary>Gets how many offers a draft makes before any card changes the count.</summary>
    public const int DefaultOfferCount = 3;

    /// <summary>Gets how many offers a draft may ever make, after cards change the count.</summary>
    public const int MaxOfferCount = 5;

    /// <summary>Gets how few offers a draft may make, so a pick is always possible.</summary>
    public const int MinOfferCount = 1;

    /// <summary>
    /// Gets how many cards in one run may change what the player can see. Past two, vision
    /// compounds into a different game, so the draft stops offering more.
    /// </summary>
    public const int MaxVisionCards = 2;

    private const int CommonWeight = 6;

    private const int UncommonWeight = 3;

    private const int RareWeight = 1;

    /// <summary>
    /// The weight a super-rare card draws at. It never rises with depth, so it stays the rarest
    /// card in the pool on the last floor just as it was on the first.
    /// </summary>
    private const int SuperRareWeight = 1;

    private const int FloorSeedStride = 7919;

    /// <summary>
    /// Returns the cards offered after <paramref name="floorNumber"/> is cleared.
    /// </summary>
    /// <param name="runSeed">The run's seed. The same seed and floor give the same offers.</param>
    /// <param name="floorNumber">The floor just cleared; the draft belongs to the next floor.</param>
    /// <param name="held">The cards already taken this run.</param>
    /// <param name="offerCount">
    /// How many offers to make, between <see cref="MinOfferCount"/> and
    /// <see cref="MaxOfferCount"/>. Fewer come back when the eligible pool is smaller.
    /// </param>
    /// <param name="draftsRemaining">
    /// How many drafts still happen after this one. A card whose price is paid in a later draft
    /// is withheld when there are not enough left for the trade to exist.
    /// </param>
    /// <param name="catalog">The cards to draw from; defaults to <see cref="CardCatalog.All"/>.</param>
    /// <param name="effectiveFloor">
    /// The depth the rarity curve is measured against, for a run that has turned back: a rewind to
    /// floor five must not hand out the rares of floor fifteen. Defaults to
    /// <paramref name="floorNumber"/>.
    /// </param>
    /// <param name="salt">
    /// Mixed into the draw's seed so a shop or a mirror is not a copy of the next draft's offers.
    /// Zero (the default) is the draft itself.
    /// </param>
    /// <returns>The offers in draw order, as a read-only list. May be empty if nothing is eligible.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="held"/> is null, or when it or <paramref name="catalog"/> holds a null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="offerCount"/> is outside the allowed range.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a held card is invalid.</exception>
    public static IReadOnlyList<Card> Offers(
        int runSeed,
        int floorNumber,
        IReadOnlyList<Card> held,
        int offerCount,
        int draftsRemaining,
        IReadOnlyList<Card>? catalog = null,
        int? effectiveFloor = null,
        int salt = 0)
    {
        ArgumentNullException.ThrowIfNull(held);

        if (offerCount is < MinOfferCount or > MaxOfferCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offerCount),
                offerCount,
                $"A draft offers between {MinOfferCount} and {MaxOfferCount} cards.");
        }

        catalog ??= CardCatalog.All;

        foreach (var card in held)
        {
            (card ?? throw new ArgumentNullException(nameof(held), "The held list contains null.")).Validate();
        }

        var heldVisionCards = 0;
        var copiesHeld = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var card in held)
        {
            if (card.Effect.TouchesVision)
            {
                heldVisionCards++;
            }

            copiesHeld[card.Id] = copiesHeld.TryGetValue(card.Id, out var count) ? count + 1 : 1;
        }

        var pool = new List<Card>();

        // Validate the whole catalog first, so a bad entry fails even when nothing is eligible.
        foreach (var card in catalog)
        {
            if (card is null)
            {
                throw new ArgumentNullException(nameof(catalog), "The catalog contains null.");
            }

            card.Validate();
        }

        foreach (var card in catalog)
        {
            if (IsEligible(card, held, copiesHeld, heldVisionCards, draftsRemaining))
            {
                pool.Add(card);
            }
        }

        var offers = new List<Card>(Math.Min(offerCount, pool.Count));
        var depth = effectiveFloor ?? floorNumber;
        var random = new SeededRandom(unchecked(Mix(runSeed, floorNumber) ^ salt));

        while (offers.Count < offerCount && pool.Count > 0)
        {
            var index = ChooseIndex(pool, random, depth);
            if (index < 0)
            {
                break;
            }

            offers.Add(pool[index]);
            pool.RemoveAt(index);
        }

        // Every fifth floor is a milestone: the draft guarantees a rare when one is still eligible.
        // A weighted draw alone would make that a promise the RNG could break, so a missed rare is
        // swapped in for the last offer. The swap is seeded, so a replay is unchanged.
        if (depth % 5 == 0 && offers.Count > 0 && !ContainsRare(offers))
        {
            var rares = new List<Card>();

            foreach (var card in pool)
            {
                if (card.Tier == CardTier.Rare)
                {
                    rares.Add(card);
                }
            }

            if (rares.Count > 0)
            {
                offers[^1] = rares[random.NextInt(rares.Count)];
            }
        }

        return new ReadOnlyCollection<Card>(offers);
    }

    /// <summary>
    /// Tests whether <paramref name="card"/> may be offered to a run holding <paramref name="held"/>.
    /// </summary>
    /// <param name="card">The candidate offer.</param>
    /// <param name="held">Every card taken so far, repeats included.</param>
    /// <param name="copiesHeld">How many copies of each id the run holds.</param>
    /// <param name="heldVisionCards">How many held cards change what the player can see.</param>
    /// <param name="draftsRemaining">How many drafts still happen after this one.</param>
    private static bool IsEligible(
        Card card,
        IReadOnlyList<Card> held,
        Dictionary<string, int> copiesHeld,
        int heldVisionCards,
        int draftsRemaining)
    {
        // A card whose drawback lands in a later draft is not a trade in a run too short to reach
        // that draft, so it is withheld rather than offered as pure upside.
        if (card.DraftsNeededAfterThisOne > Math.Max(0, draftsRemaining))
        {
            return false;
        }

        if (card.Effect.TouchesVision && heldVisionCards >= MaxVisionCards)
        {
            return false;
        }

        if (copiesHeld.TryGetValue(card.Id, out var copies) && copies >= card.MaxCopies)
        {
            return false;
        }

        foreach (var taken in held)
        {
            if (!card.IsCompatibleWith(taken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Picks an index from <paramref name="pool"/> weighted by tier and floor, or -1 when every
    /// weight is zero.
    /// </summary>
    private static int ChooseIndex(List<Card> pool, SeededRandom random, int floorNumber)
    {
        var total = 0;
        foreach (var card in pool)
        {
            total += WeightOf(card.Tier, floorNumber);
        }

        if (total == 0)
        {
            return -1;
        }

        var roll = random.NextInt(total);

        for (var index = 0; index < pool.Count; index++)
        {
            var weight = WeightOf(pool[index].Tier, floorNumber);
            if (roll < weight)
            {
                return index;
            }

            roll -= weight;
        }

        // Unreachable while the weights add up to `total`; the loop above returns first.
        return pool.Count - 1;
    }

    /// <summary>
    /// Returns how likely a tier is to be drawn at this point in the run. Curses weigh nothing:
    /// they are never offered on their own, only as the price attached to something else. The
    /// weights shift the draw towards rares as the run descends, so the last floors are where the
    /// build-defining cards live.
    /// </summary>
    private static int WeightOf(CardTier tier, int floorNumber) => tier switch
    {
        CardTier.Common => floorNumber >= 15 ? 3 : floorNumber >= 10 ? 4 : floorNumber >= 5 ? 5 : CommonWeight,
        CardTier.Uncommon => UncommonWeight + 1,
        CardTier.Rare => floorNumber >= 15 ? 4 : floorNumber >= 10 ? 3 : floorNumber >= 5 ? 2 : RareWeight,

        // The promised casino is the rarest draw at every depth: its weight never climbs the way
        // the rare tier's does.
        CardTier.SuperRare => SuperRareWeight,
        _ => 0,
    };

    /// <summary>Returns whether <paramref name="offers"/> already contains a rare card.</summary>
    private static bool ContainsRare(List<Card> offers)
    {
        foreach (var card in offers)
        {
            if (card.Tier == CardTier.Rare)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Derives the draft's seed from the run seed and floor number, the same stable way the
    /// factory derives a floor's maze seed.
    /// </summary>
    private static int Mix(int runSeed, int floorNumber) =>
        unchecked((runSeed * 397) ^ (floorNumber * FloorSeedStride));
}

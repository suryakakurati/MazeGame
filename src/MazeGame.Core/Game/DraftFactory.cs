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
/// Four guards keep the draft honest: a card already held is not offered again (unless the
/// catalog marks it stackable), a card that contradicts a held one is not offered, no card may
/// change vision once the run already holds two that do, and no card is offered when the part of
/// the run that pays for it does not exist. Rares are drawn less often than commons, which is what
/// makes a rare feel like an event without a floor-band gate.
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
        IReadOnlyList<Card>? catalog = null)
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
        foreach (var card in held)
        {
            if (card.Effect.TouchesVision)
            {
                heldVisionCards++;
            }
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
            if (IsEligible(card, held, heldVisionCards, draftsRemaining))
            {
                pool.Add(card);
            }
        }

        var offers = new List<Card>(Math.Min(offerCount, pool.Count));
        var random = new SeededRandom(Mix(runSeed, floorNumber));

        while (offers.Count < offerCount && pool.Count > 0)
        {
            var index = ChooseIndex(pool, random);
            if (index < 0)
            {
                break;
            }

            offers.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return new ReadOnlyCollection<Card>(offers);
    }

    /// <summary>
    /// Tests whether <paramref name="card"/> may be offered to a run holding <paramref name="held"/>.
    /// </summary>
    private static bool IsEligible(
        Card card,
        IReadOnlyList<Card> held,
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

        foreach (var taken in held)
        {
            if (string.Equals(taken.Id, card.Id, StringComparison.Ordinal) && !card.IsStackable)
            {
                return false;
            }

            if (!card.IsCompatibleWith(taken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Picks an index from <paramref name="pool"/> weighted by tier, or -1 when every weight is zero.
    /// </summary>
    private static int ChooseIndex(List<Card> pool, SeededRandom random)
    {
        var total = 0;
        foreach (var card in pool)
        {
            total += WeightOf(card.Tier);
        }

        if (total == 0)
        {
            return -1;
        }

        var roll = random.NextInt(total);

        for (var index = 0; index < pool.Count; index++)
        {
            var weight = WeightOf(pool[index].Tier);
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
    /// Returns how likely a tier is to be drawn. Curses weigh nothing: they are never offered on
    /// their own, only as the price attached to something else.
    /// </summary>
    private static int WeightOf(CardTier tier) => tier switch
    {
        CardTier.Common => CommonWeight,
        CardTier.Uncommon => UncommonWeight,
        CardTier.Rare => RareWeight,
        _ => 0,
    };

    /// <summary>
    /// Derives the draft's seed from the run seed and floor number, the same stable way the
    /// factory derives a floor's maze seed.
    /// </summary>
    private static int Mix(int runSeed, int floorNumber) =>
        unchecked((runSeed * 397) ^ (floorNumber * FloorSeedStride));
}

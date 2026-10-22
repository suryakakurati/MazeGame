using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The choice offered between two floors: the cards on the table and whether one was taken.
/// </summary>
/// <remarks>
/// <para>
/// A draft is a <b>read model with one mutation</b>: the campaign creates it, the frontends draw
/// it, and only <see cref="Campaign"/> may resolve it. Keeping the mutation internal is what stops
/// a frontend from taking a card without the run's modifiers hearing about it.
/// </para>
/// <para>
/// Skipping is always legal, because a draft with no good card is still a decision: taking nothing
/// heals a heart instead. That is also why an empty draft is a valid draft rather than an error.
/// </para>
/// </remarks>
public sealed class Draft
{
    private Card[] _offers;
    private Card? _taken;
    private bool _wasSkipped;

    /// <summary>
    /// Creates a draft over <paramref name="offers"/>. Internal: drafts come from the campaign,
    /// so every one of them is seeded and resolved by the same code.
    /// </summary>
    /// <param name="offers">The cards on the table, in display order. May be empty.</param>
    internal Draft(IReadOnlyList<Card> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);

        _offers = new Card[offers.Count];
        for (var index = 0; index < offers.Count; index++)
        {
            _offers[index] = offers[index]
                ?? throw new ArgumentException("The offer list contains null.", nameof(offers));
        }
    }

    /// <summary>Gets the cards on the table, in display order.</summary>
    public IReadOnlyList<Card> Offers => new ReadOnlyCollection<Card>(_offers);

    /// <summary>Gets a value indicating whether the player has taken a card or skipped.</summary>
    public bool IsResolved => _taken is not null || _wasSkipped;

    /// <summary>Gets the card taken, or <see langword="null"/> when none was.</summary>
    public Card? Taken => _taken;

    /// <summary>Gets a value indicating whether the player skipped this draft for a heart.</summary>
    public bool WasSkipped => _wasSkipped;

    /// <summary>
    /// Resolves the draft with <paramref name="card"/>.
    /// </summary>
    /// <returns><see langword="false"/> when the draft is already resolved or the card is not on
    /// the table; the draft is unchanged in that case.</returns>
    internal bool TryTake(Card card)
    {
        if (card is null || IsResolved || !IsOffered(card))
        {
            return false;
        }

        _taken = card;
        return true;
    }

    /// <summary>
    /// Resolves the draft with no card taken.
    /// </summary>
    /// <returns><see langword="false"/> when the draft is already resolved.</returns>
    internal bool TrySkip()
    {
        if (IsResolved)
        {
            return false;
        }

        _wasSkipped = true;
        return true;
    }

    /// <summary>
    /// Replaces the offers with a fresh draw, which is what a reroll bought with hearts does.
    /// </summary>
    /// <param name="offers">The new cards on the table, in display order.</param>
    /// <returns><see langword="false"/> when the draft is already resolved; it is unchanged then.</returns>
    /// <remarks>
    /// Internal like the two resolution methods: only <see cref="Campaign"/> may charge for a
    /// reroll, and a frontend that could replace the table itself could reroll for free.
    /// </remarks>
    internal bool TryReplace(IReadOnlyList<Card> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);

        if (IsResolved)
        {
            return false;
        }

        var replacement = new Card[offers.Count];

        for (var index = 0; index < offers.Count; index++)
        {
            replacement[index] = offers[index]
                ?? throw new ArgumentException("The offer list contains null.", nameof(offers));
        }

        _offers = replacement;
        return true;
    }

    /// <summary>
    /// Tests whether <paramref name="card"/> is one of the offers, by identity of its id.
    /// </summary>
    private bool IsOffered(Card card)
    {
        foreach (var offer in _offers)
        {
            if (string.Equals(offer.Id, card.Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

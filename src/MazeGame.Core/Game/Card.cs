using System;

namespace MazeGame.Core.Game;

/// <summary>
/// One draftable bargain: a name, a face that states both halves, and the numbers it changes.
/// </summary>
/// <remarks>
/// <para>
/// The face text is part of the contract. Every card states its drawback in the same sentence as
/// its upside, because a trade the player cannot see is a hidden multiplier, and the design rules
/// in <c>FUTURE.md</c> §4.2 forbid those. Tests check that no face is empty or upside-only.
/// </para>
/// <para>
/// A card never contains behaviour. <see cref="Effect"/> is plain data, composed by
/// <see cref="RunModifiers"/> and applied to a floor's <see cref="GameConfig"/>.
/// </para>
/// </remarks>
public sealed record Card
{
    /// <summary>Gets the fewest copies a card may allow: one, which is to say none.</summary>
    public const int MinCopies = 1;

    /// <summary>Gets the most copies a card may allow.</summary>
    /// <remarks>
    /// Three is the cap because past it a single card's numbers stop being a trade and become a
    /// second game: three Light Feet is a build, five is a different floor speed entirely.
    /// </remarks>
    public const int MaxCopiesAllowed = 3;

    /// <summary>Gets the stable id used by saves, the catalog and incompatibility marks.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown on the card face.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the rarity, which weights the draft and colours the card.</summary>
    public required CardTier Tier { get; init; }

    /// <summary>
    /// Gets the one-sentence face: the upside and its price together, for example
    /// "Vision +2, but the killer's light reaches 3 steps further".
    /// </summary>
    public required string FaceText { get; init; }

    /// <summary>Gets the numbers this card changes.</summary>
    public required CardEffect Effect { get; init; }

    /// <summary>
    /// Gets how many copies of this card one run may hold, from one to three.
    /// </summary>
    /// <remarks>
    /// One means a card is offered at most once. Two or three make a repeat pick a
    /// specialisation rather than a mistake, and they are the reason the draft still has three
    /// eligible cards on the last floor of a run: a twenty-three card catalog with single copies
    /// runs dry long before the nineteenth draft.
    /// </remarks>
    public int MaxCopies { get; init; } = 1;

    /// <summary>Gets a value indicating whether the player may hold more than one copy.</summary>
    public bool IsStackable => MaxCopies > 1;

    /// <summary>
    /// Gets the id of a card this one cannot be held with, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// A pair needs this when two cards set the same absolute (a seal lifetime, say), so holding
    /// both would make one of them a lie. Preventing it at the draft is honest; letting both sit
    /// in the run and picking a winner silently is not. No shipped card needs it today: the
    /// powerup cards only add charges, which compose.
    /// </remarks>
    public string? IncompatibleWith { get; init; }

    /// <summary>
    /// Gets how many drafts must still happen after this one before the card can be offered.
    /// </summary>
    /// <remarks>
    /// <c>lucky-find</c> trades a bigger coming draft for a smaller one after it, so it needs two
    /// drafts left to be a trade at all: one for the bonus and one for the price. In a run too
    /// short for that — including the shipped three-floor campaign, where the card would otherwise
    /// be a free extra choice — the draft simply does not offer it. That is the general form of the
    /// rule: a card whose drawback lands later is withheld when "later" does not exist.
    /// </remarks>
    public int DraftsNeededAfterThisOne { get; init; }

    /// <summary>Checks that the card can be shown and composed.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the id, name or face is blank, or when the effect is missing.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("A card needs an id for saves and incompatibility.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("A card needs a name on its face.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(FaceText))
        {
            throw new ArgumentException(
                "A card must state its trade on its face; a blank face is a hidden modifier.",
                nameof(FaceText));
        }

        if (Effect is null)
        {
            throw new ArgumentException("A card needs an effect.", nameof(Effect));
        }

        if (MaxCopies is < MinCopies or > MaxCopiesAllowed)
        {
            throw new ArgumentException(
                $"A card may stack between {MinCopies} and {MaxCopiesAllowed} copies.",
                nameof(MaxCopies));
        }

        if (string.Equals(IncompatibleWith, Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("A card cannot be incompatible with itself.", nameof(IncompatibleWith));
        }
    }

    /// <summary>
    /// Returns a value indicating whether this card can be held together with <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other card.</param>
    /// <returns><see langword="true"/> when the pair is legal in one run.</returns>
    public bool IsCompatibleWith(Card other) =>
        other is not null &&
        !string.Equals(IncompatibleWith, other.Id, StringComparison.Ordinal) &&
        !string.Equals(other.IncompatibleWith, Id, StringComparison.Ordinal);
}

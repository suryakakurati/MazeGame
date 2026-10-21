using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The cards a run can be offered, as data.
/// </summary>
/// <remarks>
/// <para>
/// Thirteen cards from the planned catalog are shipped here: the ones whose trade is expressible
/// with the rules the game already has. The movement cards (Light Feet, Momentum, Sprint Lungs,
/// Hollow Bones) arrived with timed player steps, since before them there was no step time to
/// trade against. The curse pair (The Debt plus Quiet) still needs the rare-with-a-price
/// machinery, so it waits rather than shipping as a dead card.
/// </para>
/// <para>
/// Every entry names its price in the same sentence as its benefit, and every entry has a test
/// asserting both halves through <see cref="RunModifiers"/>.
/// </para>
/// </remarks>
public static class CardCatalog
{
    /// <summary>Gets how many cards the shipped catalog holds.</summary>
    public const int ShippedCount = 13;

    private static readonly Card[] Cards =
    [
        new()
        {
            Id = "wide-eyes",
            Name = "Wide Eyes",
            Tier = CardTier.Common,
            FaceText = "Vision +2, but the killer's light reaches 3 steps further, so it notices you sooner.",
            Effect = new CardEffect { VisionRadius = 2, EnemyAuraDistance = 3 },
        },
        new()
        {
            Id = "deep-roots",
            Name = "Deep Roots",
            Tier = CardTier.Common,
            FaceText = "Seals hold for 18 seconds instead of 10, but you carry one barrier fewer.",
            Effect = new CardEffect { BarrierSealSeconds = 18, BarrierBudget = -1 },
            IncompatibleWith = "thornwright",
        },
        new()
        {
            Id = "thornwright",
            Name = "Thornwright",
            Tier = CardTier.Common,
            FaceText = "One extra barrier, but every seal cracks after 4 seconds.",
            Effect = new CardEffect { BarrierBudget = 1, BarrierSealSeconds = 4 },
            IncompatibleWith = "deep-roots",
        },
        new()
        {
            Id = "blood-pact",
            Name = "Blood Pact",
            Tier = CardTier.Uncommon,
            FaceText = "One more heart, but the killer gives you 12% less time between steps.",
            Effect = new CardEffect { MaxHearts = 1, EnemyStepScale = 0.88 },
        },
        new()
        {
            Id = "second-wind",
            Name = "Second Wind",
            Tier = CardTier.Common,
            FaceText = "Heal one heart now, but your torch loses a step for the rest of the run.",
            Effect = new CardEffect { HealNow = 1, VisionRadius = -1 },
        },
        new()
        {
            Id = "bait",
            Name = "Bait",
            Tier = CardTier.Common,
            FaceText = "A wisp stuns the killer for a second when it reaches it, but you carry one wisp fewer.",
            Effect = new CardEffect { LureStunSeconds = 1, LureCount = -1 },
        },
        new()
        {
            Id = "smoke-wisp",
            Name = "Smoke Wisp",
            Tier = CardTier.Uncommon,
            FaceText = "Wisps pull the killer for 10 seconds, but your torch shrinks by 2 steps while one burns.",
            Effect = new CardEffect { LureSeconds = 10, LureVisionPenalty = 2 },
        },
        new()
        {
            Id = "trophy-hunter",
            Name = "Trophy Hunter",
            Tier = CardTier.Rare,
            FaceText = "Heal a heart on every floor clear, but every floor from now on demands one more key.",
            Effect = new CardEffect { HealOnFloorClear = 1, KeyCount = 1 },
        },
        new()
        {
            Id = "light-feet",
            Name = "Light Feet",
            Tier = CardTier.Uncommon,
            FaceText = "You walk 15% faster, but you can hold one heart fewer.",
            Effect = new CardEffect { PlayerStepScale = 0.85, MaxHearts = -1 },
        },
        new()
        {
            Id = "momentum",
            Name = "Momentum",
            Tier = CardTier.Uncommon,
            FaceText = "Each straight step makes you faster, up to 25%, but a turn slows you by 15% for 2 seconds.",
            Effect = new CardEffect
            {
                MomentumRampPerStep = 0.05,
                MomentumMaxBonus = 0.25,
                TurnPenaltyScale = 0.15,
                TurnPenaltySeconds = 2.0,
            },
        },
        new()
        {
            Id = "sprint-lungs",
            Name = "Sprint Lungs",
            Tier = CardTier.Common,
            FaceText = "Your sprint lasts half again as long, but the killer's light reaches 2 steps further for the rest of the run.",
            Effect = new CardEffect { SprintStaminaScale = 1.5, EnemyAuraDistance = 2 },
        },
        new()
        {
            Id = "hollow-bones",
            Name = "Hollow Bones",
            Tier = CardTier.Uncommon,
            FaceText = "After a hit you are safe for 3 seconds instead of a second and a half, but every step you take is 25% slower.",
            Effect = new CardEffect { InvulnerabilitySeconds = 1.5, PlayerStepScale = 1.25 },
        },
        new()
        {
            Id = "lucky-find",
            Name = "Lucky Find",
            Tier = CardTier.Common,
            FaceText = "The next draft offers four cards, but the one after offers only two.",
            Effect = new CardEffect { NextDraftOfferBonus = 1, FollowingDraftOfferBonus = -1 },

            // The bonus lands in the next draft and the price in the one after it, so a run needs
            // two more drafts for the trade to exist at all.
            DraftsNeededAfterThisOne = 2,
        },
    ];

    /// <summary>Gets the shipped cards, in catalog order, as a read-only list.</summary>
    public static IReadOnlyList<Card> All { get; } = new ReadOnlyCollection<Card>(Cards);

    /// <summary>
    /// Returns the card with <paramref name="id"/>, or <see langword="null"/> when the id is not
    /// in the shipped catalog.
    /// </summary>
    /// <param name="id">The card's stable id.</param>
    /// <returns>The matching card, or <see langword="null"/>.</returns>
    public static Card? ById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var card in Cards)
        {
            if (string.Equals(card.Id, id, StringComparison.Ordinal))
            {
                return card;
            }
        }

        return null;
    }
}

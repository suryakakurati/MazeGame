using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The cards a run can be offered, as data.
/// </summary>
/// <remarks>
/// <para>
/// Twenty-three cards from the planned catalog are shipped here: the ones whose trade is
/// expressible with the rules the game already has. The movement cards (Light Feet, Momentum,
/// Sprint Lungs, Hollow Bones) arrived with timed player steps, the four powerup cards (Blink
/// Step, Long Veil, Lantern Oil, Quick Hands) with the five-verb kit, and the second wave (from
/// Featherfall to Slow Pursuit) with the rule hooks that let a card change a catch, a pickup or a
/// powerup's duration. No card trades on a verb the player cannot use, and no card is a pure
/// upgrade.
/// </para>
/// <para>
/// Eight cards are stackable (<see cref="Card.MaxCopies"/> above one): the numeric ones whose
/// downside compounds the same way its upside does, so a second copy is a sharper version of the
/// same bargain rather than a free upgrade. That is what keeps the draft's eligible pool alive to
/// the nineteenth offer of a run.
/// </para>
/// <para>
/// Every entry names its price in the same sentence as its benefit, and every entry has a test
/// asserting both halves through <see cref="RunModifiers"/>.
/// </para>
/// </remarks>
public static class CardCatalog
{
    /// <summary>Gets how many cards the shipped catalog holds.</summary>
    public const int ShippedCount = 23;

    private static readonly Card[] Cards =
    [
        new()
        {
            Id = "wide-eyes",
            Name = "Wide Eyes",
            Tier = CardTier.Common,
            MaxCopies = 2,
            FaceText = "Vision +2, but the killer's light reaches 3 steps further, so it notices you sooner.",
            Effect = new CardEffect { VisionRadius = 2, EnemyAuraDistance = 3 },
        },
        new()
        {
            Id = "blink-step",
            Name = "Blink Step",
            Tier = CardTier.Common,
            FaceText = "One more blink each floor, but the killer's light reaches 2 steps further.",
            Effect = new CardEffect { BlinkCharges = 1, EnemyAuraDistance = 2 },
        },
        new()
        {
            Id = "long-veil",
            Name = "Long Veil",
            Tier = CardTier.Common,
            FaceText = "Two more veils each floor, but every step you take is 10% slower.",
            Effect = new CardEffect { VeilCharges = 2, PlayerStepScale = 1.10 },
        },
        new()
        {
            Id = "blood-pact",
            Name = "Blood Pact",
            Tier = CardTier.Uncommon,
            MaxCopies = 2,
            FaceText = "Heal one heart now, but the killer gives you 12% less time between steps.",
            Effect = new CardEffect { HealNow = 1, MaxHearts = 1, EnemyStepScale = 0.88 },
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
            Id = "lantern-oil",
            Name = "Lantern Oil",
            Tier = CardTier.Uncommon,
            FaceText = "One more lantern each floor, but your torch loses a step for the rest of the run.",
            Effect = new CardEffect { LanternCharges = 1, VisionRadius = -1 },
        },
        new()
        {
            Id = "quick-hands",
            Name = "Quick Hands",
            Tier = CardTier.Uncommon,
            MaxCopies = 3,
            FaceText = "Two more shoves each floor, but the killer gives you 8% less time between steps.",
            Effect = new CardEffect { ShoveCharges = 2, EnemyStepScale = 0.92 },
        },
        new()
        {
            Id = "featherfall",
            Name = "Featherfall",
            Tier = CardTier.Rare,
            FaceText = "The first catch each floor costs no heart, but your torch loses a step for the rest of the run.",
            Effect = new CardEffect { FirstCatchFree = true, MaxHearts = -1, VisionRadius = -1 },
        },
        new()
        {
            Id = "magnetic-keys",
            Name = "Greedy Grasp",
            Tier = CardTier.Uncommon,
            FaceText = "Blooms within one step fly to you, but every floor from now on asks for one more bloom.",
            Effect = new CardEffect { MagneticKeys = true, KeyCount = 1 },
        },
        new()
        {
            Id = "bloom",
            Name = "Bloom",
            Tier = CardTier.Rare,
            FaceText = "One fatal catch each floor leaves you at a heart, but your torch loses two steps.",
            Effect = new CardEffect { ReviveOnce = true, VisionRadius = -2 },
        },
        new()
        {
            Id = "blink-ledger",
            Name = "Blink Ledger",
            Tier = CardTier.Uncommon,
            FaceText = "Every bloom you pick gives back a blink, but the killer gains 8% speed.",
            Effect = new CardEffect { BlinkRefundOnKey = true, EnemyStepScale = 0.92 },
        },
        new()
        {
            Id = "ghost-veil",
            Name = "Ghost Veil",
            Tier = CardTier.Common,
            FaceText = "Your veil lasts two seconds longer, but the killer's light reaches two steps further.",
            Effect = new CardEffect { VeilDurationSeconds = 2, EnemyAuraDistance = 2 },
        },
        new()
        {
            Id = "long-drag",
            Name = "Long Drag",
            Tier = CardTier.Uncommon,
            FaceText = "Drag lasts two seconds longer, but its hangover lasts three seconds longer too.",
            Effect = new CardEffect { DragSeconds = 2, DragPenaltySeconds = 3 },
        },
        new()
        {
            Id = "wide-light",
            Name = "Wide Light",
            Tier = CardTier.Common,
            FaceText = "You can throw the lantern two cells further, but you carry one veil fewer.",
            Effect = new CardEffect { LanternRange = 2, VeilCharges = -1 },
        },
        new()
        {
            Id = "glass-cannon",
            Name = "Glass Cannon",
            Tier = CardTier.Rare,
            MaxCopies = 2,
            FaceText = "You move 40% faster, but every catch costs two hearts instead of one.",
            Effect = new CardEffect { PlayerStepScale = 0.60, CatchCost = 1 },
        },
        new()
        {
            Id = "debt-of-sight",
            Name = "Debt of Sight",
            Tier = CardTier.Rare,
            FaceText = "Heal two hearts now, but your torch loses two steps for the rest of the run.",
            Effect = new CardEffect { HealNow = 2, MaxHearts = 2, VisionRadius = -2 },
        },
        new()
        {
            Id = "slow-pursuit",
            Name = "Slow Pursuit",
            Tier = CardTier.Uncommon,
            MaxCopies = 3,
            FaceText = "The killer moves 12% slower, but every floor asks for one more bloom.",
            Effect = new CardEffect { EnemyStepScale = 1.12, KeyCount = 1 },
        },
        new()
        {
            Id = "trophy-hunter",
            Name = "Trophy Hunter",
            Tier = CardTier.Rare,
            FaceText = "Heal a heart on every floor clear, but every floor from now on asks for one more bloom.",
            Effect = new CardEffect { HealOnFloorClear = 1, KeyCount = 1 },
        },
        new()
        {
            Id = "light-feet",
            Name = "Light Feet",
            Tier = CardTier.Common,
            MaxCopies = 3,
            FaceText = "You walk 35% faster, but the killer gives you 8% less time between steps.",
            Effect = new CardEffect { PlayerStepScale = 0.65, MaxHearts = -1, EnemyStepScale = 0.92 },
        },
        new()
        {
            Id = "momentum",
            Name = "Momentum",
            Tier = CardTier.Uncommon,
            MaxCopies = 2,
            FaceText = "Each straight step makes you faster, up to 35%, but a turn slows you by 15% for 2 seconds.",
            Effect = new CardEffect
            {
                MomentumRampPerStep = 0.06,
                MomentumMaxBonus = 0.35,
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
            MaxCopies = 2,
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

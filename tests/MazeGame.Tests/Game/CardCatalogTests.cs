using System;
using System.Collections.Generic;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the shipped catalog as content: the ids are unique, every face states its trade, and
/// the compatibility marks point at real cards.
/// </summary>
/// <remarks>
/// The interesting test is the face one. A card whose drawback is only discoverable by playing is
/// a hidden modifier, which the design forbids; every shipped face joins its halves with "but" and
/// ends in a full stop, so the rule is checked mechanically rather than by reading.
/// </remarks>
public sealed class CardCatalogTests
{
    [Fact]
    public void Catalog_ShipsTheExpectedNumberOfCards()
    {
        Assert.Equal(CardCatalog.ShippedCount, CardCatalog.All.Count);
        Assert.Equal(23, CardCatalog.ShippedCount);
    }

    [Fact]
    public void Catalog_HasUniqueIdsAndEveryCardValidates()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in CardCatalog.All)
        {
            card.Validate();
            Assert.True(ids.Add(card.Id), $"duplicate card id: {card.Id}");
            Assert.False(string.IsNullOrWhiteSpace(card.FaceText));
        }
    }

    [Fact]
    public void Catalog_IdsAreLowercaseAndHyphenated()
    {
        // Saves and the incompatibility marks use ids, so their shape is worth pinning down.
        foreach (var card in CardCatalog.All)
        {
            Assert.Equal(card.Id.ToLowerInvariant(), card.Id);
            Assert.DoesNotContain(' ', card.Id);
        }
    }

    [Fact]
    public void EveryFace_StatesBothHalvesInOneSentence()
    {
        foreach (var card in CardCatalog.All)
        {
            Assert.Contains(" but ", card.FaceText, StringComparison.Ordinal);
            Assert.EndsWith(".", card.FaceText, StringComparison.Ordinal);

            // One sentence means one full stop, at the end: no fine print on a second line.
            Assert.Single(card.FaceText.Split('.', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    [Fact]
    public void EveryFace_IsShortEnoughToDrawOnACard()
    {
        foreach (var card in CardCatalog.All)
        {
            Assert.InRange(card.FaceText.Length, 20, 140);
        }
    }

    [Fact]
    public void ById_ReturnsTheMatchingCard()
    {
        var card = Assert.IsType<Card>(CardCatalog.ById("wide-eyes"));

        Assert.Equal("Wide Eyes", card.Name);
        Assert.Equal(CardTier.Common, card.Tier);
    }

    [Fact]
    public void ById_WithAnUnknownId_ReturnsNull()
    {
        Assert.Null(CardCatalog.ById("no-such-card"));
        Assert.Null(CardCatalog.ById(""));
        Assert.Null(CardCatalog.ById(null!));
    }

    [Fact]
    public void Incompatibility_MarksOnlyRealSymmetricPairs()
    {
        // No shipped card sets an absolute that another could contradict, so the catalog is
        // empty of marks; the rule itself is still checked so a future pair has to be symmetric.
        foreach (var card in CardCatalog.All)
        {
            if (card.IncompatibleWith is not { } otherId)
            {
                continue;
            }

            var other = CardCatalog.ById(otherId);
            Assert.NotNull(other);
            Assert.Equal(card.Id, other!.IncompatibleWith);
            Assert.False(card.IsCompatibleWith(other));
        }

        Assert.DoesNotContain(CardCatalog.All, card => card.IncompatibleWith is not null);
    }

    [Fact]
    public void OnlyLuckyFind_NeedsDraftsLeftToPayForItself()
    {
        foreach (var card in CardCatalog.All)
        {
            var expected = string.Equals(card.Id, "lucky-find", StringComparison.Ordinal) ? 2 : 0;

            Assert.Equal(expected, card.DraftsNeededAfterThisOne);
        }
    }

    [Fact]
    public void EveryCardChangesAtLeastOneFloorSettingOrOneDraft()
    {
        foreach (var card in CardCatalog.All)
        {
            var effect = card.Effect;

            var changesSomething =
                effect.VisionRadius != 0 ||
                effect.EnemyAuraDistance != 0 ||
                effect.BarrierBudget != 0 ||
                effect.BarrierSealSeconds is not null ||
                Math.Abs(effect.EnemyStepScale - 1.0) > 0.0001 ||
                Math.Abs(effect.PlayerStepScale - 1.0) > 0.0001 ||
                effect.InvulnerabilitySeconds != 0 ||
                Math.Abs(effect.SprintStaminaScale - 1.0) > 0.0001 ||
                effect.MomentumRampPerStep != 0 ||
                effect.MomentumMaxBonus != 0 ||
                effect.TurnPenaltyScale != 0 ||
                effect.TurnPenaltySeconds != 0 ||
                effect.MaxHearts != 0 ||
                effect.KeyCount != 0 ||
                effect.LureCount != 0 ||
                effect.LureSeconds is not null ||
                effect.LureStunSeconds != 0 ||
                effect.LureVisionPenalty != 0 ||
                effect.BlinkCharges != 0 ||
                effect.VeilCharges != 0 ||
                effect.DragCharges != 0 ||
                effect.LanternCharges != 0 ||
                effect.ShoveCharges != 0 ||
                effect.FirstCatchFree ||
                effect.MagneticKeys ||
                effect.ReviveOnce ||
                effect.BlinkRefundOnKey ||
                effect.CatchCost != 0 ||
                effect.VeilDurationSeconds != 0 ||
                effect.DragSeconds != 0 ||
                effect.DragPenaltySeconds != 0 ||
                effect.LanternRange != 0 ||
                effect.HealNow != 0 ||
                effect.HealOnFloorClear != 0 ||
                effect.NextDraftOfferBonus != 0 ||
                effect.FollowingDraftOfferBonus != 0;

            Assert.True(changesSomething, $"{card.Id} changes nothing");
        }
    }

    [Fact]
    public void NoShippedCardIsACurse()
    {
        // Curses are only ever the price of a rare, and that pairing machinery is a later phase.
        // Shipping one now would put a pure cost in the offers, which the design forbids.
        foreach (var card in CardCatalog.All)
        {
            Assert.NotEqual(CardTier.Curse, card.Tier);
        }
    }
}

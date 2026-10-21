using System;
using System.Linq;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the composition of cards into a floor's settings: every card's two halves, the
/// immutability of the totals, and the clamps that keep a stacked run playable.
/// </summary>
/// <remarks>
/// These are the "both halves" tests the catalog requires. Each one asserts the upside and the
/// drawback of the same card against the config a floor would actually be built from, so a card
/// whose text drifted away from its numbers fails here.
/// </remarks>
public sealed class RunModifiersTests
{
    [Fact]
    public void None_LeavesTheFloorExactlyAsItWas()
    {
        var floor = Floor();

        Assert.Equal(floor, RunModifiers.None.Apply(floor));
    }

    [Fact]
    public void With_ReturnsANewSetAndLeavesTheOldOneAlone()
    {
        var before = RunModifiers.None;
        var after = before.With(Card("wide-eyes"));

        Assert.Empty(before.Cards);
        Assert.Single(after.Cards);
        Assert.Equal(0, before.VisionRadius);
        Assert.Equal(2, after.VisionRadius);
    }

    [Fact]
    public void WideEyes_BuysVisionAndSellsCover()
    {
        var applied = RunModifiers.None.With(Card("wide-eyes")).Apply(Floor());

        Assert.Equal(5, applied.VisionRadius);
        Assert.Equal(9, applied.EnemyAuraDistance);
    }

    [Fact]
    public void DeepRoots_LengthensSealsAndCostsABarrier()
    {
        var applied = RunModifiers.None.With(Card("deep-roots")).Apply(Floor());

        Assert.Equal(18.0, applied.BarrierSealSeconds, precision: 9);
        Assert.Equal(2, applied.BarrierBudget);
    }

    [Fact]
    public void Thornwright_BuysABarrierAndShortensSeals()
    {
        var applied = RunModifiers.None.With(Card("thornwright")).Apply(Floor());

        Assert.Equal(4, applied.BarrierBudget);
        Assert.Equal(4.0, applied.BarrierSealSeconds, precision: 9);
    }

    [Fact]
    public void BloodPact_BuysAHeartAndSpeedsTheKillerUp()
    {
        var applied = RunModifiers.None.With(Card("blood-pact")).Apply(Floor());

        Assert.Equal(4, applied.MaxHearts);
        Assert.Equal(0.6 * 0.88, applied.EnemyStepSeconds, precision: 9);
        Assert.Equal(3, applied.VisionRadius);
    }

    [Fact]
    public void SecondWind_HealsNowAndDimsTheTorch()
    {
        var card = Card("second-wind");
        var modifiers = RunModifiers.None.With(card);

        Assert.Equal(1, card.Effect.HealNow);
        Assert.Equal(2, modifiers.Apply(Floor()).VisionRadius);

        // The heal is a one-shot spent on the next floor, not a per-clear effect.
        Assert.Equal(0, modifiers.HealOnFloorClear);
    }

    [Fact]
    public void Bait_MakesTheWispStunAndCostsACharge()
    {
        var applied = RunModifiers.None.With(Card("bait")).Apply(Floor());

        Assert.Equal(1.0, applied.LureStunSeconds, precision: 9);
        Assert.Equal(1, applied.LureCount);
        Assert.Equal(6.0, applied.LureSeconds, precision: 9);
    }

    [Fact]
    public void SmokeWisp_PullsLongerAndNarrowsTheTorchWhileItBurns()
    {
        var applied = RunModifiers.None.With(Card("smoke-wisp")).Apply(Floor());

        Assert.Equal(10.0, applied.LureSeconds, precision: 9);
        Assert.Equal(2, applied.LureVisionPenalty);
        Assert.Equal(2, applied.LureCount);
        Assert.Equal(3, applied.VisionRadius);
    }

    [Fact]
    public void TrophyHunter_HealsOnEveryClearAndDemandsAKey()
    {
        var card = Card("trophy-hunter");
        var modifiers = RunModifiers.None.With(card);
        var applied = modifiers.Apply(Floor());

        Assert.Equal(4, applied.KeyCount);
        Assert.Equal(1, modifiers.HealOnFloorClear);
    }

    [Fact]
    public void LuckyFind_ChangesTheDraftsAndNotTheFloor()
    {
        var card = Card("lucky-find");

        Assert.Equal(1, card.Effect.NextDraftOfferBonus);
        Assert.Equal(-1, card.Effect.FollowingDraftOfferBonus);
        Assert.Equal(Floor(), RunModifiers.None.With(card).Apply(Floor()));
    }

    [Fact]
    public void Apply_ClampsVisionAtOneStep()
    {
        // Two dimming cards leave one step of sight; a third is clamped rather than blinding the
        // player, because the floor's own zero-vision setting is not the same thing as a card
        // taking sight away.
        var modifiers = RunModifiers.None
            .With(Card("second-wind"))
            .With(Card("second-wind"))
            .With(Card("second-wind"));

        Assert.Equal(1, modifiers.Apply(Floor()).VisionRadius);
    }

    [Fact]
    public void LightFeet_BuysSpeedAndCostsAHeartSlot()
    {
        var applied = RunModifiers.None.With(Card("light-feet")).Apply(Floor() with { StartingHearts = 2 });

        Assert.Equal(0.11 * 0.85, applied.PlayerStepSeconds, precision: 9);

        // The slot is taken from the ceiling, never from a heart the player is already holding:
        // the clamp is what keeps a card from throwing a wound at the player.
        Assert.Equal(2, applied.MaxHearts);
        Assert.Equal(3, RunModifiers.None.With(Card("light-feet")).Apply(Floor()).MaxHearts);
    }

    [Fact]
    public void Momentum_TurnsOnTheRampAndItsTurnPenalty()
    {
        var applied = RunModifiers.None.With(Card("momentum")).Apply(Floor());

        Assert.Equal(0.05, applied.MomentumRampPerStep, precision: 9);
        Assert.Equal(0.25, applied.MomentumMaxBonus, precision: 9);
        Assert.Equal(0.15, applied.TurnPenaltyScale, precision: 9);
        Assert.Equal(2.0, applied.TurnPenaltySeconds, precision: 9);
    }

    [Fact]
    public void SprintLungs_LengthensTheBarAndFeedsTheKillerLight()
    {
        var applied = RunModifiers.None.With(Card("sprint-lungs")).Apply(Floor());

        Assert.Equal(1.8, applied.SprintStaminaSeconds, precision: 9);
        Assert.Equal(8, applied.EnemyAuraDistance);
    }

    [Fact]
    public void HollowBones_LengthensTheGraceAndSlowsEveryStep()
    {
        var applied = RunModifiers.None.With(Card("hollow-bones")).Apply(Floor());

        Assert.Equal(3.0, applied.InvulnerabilitySeconds, precision: 9);
        Assert.Equal(0.11 * 1.25, applied.PlayerStepSeconds, precision: 9);
    }

    [Fact]
    public void SpeedCards_MultiplyAgainstEachOther()
    {
        var applied = RunModifiers.None
            .With(Card("light-feet"))
            .With(Card("hollow-bones"))
            .Apply(Floor());

        // 15% faster and 25% slower roughly cancel, which is the honest result of holding both.
        Assert.Equal(0.11 * 0.85 * 1.25, applied.PlayerStepSeconds, precision: 9);
    }

    [Fact]
    public void Apply_LeavesAnInstantFloorInstant()
    {
        // Speed cards must not conjure a cadence out of a zero-step floor: that is the seam the
        // rule tests and the tools rely on.
        var instant = Floor() with { PlayerStepSeconds = 0 };

        var applied = RunModifiers.None.With(Card("light-feet")).Apply(instant);

        Assert.Equal(0, applied.PlayerStepSeconds, precision: 9);
    }

    [Fact]
    public void TwoPaceCards_MultiplyRatherThanAdd()
    {
        var hasty = CustomCard(new CardEffect { EnemyStepScale = 0.8 });
        var applied = RunModifiers.None.With(hasty).With(hasty).Apply(Floor());

        // Two 20% speed-ups are 36%, not 40%: pace is a multiplier, and it stays one.
        Assert.Equal(0.6 * 0.8 * 0.8, applied.EnemyStepSeconds, precision: 9);
    }

    [Fact]
    public void Apply_NeverLetsABudgetOrCountGoNegative()
    {
        var hungry = CustomCard(new CardEffect { BarrierBudget = -9, LureCount = -9, KeyCount = -9 });

        var applied = RunModifiers.None.With(hungry).Apply(Floor());

        Assert.Equal(0, applied.BarrierBudget);
        Assert.Equal(0, applied.LureCount);
        Assert.Equal(0, applied.KeyCount);
    }

    [Fact]
    public void Apply_ClampsTheKillerAtTheMinimumStep()
    {
        // Twenty Blood Pacts multiply the step time down past the floor; the clamp is what keeps
        // the chase playable instead of instantaneous.
        var modifiers = Enumerable
            .Range(0, 20)
            .Aggregate(RunModifiers.None, (totals, _) => totals.With(Card("blood-pact")));

        var applied = modifiers.Apply(Floor());

        Assert.Equal(RunModifiers.MinimumStepSeconds, applied.EnemyStepSeconds, precision: 9);
    }

    [Fact]
    public void Apply_NeverPutsTheCeilingBelowTheHeartsCarried()
    {
        var cruel = CustomCard(new CardEffect { MaxHearts = -5 });
        var woundedFloor = Floor() with { StartingHearts = 2, MaxHearts = 3 };

        var applied = RunModifiers.None.With(cruel).Apply(woundedFloor);

        // The floor must stay buildable: a ceiling under StartingHearts would throw.
        Assert.Equal(2, applied.MaxHearts);
        Assert.Equal(2, applied.StartingHearts);
    }

    [Fact]
    public void Apply_KeepsTheFloorsOwnShapeAndSeed()
    {
        var applied = RunModifiers.None.With(Card("wide-eyes")).Apply(Floor());

        Assert.Equal(15, applied.Rows);
        Assert.Equal(15, applied.Columns);
        Assert.Equal(42, applied.Seed);
        Assert.Equal(1.5, applied.InvulnerabilitySeconds, precision: 9);
        Assert.Equal(8, applied.RespawnDistance);
    }

    [Fact]
    public void Apply_DoesNotMutateTheFloorConfig()
    {
        var floor = Floor();

        RunModifiers.None.With(Card("wide-eyes")).Apply(floor);

        Assert.Equal(3, floor.VisionRadius);
        Assert.Equal(6, floor.EnemyAuraDistance);
    }

    [Fact]
    public void Describe_WithNoCards_SaysSo()
    {
        Assert.Equal("NO CARDS", RunModifiers.None.Describe());
    }

    [Fact]
    public void Describe_ListsTheTotalsOfTheHeldCards()
    {
        var text = RunModifiers.None
            .With(Card("wide-eyes"))
            .With(Card("blood-pact"))
            .Describe();

        Assert.Contains("VISION +2", text, StringComparison.Ordinal);
        Assert.Contains("AURA +3", text, StringComparison.Ordinal);
        Assert.Contains("HEARTS +1", text, StringComparison.Ordinal);
        Assert.Contains("KILLER PACE -12%", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ShowsAnAbsoluteSealTime()
    {
        var text = RunModifiers.None.With(Card("deep-roots")).Describe();

        Assert.Contains("SEALS 18s", text, StringComparison.Ordinal);
        Assert.Contains("BARRIERS -1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_RejectsInvalidCards()
    {
        var blank = CustomCard(new CardEffect { VisionRadius = 1 }) with { Id = " " };

        Assert.Throws<ArgumentException>(() => RunModifiers.None.With(blank));
        Assert.Throws<ArgumentNullException>(() => RunModifiers.None.With(null!));
    }

    /// <summary>The base floor these tests compose over: the shipped numbers.</summary>
    private static GameConfig Floor() => new()
    {
        Rows = 15,
        Columns = 15,
        KeyCount = 3,
        BarrierBudget = 3,
        BarrierSealSeconds = 10.0,
        EnemyStepSeconds = 0.6,
        PlayerStepSeconds = 0.11,
        VisionRadius = 3,
        EnemyAuraDistance = 6,
        EnemyAuraRadius = 2,
        LureCount = 2,
        LureSeconds = 6.0,
        StartingHearts = 3,
        MaxHearts = 3,
        Seed = 42,
    };

    private static Card Card(string id) => CardCatalog.ById(id)!;

    /// <summary>A card with an arbitrary effect, for testing the clamps.</summary>
    private static Card CustomCard(CardEffect effect) => new()
    {
        Id = "test-card",
        Name = "Test Card",
        Tier = CardTier.Common,
        FaceText = "Something useful, but something expensive.",
        Effect = effect,
    };
}

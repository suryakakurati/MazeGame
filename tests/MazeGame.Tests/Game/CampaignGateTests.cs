using System.Collections.Generic;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the two descent gates: the run cannot descend past floor 8 or floor 14 until a
/// condition has been chosen, the offer excludes what the run already carries, and the chosen
/// rules reach every later floor.
/// </summary>
/// <remarks>
/// The floors are 1x6 corridors with no keys, so clearing one is a single step east. That keeps
/// the test about the gate rather than about maze traversal, and it lets it walk the whole
/// fourteen-floor run quickly.
/// </remarks>
public sealed class CampaignGateTests
{
    [Fact]
    public void Gate_RefusesToAdvanceUntilAConditionIsChosen()
    {
        var campaign = CampaignOf(8);

        WalkTo(campaign, 7);

        var offer = campaign.ConditionOffer;
        Assert.NotNull(offer);
        Assert.Equal(2, offer!.Options.Count);
        Assert.False(campaign.TryAdvanceFloor());
        Assert.Empty(campaign.ActiveConditions);

        Assert.True(campaign.TakeCondition(offer.Options[0]));
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(8, campaign.FloorNumber);
        Assert.Single(campaign.ActiveConditions);
    }

    [Fact]
    public void SecondGate_ExcludesTheFirstChoiceAndTheRunCarriesBoth()
    {
        var campaign = CampaignOf(14);

        WalkTo(campaign, 13);

        var first = campaign.ActiveConditions[0];
        var offer = campaign.ConditionOffer;
        Assert.NotNull(offer);
        Assert.DoesNotContain(first, offer!.Options);

        Assert.True(campaign.TakeCondition(offer.Options[0]));
        Assert.True(campaign.TryAdvanceFloor());

        Assert.Equal(14, campaign.FloorNumber);
        Assert.Equal(2, campaign.ActiveConditions.Count);
        Assert.Equal(campaign.ActiveConditions, campaign.Floor.Config.Conditions);
    }

    [Fact]
    public void Offer_IsSeededAndNeverSuggestsAnActiveCondition()
    {
        var active = new[] { DescentCondition.Eclipse };

        var first = DescentConditionCatalog.Offer(31, 8, active);
        var again = DescentConditionCatalog.Offer(31, 8, active);

        Assert.Equal(first, again);
        Assert.Equal(2, first.Count);
        Assert.DoesNotContain(DescentCondition.Eclipse, first);
        Assert.Equal(first.Count, first.Distinct().Count());
    }

    /// <summary>Walks to <paramref name="lastFloor"/> and clears every floor on the way.</summary>
    private static void WalkTo(Campaign campaign, int lastFloor)
    {
        while (campaign.FloorNumber < lastFloor)
        {
            Assert.True(campaign.Floor.TryMove(Direction.East));
            Assert.True(campaign.IsFloorCleared);

            ResolveDraft(campaign);

            if (campaign.ConditionOffer is { IsResolved: false } gate)
            {
                Assert.True(campaign.TakeCondition(gate.Options[0]));
            }

            Assert.True(campaign.TryAdvanceFloor());
        }

        // Clear the last floor too, but do not advance: the run may end there. Its draft still
        // has to be resolved before the gate can be taken, exactly as a frontend would.
        Assert.True(campaign.Floor.TryMove(Direction.East));

        ResolveDraft(campaign);
    }

    /// <summary>Takes the first offer, or skips when the draft is legitimately empty.</summary>
    private static void ResolveDraft(Campaign campaign)
    {
        var draft = campaign.Draft;
        Assert.NotNull(draft);

        if (draft!.Offers.Count > 0)
        {
            Assert.True(campaign.TakeCard(draft.Offers[0]));
        }
        else
        {
            Assert.True(campaign.SkipDraft());
        }
    }

    /// <summary>A campaign of corridor floors that need no keys.</summary>
    private static Campaign CampaignOf(int floorCount)
    {
        var floors = new List<FloorDefinition>();

        for (var number = 1; number <= floorCount; number++)
        {
            floors.Add(new FloorDefinition
            {
                Number = number,
                Name = $"Floor {number}",
                ThemeId = "test",
                Rows = 1,
                Columns = 6,
                KeyCount = 0,
                EnemyStepSeconds = 1.0,
                MaxHearts = 3,
            });
        }

        return new Campaign(floors, BuildFloor, runSeed: 5);
    }

    private static GameState BuildFloor(
        FloorDefinition floor,
        int hearts,
        RunModifiers modifiers,
        IReadOnlyList<DescentCondition> conditions) =>
        TestLevels.GameOver(
            TestLevels.Corridor(),
            new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = 0,
                EnemyStepSeconds = 1.0,
                StartingHearts = hearts,
                MaxHearts = Math.Max(3, hearts),
                Conditions = conditions,
                Seed = null,
            });
}

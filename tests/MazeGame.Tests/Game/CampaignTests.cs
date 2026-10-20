using System;
using System.Collections.Generic;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the run object: floor order, the fact that a cleared floor is progress rather than
/// victory, hearts carried between floors, and the two ways a run can end.
/// </summary>
/// <remarks>
/// The builder here makes a 1x3 corridor per floor — player (0,0), exit (0,1), killer (0,2) — so a
/// floor can be cleared by one step east and lost by two ticks. That keeps these tests about the
/// campaign, not about maze generation.
/// </remarks>
public sealed class CampaignTests
{
    [Fact]
    public void NewCampaign_StartsOnTheFirstFloorWithFullHearts()
    {
        var campaign = CampaignOf(3);

        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(3, campaign.FloorCount);
        Assert.Equal(3, campaign.Hearts);
        Assert.Equal(3, campaign.MaxHearts);
        Assert.Equal(0, campaign.FloorsCleared);
        Assert.False(campaign.IsFloorCleared);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void ClearingAFloor_IsProgressNotVictory()
    {
        var campaign = CampaignOf(3);

        ClearFloor(campaign);

        Assert.True(campaign.IsFloorCleared);
        Assert.Equal(1, campaign.FloorsCleared);
        Assert.Equal(3, campaign.MaxHearts);
    }

    [Fact]
    public void TryAdvanceFloor_CarriesTheHeartsLeft()
    {
        var campaign = CampaignOf(3);

        // Two ticks: the killer catches the player once, costing one heart.
        campaign.Floor.Tick(1.0);
        campaign.Floor.Tick(1.0);
        Assert.Equal(2, campaign.Hearts);

        ClearFloor(campaign);

        Assert.True(campaign.TryAdvanceFloor());
        Assert.Equal(2, campaign.FloorNumber);
        Assert.Equal(2, campaign.Hearts);
        Assert.Equal(3, campaign.MaxHearts);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void TryAdvanceFloor_BeforeClearingTheFloor_ReturnsFalse()
    {
        var campaign = CampaignOf(3);

        Assert.False(campaign.TryAdvanceFloor());
        Assert.Equal(1, campaign.FloorNumber);
        Assert.Equal(CampaignStatus.InProgress, campaign.Status);
    }

    [Fact]
    public void ClearingTheLastFloor_WinsTheRun()
    {
        var campaign = CampaignOf(3);

        ClearFloor(campaign);
        Assert.True(campaign.TryAdvanceFloor());

        ClearFloor(campaign);
        Assert.True(campaign.TryAdvanceFloor());

        Assert.True(campaign.IsOnLastFloor);
        ClearFloor(campaign);

        Assert.Equal(3, campaign.FloorsCleared);
        Assert.Equal(CampaignStatus.Won, campaign.Status);
        Assert.False(campaign.TryAdvanceFloor());
    }

    [Fact]
    public void LosingTheLastHeart_EndsTheRunImmediately()
    {
        var fragileFloor = Floor(1) with { MaxHearts = 1 };
        var campaign = new Campaign([fragileFloor], BuildFloor);

        campaign.Floor.Tick(1.0);
        campaign.Floor.Tick(1.0);

        Assert.Equal(GameStatus.Caught, campaign.Floor.Status);
        Assert.Equal(0, campaign.Hearts);
        Assert.Equal(CampaignStatus.Lost, campaign.Status);
        Assert.False(campaign.TryAdvanceFloor());
    }

    [Fact]
    public void Campaign_SnapshotsItsFloorList()
    {
        var floors = new List<FloorDefinition>
        {
            Floor(1),
            Floor(2),
            Floor(3),
        };
        var campaign = new Campaign(floors, BuildFloor);

        floors.Clear();

        Assert.Equal(3, campaign.FloorCount);
        Assert.Equal(1, campaign.FloorNumber);
    }

    [Fact]
    public void Campaign_WithNoFloors_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Campaign(Array.Empty<FloorDefinition>(), BuildFloor));
    }

    [Fact]
    public void Campaign_WithFloorNumbersOutOfOrder_Throws()
    {
        var floors = new[] { Floor(1), Floor(3), Floor(2) };

        Assert.Throws<ArgumentException>(() => new Campaign(floors, BuildFloor));
    }

    [Fact]
    public void Campaign_WithAnInvalidFloor_Throws()
    {
        var invalid = Floor(1) with { Name = "  " };

        Assert.Throws<ArgumentException>(() => new Campaign([invalid], BuildFloor));
    }

    [Fact]
    public void Campaign_WhenTheBuilderReturnsNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new Campaign([Floor(1)], (_, _) => null!));
    }

    [Fact]
    public void Campaign_WithNullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Campaign(null!, BuildFloor));
        Assert.Throws<ArgumentNullException>(() => new Campaign([Floor(1)], null!));
    }

    /// <summary>A campaign of <paramref name="floorCount"/> corridor floors, each allowing 3 hearts.</summary>
    private static Campaign CampaignOf(int floorCount)
    {
        var floors = new List<FloorDefinition>();
        for (var number = 1; number <= floorCount; number++)
        {
            floors.Add(Floor(number));
        }

        return new Campaign(floors, BuildFloor);
    }

    private static FloorDefinition Floor(int number) => new()
    {
        Number = number,
        Name = $"Floor {number}",
        ThemeId = "test",
        Rows = 1,
        Columns = 3,
        KeyCount = 0,
        EnemyStepSeconds = 1.0,
        MaxHearts = 3,
    };

    /// <summary>
    /// Builds the 1x3 floor described above with the requested hearts. The exit is open with no
    /// keys, so stepping east clears it.
    /// </summary>
    private static GameState BuildFloor(FloorDefinition floor, int hearts) =>
        new(
            new GameConfig
            {
                Rows = 1,
                Columns = 3,
                KeyCount = 0,
                StartingHearts = hearts,
                MaxHearts = floor.MaxHearts,
                EnemyStepSeconds = 1.0,
                Seed = null,
            },
            TestLevels.Corridor(3),
            new DijkstraPathfinder(),
            new FixedRandom(0));

    private static void ClearFloor(Campaign campaign) =>
        Assert.True(campaign.Floor.TryMove(Direction.East));
}

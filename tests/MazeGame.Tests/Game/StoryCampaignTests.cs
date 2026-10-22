using System;
using System.Linq;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the run-side half of the story: which page a cleared floor turns, and what the garden
/// is worth while the run is being played.
/// </summary>
/// <remarks>
/// The garden is the run's only score, and it is not saved anywhere, so the numbers here are the
/// whole contract: blooms picked plus pages turned.
/// </remarks>
public sealed class StoryCampaignTests
{
    [Fact]
    public void AFreshRun_StartsOnPageOne_WithAnEmptyGarden()
    {
        var campaign = Campaign(0);

        Assert.Equal(1, campaign.CurrentPage.FloorNumber);
        Assert.Same(StoryCatalog.Page(1), campaign.CurrentPage);
        Assert.Equal(StoryMood.None, campaign.CurrentMood);
        Assert.Null(campaign.CurrentMoodLine);
        Assert.Equal(0, campaign.BloomsPicked);
        Assert.Equal(0, campaign.GardenCount);
    }

    [Fact]
    public void PickingABloom_GrowsTheGarden()
    {
        var campaign = Campaign(0);
        var bloom = campaign.Floor.Keys.First();

        Assert.True(WalkTo(campaign.Floor, bloom));
        Assert.Equal(GameStatus.InProgress, campaign.Floor.Status);

        Assert.Equal(campaign.Floor.KeysCollected, campaign.BloomsPicked);
        Assert.True(campaign.BloomsPicked >= 1);
        Assert.Equal(campaign.BloomsPicked, campaign.GardenCount);
    }

    [Fact]
    public void AClearedFloor_TurnsItsPage_AndAddsToTheGarden()
    {
        // A one-floor run with a hunter that never gets a step: the test is about the garden's
        // arithmetic, not about escaping, and a real floor-1 chase would make it a coin toss.
        var floor = new FloorDefinition
        {
            Number = 1,
            Name = "The Shallows",
            ThemeId = "shallows",
            KeyCount = 2,
            EnemyStepSeconds = 60.0,
        };

        var factory = new GameFactory(
            new GameConfig { Seed = 5 },
            value => new SeededRandom(value),
            new DijkstraPathfinder());

        var campaign = new Campaign(
            [floor],
            (definition, hearts, modifiers, conditions) =>
                factory.CreateFloor(definition, hearts, modifiers, conditions),
            runSeed: 5);

        Assert.True(ClearFloor(campaign), "the floor could not be walked to its arch");

        var blooms = campaign.Floor.KeysCollected;

        Assert.Equal(floor.KeyCount, blooms);
        Assert.Equal(blooms, campaign.BloomsPicked);
        Assert.Equal(blooms + 1, campaign.GardenCount);
        Assert.Equal(1, campaign.FloorsCleared);
        Assert.Same(StoryCatalog.Page(1), campaign.CurrentPage);
    }

    private static Campaign Campaign(int seed) =>
        new GameFactory(
            new GameConfig { Seed = seed },
            value => new SeededRandom(value),
            new DijkstraPathfinder())
        .CreateCampaign();

    /// <summary>Walks every bloom, then the arch. Returns false when the run was lost on the way.</summary>
    private static bool ClearFloor(Campaign campaign)
    {
        foreach (var bloom in campaign.Floor.Keys.ToList())
        {
            if (!WalkTo(campaign.Floor, bloom))
            {
                return false;
            }
        }

        return WalkTo(campaign.Floor, campaign.Floor.Exit) &&
               campaign.Floor.Status == GameStatus.Won;
    }

    /// <summary>
    /// Walks the shortest route to <paramref name="target"/>, winding the step clock and letting go
    /// of the Echo if the route happens to pass through it.
    /// </summary>
    private static bool WalkTo(GameState state, Cell target)
    {
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, target, _ => 1);

        if (path.Count == 0)
        {
            return false;
        }

        for (var index = 1; index < path.Count; index++)
        {
            if (state.IsEchoOpen)
            {
                state.CloseEcho();
            }

            if (state.Status != GameStatus.InProgress)
            {
                return false;
            }

            if (!state.TryMove(DirectionToward(state.Player, path[index])))
            {
                return false;
            }

            if (state.IsPlayerStepping)
            {
                state.Tick(state.LastPlayerStepSeconds);
            }

            if (state.IsEchoOpen)
            {
                state.CloseEcho();
            }
        }

        // The last step may have been the one that cleared the floor, so the position is the
        // answer here and the status is the caller's business.
        return state.Player.Row == target.Row && state.Player.Column == target.Column;
    }

    private static Direction DirectionToward(Cell from, Cell to)
    {
        if (to.Row < from.Row)
        {
            return Direction.North;
        }

        if (to.Row > from.Row)
        {
            return Direction.South;
        }

        return to.Column > from.Column ? Direction.East : Direction.West;
    }
}

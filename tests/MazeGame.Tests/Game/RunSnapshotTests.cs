using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the run snapshot: what a save remembers, and what a restore rebuilds.
/// </summary>
/// <remarks>
/// A restore rebuilds the floor from the run's seed, so the tests here compare the things a run
/// keeps across a save — seed, floor, hearts, cards, garden — and prove the rebuilt floor is a
/// real, playable floor rather than a half-filled shell.
/// </remarks>
public sealed class RunSnapshotTests
{
    private const int Seed = 7;

    [Fact]
    public void AFreshRun_RestoresOnTheSameSeedAndFloor()
    {
        var campaign = CampaignOf();
        var restored = Restore(campaign.Snapshot());

        Assert.Equal(campaign.RunSeed, restored.RunSeed);
        Assert.Equal(campaign.FloorNumber, restored.FloorNumber);
        Assert.Equal(campaign.Hearts, restored.Hearts);
        Assert.Equal(campaign.GardenCount, restored.GardenCount);
        Assert.Equal(campaign.Floor.Player, restored.Floor.Player);
        Assert.Equal(campaign.Floor.Enemy, restored.Floor.Enemy);
        Assert.Equal(campaign.Floor.Exit, restored.Floor.Exit);
    }

    [Fact]
    public void ACardAndItsHearts_SurviveTheRoundTrip()
    {
        var campaign = CampaignOf();
        ClearFloor(campaign);

        var card = campaign.Draft!.Offers[0];
        Assert.True(campaign.TakeCard(card));
        Assert.True(campaign.TryAdvanceFloor());

        var snapshot = campaign.Snapshot();
        var restored = Restore(snapshot);

        Assert.Equal(campaign.FloorNumber, restored.FloorNumber);
        Assert.Equal(campaign.Hearts, restored.Hearts);
        Assert.Equal(campaign.Modifiers.Cards.Select(held => held.Id), restored.Modifiers.Cards.Select(held => held.Id));
        Assert.Equal(campaign.BloomsPicked, restored.BloomsPicked);
        Assert.Equal(campaign.GardenCount, restored.GardenCount);
    }

    [Fact]
    public void ASavedFloor_IsPlayableAfterTheRestore()
    {
        var campaign = CampaignOf();
        var restored = Restore(campaign.Snapshot());

        Assert.Equal(GameStatus.InProgress, restored.Floor.Status);
        Assert.True(restored.Floor.TryMove(FurthestDirection(restored.Floor)));
    }

    [Fact]
    public void TheGarden_KeepsTheBloomsPickedOnTheSavedFloor()
    {
        var campaign = CampaignOf(keyCount: 1);
        var key = campaign.Floor.Keys.First();

        Assert.True(WalkTo(campaign.Floor, key));

        var restored = Restore(campaign.Snapshot(), keyCount: 1);

        // The rebuilt floor starts its own bloom count at zero, but the garden keeps the one the
        // player had already picked: a save must never cost progress.
        Assert.Equal(campaign.BloomsPicked, restored.BloomsPicked);
        Assert.Equal(0, restored.Floor.KeysCollected);
    }

    [Fact]
    public void ASnapshotFromAnotherVersion_IsRefused()
    {
        var snapshot = CampaignOf().Snapshot() with { Version = RunSnapshot.CurrentVersion + 1 };

        Assert.Throws<ArgumentException>(() => Restore(snapshot));
    }

    [Fact]
    public void ASnapshotNamingAnUnknownCard_IsRefused()
    {
        var snapshot = CampaignOf().Snapshot() with { CardIds = new[] { "no-such-card" } };

        Assert.Throws<ArgumentException>(() => Restore(snapshot));
    }

    [Fact]
    public void ASnapshotOnAFloorTheRunDoesNotHave_IsRefused()
    {
        var snapshot = CampaignOf().Snapshot() with { FloorIndex = 9 };

        Assert.Throws<ArgumentException>(() => Restore(snapshot));
    }

    [Fact]
    public void ASnapshotWithNoHearts_IsRefused()
    {
        var snapshot = CampaignOf().Snapshot() with { Hearts = 0 };

        Assert.Throws<ArgumentException>(() => Restore(snapshot));
    }

    [Fact]
    public void AShippedRun_RestoresThroughTheFactory()
    {
        // The frontends restore through GameFactory, not through this test's little corridors:
        // this proves the shipped catalog is what a save is rebuilt from.
        var factory = new GameFactory(
            new GameConfig { Seed = Seed },
            seed => new MazeGame.Core.Abstractions.SeededRandom(seed),
            new DijkstraPathfinder());
        var campaign = factory.CreateCampaign();
        var snapshot = campaign.Snapshot();

        var restored = factory.RestoreCampaign(snapshot);

        Assert.Equal(campaign.RunSeed, restored.RunSeed);
        Assert.Equal(campaign.FloorNumber, restored.FloorNumber);
        Assert.Equal(campaign.Hearts, restored.Hearts);
        Assert.Equal(campaign.Floor.Exit, restored.Floor.Exit);
        Assert.Equal(campaign.Floor.Player, restored.Floor.Player);
    }

    private static Campaign Restore(RunSnapshot snapshot, int keyCount = 0) =>
        Campaign.Restore(Floors(keyCount), BuildFloor, snapshot);

    private static Campaign CampaignOf(int keyCount = 0) =>
        new(Floors(keyCount), BuildFloor, Seed);

    private static List<FloorDefinition> Floors(int keyCount)
    {
        var floors = new List<FloorDefinition>();

        for (var number = 1; number <= 3; number++)
        {
            floors.Add(new FloorDefinition
            {
                Number = number,
                Name = $"Floor {number}",
                ThemeId = "test",
                Rows = 3,
                Columns = 3,
                KeyCount = keyCount,
                EnemyStepSeconds = 1000.0,
                MaxHearts = 4,
            });
        }

        return floors;
    }

    private static GameState BuildFloor(
        FloorDefinition floor,
        int hearts,
        RunModifiers modifiers,
        IReadOnlyList<DescentCondition> conditions) =>
        new(
            modifiers.Apply(new GameConfig
            {
                Rows = 3,
                Columns = 3,
                KeyCount = floor.KeyCount,
                StartingHearts = hearts,
                MaxHearts = floor.MaxHearts,
                EnemyStepSeconds = 1000.0,
                Seed = floor.Number,
                Conditions = conditions,
            }),
            Maze(),
            new DijkstraPathfinder(),
            new MazeGame.Core.Abstractions.SeededRandom(floor.Number));

    /// <summary>Three-by-three, every wall down: a floor the player can walk anywhere on.</summary>
    private static Grid Maze()
    {
        var grid = new Grid(3, 3);

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (column < 2)
                {
                    grid.RemoveWall(row, column, Direction.East);
                }

                if (row < 2)
                {
                    grid.RemoveWall(row, column, Direction.South);
                }
            }
        }

        return grid;
    }

    private static void ClearFloor(Campaign campaign) =>
        Assert.True(WalkTo(campaign.Floor, campaign.Floor.Exit));

    private static bool WalkTo(GameState state, Cell target)
    {
        var pathfinder = new DijkstraPathfinder();

        while (state.Player != target)
        {
            var route = pathfinder.FindPath(state.Maze, state.Player, target, _ => 1);

            if (route.Count < 2 || !state.TryMove(DirectionToward(state.Player, route[1])))
            {
                return false;
            }
        }

        return true;
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

        return to.Column < from.Column ? Direction.West : Direction.East;
    }

    private static Direction FurthestDirection(GameState state) =>
        state.Player.Column < state.Maze.Columns - 1 ? Direction.East : Direction.West;
}

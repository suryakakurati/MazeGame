using System.Linq;
using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Pathfinding;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// The per-run floor variation: that it is deterministic, that it leaves the opening floors alone,
/// and that each face bends the floor in exactly the way its name promises.
/// </summary>
public sealed class FloorFacesTests
{
    [Fact]
    public void TheOpeningFloors_AreNeverBent()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            for (var number = 1; number < FloorFaces.FirstVariedFloor; number++)
            {
                var floor = Floor(number);

                Assert.Equal(FloorFace.Authored, FloorFaces.Drawn(floor, seed));
                Assert.Same(floor, FloorFaces.Apply(floor, FloorFace.Authored));
            }
        }
    }

    [Fact]
    public void ASeed_AlwaysRollsTheSameFace()
    {
        var floor = Floor(9);

        Assert.Equal(FloorFaces.Drawn(floor, 4242), FloorFaces.Drawn(floor, 4242));
    }

    [Fact]
    public void DifferentSeeds_BendTheCampaignDifferently()
    {
        var floors = new FloorDefinition[20];
        for (var number = 1; number <= floors.Length; number++)
        {
            floors[number - 1] = Floor(number);
        }

        var first = Faces(floors, seed: 11);
        var second = Faces(floors, seed: 12);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EveryFace_KeepsTheFloorNumber()
    {
        var floor = Floor(12);

        foreach (var face in new[]
                 {
                     FloorFace.Authored,
                     FloorFace.Quiet,
                     FloorFace.Greedy,
                     FloorFace.Haunted,
                     FloorFace.Starved,
                 })
        {
            Assert.Equal(floor.Number, FloorFaces.Apply(floor, face).Number);
            Assert.Equal(floor.Name, FloorFaces.Apply(floor, face).Name);
        }
    }

    [Fact]
    public void Quiet_TakesAHunter_AndGivesAStepOfSight()
    {
        var floor = Floor(8) with { HunterCount = 2, VisionRadius = 3 };
        var quiet = FloorFaces.Apply(floor, FloorFace.Quiet);

        Assert.Equal(1, quiet.HunterCount);
        Assert.Equal(4, quiet.VisionRadius);
        Assert.True(quiet.EnemyStepSeconds > floor.EnemyStepSeconds);
        Assert.Contains("quiet", quiet.Gimmick);
    }

    [Fact]
    public void Quiet_NeverLeavesAFloorWithoutAHunter()
    {
        var solo = FloorFaces.Apply(Floor(8) with { HunterCount = 1 }, FloorFace.Quiet);

        Assert.Equal(1, solo.HunterCount);
    }

    [Fact]
    public void Greedy_AddsABloom_AndAHunter()
    {
        var floor = Floor(8) with { KeyCount = 3, HunterCount = 1 };
        var greedy = FloorFaces.Apply(floor, FloorFace.Greedy);

        Assert.Equal(4, greedy.KeyCount);
        Assert.Equal(2, greedy.HunterCount);
        Assert.True(greedy.EnemyStepSeconds < floor.EnemyStepSeconds);
        Assert.Contains("greedy", greedy.Gimmick);
    }

    [Fact]
    public void Haunted_TakesAStepOfSight_AndOpensTheWalls()
    {
        var floor = Floor(8) with { VisionRadius = 3, ExtraLoops = 4 };
        var haunted = FloorFaces.Apply(floor, FloorFace.Haunted);

        Assert.Equal(2, haunted.VisionRadius);
        Assert.Equal(10, haunted.ExtraLoops);
        Assert.Equal(80, haunted.DeadEndBraidPercent);
        Assert.Equal(FloorRule.BurningLantern, haunted.Rule);
        Assert.Contains("haunted", haunted.Gimmick);
    }

    [Fact]
    public void Starved_TakesABloom_AndHurriesTheHunt()
    {
        var floor = Floor(8) with { KeyCount = 3 };
        var starved = FloorFaces.Apply(floor, FloorFace.Starved);

        Assert.Equal(2, starved.KeyCount);
        Assert.True(starved.EnemyStepSeconds < floor.EnemyStepSeconds);
        Assert.Contains("starved", starved.Gimmick);
    }

    [Fact]
    public void Starved_NeverLeavesAFloorWithNothingToFind()
    {
        var starved = FloorFaces.Apply(Floor(8) with { KeyCount = 1 }, FloorFace.Starved);

        Assert.Equal(1, starved.KeyCount);
    }

    [Fact]
    public void EveryFace_IsRolledBySomeSeed()
    {
        var seen = new System.Collections.Generic.HashSet<FloorFace>();

        for (var seed = 0; seed < 400; seed++)
        {
            seen.Add(FloorFaces.Drawn(Floor(8), seed));
        }

        Assert.Equal(5, seen.Count);
    }

    [Fact]
    public void Haunted_KeepsAFloorThatAlreadyHasARule()
    {
        var floor = Floor(9) with { Rule = FloorRule.Tide, VisionRadius = 4 };
        var haunted = FloorFaces.Apply(floor, FloorFace.Haunted);

        Assert.Equal(FloorRule.Tide, haunted.Rule);
        Assert.Equal(3, haunted.VisionRadius);
    }

    [Fact]
    public void Haunted_NeverPutsTheTorchOut()
    {
        var haunted = FloorFaces.Apply(Floor(8) with { VisionRadius = 0 }, FloorFace.Haunted);

        Assert.Equal(1, haunted.VisionRadius);
    }

    [Fact]
    public void AFloorsOwnGimmick_IsKeptBehindTheFacesClause()
    {
        var quiet = FloorFaces.Apply(Floor(8) with { Gimmick = "the water is rising" }, FloorFace.Quiet);

        Assert.EndsWith("the water is rising", quiet.Gimmick);
    }

    [Fact]
    public void TheShippedCampaign_ChangesShapeWithTheSeed()
    {
        // The point of faces in one assertion: the same twenty floors, two seeds, two runs.
        Assert.NotEqual(Shape(seed: 1), Shape(seed: 2));
    }

    /// <summary>Summarises every floor's levers into one string, for a whole-run comparison.</summary>
    private static string Shape(int seed)
    {
        var factory = new GameFactory(
            new GameConfig { Seed = seed },
            floorSeed => new SeededRandom(floorSeed),
            new DijkstraPathfinder());

        return string.Join(
            "|",
            factory.CreateCampaign().Floors.Select(floor =>
                $"{floor.Number}:{floor.KeyCount}/{floor.HunterCount}/{floor.VisionRadius}/{floor.Rule}/{floor.Gimmick}"));
    }

    /// <summary>Returns the face each floor wears, in order.</summary>
    private static FloorFace[] Faces(FloorDefinition[] floors, int seed)
    {
        var faces = new FloorFace[floors.Length];

        for (var index = 0; index < floors.Length; index++)
        {
            faces[index] = FloorFaces.Drawn(floors[index], seed);
        }

        return faces;
    }

    /// <summary>A plain floor of the campaign's size, so only the faces vary between tests.</summary>
    private static FloorDefinition Floor(int number) => new()
    {
        Number = number,
        Name = $"Floor {number}",
        ThemeId = "test",
        Rows = 15,
        Columns = 15,
        KeyCount = 3,
        EnemyStepSeconds = 0.6,
        HunterCount = 1,
        MaxHearts = 3,
    };
}

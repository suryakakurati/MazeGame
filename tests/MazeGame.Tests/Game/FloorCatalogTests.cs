using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the shipped twenty-floor campaign: numbered in order, valid and buildable, every floor
/// its own place and rule, and the descent getting meaner as it goes.
/// </summary>
/// <remarks>
/// Pace is no longer a straight line down the list: several floors are deliberately slow and lit,
/// or fast and blind, because that is the gimmick. The tests therefore assert the *shape* of the
/// descent — the last floor is the fastest, most floors tighten, and the player always outruns the
/// killer — rather than a strictly falling number.
/// </remarks>
public sealed class FloorCatalogTests
{
    [Fact]
    public void Shipped_HasTheAnnouncedNumberOfFloors()
    {
        Assert.Equal(FloorCatalog.ShippedFloorCount, FloorCatalog.Shipped.Count);
        Assert.Equal(20, FloorCatalog.Shipped.Count);
    }

    [Fact]
    public void Shipped_IsNumberedInOrderFromOne()
    {
        for (var index = 0; index < FloorCatalog.Shipped.Count; index++)
        {
            Assert.Equal(index + 1, FloorCatalog.Shipped[index].Number);
        }
    }

    [Fact]
    public void Shipped_FloorsAreValidAndTheirMazesCanHoldTheirContents()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            floor.Validate();

            var config = new GameConfig
            {
                Rows = floor.Rows,
                Columns = floor.Columns,
                KeyCount = floor.KeyCount,
                VisionRadius = floor.VisionRadius ?? 3,
            };

            config.Validate();
        }
    }

    [Fact]
    public void Shipped_EveryFloorHasItsOwnNameAndTheme()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var themes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.False(string.IsNullOrWhiteSpace(floor.Name));
            Assert.False(string.IsNullOrWhiteSpace(floor.ThemeId));
            Assert.True(names.Add(floor.Name), $"the name '{floor.Name}' is reused");
            Assert.True(themes.Add(floor.ThemeId), $"the theme '{floor.ThemeId}' is reused");
        }
    }

    [Fact]
    public void Shipped_EveryFloorExplainsItsGimmick()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(floor.Gimmick),
                $"floor {floor.Number} ({floor.Name}) has no line for its card.");

            // The card draws the line at scale 2 under the name, so it has to stay a sentence.
            Assert.True(
                floor.Gimmick!.Length <= 48,
                $"floor {floor.Number}'s gimmick is {floor.Gimmick!.Length} characters and will not fit its card.");
        }
    }

    [Fact]
    public void Shipped_EveryFloorIsGivenSomethingOfItsOwn()
    {
        // A floor may be distinguished by a rule of its own (vision, barriers, wisps, the maze's
        // shape, the respawn) or by its numbers. What it may not be is a duplicate of the floor
        // before it with a new name on the card.
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        foreach (var floor in FloorCatalog.Shipped)
        {
            var signature = string.Join(
                '|',
                floor.Rows,
                floor.Columns,
                floor.KeyCount,
                floor.EnemyStepSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                floor.PlayerStepSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                floor.VisionRadius,
                floor.BarrierBudget,
                floor.LureCount,
                floor.ExtraLoops,
                floor.DeadEndBraidPercent,
                floor.EnemyAuraRadius,
                floor.RespawnDistance,
                floor.InvulnerabilitySeconds,
                floor.HunterCount);

            Assert.True(signatures.Add(signature), $"floor {floor.Number} ({floor.Name}) replays floor {floor.Number - 1}.");
        }
    }

    [Fact]
    public void Shipped_AtLeastHalfTheFloorsChangeARuleNotJustANumber()
    {
        var gimmicked = 0;

        foreach (var floor in FloorCatalog.Shipped)
        {
            if (floor.VisionRadius is not null ||
                floor.BarrierBudget is not null ||
                floor.LureCount is not null ||
                floor.ExtraLoops is not null ||
                floor.DeadEndBraidPercent is not null ||
                floor.EnemyAuraRadius is not null ||
                floor.RespawnDistance is not null ||
                floor.InvulnerabilitySeconds is not null)
            {
                gimmicked++;
            }
        }

        Assert.True(gimmicked >= 10, $"Only {gimmicked} floors change a rule of their own.");
    }

    [Fact]
    public void Shipped_EveryFloorNeedsBetweenTwoAndFiveKeys()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.InRange(floor.KeyCount, 2, 5);
        }
    }

    [Fact]
    public void Shipped_TheRunAsksForMoreKeysThanItHasFloors()
    {
        var keys = 0;

        foreach (var floor in FloorCatalog.Shipped)
        {
            keys += floor.KeyCount;
        }

        Assert.True(keys >= 60, $"The whole run only asks for {keys} keys.");
    }

    [Fact]
    public void Shipped_TheDescentGetsMeaner()
    {
        var floors = FloorCatalog.Shipped;
        var raised = 0;

        for (var index = 1; index < floors.Count; index++)
        {
            Assert.True(
                floors[index].EnemyStepSeconds <= 1.0,
                $"floor {index + 1} gives the killer {floors[index].EnemyStepSeconds}s a step; only a slow, lit floor is allowed to be a breather.");

            // Pressure is steps per second times hunters: the number of hunters is the big dial,
            // and the step time is the small one. A floor that is slower per hunter can still be
            // meaner because there are more of them.
            if (Pressure(floors[index]) > Pressure(floors[index - 1]))
            {
                raised++;
            }
        }

        Assert.True(raised >= 12, $"Only {raised} of {floors.Count - 1} floors raise the pressure.");

        var opening = floors.Take(5).Average(Pressure);
        var closing = floors.TakeLast(5).Average(Pressure);

        Assert.True(
            closing > opening * 2,
            $"The last five floors average {closing:0.00} hunter-steps a second against {opening:0.00} at the start.");

        // The last floor is the hardest one: the most hunter-steps per second in the run.
        var strongest = floors[0];

        foreach (var floor in floors)
        {
            if (Pressure(floor) > Pressure(strongest))
            {
                strongest = floor;
            }
        }

        Assert.Equal(floors[^1], strongest);
    }

    [Fact]
    public void Shipped_TheHuntersMultiplyAtTenAndSixteen()
    {
        var floors = FloorCatalog.Shipped;

        for (var index = 0; index < 9; index++)
        {
            Assert.Equal(1, floors[index].HunterCount);
        }

        for (var index = 9; index < 15; index++)
        {
            Assert.Equal(2, floors[index].HunterCount);
        }

        for (var index = 15; index < 20; index++)
        {
            Assert.Equal(3, floors[index].HunterCount);
        }

        // A second hunter is the biggest step up the descent can take, so it arrives on a floor
        // that is otherwise no faster than the one before it: the rule changes, not the numbers.
        Assert.True(
            floors[9].EnemyStepSeconds > floors[8].EnemyStepSeconds,
            "floor 10 adds a hunter and speeds the old one up at the same time.");
    }

    [Fact]
    public void Shipped_ThePlayerStartsSlowAndEarnsSpeedInEachArc()
    {
        var floors = FloorCatalog.Shipped;

        Assert.Equal(0.14, floors[0].PlayerStepSeconds, 3);
        Assert.True(
            floors[8].PlayerStepSeconds < floors[0].PlayerStepSeconds,
            "the first arc never gives the player any speed back.");
        Assert.True(
            floors[14].PlayerStepSeconds < floors[9].PlayerStepSeconds - 0.02,
            "a two-hunter arc has to be faster than its own start, or the new hunters are the only change.");
        Assert.True(
            floors[19].PlayerStepSeconds < floors[15].PlayerStepSeconds,
            "the last arc does not tighten the player's pace.");
    }

    /// <summary>Returns a floor's danger as hunter-steps per second.</summary>
    /// <param name="floor">The floor to score.</param>
    private static double Pressure(FloorDefinition floor) =>
        floor.HunterCount / floor.EnemyStepSeconds;

    [Fact]
    public void Shipped_EveryFloorHasTimedMovementAndOutrunsTheKiller()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.True(
                floor.PlayerStepSeconds > 0,
                $"floor {floor.Number} would be instant, which is a test-floor setting, not a shipped one");

            // The chase's shape comes from this ratio: the player must always be the faster one.
            Assert.True(
                floor.PlayerStepSeconds < floor.EnemyStepSeconds,
                $"floor {floor.Number} would let the killer outrun the player");
        }
    }

    [Fact]
    public void Shipped_NoFloorLetsThePlayerStepSlowerThanATenth()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.True(
                floor.PlayerStepSeconds <= 0.15,
                $"floor {floor.Number} moves at {floor.PlayerStepSeconds}s a cell, which feels stuck rather than heavy.");
        }
    }

    [Fact]
    public void Shipped_TheMazeGrowsAsTheRunDescends()
    {
        // A bigger cave is a longer hunt: the same killer pace covers more ground, so the floor
        // gets larger without every number on it having to move. The growth is a ladder, not a
        // ramp, so a floor is never smaller than the one before it except where the catalogue
        // says so on purpose.
        var floors = FloorCatalog.Shipped;

        Assert.Equal(15, floors[0].Rows);
        Assert.Equal(15, floors[0].Columns);
        Assert.Equal(25, floors[^1].Rows);
        Assert.Equal(25, floors[^1].Columns);

        // Exactly one floor shrinks, and it is the Warren: "a maze that fits in your arms" is a
        // rule, so it is allowed to break the ladder the way no other floor can.
        var shrunk = new List<int>();

        for (var index = 1; index < floors.Count; index++)
        {
            if (floors[index].Rows < floors[index - 1].Rows ||
                floors[index].Columns < floors[index - 1].Columns)
            {
                shrunk.Add(floors[index].Number);
            }
        }

        Assert.Equal([9], shrunk);
        Assert.Equal("The Warren", FloorCatalog.ByNumber(9).Name);
    }

    [Fact]
    public void ByNumber_ReturnsTheFloorWithThatNumber()
    {
        var floor = FloorCatalog.ByNumber(2);

        Assert.Equal(2, floor.Number);
        Assert.Equal(FloorCatalog.Shipped[1], floor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-1)]
    public void ByNumber_WithAnUnknownNumber_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FloorCatalog.ByNumber(number));
    }

    [Fact]
    public void Shipped_IsReadOnly()
    {
        var list = (IList<FloorDefinition>)FloorCatalog.Shipped;

        Assert.Throws<NotSupportedException>(() => list.Add(FloorCatalog.Shipped[0]));
    }
}

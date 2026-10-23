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
/// Player pace is a run-wide constant: the descent gets meaner through enemy pace and hunter count,
/// and only cards make the player faster. The tests therefore assert that every floor builds the
/// same player cadence, that the player starts ahead of the hunt, that the late hunters can outpace
/// the base player, and that the *enemy* pressure rises across the run.
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
            // The opening floors are deliberately slow, and the lit Salt Choir is a scripted
            // breather; every other floor has to keep the hunt moving.
            Assert.True(
                floors[index].EnemyStepSeconds <= 1.4,
                $"floor {index + 1} gives the killer {floors[index].EnemyStepSeconds}s a step; only the opening or a slow, lit floor is allowed to be a breather.");

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
    public void Shipped_PlayerSpeedIsConstantAndOnlyCardsChangeIt()
    {
        // A roguelike's power curve belongs to the draft, not to the floor number: the player must
        // not wake up faster on floor 12 for free. Enemy pace and hunter count carry the descent.
        Assert.Equal(0.26, FloorCatalog.PlayerCadenceSeconds, 3);

        foreach (var floor in FloorCatalog.Shipped)
        {
            Assert.Equal(
                FloorCatalog.PlayerCadenceSeconds,
                floor.PlayerStepSeconds,
                3);
        }
    }

    [Fact]
    public void Shipped_OnlyACardCanBeatTheBaseCadence()
    {
        // The last floor is the check that matters: its config must still be the base pace, and a
        // speed card must be what shortens it. A floor number must never do the card's job.
        var config = new GameConfig().ForFloor(FloorCatalog.ByNumber(20), startingHearts: 3);

        Assert.Equal(FloorCatalog.PlayerCadenceSeconds, config.PlayerStepSeconds, 9);

        var faster = RunModifiers.None.With(CardCatalog.ById("light-feet")!).Apply(config);

        Assert.True(
            faster.PlayerStepSeconds < config.PlayerStepSeconds,
            "Light Feet must still speed up the player on the last floor.");

        // One card is no longer enough to outrun the Deep's base hunter: player speed bonuses
        // were trimmed by a fifth, so the last floor needs two of them. The hunter's own numbers
        // were deliberately left alone.
        Assert.True(
            faster.PlayerStepSeconds > config.EnemyStepSeconds,
            "one speed card alone no longer beats the last floor's base hunter.");

        var twoCards = RunModifiers.None
            .With(CardCatalog.ById("light-feet")!)
            .With(CardCatalog.ById("glass-cannon")!)
            .Apply(config);

        Assert.True(
            twoCards.PlayerStepSeconds < config.EnemyStepSeconds,
            "two speed cards are enough to outrun the last floor's base hunter.");
    }

    /// <summary>Returns a floor's danger as hunter-steps per second.</summary>
    /// <param name="floor">The floor to score.</param>
    private static double Pressure(FloorDefinition floor) =>
        floor.HunterCount / floor.EnemyStepSeconds;

    [Fact]
    public void Shipped_ThePlayerStartsAheadButTheHuntCatchesUp()
    {
        var floors = FloorCatalog.Shipped;

        foreach (var floor in floors)
        {
            Assert.True(
                floor.PlayerStepSeconds > 0,
                $"floor {floor.Number} would be instant, which is a test-floor setting, not a shipped one");
        }

        // Floor 1 is the teaching floor: the player has a clear speed advantage.
        Assert.True(
            floors[0].PlayerStepSeconds < floors[0].EnemyStepSeconds,
            "the player does not start faster than the first hunter");

        // By the bottom, the base hunter is quicker than the base player. That is the pressure
        // that makes speed cards a real draft decision rather than a nice-to-have.
        Assert.True(
            floors[^1].EnemyStepSeconds < floors[^1].PlayerStepSeconds,
            "the last floor never forces the player to spend a card on speed");

        Assert.True(
            floors[^1].EnemyStepSeconds < floors[0].EnemyStepSeconds,
            "the enemy pace never ramps up across the run");
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

using System;
using System.Collections.Generic;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the shipped three-floor campaign: the floors are numbered in order, valid, buildable,
/// visually distinct, and genuinely harder as the run descends.
/// </summary>
public sealed class FloorCatalogTests
{
    [Fact]
    public void Shipped_HasTheAnnouncedNumberOfFloors()
    {
        Assert.Equal(FloorCatalog.ShippedFloorCount, FloorCatalog.Shipped.Count);
        Assert.Equal(3, FloorCatalog.Shipped.Count);
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

            // The capacity rule lives in GameConfig; building the derived config here proves the
            // floor's size actually fits its player, killer, exit and keys.
            var config = new GameConfig
            {
                Rows = floor.Rows,
                Columns = floor.Columns,
                KeyCount = floor.KeyCount,
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
    public void Shipped_LaterFloorsAskForMoreOrEqualKeys()
    {
        for (var index = 1; index < FloorCatalog.Shipped.Count; index++)
        {
            Assert.True(
                FloorCatalog.Shipped[index].KeyCount >= FloorCatalog.Shipped[index - 1].KeyCount,
                "a later floor must not need fewer keys");
        }
    }

    [Fact]
    public void Shipped_LaterFloorsHaveAFasterKiller()
    {
        for (var index = 1; index < FloorCatalog.Shipped.Count; index++)
        {
            Assert.True(
                FloorCatalog.Shipped[index].EnemyStepSeconds < FloorCatalog.Shipped[index - 1].EnemyStepSeconds,
                "a later floor must give the killer less time per step");
        }
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
    [InlineData(4)]
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

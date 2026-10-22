using System;
using System.Collections.Generic;
using System.Linq;
using MazeGame.Core.Game;
using Xunit;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the gardener's book: one page per shipped floor, in run order, short enough to fit the
/// cleared card, with an Echo script on every floor that ends in that floor's own hint.
/// </summary>
/// <remarks>
/// The story is authored text, so most of these tests are shape checks. What they protect is the
/// two things a player notices at once: a floor with no chapter, and a page that does not fit.
/// </remarks>
public sealed class StoryCatalogTests
{
    [Fact]
    public void EveryShippedFloor_HasExactlyOnePage_InRunOrder()
    {
        Assert.Equal(FloorCatalog.ShippedFloorCount, StoryCatalog.Pages.Count);

        for (var index = 0; index < StoryCatalog.Pages.Count; index++)
        {
            var page = StoryCatalog.Pages[index];
            var floor = FloorCatalog.Shipped[index];

            Assert.Equal(floor.Number, page.FloorNumber);
            Assert.Same(page, StoryCatalog.Page(floor.Number));
        }
    }

    [Fact]
    public void EveryPage_HasOneToThreeLines_ThatFitTheCard()
    {
        foreach (var page in StoryCatalog.Pages)
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Title));
            Assert.InRange(page.Lines.Count, 1, StoryCatalog.MaxPageLines);

            foreach (var line in page.Lines)
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                Assert.True(
                    line.Length <= StoryCatalog.MaxLineLength,
                    $"Page {page.FloorNumber} has a {line.Length}-character line: {line}");
            }
        }
    }

    [Fact]
    public void EveryFloor_HasAnEchoThatEndsInItsHint()
    {
        for (var floor = 1; floor <= FloorCatalog.ShippedFloorCount; floor++)
        {
            var lines = StoryCatalog.Echo(floor);

            Assert.InRange(lines.Count, 2, StoryCatalog.EchoLineCount);
            Assert.Equal(StoryCatalog.Hint(floor), lines[^1]);

            foreach (var line in lines)
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                Assert.True(line.Length <= StoryCatalog.MaxLineLength);
            }
        }
    }

    [Fact]
    public void NoTwoFloors_TeachTheSameHint()
    {
        var hints = Enumerable
            .Range(1, FloorCatalog.ShippedFloorCount)
            .Select(StoryCatalog.Hint)
            .ToList();

        Assert.Equal(hints.Count, hints.Distinct().Count());
    }

    [Fact]
    public void TheGardenerOnlyAddsALineWhenTheRunAsksForOne()
    {
        Assert.Null(StoryCatalog.MoodLine(StoryMood.None));
        Assert.False(string.IsNullOrWhiteSpace(StoryCatalog.MoodLine(StoryMood.LastHeart)));
        Assert.False(string.IsNullOrWhiteSpace(StoryCatalog.MoodLine(StoryMood.Conditioned)));
        Assert.NotEqual(StoryCatalog.MoodLine(StoryMood.LastHeart), StoryCatalog.MoodLine(StoryMood.Conditioned));
    }

    [Fact]
    public void AFloorOutsideTheBook_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StoryCatalog.Page(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoryCatalog.Page(FloorCatalog.ShippedFloorCount + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoryCatalog.Echo(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoryCatalog.Hint(0));
    }

    [Fact]
    public void APageWithTooManyLines_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StoryPage(1, "TITLE", ["one", "two", "three", "four"]));

        Assert.Throws<ArgumentOutOfRangeException>(() => new StoryPage(1, "TITLE", []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoryPage(0, "TITLE", ["one"]));
    }

    [Fact]
    public void EveryPlayerFacingString_SaysBloomRatherThanKey()
    {
        // The code still says key where it means the mechanic (Keys, KeyCount); the player never
        // sees those words. This is the sweep that keeps the rename from being half-done.
        var modifiers = RunModifiers.None.With(CardCatalog.ById("magnetic-keys")!);

        Assert.Contains("BLOOMS", modifiers.Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain("KEY", modifiers.Describe(), StringComparison.Ordinal);

        foreach (var card in CardCatalog.All)
        {
            Assert.DoesNotContain("key", card.FaceText, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var condition in DescentConditionCatalog.All)
        {
            Assert.DoesNotContain(
                "key",
                DescentConditionCatalog.Description(condition),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

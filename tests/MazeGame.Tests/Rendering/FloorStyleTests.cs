using System;
using System.Collections.Generic;
using MazeGame.Core.Game;
using MazeGame.UI.Rendering;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies the identity table the HUD and the weather read: every floor has a style, every accent
/// is its own colour, and the twenty emblems are twenty different marks.
/// </summary>
/// <remarks>
/// The atlas tests prove the art differs. These prove the parts that are drawn in window pixels —
/// the HUD, the cards and the particle layer — differ too, because those are what a player sees
/// around the tiles.
/// </remarks>
public sealed class FloorStyleTests
{
    [Fact]
    public void TheStyleTable_LinesUpWithTheFloorCatalog()
    {
        Assert.Equal(FloorCatalog.Shipped.Count, FloorStyleCatalog.Shipped.Count);

        for (var index = 0; index < FloorCatalog.Shipped.Count; index++)
        {
            Assert.Equal(FloorCatalog.Shipped[index].ThemeId, FloorStyleCatalog.Shipped[index].ThemeId);
            Assert.Same(FloorStyleCatalog.Shipped[index], FloorStyleCatalog.ForFloor(index + 1));
        }
    }

    [Fact]
    public void EveryShippedFloor_HasADistinctAccent()
    {
        var accents = new HashSet<(byte, byte, byte)>();

        foreach (var floor in FloorCatalog.Shipped)
        {
            var accent = FloorStyleCatalog.For(floor.ThemeId).Accent;

            Assert.True(
                accents.Add((accent.R, accent.G, accent.B)),
                $"{floor.Name} repeats another floor's accent colour.");
        }
    }

    [Fact]
    public void TheTwentyFloors_HaveManyDifferentWeathers()
    {
        var weathers = new HashSet<FloorAmbient>();

        foreach (var floor in FloorCatalog.Shipped)
        {
            weathers.Add(FloorStyleCatalog.For(floor.ThemeId).Ambient);
        }

        Assert.True(weathers.Count >= 10, $"Only {weathers.Count} kinds of weather are used.");
    }

    [Fact]
    public void EveryShippedFloor_HasASensibleLightAndFog()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            var style = FloorStyleCatalog.For(floor.ThemeId);
            var light = style.LightTint;

            Assert.InRange(style.Warmth, -1.0, 1.0);
            Assert.InRange(style.FogDensity, 0.0, 1.0);
            Assert.InRange(light.R, (byte)1, (byte)255);
            Assert.InRange(light.G, (byte)1, (byte)255);
            Assert.InRange(light.B, (byte)1, (byte)255);
        }
    }

    [Fact]
    public void TheTwentyFloors_HaveTwentyDifferentEmblems()
    {
        var patterns = new HashSet<string>(StringComparer.Ordinal);

        foreach (var floor in FloorCatalog.Shipped)
        {
            var pattern = PatternKey(FloorEmblem.Pattern(floor.ThemeId));

            Assert.True(patterns.Add(pattern), $"{floor.Name} repeats another floor's emblem.");
        }
    }

    [Fact]
    public void AnEmblem_IsMirroredAroundItsCentre()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            var pattern = FloorEmblem.Pattern(floor.ThemeId);

            for (var row = 0; row < FloorEmblem.Size; row++)
            {
                for (var column = 0; column < FloorEmblem.Size / 2; column++)
                {
                    Assert.Equal(
                        pattern[row, column],
                        pattern[row, FloorEmblem.Size - 1 - column]);
                }
            }
        }
    }

    [Fact]
    public void AnUnknownTheme_FailsLoudly()
    {
        Assert.Throws<KeyNotFoundException>(() => FloorStyleCatalog.For("not-a-floor"));
        Assert.Throws<ArgumentNullException>(() => FloorStyleCatalog.For(null!));
    }

    /// <summary>Flattens an emblem into a comparable string of zeros and ones.</summary>
    private static string PatternKey(bool[,] pattern)
    {
        var key = new System.Text.StringBuilder();

        for (var row = 0; row < FloorEmblem.Size; row++)
        {
            for (var column = 0; column < FloorEmblem.Size; column++)
            {
                key.Append(pattern[row, column] ? '1' : '0');
            }
        }

        return key.ToString();
    }
}

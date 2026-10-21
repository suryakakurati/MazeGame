using System;
using System.Collections.Generic;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the derived palette: the fixed lightness ladder, the distinctness of every slot, and
/// the metric the check pass uses to reject two themes that are the same colour.
/// </summary>
public sealed class PaletteTests
{
    [Fact]
    public void APalette_NeedsExactlySixteenColours()
    {
        Assert.Equal(16, Palette.Size);

        Assert.Throws<ArgumentException>(() => new Palette(Array.Empty<Rgb>()));
        Assert.Throws<ArgumentNullException>(() => new Palette(null!));
    }

    [Fact]
    public void EveryDerivedPalette_HasDistinctColours()
    {
        // Duplicate slots would make a tile read as the wrong material, so the check is a hard one.
        foreach (var theme in ThemeCatalog.Shipped)
        {
            Palette.Derive(theme).Validate();
        }
    }

    [Fact]
    public void TheTransparentSlot_IsBlackAndNeverADrawnColour()
    {
        var palette = Palette.Derive(ThemeCatalog.Shipped[0]);

        Assert.Equal(new Rgb(0, 0, 0), palette[PaletteRole.Transparent]);

        foreach (var theme in ThemeCatalog.Shipped)
        {
            var derived = Palette.Derive(theme);

            for (var role = (int)PaletteRole.Floor; role < Palette.Size; role++)
            {
                Assert.NotEqual(new Rgb(0, 0, 0), derived[(PaletteRole)role]);
            }
        }
    }

    [Fact]
    public void TheLightnessLadder_KeepsWallsAboveTheFloor()
    {
        // This is the "contrast is guaranteed" rule: every theme, whatever its hue, has a wall
        // lighter than the floor it stands on and darker than its lit top.
        foreach (var theme in ThemeCatalog.Shipped)
        {
            var palette = Palette.Derive(theme);
            var floor = Luma(palette[PaletteRole.Floor]);
            var wall = Luma(palette[PaletteRole.Wall]);
            var top = Luma(palette[PaletteRole.WallTop]);

            Assert.True(wall > floor, $"{theme.Id}: wall is not lighter than the floor");
            Assert.True(top > wall, $"{theme.Id}: the wall top is not lighter than the wall");
        }
    }

    [Fact]
    public void ADerivedPalette_IsIdenticalForTheSameTheme()
    {
        var theme = ThemeCatalog.ById("rootbound")!;

        Assert.Equal(0, Palette.Derive(theme).DistanceTo(Palette.Derive(theme)));
    }

    [Fact]
    public void NeighbouringThemes_AreFarApartEnoughToReadAsDifferent()
    {
        var themes = ThemeCatalog.Shipped;

        for (var index = 1; index < themes.Count; index++)
        {
            var distance = Palette.Derive(themes[index - 1]).DistanceTo(Palette.Derive(themes[index]));

            Assert.True(
                distance >= Palette.MinimumThemeDistance,
                $"{themes[index - 1].Id} and {themes[index].Id} are {distance:0.0} apart");
        }
    }

    [Fact]
    public void AThemeWithOnlyAHueChange_IsNotATheme()
    {
        var shallows = ThemeCatalog.ById("shallows")!;
        var tint = shallows with { Id = "shallows-tint", Hue = shallows.Hue + 2, AccentHue = shallows.AccentHue };

        // A two-degree hue nudge is a tint, not a floor: the structural count is the guard.
        Assert.True(Palette.Derive(shallows).DistanceTo(Palette.Derive(tint)) < Palette.MinimumThemeDistance);
        Assert.Equal(0, shallows.StructuralDifferences(tint));
    }

    [Fact]
    public void Themes_ValidateTheirOwnParameters()
    {
        var theme = ThemeCatalog.Shipped[0];

        Assert.Throws<ArgumentException>(() => (theme with { Id = " " }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { Name = " " }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { VariantCount = 1 }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { VariantCount = 5 }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { MossDensity = 1.4 }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { SpeckleRate = -0.1 }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { GlowStrength = 2 }).Validate());
        Assert.Throws<ArgumentException>(() => (theme with { BoulderRoundness = -1 }).Validate());
    }

    private static double Luma(Rgb colour) => (0.299 * colour.Red) + (0.587 * colour.Green) + (0.114 * colour.Blue);
}

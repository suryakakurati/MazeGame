using System.Collections.Generic;
using MazeGame.Core.Game;
using MazeGame.UI.Rendering;
using Raylib_cs;

namespace MazeGame.Tests.Rendering;

/// <summary>
/// Verifies every shipped floor has its own accent colour, and that the game's colour table stays
/// in step with the campaign.
/// </summary>
/// <remarks>
/// The sprite compiler and the renderer keep separate theme tables on purpose — the game must not
/// reference the tools — so the only thing holding them together is a test. A floor with no accent
/// would otherwise draw its cards and its letterbox in whatever colour was left over.
/// </remarks>
public sealed class ThemeAccentTests
{
    [Fact]
    public void EveryShippedFloor_HasAnAccentOfItsOwn()
    {
        var colours = new HashSet<uint>();

        foreach (var floor in FloorCatalog.Shipped)
        {
            var accent = RaylibRenderer.ThemeAccent(floor.ThemeId);

            Assert.True(
                colours.Add(Packed(accent)),
                $"floor {floor.Number} ({floor.Name}) shares its accent with an earlier floor.");
        }
    }

    [Fact]
    public void EveryShippedFloor_IsBrightEnoughToReadOnTheCards()
    {
        foreach (var floor in FloorCatalog.Shipped)
        {
            var accent = RaylibRenderer.ThemeAccent(floor.ThemeId);

            // The cards draw their heading in this colour on near-black; a dark accent would vanish.
            Assert.True(
                accent.R + accent.G + accent.B > 240,
                $"floor {floor.Number} ({floor.Name}) has an accent of {accent.R},{accent.G},{accent.B}.");
        }
    }

    [Fact]
    public void ThemeAccent_ThrowsForAThemeTheGameDoesNotShip()
    {
        Assert.Throws<KeyNotFoundException>(() => RaylibRenderer.ThemeAccent("not-a-floor"));
    }

    private static uint Packed(Color colour) =>
        ((uint)colour.R << 16) | ((uint)colour.G << 8) | colour.B;
}

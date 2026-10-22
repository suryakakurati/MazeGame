using System.Linq;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the guided tutorial is real content: every page says something, the pages are read in
/// order, and the guide names every control the game actually binds.
/// </summary>
/// <remarks>
/// The tutorial is what the title screen's second entry opens in both frontends, so a page that is
/// empty or a control that is never taught is a player left guessing rather than a cosmetic bug.
/// </remarks>
public sealed class TutorialTests
{
    [Fact]
    public void EveryPage_SaysSomething()
    {
        Assert.NotEmpty(Tutorial.Steps);

        foreach (var page in Tutorial.Steps)
        {
            page.Validate();
            Assert.NotEmpty(page.Lines);
        }
    }

    [Fact]
    public void TheGuide_TeachesEveryAbilityTheGameBinds()
    {
        var keys = string.Join(" ", Tutorial.Steps.Select(page => page.Keys));

        Assert.Contains("SPACE: BLINK", keys, System.StringComparison.Ordinal);
        Assert.Contains("V: VEIL", keys, System.StringComparison.Ordinal);
        Assert.Contains("T: DRAG", keys, System.StringComparison.Ordinal);
        Assert.Contains("L: LANTERN", keys, System.StringComparison.Ordinal);
        Assert.Contains("F: SHOVE", keys, System.StringComparison.Ordinal);
        Assert.Contains("E: OPEN THE ROOM", keys, System.StringComparison.Ordinal);
        Assert.Contains("R: REROLL", keys, System.StringComparison.Ordinal);
        Assert.Contains("ESC: PAUSE", keys, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Page_IsClampedIntoTheGuide()
    {
        Assert.Same(Tutorial.Steps[0], Tutorial.Page(-5));
        Assert.Same(Tutorial.Steps[Tutorial.Steps.Count - 1], Tutorial.Page(999));
    }
}

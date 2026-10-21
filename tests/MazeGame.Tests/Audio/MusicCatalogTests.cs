using MazeGame.Core.Game;
using MazeGame.UI.Audio;

namespace MazeGame.Tests.Audio;

/// <summary>
/// Covers the score's content: one track per floor, in the same order, with parameters the engine
/// can actually play. A missing track is a red test rather than a silent floor.
/// </summary>
public sealed class MusicCatalogTests
{
    [Fact]
    public void TheCatalog_HasOneTrackPerShippedFloor_InTheSameOrder()
    {
        Assert.Equal(FloorCatalog.Shipped.Count, MusicCatalog.All.Count);

        for (var index = 0; index < MusicCatalog.All.Count; index++)
        {
            Assert.Equal(FloorCatalog.Shipped[index].ThemeId, MusicCatalog.All[index].ThemeId);
        }
    }

    [Fact]
    public void EveryTrack_HasPlayableParameters()
    {
        foreach (var theme in MusicCatalog.All)
        {
            Assert.InRange(theme.RootMidi, 24, 60);
            Assert.InRange(theme.Tempo, 40.0, 140.0);
            Assert.InRange(theme.StepsPerBar, 12, 20);
            Assert.InRange(theme.BarsPerLoop, 4, 8);
            Assert.InRange(theme.Band, 1, 4);
            Assert.NotEmpty(theme.Melody.Degrees);
            Assert.InRange(theme.Melody.Spacing, 1, 8);
            Assert.True(Enum.IsDefined(theme.Style), $"{theme.ThemeId} has no style.");
            Assert.InRange(theme.Density, 0.0, 1.0);
            Assert.InRange(theme.Tension, 0.0, 1.0);
            Assert.InRange(theme.ReverbWet, 0.0, 1.0);
            Assert.NotEmpty(theme.Progression);

            foreach (var degree in theme.Progression)
            {
                Assert.InRange(degree, 0, 6);
            }

            Assert.InRange(theme.Mix.Pad, 0.5, 1.0);
            Assert.InRange(theme.Mix.Bass, 0.0, 1.0);
            Assert.InRange(theme.Mix.Lead, 0.0, 1.0);
            Assert.InRange(theme.Mix.Perc, 0.0, 1.0);
            Assert.InRange(theme.Mix.Choir, 0.0, 1.0);
        }
    }

    [Fact]
    public void TheTracks_GetDenserAndMoreTenseAsTheRunGoesDeeper()
    {
        var first = MusicCatalog.All[0];
        var last = MusicCatalog.All[^1];

        Assert.True(last.Band > first.Band);
        Assert.True(last.Tension > first.Tension);
        Assert.True(last.Density > first.Density);
        Assert.True(last.Mix.Choir > first.Mix.Choir);
    }

    [Fact]
    public void TheTwentyFloors_HaveTwentyDifferentTracks()
    {
        // Every floor's theme is a parameter set, not a recording. Two floors with identical
        // parameters would be the same music under a new name, which is exactly what "different
        // music on every floor" must not mean.
        var signatures = new HashSet<string>(StringComparer.Ordinal);

        foreach (var theme in MusicCatalog.All)
        {
            var signature = string.Join(
                '|',
                theme.RootMidi,
                theme.Mode,
                theme.Tempo,
                theme.BarsPerLoop,
                string.Join(',', theme.Progression),
                theme.Style,
                theme.StepsPerBar,
                string.Join(',', theme.Melody.Degrees),
                theme.Melody.Spacing,
                theme.Mix.Pad,
                theme.Mix.Bass,
                theme.Mix.Lead,
                theme.Mix.Perc,
                theme.Mix.Choir,
                theme.Density,
                theme.Tension,
                theme.ReverbWet,
                theme.Band,
                theme.Canon);

            Assert.True(signatures.Add(signature), $"{theme.ThemeId} repeats another floor's score.");
        }
    }

    [Fact]
    public void For_FindsTheTrack()
    {
        var theme = MusicCatalog.For("loom");

        Assert.Equal("loom", theme.ThemeId);
        Assert.Equal(18, MusicCatalog.All.ToList().IndexOf(theme) + 1);
    }

    [Fact]
    public void For_RefusesAThemeThatDoesNotExist()
    {
        var error = Assert.Throws<ArgumentException>(() => MusicCatalog.For("not-a-floor"));

        Assert.Contains("not-a-floor", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFloor_HasItsOwnMelody()
    {
        // The complaint this answers was that twenty floors sounded like one tune at twenty
        // pitches. A shared melody is exactly how that happens, so no two floors may have one.
        var melodies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var theme in MusicCatalog.All)
        {
            var signature = $"{string.Join(',', theme.Melody.Degrees)}|{theme.Melody.Spacing}";

            Assert.True(melodies.Add(signature), $"{theme.ThemeId} reuses another floor's melody.");
        }
    }

    [Fact]
    public void TheDescent_ChangesTheKindOfMusicNotOnlyItsNumbers()
    {
        var styles = new HashSet<MusicStyle>();

        foreach (var theme in MusicCatalog.All)
        {
            styles.Add(theme.Style);
        }

        Assert.True(styles.Count >= 8, $"The twenty floors only use {styles.Count} styles.");

        // Neighbouring floors never share a style: the player should hear the next floor as a new
        // piece, not as the previous one in a different key.
        for (var index = 1; index < MusicCatalog.All.Count; index++)
        {
            Assert.NotEqual(MusicCatalog.All[index - 1].Style, MusicCatalog.All[index].Style);
        }
    }

    [Fact]
    public void TheDescent_UsesMoreThanOneMeter()
    {
        var meters = new HashSet<int>();

        foreach (var theme in MusicCatalog.All)
        {
            meters.Add(theme.StepsPerBar);
        }

        Assert.Contains(16, meters);
        Assert.Contains(12, meters);
        Assert.Contains(20, meters);
    }

    [Fact]
    public void Midi_WrapsOctavesInBothDirections()
    {
        var theme = MusicCatalog.For("shallows");

        Assert.Equal(theme.RootMidi, theme.Midi(0));
        Assert.Equal(theme.RootMidi + 12, theme.Midi(7));
        Assert.Equal(theme.RootMidi - 2, theme.Midi(-1));
        Assert.Equal(theme.RootMidi + 14, theme.Midi(8));
    }

    [Fact]
    public void EveryMode_SpellsSevenAscendingStepsFromTheTonic()
    {
        foreach (var mode in Enum.GetValues<MusicMode>())
        {
            var scale = MusicTheme.ScaleOf(mode);

            Assert.Equal(7, scale.Length);
            Assert.Equal(0, scale[0]);

            for (var step = 1; step < scale.Length; step++)
            {
                Assert.InRange(scale[step] - scale[step - 1], 1, 3);
            }
        }
    }
}

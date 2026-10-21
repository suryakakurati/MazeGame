using System;
using MazeGame.UI.Audio;

namespace MazeGame.Tests.Audio;

/// <summary>
/// Covers the score renderer: every floor's track is audible, inside the ceiling, in time, seamless
/// at the loop point, and reproducible from its seed. These are the checks that replace listening
/// to twenty tracks before every commit.
/// </summary>
public sealed class ScoreRendererTests
{
    [Fact]
    public void EveryTheme_RendersAudibleMusicInsideTheCeiling()
    {
        for (var index = 0; index < MusicCatalog.All.Count; index++)
        {
            var stems = ScoreRenderer.Render(MusicCatalog.All[index], index + 1, seed: 7);
            var sum = Sum(stems);

            Assert.True(stems.Frames > Synth.SampleRate, $"{stems.Frames} samples is not a loop");
            Assert.InRange(Synth.Peak(sum), 0.4f, Synth.PeakCeiling);
            Assert.InRange(Synth.Rms(sum), 0.05, 0.35);
        }
    }

    [Fact]
    public void TheLoop_DoesNotClick()
    {
        // A seam step near zero means the last sample flows into the first: the loop is the same
        // piece, not two halves butted together.
        foreach (var floorNumber in new[] { 1, 7, 14, 20 })
        {
            var stems = ScoreRenderer.Render(
                MusicCatalog.All[floorNumber - 1],
                floorNumber,
                seed: 42);

            Assert.True(
                Synth.SeamStep(Sum(stems)) < 0.02f,
                $"floor {floorNumber} seams at {Synth.SeamStep(Sum(stems)):F4}");
            Assert.True(
                Synth.SeamStep(stems.Bed) < 0.005f,
                $"floor {floorNumber} bed seams at {Synth.SeamStep(stems.Bed):F4}");
        }
    }

    [Fact]
    public void TheLoopLength_FollowsTheTempo()
    {
        foreach (var theme in MusicCatalog.All)
        {
            var stems = ScoreRenderer.Render(theme, 1, seed: 3);
            var expected = theme.BarsPerLoop * 4 * 60.0 / theme.Tempo;

            Assert.InRange(stems.LoopSeconds, expected * 0.99, expected * 1.01);
        }
    }

    [Fact]
    public void TheSameSeed_RendersTheSameTrack()
    {
        var theme = MusicCatalog.For("foundry");

        var first = ScoreRenderer.Render(theme, 12, seed: 90210);
        var second = ScoreRenderer.Render(theme, 12, seed: 90210);

        Assert.Equal(first.Frames, second.Frames);
        Assert.True(first.Bed.AsSpan().SequenceEqual(second.Bed));
        Assert.True(first.Rhythm.AsSpan().SequenceEqual(second.Rhythm));
        Assert.True(first.Lead.AsSpan().SequenceEqual(second.Lead));
    }

    [Fact]
    public void ADifferentSeed_ChangesThePhrasingButNotTheLength()
    {
        var theme = MusicCatalog.For("warren");

        var first = ScoreRenderer.Render(theme, 9, seed: 1);
        var second = ScoreRenderer.Render(theme, 9, seed: 2);

        Assert.Equal(first.Frames, second.Frames);
        Assert.False(first.Lead.AsSpan().SequenceEqual(second.Lead));
    }

    [Fact]
    public void TheDeeperFloors_UseAllThreeStems()
    {
        var stems = ScoreRenderer.Render(MusicCatalog.For("deep"), 20, seed: 5);

        Assert.True(Synth.Rms(stems.Bed) > 0.03, "the bed is silent");
        Assert.True(Synth.Rms(stems.Rhythm) > 0.02, "the rhythm is silent");
        Assert.True(Synth.Rms(stems.Lead) > 0.01, "the lead is silent");
    }

    [Fact]
    public void OnlyTheLastTrack_CarriesTheMotifInCanon()
    {
        var canonical = MusicCatalog.All.Where(theme => theme.Canon).ToList();

        Assert.Single(canonical);
        Assert.Equal(MusicCatalog.All[^1], canonical[0]);
    }

    [Fact]
    public void TheCanon_ChangesTheLeadWithoutChangingTheLoop()
    {
        // The canon adds a second voice, so the stem is not the same audio; the loop length and
        // the mix balance stay put. Whether the two statements *sound* like a canon is an ear
        // check — the stem is normalised to the bed, so it cannot be heard as extra level here.
        var theme = MusicCatalog.For("deep");

        var canon = ScoreRenderer.Render(theme, 20, seed: 11);
        var single = ScoreRenderer.Render(theme with { Canon = false }, 20, seed: 11);

        Assert.Equal(canon.Frames, single.Frames);
        Assert.False(canon.Lead.AsSpan().SequenceEqual(single.Lead));
        Assert.InRange(Synth.Rms(canon.Lead), Synth.Rms(single.Lead) * 0.9, Synth.Rms(single.Lead) * 1.1);
    }

    [Fact]
    public void TheFloorNumber_SelectsTheMotif()
    {
        var theme = MusicCatalog.For("gallery");

        // Floor 1 and floor 2 are the same theme with different motifs, so only the lead differs.
        var first = ScoreRenderer.Render(theme, 1, seed: 8);
        var second = ScoreRenderer.Render(theme, 2, seed: 8);

        Assert.Equal(first.Frames, second.Frames);
        Assert.False(first.Lead.AsSpan().SequenceEqual(second.Lead));
    }

    /// <summary>Mixes the three stems into one buffer, which is what a listener hears.</summary>
    private static float[] Sum(ScoreStems stems)
    {
        var sum = new float[stems.Frames];

        for (var index = 0; index < sum.Length; index++)
        {
            sum[index] = stems.Bed[index] + stems.Rhythm[index] + stems.Lead[index];
        }

        return sum;
    }
}

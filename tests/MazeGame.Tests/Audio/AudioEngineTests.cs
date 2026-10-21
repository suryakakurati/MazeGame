using MazeGame.UI.Audio;

namespace MazeGame.Tests.Audio;

/// <summary>
/// Covers the mixer without a device: what the engine writes for a given score, intensity and
/// effect queue. The state-to-intensity function has its own tests in <see cref="MusicMixTests"/>.
/// </summary>
public sealed class AudioEngineTests
{
    private const int BarFrames = 1000;

    [Fact]
    public void AnEmptyEngine_RendersSilence()
    {
        var engine = new AudioEngine();

        var block = Render(engine, 512);

        Assert.All(block, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void AScore_RendersAudibleSamples()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));

        var block = Render(engine, 512);

        Assert.InRange(Rms(block), 0.05, 0.4);
    }

    [Fact]
    public void TheChase_IsLouderThanCalm_AndOnlyStartsOnABarLine()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));
        engine.SetIntensity(MusicIntensity.Calm);

        var calm = Rms(Render(engine, 200));

        engine.SetIntensity(MusicIntensity.Chase);

        // Still inside the first bar: the arrangement has not hatched yet, so the level must not
        // have moved. This is the difference between composing and triggering.
        var sameBar = Rms(Render(engine, 200));

        Assert.Equal(MusicIntensity.Calm, engine.ActiveIntensity);
        Assert.InRange(sameBar, calm * 0.99, calm * 1.01);

        // Still the first bar, right up to its last frame.
        Render(engine, 600);
        Assert.Equal(MusicIntensity.Calm, engine.ActiveIntensity);

        // The block that starts on the bar line is where the chase is adopted.
        Render(engine, 200);
        Assert.Equal(MusicIntensity.Chase, engine.ActiveIntensity);

        // The mix fades in over 400 ms, so the level is only at its chase value after that.
        Render(engine, Synth.SampleRate / 2);
        var chase = Rms(Render(engine, 200));

        Assert.True(chase > calm * 1.5, $"chase {chase:F3} is not louder than calm {calm:F3}");
    }

    [Fact]
    public void AChase_FadesIn_InsteadOfSteppingOnTopOfTheMusic()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));
        engine.SetIntensity(MusicIntensity.Calm);

        var calm = Rms(Render(engine, 1000));

        engine.SetIntensity(MusicIntensity.Chase);

        // This block starts on the bar line and is where the chase is adopted. Its first sample
        // must still be at the calm level and only a few percent of the ramp may fit inside the
        // block, or the new stem would step in rather than fade in.
        var onTheBar = Rms(Render(engine, 1000));

        Assert.InRange(onTheBar, calm * 0.99, calm * 1.08);

        Render(engine, Synth.SampleRate / 2);
        var fadedIn = Rms(Render(engine, 200));

        Assert.True(fadedIn > calm * 1.5, $"the fade never arrived ({fadedIn:F3} vs {calm:F3})");
    }

    [Fact]
    public void ACalmHold_KeepsTheDrumsBack_ThenLetsThemIn()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));
        engine.HoldCalm(0.1);
        engine.SetIntensity(MusicIntensity.Chase);

        var opening = Rms(Render(engine, 512));

        Assert.Equal(MusicIntensity.Calm, engine.ActiveIntensity);
        Assert.InRange(engine.CalmHoldSeconds, 0.08, 0.1);

        for (var block = 0; block < 30; block++)
        {
            Render(engine, 512);
        }

        Assert.Equal(0.0, engine.CalmHoldSeconds);
        Assert.Equal(MusicIntensity.Chase, engine.ActiveIntensity);

        Render(engine, Synth.SampleRate / 2);
        Assert.True(Rms(Render(engine, 200)) > opening * 1.5);
    }

    [Fact]
    public void TheChase_IsLouderThanCalm_OnARealTrack()
    {
        // The flat-stem test above proves the gating; this one proves the *balance*: on a floor
        // with drums and a lead, the bed alone must not be so loud that opening them adds nothing.
        // (A drone floor is deliberately the bed, so the chase is only a little louder there.)
        var score = ScoreRenderer.Render(MusicCatalog.For("ember"), 5, seed: 99);

        var calm = LevelOf(score, MusicIntensity.Calm);
        var chase = LevelOf(score, MusicIntensity.Chase);

        Assert.True(chase > calm * 1.2, $"chase {chase:F4} is not clearly above calm {calm:F4}");
    }

    [Fact]
    public void Ducking_TakesTheMusicDown_AndComesBack()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));

        var open = Rms(Render(engine, 512));

        engine.Duck(1.0);

        // The duck is a ramp, not a switch, so it needs a moment to reach its depth.
        Render(engine, Synth.SampleRate / 2);
        var ducked = Rms(Render(engine, 512));

        Assert.True(ducked < open * 0.6, $"ducked {ducked:F3} is not below open {open:F3}");
        Assert.True(ducked > 0, "ducking must not mute the score");

        // A second of audio later the duck has expired and the level is back.
        Render(engine, Synth.SampleRate);
        Render(engine, Synth.SampleRate / 2);
        var recovered = Rms(Render(engine, 512));

        Assert.InRange(recovered, open * 0.95, open * 1.05);
    }

    [Fact]
    public void AnEffect_SoundsWithoutMusic_AndStopsWhenItEnds()
    {
        var engine = new AudioEngine();
        engine.Play(SfxId.CardHover);

        var start = Render(engine, 512);
        Assert.True(Rms(start) > 0.005, "the effect never sounded");

        // CardHover is 90 ms; once it is spent the mixer must be silent again, not stuck.
        Render(engine, Synth.SampleRate);
        var after = Render(engine, 512);

        Assert.Equal(0, Rms(after));
    }

    [Fact]
    public void ManyEffects_DoNotOutrunTheVoiceBudget()
    {
        var engine = new AudioEngine();

        for (var index = 0; index < 40; index++)
        {
            engine.Play(SfxId.KeyPicked, 0.2f);
        }

        var block = Render(engine, 1024);

        Assert.All(block, sample => Assert.InRange(sample, short.MinValue, short.MaxValue));
        Assert.True(Rms(block) > 0.01, "the burst produced nothing");
    }

    [Fact]
    public void TheSwappedScore_RestartsRatherThanReadingPastTheEnd()
    {
        var engine = new AudioEngine();
        engine.SetScore(FlatScore(0.25f));
        Render(engine, 3000);

        engine.SetScore(FlatScore(0.25f, frames: 1500));

        var block = Render(engine, 512);

        Assert.True(Rms(block) > 0.01);
    }

    /// <summary>Measures a whole loop of a generated track at one intensity.</summary>
    private static double LevelOf(ScoreStems score, MusicIntensity intensity)
    {
        var engine = new AudioEngine();
        engine.SetScore(score);
        engine.SetIntensity(intensity);

        var block = new short[2048 * AudioEngine.Channels];
        var blocks = (score.Frames / 2048) + 2;
        var sum = 0.0;
        var count = 0;

        for (var index = 0; index < blocks; index++)
        {
            engine.Render(block);

            foreach (var sample in block)
            {
                var value = sample / 32768.0;
                sum += value * value;
            }

            count += block.Length;
        }

        return Math.Sqrt(sum / count);
    }

    /// <summary>Builds a score whose stems are a constant level, so gains are easy to measure.</summary>
    private static ScoreStems FlatScore(float level, int frames = 8000)
    {
        var bed = new float[frames];
        var rhythm = new float[frames];
        var lead = new float[frames];

        for (var index = 0; index < frames; index++)
        {
            bed[index] = level;
            rhythm[index] = level;
            lead[index] = level;
        }

        return new ScoreStems(bed, rhythm, lead, BarFrames);
    }

    private static short[] Render(AudioEngine engine, int frames)
    {
        var block = new short[frames * AudioEngine.Channels];
        engine.Render(block);
        return block;
    }

    /// <summary>Measures a block's level as a fraction of full scale.</summary>
    private static double Rms(short[] block)
    {
        var sum = 0.0;

        foreach (var sample in block)
        {
            var value = sample / 32768.0;
            sum += value * value;
        }

        return Math.Sqrt(sum / block.Length);
    }
}

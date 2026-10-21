using System;

namespace MazeGame.UI.Audio;

/// <summary>
/// One floor's score, rendered into three loopable stems.
/// </summary>
/// <remarks>
/// The stems exist so the mix can react without re-arranging: the bed is the pad and choir, the
/// rhythm is the bass and percussion, and the lead is the motif and bells. Intensity is then a
/// set of gains, which is why a chase can start on the next bar with no crossfade and no second
/// track.
/// </remarks>
public sealed class ScoreStems
{
    /// <summary>Creates a score from its three stems.</summary>
    /// <param name="bed">The pad and choir.</param>
    /// <param name="rhythm">The bass and percussion.</param>
    /// <param name="lead">The motif and bells.</param>
    /// <param name="barFrames">How many samples one bar lasts.</param>
    public ScoreStems(float[] bed, float[] rhythm, float[] lead, int barFrames)
    {
        ArgumentNullException.ThrowIfNull(bed);
        ArgumentNullException.ThrowIfNull(rhythm);
        ArgumentNullException.ThrowIfNull(lead);

        if (bed.Length != rhythm.Length || bed.Length != lead.Length)
        {
            throw new ArgumentException("The three stems must be the same length.", nameof(rhythm));
        }

        Bed = bed;
        Rhythm = rhythm;
        Lead = lead;
        BarFrames = barFrames;
    }

    /// <summary>Gets the sustained stem: pad and choir.</summary>
    public float[] Bed { get; }

    /// <summary>Gets the rhythmic stem: bass and drums.</summary>
    public float[] Rhythm { get; }

    /// <summary>Gets the melodic stem: the motif and the bells.</summary>
    public float[] Lead { get; }

    /// <summary>Gets the length of one bar in samples, for beat-aligned mix changes.</summary>
    public int BarFrames { get; }

    /// <summary>Gets the loop length in samples.</summary>
    public int Frames => Bed.Length;

    /// <summary>Gets the loop length in seconds.</summary>
    public double LoopSeconds => Frames / (double)Synth.SampleRate;
}

/// <summary>
/// Arranges a <see cref="MusicTheme"/> into a loop and renders it, offline, into stems.
/// </summary>
/// <remarks>
/// <para>
/// The whole track is generated from the theme plus a seed: chord bed from the progression, bass
/// from the bar roots, the floor's motif transformed by its band, and drums from the density.
/// Rendering happens once per floor, while the intro card is up, so the per-frame cost of the
/// music afterwards is a copy of three arrays rather than a synth.
/// </para>
/// <para>
/// Every note that runs past the end of the loop is wrapped to the start, and the reverb is run
/// around the loop point (see <see cref="Reverb.Apply"/>), so the last sample flows into the
/// first. That is what makes a generated loop sound composed rather than cut.
/// </para>
/// </remarks>
public static class ScoreRenderer
{
    /// <summary>The peak the finished mix is normalised to, leaving headroom for the effects.</summary>
    private const float TargetPeak = 0.72f;

    /// <summary>
    /// Renders <paramref name="theme"/> for <paramref name="floorNumber"/>.
    /// </summary>
    /// <param name="theme">The floor's music parameters.</param>
    /// <param name="floorNumber">The floor's number, which selects the motif.</param>
    /// <param name="seed">The run seed; it only humanises velocity and timing.</param>
    /// <returns>The three stems, looping seamlessly.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static ScoreStems Render(MusicTheme theme, int floorNumber, int seed)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Progression);

        var stepFrames = (int)Math.Round(60.0 / theme.Tempo / 4.0 * Synth.SampleRate);
        var barFrames = stepFrames * 16;
        var loopFrames = barFrames * theme.BarsPerLoop;
        var barSeconds = barFrames / (double)Synth.SampleRate;

        var bed = new float[loopFrames];
        var rhythm = new float[loopFrames];
        var lead = new float[loopFrames];

        var human = new NoiseSource(SeedFor(theme.ThemeId, seed));
        var motif = MusicCatalog.MotifFor(floorNumber);

        var chordSteps = theme.Tension > 0.62 ? new[] { 0, 2, 4, 6 } : new[] { 0, 2, 4 };

        for (var bar = 0; bar < theme.BarsPerLoop; bar++)
        {
            var barStart = bar * barFrames;
            var degree = theme.Progression[bar % theme.Progression.Length];

            RenderPad(theme, bed, barStart, barFrames, barSeconds, degree, chordSteps);
            RenderChoir(theme, bed, barStart, barFrames, barSeconds, degree, chordSteps);
            RenderBass(theme, rhythm, barStart, stepFrames, degree);
            RenderBell(theme, lead, barStart, stepFrames, degree);
            RenderDrums(theme, rhythm, barStart, stepFrames);
        }

        RenderLead(theme, lead, motif, stepFrames, human);

        // A themed canon: a second statement of the motif half a loop later, quieter, so the same
        // tune is heard against itself. Only the last floor asks for it.
        if (theme.Canon)
        {
            RenderLead(theme, lead, motif, stepFrames, human, lead.Length / 2, 0.45f);
        }

        // The reverb runs around the loop point, so the tail of the last bar is already present
        // when the loop restarts.
        new Reverb().Apply(bed, theme.ReverbWet, circular: true);
        new Reverb().Apply(lead, theme.ReverbWet * 0.6, circular: true);

        Mix(bed, rhythm, lead);

        return new ScoreStems(bed, rhythm, lead, barFrames);
    }

    /// <summary>Renders the sustained chord bed for one bar.</summary>
    private static void RenderPad(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree,
        int[] chordSteps)
    {
        var attack = barSeconds * 0.22;
        var release = barSeconds * 0.3;
        var detune = 1.0 + 0.003 + (theme.Tension * 0.006);

        foreach (var step in chordSteps)
        {
            var hertz = Synth.MidiToHz(theme.ChordTone(degree, step) + 12);
            var gain = theme.Mix.Pad / chordSteps.Length;

            var low = new OnePole(2400);
            var phase = 0.0;
            var detuned = 0.0;
            var sub = 0.0;

            for (var index = 0; index < barFrames; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, barSeconds, attack, release) * gain;

                if (level <= 0)
                {
                    continue;
                }

                var value = (Synth.Saw(phase) * 0.5f) +
                            (Synth.Saw(detuned) * 0.35f) +
                            (Synth.SineFast(sub) * 0.3f);

                value = low.Process(value);

                bed[(barStart + index) % bed.Length] += value * (float)level;

                phase = Synth.Advance(phase, hertz);
                detuned = Synth.Advance(detuned, hertz * detune);
                sub = Synth.Advance(sub, hertz * 0.5);
            }
        }
    }

    /// <summary>Renders the slow choir voice for one bar, when the floor has one.</summary>
    private static void RenderChoir(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree,
        int[] chordSteps)
    {
        if (theme.Mix.Choir <= 0)
        {
            return;
        }

        var attack = barSeconds * 0.4;
        var release = barSeconds * 0.25;

        foreach (var step in chordSteps)
        {
            var hertz = Synth.MidiToHz(theme.ChordTone(degree, step) + 24);
            var gain = theme.Mix.Choir / chordSteps.Length * 0.5;
            var band = new BandPass(hertz * 3.0, 1.2);

            var phase = 0.0;
            var drift = 0.0;
            var driftPhase = 0.0;

            for (var index = 0; index < barFrames; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, barSeconds, attack, release) * gain;

                if (level <= 0)
                {
                    continue;
                }

                // A gentle vibrato is the whole difference between "organ" and "voice".
                var vibrato = 1.0 + (0.004 * Synth.SineFast(driftPhase));
                var value = (Synth.Triangle(phase) * 0.6f) + (band.Process(Synth.Saw(drift)) * 0.25f);

                bed[(barStart + index) % bed.Length] += value * (float)level;

                phase = Synth.Advance(phase, hertz * vibrato);
                drift = Synth.Advance(drift, hertz * 1.004);
                driftPhase = Synth.Advance(driftPhase, 5.0);
            }
        }
    }

    /// <summary>Renders the bass ostinato for one bar.</summary>
    private static void RenderBass(MusicTheme theme, float[] rhythm, int barStart, int stepFrames, int degree)
    {
        var root = theme.Midi(degree) - 12;

        AddBassNote(theme, rhythm, barStart, stepFrames * 2, root, 1.0f);

        if (theme.Density > 0.45)
        {
            AddBassNote(theme, rhythm, barStart + (stepFrames * 8), stepFrames * 2, root, 0.9f);
        }

        if (theme.Density > 0.52)
        {
            AddBassNote(theme, rhythm, barStart + (stepFrames * 4), stepFrames, root, 0.5f);
            AddBassNote(theme, rhythm, barStart + (stepFrames * 12), stepFrames, root, 0.5f);
        }

        if (theme.Density > 0.62)
        {
            AddBassNote(theme, rhythm, barStart + (stepFrames * 14), stepFrames, theme.Midi(degree + 4) - 12, 0.45f);
        }
    }

    /// <summary>Renders one bass note.</summary>
    private static void AddBassNote(
        MusicTheme theme,
        float[] rhythm,
        int startFrame,
        int frames,
        int midi,
        float velocity)
    {
        if (theme.Mix.Bass <= 0)
        {
            return;
        }

        var hertz = Synth.MidiToHz(midi);
        var low = new OnePole(420);
        var phase = 0.0;
        var overtone = 0.0;
        var gain = theme.Mix.Bass * 0.5f * velocity;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.17, 0.004) * gain;

            var value = low.Process((Synth.SineFast(phase) * 0.75f) + (Synth.Saw(overtone) * 0.3f));
            rhythm[(startFrame + index) % rhythm.Length] += value * (float)level;

            phase = Synth.Advance(phase, hertz);
            overtone = Synth.Advance(overtone, hertz * 2.0);
        }
    }

    /// <summary>Renders the floor's motif over the whole loop.</summary>
    private static void RenderLead(
        MusicTheme theme,
        float[] lead,
        Motif motif,
        int stepFrames,
        NoiseSource human,
        int offsetFrames = 0,
        float level = 1.0f)
    {
        if (theme.Mix.Lead <= 0)
        {
            return;
        }

        var degrees = Transform(motif.Degrees, theme.Band);
        var noteFrames = stepFrames * motif.Spacing;

        for (var index = 0; index < (lead.Length / noteFrames) + 1; index++)
        {
            var start = (index * noteFrames) + offsetFrames;
            var bar = (start % lead.Length) / (stepFrames * 16);
            var degree = theme.Progression[bar % theme.Progression.Length] + degrees[index % degrees.Length];
            var midi = theme.Midi(degree) + 12;
            var velocity = (0.82f + (0.09f * human.Next())) * level;

            AddLeadNote(theme, lead, start, (int)(noteFrames * 0.92), midi, velocity);
        }
    }

    /// <summary>Renders one motif note.</summary>
    private static void AddLeadNote(
        MusicTheme theme,
        float[] lead,
        int startFrame,
        int frames,
        int midi,
        float velocity)
    {
        var hertz = Synth.MidiToHz(midi);
        var phase = 0.0;
        var third = 0.0;
        var gain = theme.Mix.Lead * 0.34f * velocity;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.21, 0.006) * gain;

            var value = (Synth.Triangle(phase) * 0.65f) + (Synth.SineFast(third) * 0.2f);
            lead[(startFrame + index) % lead.Length] += value * (float)level;

            phase = Synth.Advance(phase, hertz);
            third = Synth.Advance(third, hertz * 3.0);
        }
    }

    /// <summary>Renders a bell at the start of a bar on the deeper floors.</summary>
    private static void RenderBell(MusicTheme theme, float[] lead, int barStart, int stepFrames, int degree)
    {
        if (theme.Band < 3 || theme.Mix.Lead <= 0)
        {
            return;
        }

        var hertz = Synth.MidiToHz(theme.ChordTone(degree, 4) + 24);
        var frames = stepFrames * 14;
        var gain = theme.Mix.Lead * 0.16f;

        var phase = 0.0;
        var partial = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.42, 0.003) * gain;

            var value = (Synth.SineFast(phase) * 0.7f) + (Synth.SineFast(partial) * 0.25f);
            lead[(barStart + index) % lead.Length] += value * (float)level;

            phase = Synth.Advance(phase, hertz);
            partial = Synth.Advance(partial, hertz * 2.76);
        }
    }

    /// <summary>Renders kick, hat and snare for one bar.</summary>
    private static void RenderDrums(MusicTheme theme, float[] rhythm, int barStart, int stepFrames)
    {
        if (theme.Mix.Perc <= 0)
        {
            return;
        }

        var gain = (float)theme.Mix.Perc;

        AddKick(rhythm, barStart, stepFrames * 6, gain);
        AddKick(rhythm, barStart + (stepFrames * 8), stepFrames * 6, gain * 0.9f);

        for (var step = 0; step < 16; step += 2)
        {
            AddHat(rhythm, barStart + (step * stepFrames), stepFrames * 2, gain * (step % 4 == 0 ? 0.2f : 0.12f));
        }

        if (theme.Density > 0.5)
        {
            AddSnare(rhythm, barStart + (stepFrames * 4), stepFrames * 3, gain * 0.34f);
            AddSnare(rhythm, barStart + (stepFrames * 12), stepFrames * 3, gain * 0.38f);
        }
    }

    /// <summary>Renders a kick: a fast pitch drop with a short body.</summary>
    private static void AddKick(float[] rhythm, int startFrame, int frames, float gain)
    {
        var phase = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var progress = index / (double)frames;
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.085) * gain * 0.9f;

            rhythm[(startFrame + index) % rhythm.Length] +=
                Synth.SineFast(phase) * (float)level;

            phase = Synth.Advance(phase, 118.0 - (70.0 * Math.Min(1.0, progress * 6.0)));
        }
    }

    /// <summary>Renders a hat: high-passed noise with a very short decay.</summary>
    private static void AddHat(float[] rhythm, int startFrame, int frames, float gain)
    {
        var noise = new NoiseSource((uint)(0x9E37 + startFrame));
        var low = new OnePole(3600);

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.016) * gain;
            var sample = noise.Next();
            var high = sample - low.Process(sample);

            rhythm[(startFrame + index) % rhythm.Length] += high * (float)level;
        }
    }

    /// <summary>Renders a snare: band-passed noise over a short body tone.</summary>
    private static void AddSnare(float[] rhythm, int startFrame, int frames, float gain)
    {
        var noise = new NoiseSource((uint)(0x51A7 + startFrame));
        var filter = new OnePole(1800);
        var phase = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.055) * gain;

            var value = (filter.Process(noise.Next()) * 0.8f) + (Synth.SineFast(phase) * 0.3f);
            rhythm[(startFrame + index) % rhythm.Length] += value * (float)level;

            phase = Synth.Advance(phase, 190.0);
        }
    }

    /// <summary>
    /// Applies the floor's transformation to the motif: inverted from floor 11, retrograded from
    /// floor 16. The melody is the thread that ties twenty floors together, so it is the thing
    /// that gets progressively wrong.
    /// </summary>
    private static int[] Transform(int[] degrees, int band)
    {
        var result = (int[])degrees.Clone();

        if (band >= 4)
        {
            Array.Reverse(result);
        }

        if (band >= 3)
        {
            var pivot = result[0];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = (2 * pivot) - result[index];
            }
        }

        return result;
    }

    /// <summary>
    /// Balances the three stems against each other, then scales them together under the ceiling.
    /// </summary>
    /// <remarks>
    /// The arrangement is not mixed by hand, so the balance has to be derived: a sustained pad with
    /// three notes under it is far louder than a few drum hits, and without this step opening the
    /// rhythm and lead at a chase would add almost nothing the ear could hear. Each of them is
    /// trimmed to a fraction of the bed's level, then the whole mix is normalised to one peak so a
    /// loud floor cannot clip.
    /// </remarks>
    private static void Mix(float[] bed, float[] rhythm, float[] lead)
    {
        var bedLevel = Synth.Rms(bed);

        if (bedLevel <= 0)
        {
            return;
        }

        Trim(rhythm, GainFor(Synth.Rms(rhythm), bedLevel * 0.75));
        Trim(lead, GainFor(Synth.Rms(lead), bedLevel * 0.55));

        var peak = 0.0f;

        for (var index = 0; index < bed.Length; index++)
        {
            var sum = Math.Abs(bed[index] + rhythm[index] + lead[index]);
            if (sum > peak)
            {
                peak = sum;
            }
        }

        if (peak <= 0)
        {
            return;
        }

        var gain = TargetPeak / peak;

        for (var index = 0; index < bed.Length; index++)
        {
            bed[index] = Synth.SoftClip(bed[index] * gain);
            rhythm[index] = Synth.SoftClip(rhythm[index] * gain);
            lead[index] = Synth.SoftClip(lead[index] * gain);
        }
    }

    /// <summary>Finds the gain that takes <paramref name="current"/> to <paramref name="target"/>.</summary>
    private static float GainFor(double current, double target) =>
        current <= 1e-6 ? 1.0f : (float)Math.Clamp(target / current, 0.25, 6.0);

    /// <summary>Scales a buffer in place.</summary>
    private static void Trim(float[] buffer, float gain)
    {
        if (Math.Abs(gain - 1.0f) < 1e-6)
        {
            return;
        }

        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] *= gain;
        }
    }

    /// <summary>Derives the humanisation seed from the theme and the run.</summary>
    private static uint SeedFor(string themeId, int seed)
    {
        var hash = 2166136261u;

        foreach (var character in themeId)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash ^ (uint)seed;
    }
}

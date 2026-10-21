using System;

namespace MazeGame.UI.Audio;

/// <summary>
/// One floor's score, rendered into three loopable stems.
/// </summary>
/// <remarks>
/// The stems exist so the mix can react without re-arranging: the bed is the pad and choir, the
/// rhythm is the bass and percussion, and the lead is the melody and bells. Intensity is then a
/// set of gains, which is why a chase can start on the next bar with no crossfade and no second
/// track.
/// </remarks>
public sealed class ScoreStems
{
    /// <summary>Creates a score from its three stems.</summary>
    /// <param name="bed">The pad and choir.</param>
    /// <param name="rhythm">The bass and percussion.</param>
    /// <param name="lead">The melody and bells.</param>
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

    /// <summary>Gets the melodic stem: the melody and the bells.</summary>
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
/// The theme decides the piece: its <see cref="MusicTheme.Style"/> picks the instruments and the
/// rhythm, its <see cref="MusicTheme.Melody"/> is the tune, its mode and progression are the
/// harmony, and its meter decides whether a bar is four beats, three or five. An arrangement is
/// therefore not one fixed template with different numbers — a drone, a waltz and a 5/4 machine
/// piece share the stems and the loop rule, nothing else.
/// </para>
/// <para>
/// Rendering happens once per floor, while the intro card is up, so the per-frame cost afterwards
/// is a copy of three arrays. Every note that runs past the end of the loop is wrapped to the
/// start, and the reverb runs around the loop point, so the last sample flows into the first.
/// </para>
/// </remarks>
public static class ScoreRenderer
{
    /// <summary>The peak the finished mix is normalised to, leaving headroom for the effects.</summary>
    private const float TargetPeak = 0.72f;

    /// <summary>
    /// Renders <paramref name="theme"/> for <paramref name="floorNumber"/>.
    /// </summary>
    /// <param name="theme">The floor's piece.</param>
    /// <param name="floorNumber">The floor's number, used to salt the humanisation.</param>
    /// <param name="seed">The run seed; it only humanises velocity and timing.</param>
    /// <returns>The three stems, looping seamlessly.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
    public static ScoreStems Render(MusicTheme theme, int floorNumber, int seed)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Progression);
        ArgumentNullException.ThrowIfNull(theme.Melody);

        var stepFrames = (int)Math.Round(60.0 / theme.Tempo / 4.0 * Synth.SampleRate);
        var barFrames = stepFrames * theme.StepsPerBar;
        var loopFrames = barFrames * theme.BarsPerLoop;
        var barSeconds = barFrames / (double)Synth.SampleRate;

        var bed = new float[loopFrames];
        var rhythm = new float[loopFrames];
        var lead = new float[loopFrames];

        var human = new NoiseSource(SeedFor(theme.ThemeId, seed) ^ (uint)floorNumber);

        for (var bar = 0; bar < theme.BarsPerLoop; bar++)
        {
            var barStart = bar * barFrames;
            var degree = theme.Progression[bar % theme.Progression.Length];

            RenderBed(theme, bed, barStart, barFrames, barSeconds, degree, bar);
            RenderBass(theme, rhythm, barStart, stepFrames, degree, bar);
            RenderRhythm(theme, rhythm, barStart, stepFrames, bar);
            RenderBells(theme, lead, barStart, stepFrames, degree, bar);
        }

        RenderMelody(theme, lead, stepFrames, human);

        // A style can add a second line of its own: the wispy interlock of a gamelan or the
        // sixteen-step arpeggio under a pulse.
        RenderCounterline(theme, lead, stepFrames);

        // A themed canon: a second statement of the melody half a loop later, quieter, so the same
        // tune is heard against itself. Only the last floor asks for it.
        if (theme.Canon)
        {
            RenderMelody(theme, lead, stepFrames, human, lead.Length / 2, 0.45f);
        }

        // The reverb runs around the loop point, so the tail of the last bar is already present
        // when the loop restarts.
        new Reverb().Apply(bed, theme.ReverbWet, circular: true);
        new Reverb().Apply(lead, theme.ReverbWet * 0.6, circular: true);

        Mix(bed, rhythm, lead);

        // The bed is smooth around the loop by construction; a drum or a staccato lead can end
        // mid-hit, and that step is a click. A two-millisecond fade at both ends removes it
        // without a gap anyone can hear.
        SmoothEdges(rhythm);
        SmoothEdges(lead);

        return new ScoreStems(bed, rhythm, lead, barFrames);
    }

    /// <summary>Draws the bar's chord bed in the style's own voice.</summary>
    private static void RenderBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree,
        int bar)
    {
        switch (theme.Style)
        {
            case MusicStyle.Drone:
                RenderDroneBed(theme, bed, barStart, barFrames, degree);
                break;

            case MusicStyle.Gamelan:
                RenderGongBed(theme, bed, barStart, barFrames, degree, bar);
                break;

            case MusicStyle.Machine:
                RenderMachineBed(theme, bed, barStart, barFrames, degree, bar);
                break;

            case MusicStyle.Requiem:
                RenderOrganBed(theme, bed, barStart, barFrames, barSeconds, degree);
                break;

            case MusicStyle.Chant:
                RenderChoirBed(theme, bed, barStart, barFrames, barSeconds, degree);
                break;

            case MusicStyle.Lullaby:
            case MusicStyle.Nocturne:
                RenderSoftBed(theme, bed, barStart, barFrames, barSeconds, degree);
                break;

            default:
                RenderSawBed(theme, bed, barStart, barFrames, barSeconds, degree);
                break;
        }
    }

    /// <summary>A held fifth with two detuned saws: pressure rather than a chord.</summary>
    private static void RenderDroneBed(MusicTheme theme, float[] bed, int barStart, int barFrames, int degree)
    {
        var tonic = theme.ChordTone(degree, 0) + 12;
        var attack = barFrames * 0.45;
        var release = barFrames * 0.35;
        var voices = new (int Midi, double Detune, float Level)[]
        {
            (tonic, 1.0, 0.55f),
            (tonic, 1.004, 0.4f),
            (theme.ChordTone(degree, 4) + 12, 1.0, 0.22f),
        };

        foreach (var (midi, detune, level) in voices)
        {
            var hertz = Synth.MidiToHz(midi) * detune;
            var phase = 0.0;
            var low = new OnePole(900);
            var gain = theme.Mix.Pad * level;

            for (var index = 0; index < barFrames; index++)
            {
                var envelope = Math.Min(1.0, index / attack) * Math.Min(1.0, (barFrames - index) / release);

                if (envelope <= 0)
                {
                    continue;
                }

                bed[(barStart + index) % bed.Length] += low.Process(Synth.Saw(phase)) * (float)(envelope * gain * 0.28);
                phase = Synth.Advance(phase, hertz);
            }
        }

        // The sub is what the player feels rather than hears: one sine an octave down.
        var subHertz = Synth.MidiToHz(tonic - 12);
        var subPhase = 0.0;

        for (var index = 0; index < barFrames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var envelope = Synth.Sustain(t, barFrames / (double)Synth.SampleRate, attack / Synth.SampleRate, release / Synth.SampleRate);

            bed[(barStart + index) % bed.Length] += Synth.SineFast(subPhase) * (float)(envelope * theme.Mix.Pad * 0.22);
            subPhase = Synth.Advance(subPhase, subHertz);
        }
    }

    /// <summary>A struck gong at the bar line, with inharmonic partials, over a quiet fifth.</summary>
    private static void RenderGongBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        int degree,
        int bar)
    {
        var root = theme.ChordTone(degree, 0) + 12 + (bar % 2 == 0 ? 0 : 5);
        var frames = Math.Min(barFrames + (barFrames / 2), bed.Length);
        var gain = theme.Mix.Pad * 0.30f;
        var fundamental = Synth.MidiToHz(root);
        var partial = Synth.MidiToHz(root) * 2.76;
        var high = Synth.MidiToHz(root) * 5.40;

        var phase = 0.0;
        var phasePartial = 0.0;
        var phaseHigh = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.85, 0.002) * gain;

            var value = (Synth.SineFast(phase) * 0.8f) +
                (Synth.SineFast(phasePartial) * 0.22f) +
                (Synth.SineFast(phaseHigh) * 0.08f);

            bed[(barStart + index) % bed.Length] += value * (float)level;

            phase = Synth.Advance(phase, fundamental);
            phasePartial = Synth.Advance(phasePartial, partial);
            phaseHigh = Synth.Advance(phaseHigh, high);
        }

        // A soft, unchanging fifth underneath keeps the floor from being silent between gongs.
        var fifth = Synth.MidiToHz(theme.ChordTone(degree, 4) + 12);
        var fifthPhase = 0.0;

        for (var index = 0; index < barFrames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var envelope = Synth.Sustain(t, barFrames / (double)Synth.SampleRate, 0.02, 0.3);

            bed[(barStart + index) % bed.Length] += Synth.SineFast(fifthPhase) * (float)(envelope * theme.Mix.Pad * 0.10);
            fifthPhase = Synth.Advance(fifthPhase, fifth);
        }
    }

    /// <summary>A filtered saw stack and a noise wash: the room is machinery.</summary>
    private static void RenderMachineBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        int degree,
        int bar)
    {
        var root = theme.ChordTone(degree, 0) + 12;
        var fifth = theme.ChordTone(degree, 4) + 12;
        var noise = new NoiseSource((uint)(0x51AA + barStart));
        var wash = new BandPass(260, 0.8);
        var low = new OnePole(700);
        var rootPhase = 0.0;
        var fifthPhase = 0.0;
        var detuned = 0.0;

        for (var index = 0; index < barFrames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var envelope = Synth.Sustain(t, barFrames / (double)Synth.SampleRate, 0.01, 0.12);
            var value =
                (Synth.Saw(rootPhase) * 0.4f) +
                (Synth.Saw(detuned) * 0.3f) +
                (Synth.Square(fifthPhase) * 0.16f) +
                (wash.Process(noise.Next()) * 0.30f);

            bed[(barStart + index) % bed.Length] += low.Process(value) * (float)(envelope * theme.Mix.Pad * 0.34);

            rootPhase = Synth.Advance(rootPhase, Synth.MidiToHz(root));
            detuned = Synth.Advance(detuned, Synth.MidiToHz(root) * 1.008);
            fifthPhase = Synth.Advance(fifthPhase, Synth.MidiToHz(fifth));
        }
    }

    /// <summary>An organ: stacked sines with a slow swell and a gentle tremolo.</summary>
    private static void RenderOrganBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree)
    {
        var chord = new[] { 0, 2, 4, 7 };
        var attack = barSeconds * 0.35;
        var release = barSeconds * 0.3;

        foreach (var step in chord)
        {
            var midi = theme.ChordTone(degree, step) + 12;
            var hertz = Synth.MidiToHz(midi);
            var gain = theme.Mix.Pad / chord.Length * 0.9;
            var phase = 0.0;
            var octave = 0.0;
            var tremolo = 0.0;

            for (var index = 0; index < barFrames; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, barSeconds, attack, release) * gain;
                var swell = 0.85 + (0.15 * Synth.SineFast(tremolo));

                var value = (Synth.SineFast(phase) * 0.7f) + (Synth.SineFast(octave) * 0.18f);
                bed[(barStart + index) % bed.Length] += value * (float)(level * swell);

                phase = Synth.Advance(phase, hertz);
                octave = Synth.Advance(octave, hertz * 2.0);
                tremolo = Synth.Advance(tremolo, 4.5);
            }
        }
    }

    /// <summary>A formant choir on root, fifth and octave: the voices are the harmony.</summary>
    private static void RenderChoirBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree)
    {
        var chord = new[] { 0, 4, 7 };
        var attack = barSeconds * 0.45;
        var release = barSeconds * 0.22;

        foreach (var step in chord)
        {
            var hertz = Synth.MidiToHz(theme.ChordTone(degree, step) + 12);
            var gain = theme.Mix.Choir / chord.Length * 0.55;
            var band = new BandPass(hertz * 2.6, 1.4);
            var phase = 0.0;
            var drift = 0.0;
            var vibrato = 0.0;

            for (var index = 0; index < barFrames; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, barSeconds, attack, release) * gain;

                if (level <= 0)
                {
                    continue;
                }

                var wobble = 1.0 + (0.005 * Synth.SineFast(vibrato));
                var value = (Synth.Triangle(phase) * 0.55f) + (band.Process(Synth.Saw(drift)) * 0.3f);

                bed[(barStart + index) % bed.Length] += value * (float)level;

                phase = Synth.Advance(phase, hertz * wobble);
                drift = Synth.Advance(drift, hertz * 1.003);
                vibrato = Synth.Advance(vibrato, 4.8);
            }
        }
    }

    /// <summary>A triangle chord with a slow swell: a box opened in another room.</summary>
    private static void RenderSoftBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree)
    {
        var chord = new[] { 0, 2, 4 };
        var attack = barSeconds * 0.5;
        var release = barSeconds * 0.4;

        foreach (var step in chord)
        {
            var hertz = Synth.MidiToHz(theme.ChordTone(degree, step) + 12);
            var gain = theme.Mix.Pad / chord.Length * 0.6;
            var high = 0.0;
            var phase = 0.0;

            for (var index = 0; index < barFrames; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, barSeconds, attack, release) * gain;

                if (level <= 0)
                {
                    continue;
                }

                var value = (Synth.Triangle(phase) * 0.6f) + (Synth.SineFast(high) * 0.12f);
                bed[(barStart + index) % bed.Length] += value * (float)level;

                phase = Synth.Advance(phase, hertz);
                high = Synth.Advance(high, hertz * 3.0);
            }
        }
    }

    /// <summary>The original pad: a detuned saw chord with a sub, for the driving styles.</summary>
    private static void RenderSawBed(
        MusicTheme theme,
        float[] bed,
        int barStart,
        int barFrames,
        double barSeconds,
        int degree)
    {
        var chordSteps = theme.Tension > 0.62 ? new[] { 0, 2, 4, 6 } : new[] { 0, 2, 4 };
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

    /// <summary>Renders the bass line the style calls for.</summary>
    private static void RenderBass(
        MusicTheme theme,
        float[] rhythm,
        int barStart,
        int stepFrames,
        int degree,
        int bar)
    {
        if (theme.Mix.Bass <= 0)
        {
            return;
        }

        var root = theme.Midi(degree) - 12;
        var fifth = theme.Midi(degree + 4) - 12;
        var third = theme.Midi(degree + 2) - 12;
        var steps = theme.StepsPerBar;

        switch (theme.Style)
        {
            case MusicStyle.Drone:
            case MusicStyle.Requiem:
                // The bed already holds the low end; a bass here would only thicken the mud.
                return;

            case MusicStyle.Lullaby:
                AddBassNote(theme, rhythm, barStart, stepFrames * 6, root, 0.75f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * (steps / 2)), stepFrames * 4, fifth, 0.5f);
                return;

            case MusicStyle.Nocturne:
                AddBassNote(theme, rhythm, barStart, stepFrames * 8, root, 0.7f);
                return;

            case MusicStyle.Chant:
                AddBassNote(theme, rhythm, barStart, stepFrames * steps, root, 0.6f);
                return;

            case MusicStyle.Procession:
                AddBassNote(theme, rhythm, barStart, stepFrames * 3, root, 1.0f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * 4), stepFrames * 3, root, 0.85f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * 8), stepFrames * 3, fifth, 0.8f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * 12), stepFrames * 3, third, 0.7f);

                if (bar % 2 == 1)
                {
                    AddBassNote(theme, rhythm, barStart + (stepFrames * 14), stepFrames * 2, fifth, 0.55f);
                }

                return;

            case MusicStyle.Pulse:
                for (var step = 0; step < steps; step += 2)
                {
                    var midi = step % 8 == 6 ? fifth : root;
                    AddBassNote(theme, rhythm, barStart + (step * stepFrames), stepFrames, midi, step % 4 == 0 ? 0.95f : 0.55f);
                }

                return;

            case MusicStyle.Waltz:
                AddBassNote(theme, rhythm, barStart, stepFrames * 3, root, 1.0f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * 4), stepFrames * 2, fifth, 0.7f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * 8), stepFrames * 2, third, 0.6f);
                return;

            case MusicStyle.Gamelan:
                AddBassNote(theme, rhythm, barStart, stepFrames * 2, root, 0.9f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * (steps / 2)), stepFrames * 2, root + 12, 0.7f);
                AddBassNote(theme, rhythm, barStart + (stepFrames * (steps - 4)), stepFrames * 2, fifth, 0.6f);
                return;

            case MusicStyle.Machine:
                for (var step = 0; step < steps; step += 2)
                {
                    var midi = step == steps - 2 ? root + 1 : root;
                    AddBassNote(theme, rhythm, barStart + (step * stepFrames), stepFrames, midi, step % 4 == 0 ? 1.0f : 0.65f);
                }

                return;

            default:
                AddBassNote(theme, rhythm, barStart, stepFrames * 2, root, 1.0f);
                return;
        }
    }

    /// <summary>Renders the percussion the style calls for: some floors have none at all.</summary>
    private static void RenderRhythm(
        MusicTheme theme,
        float[] rhythm,
        int barStart,
        int stepFrames,
        int bar)
    {
        if (theme.Mix.Perc <= 0)
        {
            return;
        }

        var gain = (float)theme.Mix.Perc;
        var steps = theme.StepsPerBar;

        switch (theme.Style)
        {
            case MusicStyle.Drone:
            case MusicStyle.Lullaby:
            case MusicStyle.Nocturne:
            case MusicStyle.Gamelan:
            case MusicStyle.Requiem:
                return;

            case MusicStyle.Chant:
                AddFrameDrum(rhythm, barStart, stepFrames * 8, gain * 0.55f);
                AddFrameDrum(rhythm, barStart + (stepFrames * (steps / 2)), stepFrames * 6, gain * 0.4f);
                return;

            case MusicStyle.Procession:
                AddKick(rhythm, barStart, stepFrames * 5, gain);
                AddKick(rhythm, barStart + (stepFrames * (steps / 2)), stepFrames * 5, gain * 0.85f);
                AddSnare(rhythm, barStart + (stepFrames * 4), stepFrames * 3, gain * 0.4f);
                AddSnare(rhythm, barStart + (stepFrames * (steps - 4)), stepFrames * 3, gain * 0.45f);
                return;

            case MusicStyle.Waltz:
                AddKick(rhythm, barStart, stepFrames * 3, gain * 0.85f);
                AddHat(rhythm, barStart + (stepFrames * 4), stepFrames * 2, gain * 0.30f);
                AddHat(rhythm, barStart + (stepFrames * 8), stepFrames * 2, gain * 0.26f);
                AddSnare(rhythm, barStart + (stepFrames * 10), stepFrames * 2, gain * 0.24f);
                return;

            case MusicStyle.Machine:
                AddKick(rhythm, barStart, stepFrames * 4, gain);
                AddKick(rhythm, barStart + (stepFrames * 6), stepFrames * 3, gain * 0.8f);
                AddKick(rhythm, barStart + (stepFrames * 10), stepFrames * 4, gain * 0.9f);
                AddKick(rhythm, barStart + (stepFrames * 15), stepFrames * 3, gain * 0.75f);
                AddSnare(rhythm, barStart + (stepFrames * 8), stepFrames * 3, gain * 0.55f);
                AddSnare(rhythm, barStart + (stepFrames * (steps - 4)), stepFrames * 3, gain * 0.6f);

                for (var step = 5; step < steps; step += 7)
                {
                    AddClank(rhythm, barStart + (step * stepFrames), stepFrames * 3, gain * 0.5f);
                }

                return;

            default:
                // Pulse: four on the floor, hats on the offbeat, a snare either side of the bar.
                for (var step = 0; step < steps; step += 4)
                {
                    AddKick(rhythm, barStart + (step * stepFrames), stepFrames * 4, gain);
                }

                for (var step = 0; step < steps; step += 2)
                {
                    AddHat(rhythm, barStart + (step * stepFrames), stepFrames, gain * (step % 4 == 0 ? 0.18f : 0.11f));
                }

                AddSnare(rhythm, barStart + (stepFrames * 4), stepFrames * 3, gain * 0.35f);
                AddSnare(rhythm, barStart + (stepFrames * 12), stepFrames * 3, gain * 0.4f);

                if (theme.Density > 0.6 && bar % 2 == 1)
                {
                    AddSnare(rhythm, barStart + (stepFrames * 14), stepFrames * 2, gain * 0.25f);
                }

                return;
        }
    }

    /// <summary>Renders a bell or gong accent on the styles that use one.</summary>
    private static void RenderBells(
        MusicTheme theme,
        float[] lead,
        int barStart,
        int stepFrames,
        int degree,
        int bar)
    {
        if (theme.Mix.Lead <= 0)
        {
            return;
        }

        var wantsBell = theme.Style is MusicStyle.Procession or MusicStyle.Gamelan or MusicStyle.Requiem
            or MusicStyle.Drone;

        if (!wantsBell || (theme.Style == MusicStyle.Gamelan && bar % 2 == 1))
        {
            return;
        }

        var root = theme.Style == MusicStyle.Gamelan
            ? theme.ChordTone(degree, 6) + 24
            : theme.ChordTone(degree, 4) + 24;

        AddBell(theme, lead, barStart, stepFrames * Math.Max(6, theme.StepsPerBar - 2), root, (float)(theme.Mix.Lead * 0.18));
    }

    /// <summary>Plays the floor's melody over the loop.</summary>
    private static void RenderMelody(
        MusicTheme theme,
        float[] lead,
        int stepFrames,
        NoiseSource human,
        int offsetFrames = 0,
        float level = 1.0f)
    {
        if (theme.Mix.Lead <= 0)
        {
            return;
        }

        var degrees = theme.Melody.Degrees;
        var noteFrames = stepFrames * theme.Melody.Spacing;
        var barFrames = stepFrames * theme.StepsPerBar;

        for (var index = 0; index < (lead.Length / noteFrames) + 1; index++)
        {
            var start = (index * noteFrames) + offsetFrames;
            var bar = (start % lead.Length) / barFrames;
            var degree = theme.Progression[bar % theme.Progression.Length] + degrees[index % degrees.Length];
            var midi = theme.Midi(degree) + 12;
            var velocity = (0.82f + (0.09f * human.Next())) * level;

            AddMelodyNote(theme, lead, start, (int)(noteFrames * 0.94), midi, velocity);
        }
    }

    /// <summary>Adds a second line for the styles that interlock or arpeggiate.</summary>
    private static void RenderCounterline(MusicTheme theme, float[] lead, int stepFrames)
    {
        var steps = theme.StepsPerBar;
        var barFrames = stepFrames * steps;

        switch (theme.Style)
        {
            case MusicStyle.Pulse:
            {
                // A sixteen-step arpeggio under the tune: it is what makes the floor move.
                var chord = new[] { 0, 4, 7, 4 };
                var frames = stepFrames * 2;

                for (var bar = 0; bar < theme.BarsPerLoop; bar++)
                {
                    var degree = theme.Progression[bar % theme.Progression.Length];

                    for (var step = 0; step < steps; step += 2)
                    {
                        var tone = chord[(step / 2) % chord.Length];
                        var midi = theme.Midi(degree + tone) + 24;
                        AddPluck(theme, lead, (bar * barFrames) + (step * stepFrames), frames, midi, 0.34f);
                    }
                }

                return;
            }

            case MusicStyle.Gamelan:
            {
                // The interlock: the melody again, three steps late and an octave down.
                var offset = stepFrames * 3;
                var degrees = theme.Melody.Degrees;
                var noteFrames = stepFrames * theme.Melody.Spacing;

                for (var index = 0; index < (lead.Length / noteFrames) + 1; index++)
                {
                    var start = (index * noteFrames) + offset;
                    var bar = (start % lead.Length) / barFrames;
                    var degree = theme.Progression[bar % theme.Progression.Length] + degrees[index % degrees.Length];

                    AddPluck(theme, lead, start, (int)(noteFrames * 0.9), theme.Midi(degree) + 12, 0.30f);
                }

                return;
            }

            case MusicStyle.Machine:
            {
                // An offbeat metal tick that keeps the odd meter countable.
                for (var bar = 0; bar < theme.BarsPerLoop; bar++)
                {
                    var degree = theme.Progression[bar % theme.Progression.Length];

                    for (var step = 3; step < steps; step += 5)
                    {
                        AddPluck(
                            theme,
                            lead,
                            (bar * barFrames) + (step * stepFrames),
                            stepFrames,
                            theme.Midi(degree + 7) + 24,
                            0.22f);
                    }
                }

                return;
            }

            default:
                return;
        }
    }

    /// <summary>Adds one melody note in the style's own instrument.</summary>
    private static void AddMelodyNote(
        MusicTheme theme,
        float[] lead,
        int startFrame,
        int frames,
        int midi,
        float velocity)
    {
        var hertz = Synth.MidiToHz(midi);
        var gain = theme.Mix.Lead * 0.32f * velocity;
        var phase = 0.0;
        var partial = 0.0;
        var third = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = 0.0;

            switch (theme.Style)
            {
                case MusicStyle.Lullaby:
                    level = Synth.Pluck(t, 0.42, 0.002) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.SineFast(phase) * 0.75f) + (Synth.SineFast(third) * 0.12f)) * (float)level;
                    break;

                case MusicStyle.Nocturne:
                    level = Synth.Pluck(t, 0.55, 0.004) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.SineFast(phase) * 0.7f) + (Synth.SineFast(partial) * 0.16f)) * (float)level;
                    break;

                case MusicStyle.Drone:
                {
                    var seconds = frames / (double)Synth.SampleRate;
                    level = Synth.Sustain(t, seconds, seconds * 0.3, seconds * 0.4) * gain * 0.9;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.SineFast(phase) * 0.8f) + (Synth.SineFast(partial) * 0.1f)) * (float)level;
                    break;
                }

                case MusicStyle.Procession:
                    level = Synth.Pluck(t, 0.34, 0.01) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.Saw(phase) * 0.4f) + (Synth.Square(partial) * 0.2f)) * (float)level;
                    break;

                case MusicStyle.Gamelan:
                    level = Synth.Pluck(t, 0.5, 0.001) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.SineFast(phase) * 0.6f) +
                         (Synth.SineFast(partial) * 0.22f) +
                         (Synth.SineFast(third) * 0.1f)) * (float)level;
                    break;

                case MusicStyle.Pulse:
                    level = Synth.Pluck(t, 0.16, 0.002) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.Square(phase) * 0.35f) + (Synth.Saw(partial) * 0.2f)) * (float)level;
                    break;

                case MusicStyle.Chant:
                    level = Synth.Pluck(t, 0.5, 0.02) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.Triangle(phase) * 0.55f) + (Synth.SineFast(third) * 0.2f)) * (float)level;
                    break;

                case MusicStyle.Machine:
                    level = Synth.Pluck(t, 0.22, 0.001) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        Synth.SoftClip(Synth.Saw(phase) * 1.6f + (Synth.Square(partial) * 0.4f), 1.2f) * (float)level;
                    break;

                case MusicStyle.Waltz:
                    level = Synth.Pluck(t, 0.3, 0.004) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.Triangle(phase) * 0.6f) + (Synth.SineFast(partial) * 0.18f)) * (float)level;
                    break;

                default:
                {
                    var seconds = frames / (double)Synth.SampleRate;
                    level = Synth.Sustain(t, seconds, seconds * 0.25, seconds * 0.35) * gain;
                    lead[(startFrame + index) % lead.Length] +=
                        ((Synth.SineFast(phase) * 0.6f) +
                         (Synth.SineFast(partial) * 0.2f) +
                         (Synth.SineFast(third) * 0.1f)) * (float)level;
                    break;
                }
            }

            phase = Synth.Advance(phase, hertz);
            partial = Synth.Advance(partial, hertz * (theme.Style == MusicStyle.Procession ? 2.0 : 2.76));
            third = Synth.Advance(third, hertz * 5.4);
        }
    }

    /// <summary>Adds one short plucked note for a counterline.</summary>
    private static void AddPluck(
        MusicTheme theme,
        float[] lead,
        int startFrame,
        int frames,
        int midi,
        float level)
    {
        var hertz = Synth.MidiToHz(midi);
        var gain = theme.Mix.Lead * level;
        var phase = 0.0;
        var partial = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var envelope = Synth.Pluck(t, 0.18, 0.001) * gain;
            var value = (Synth.Triangle(phase) * 0.5f) + (Synth.SineFast(partial) * 0.2f);

            lead[(startFrame + index) % lead.Length] += value * (float)envelope;

            phase = Synth.Advance(phase, hertz);
            partial = Synth.Advance(partial, hertz * 2.0);
        }
    }

    /// <summary>Adds a gong-like bell.</summary>
    private static void AddBell(
        MusicTheme theme,
        float[] lead,
        int startFrame,
        int frames,
        int midi,
        float gain)
    {
        var hertz = Synth.MidiToHz(midi);
        var phase = 0.0;
        var partial = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.5, 0.002) * gain;

            var value = (Synth.SineFast(phase) * 0.7f) + (Synth.SineFast(partial) * 0.25f);
            lead[(startFrame + index) % lead.Length] += value * (float)level;

            phase = Synth.Advance(phase, hertz);
            partial = Synth.Advance(partial, hertz * 2.76);
        }
    }

    /// <summary>Adds one bass note.</summary>
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

    /// <summary>Renders a low frame drum: a soft, rounded hit with no pitch drop.</summary>
    private static void AddFrameDrum(float[] rhythm, int startFrame, int frames, float gain)
    {
        var phase = 0.0;
        var noise = new NoiseSource((uint)(0x3F11 + startFrame));
        var filter = new OnePole(300);

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.12, 0.002) * gain;

            var value = (Synth.SineFast(phase) * 0.7f) + (filter.Process(noise.Next()) * 0.3f);
            rhythm[(startFrame + index) % rhythm.Length] += value * (float)level;

            phase = Synth.Advance(phase, 72.0);
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

    /// <summary>Renders a metal clank: a high inharmonic ring plus a noise transient.</summary>
    private static void AddClank(float[] rhythm, int startFrame, int frames, float gain)
    {
        var noise = new NoiseSource((uint)(0x77C1 + startFrame));
        var high = new OnePole(5200);
        var phase = 0.0;
        var partial = 0.0;

        for (var index = 0; index < frames; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, 0.09, 0.001) * gain;

            var sample = noise.Next() - high.Process(noise.Next());
            var value = (Synth.SineFast(phase) * 0.4f) + (Synth.SineFast(partial) * 0.3f) + (sample * 0.4f);
            rhythm[(startFrame + index) % rhythm.Length] += value * (float)level;

            phase = Synth.Advance(phase, 880.0);
            partial = Synth.Advance(partial, 2200.0);
        }
    }

    /// <summary>
    /// Balances the three stems against each other, then scales them together under the ceiling.
    /// </summary>
    /// <remarks>
    /// The arrangement is not mixed by hand, so the balance has to be derived: a sustained bed with
    /// four notes under it is far louder than a few drum hits, and without this step opening the
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

    /// <summary>
    /// Fades the first and last few hundred samples of a stem to zero, so a loop whose final hit
    /// is still sounding does not step at the seam.
    /// </summary>
    /// <param name="buffer">The stem to smooth, in place.</param>
    private static void SmoothEdges(float[] buffer)
    {
        var edge = Math.Min(128, buffer.Length / 2);

        for (var index = 0; index < edge; index++)
        {
            var gain = (float)index / edge;
            var tail = buffer.Length - 1 - index;

            buffer[index] *= gain;
            buffer[tail] *= gain;
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

using System;
using System.Collections.Generic;

namespace MazeGame.UI.Audio;

/// <summary>
/// The sound bank: every one-shot in the game, synthesised at startup from a recipe table.
/// </summary>
/// <remarks>
/// <para>
/// There is nothing to decode, stream or licence. Each recipe is a few lines of oscillators and
/// noise shaped by an envelope, rendered into a mono buffer once and then played by
/// <see cref="AudioEngine"/>. Peaks are normalised rather than equalised, because a stamp should
/// be louder than a footstep.
/// </para>
/// <para>
/// The bank is a static, lazily built table: the buffers are ~2 MB together, and the test suite
/// can inspect them without an audio device.
/// </para>
/// </remarks>
public static class SfxBank
{
    // Chord and motif tables are stored once; a fresh array per call would allocate on the audio
    // path and make the recipes harder to read. They are declared before the bank because static
    // field initialisers run in order, and the recipes read them.
    private static readonly double[] KeyNotes = [880.00, 1108.73, 1318.51];
    private static readonly double[] ArchChord = [392.00, 493.88, 587.33];
    private static readonly double[] ClearTense = [349.23, 440.00, 523.25];
    private static readonly double[] ClearResolved = [392.00, 493.88, 587.33];
    private static readonly double[] LostMotif = [440.00, 349.23, 261.63];

    private static readonly Dictionary<SfxId, float[]> Sounds = Build();

    /// <summary>Gets every effect in the bank, in enum order.</summary>
    public static IReadOnlyList<SfxId> Ids { get; } = (SfxId[])Enum.GetValues(typeof(SfxId));

    /// <summary>
    /// Gets the samples for <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The effect to fetch.</param>
    /// <returns>The mono buffer at <see cref="Synth.SampleRate"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is unknown.</exception>
    public static ReadOnlySpan<float> Get(SfxId id) =>
        Sounds.TryGetValue(id, out var buffer)
            ? buffer
            : throw new ArgumentOutOfRangeException(nameof(id), id, "No recipe for this effect.");

    /// <summary>
    /// Gets the recipe's buffer itself, for the mixer to hold while it plays.
    /// </summary>
    /// <param name="id">The effect to fetch.</param>
    /// <returns>The shared buffer; callers must treat it as read-only.</returns>
    internal static float[] Samples(SfxId id) => Sounds[id];

    private static Dictionary<SfxId, float[]> Build() => new()
    {
        [SfxId.PlayerStep1] = PlayerStep(0x51A1u, 1500, 96),
        [SfxId.PlayerStep2] = PlayerStep(0x51A2u, 1180, 88),
        [SfxId.PlayerStep3] = PlayerStep(0x51A3u, 1850, 104),
        [SfxId.KillerStep] = KillerStep(),
        [SfxId.Damage] = Damage(),
        [SfxId.LastHeart] = LastHeart(),
        [SfxId.Heartbeat] = Heartbeat(),
        [SfxId.KeyPicked] = KeyPicked(),
        [SfxId.KeyPulse] = KeyPulse(),
        [SfxId.SealPlaced] = SealPlaced(),
        [SfxId.SealCracked] = SealCracked(),
        [SfxId.SealBroken] = SealBroken(),
        [SfxId.WispLit] = WispLit(),
        [SfxId.WispConsumed] = WispConsumed(),
        [SfxId.ArchLocked] = ArchLocked(),
        [SfxId.ArchUnlocked] = ArchUnlocked(),
        [SfxId.FloorCleared] = FloorCleared(),
        [SfxId.CardHover] = CardHover(),
        [SfxId.CardTake] = CardTake(),
        [SfxId.RunLost] = RunLost(),
        [SfxId.LungeTell] = LungeTell(),
        [SfxId.NearMiss] = NearMiss(),
    };

    /// <summary>A boot: a filtered noise scuff over a soft low thump.</summary>
    private static float[] PlayerStep(uint seed, double cutoff, double thumpHz)
    {
        var buffer = Buffer(0.09);

        AddNoise(buffer, 0, 0.08, cutoff, 0.022, 0.9f, seed);
        AddTone(buffer, 0, 0.09, thumpHz, 0.03, 0.3f);

        return Finish(buffer, 0.62f);
    }

    /// <summary>The killer's step: a low sine thump with a wet noise tail.</summary>
    private static float[] KillerStep()
    {
        var buffer = Buffer(0.38);

        AddSweep(buffer, 0, 0.32, 62, 36, 0.11, 0.9f);
        AddNoise(buffer, 0, 0.26, 380, 0.08, 0.45f, 0x2C0Eu);

        return Finish(buffer, 0.7f);
    }

    /// <summary>A catch: a crack, a falling tone and a short shimmer.</summary>
    private static float[] Damage()
    {
        var buffer = Buffer(0.6);

        AddNoise(buffer, 0, 0.12, 5200, 0.018, 0.85f, 0xDA11u);
        AddSweep(buffer, 0, 0.52, 470, 72, 0.16, 0.85f);
        AddTone(buffer, 0.01, 0.42, 880, 0.26, 0.22f);
        AddTone(buffer, 0.01, 0.42, 1320, 0.26, 0.12f);

        return Finish(buffer, 0.86f);
    }

    /// <summary>The last heart: the damage sound, longer, with a detuned choir pair under it.</summary>
    private static float[] LastHeart()
    {
        var buffer = Buffer(1.6);

        AddNoise(buffer, 0, 0.18, 4400, 0.03, 0.9f, 0x1A57u);
        AddSweep(buffer, 0, 1.2, 420, 44, 0.42, 0.85f);
        AddTone(buffer, 0.02, 1.5, 220, 0.7, 0.3f, Waveform.Saw);
        AddTone(buffer, 0.02, 1.5, 221.6, 0.7, 0.22f, Waveform.Saw);
        AddTone(buffer, 0.05, 1.4, 330, 0.6, 0.18f);

        return Finish(buffer, 0.9f);
    }

    /// <summary>Two thumps, a beat apart, for the last-heart heartbeat loop.</summary>
    private static float[] Heartbeat()
    {
        var buffer = Buffer(0.72);

        AddSweep(buffer, 0, 0.18, 64, 40, 0.055, 1.0f);
        AddSweep(buffer, 0.32, 0.16, 58, 38, 0.045, 0.8f);

        return Finish(buffer, 0.8f);
    }

    /// <summary>A key lifting off the floor: three rising bell notes.</summary>
    private static float[] KeyPicked()
    {
        var buffer = Buffer(1.0);

        for (var note = 0; note < KeyNotes.Length; note++)
        {
            AddTone(buffer, note * 0.1, 0.55, KeyNotes[note], 0.3, 0.5f);
            AddTone(buffer, note * 0.1, 0.4, KeyNotes[note] * 2.76, 0.14, 0.12f);
        }

        return Finish(buffer, 0.84f);
    }

    /// <summary>A sonar ping with one delayed tap, for a key the run can still find.</summary>
    private static float[] KeyPulse()
    {
        var buffer = Buffer(1.6);

        AddTone(buffer, 0, 1.2, 1180, 0.42, 0.7f);
        AddTone(buffer, 0.26, 1.0, 1180, 0.3, 0.22f);
        AddNoise(buffer, 0, 0.4, 2000, 0.09, 0.2f, 0x9E1Fu);

        return Finish(buffer, 0.6f);
    }

    /// <summary>A barrier thudding into place: a dry seal, then a heavy settle.</summary>
    private static float[] SealPlaced()
    {
        var buffer = Buffer(0.45);

        AddNoise(buffer, 0, 0.2, 1100, 0.05, 0.9f, 0x5EA1u);
        AddSweep(buffer, 0, 0.38, 120, 58, 0.1, 0.8f);
        AddNoise(buffer, 0.04, 0.3, 280, 0.08, 0.35f, 0x5EA2u);

        return Finish(buffer, 0.8f);
    }

    /// <summary>A seal running out of time: a high crack and a tired low note.</summary>
    private static float[] SealCracked()
    {
        var buffer = Buffer(0.4);

        AddNoise(buffer, 0, 0.18, 6200, 0.018, 0.9f, 0xC7ACu);
        AddSweep(buffer, 0.03, 0.3, 150, 92, 0.08, 0.45f);

        return Finish(buffer, 0.7f);
    }

    /// <summary>The killer tearing a live seal apart: two cracks and a body blow.</summary>
    private static float[] SealBroken()
    {
        var buffer = Buffer(0.55);

        AddNoise(buffer, 0, 0.14, 7000, 0.015, 0.9f, 0xB0AEu);
        AddNoise(buffer, 0.07, 0.2, 4800, 0.025, 0.7f, 0xB0AFu);
        AddSweep(buffer, 0.05, 0.45, 130, 52, 0.11, 0.75f);

        return Finish(buffer, 0.85f);
    }

    /// <summary>A wisp catching light: airy noise under a rising tone.</summary>
    private static float[] WispLit()
    {
        var buffer = Buffer(0.75);

        AddNoise(buffer, 0, 0.6, 2400, 0.14, 0.3f, 0x1157u);
        AddSweep(buffer, 0.02, 0.55, 520, 990, 0.24, 0.5f);
        AddTone(buffer, 0.08, 0.5, 1480, 0.2, 0.18f);

        return Finish(buffer, 0.66f);
    }

    /// <summary>The wisp swallowed: a falling tone with the air going out of it.</summary>
    private static float[] WispConsumed()
    {
        var buffer = Buffer(0.65);

        AddSweep(buffer, 0, 0.55, 760, 105, 0.18, 0.7f);
        AddNoise(buffer, 0, 0.5, 900, 0.16, 0.4f, 0x77A5u);

        return Finish(buffer, 0.7f);
    }

    /// <summary>A locked arch: a dull, final thud.</summary>
    private static float[] ArchLocked()
    {
        var buffer = Buffer(0.55);

        AddSweep(buffer, 0, 0.45, 118, 68, 0.1, 0.9f);
        AddNoise(buffer, 0, 0.3, 260, 0.06, 0.4f, 0xA2C4u);

        return Finish(buffer, 0.78f);
    }

    /// <summary>The arch opening: a chord that blooms rather than strikes.</summary>
    private static float[] ArchUnlocked()
    {
        var buffer = Buffer(1.4);

        AddChord(buffer, 0, 1.3, 0.12, 0.4, ArchChord, 0.5f);
        AddTone(buffer, 0.05, 1.2, 783.99, 0.55, 0.14f);

        return Finish(buffer, 0.8f);
    }

    /// <summary>A floor cleared: a tense chord resolving into an open one.</summary>
    private static float[] FloorCleared()
    {
        var buffer = Buffer(1.8);

        AddChord(buffer, 0, 0.7, 0.02, 0.25, ClearTense, 0.42f);
        AddChord(buffer, 0.36, 1.4, 0.05, 0.6, ClearResolved, 0.5f);

        return Finish(buffer, 0.82f);
    }

    /// <summary>A card under the cursor: a paper flick.</summary>
    private static float[] CardHover()
    {
        var buffer = Buffer(0.09);

        AddNoise(buffer, 0, 0.07, 3200, 0.012, 0.7f, 0xCA2Du);

        return Finish(buffer, 0.4f);
    }

    /// <summary>A card taken: the stamp.</summary>
    private static float[] CardTake()
    {
        var buffer = Buffer(0.28);

        AddNoise(buffer, 0, 0.14, 700, 0.03, 0.9f, 0xCA2Eu);
        AddSweep(buffer, 0, 0.24, 150, 86, 0.06, 0.7f);

        return Finish(buffer, 0.74f);
    }

    /// <summary>The run ending: the motif, cut off before it resolves.</summary>
    private static float[] RunLost()
    {
        var buffer = Buffer(1.7);

        for (var note = 0; note < LostMotif.Length; note++)
        {
            var start = note * 0.3;

            AddTone(buffer, start, 0.55, LostMotif[note], 0.4, 0.42f, Waveform.Saw);
            AddTone(buffer, start, 0.5, LostMotif[note] * 1.003, 0.4, 0.3f, Waveform.Saw);
        }

        AddNoise(buffer, 0, 1.6, 220, 0.9, 0.12f, 0x1075u);

        return Finish(buffer, 0.8f);
    }

    /// <summary>The killer drawing breath: a band of noise swelling in.</summary>
    private static float[] LungeTell()
    {
        var buffer = Buffer(0.55);

        AddNoise(buffer, 0, 0.5, 900, 0.35, 0.55f, 0x1A46u, attack: 0.34);
        AddSweep(buffer, 0.1, 0.4, 220, 640, 0.3, 0.18f);

        return Finish(buffer, 0.55f);
    }

    /// <summary>Something large passing very close: a swept band of noise.</summary>
    private static float[] NearMiss()
    {
        var buffer = Buffer(0.4);

        AddNoise(buffer, 0, 0.36, 1400, 0.1, 0.6f, 0x0FF1u);
        AddSweep(buffer, 0, 0.34, 260, 1250, 0.12, 0.3f);

        return Finish(buffer, 0.55f);
    }

    /// <summary>Allocates a silent mono buffer of <paramref name="seconds"/>.</summary>
    private static float[] Buffer(double seconds) =>
        new float[(int)Math.Round(seconds * Synth.SampleRate)];

    /// <summary>Adds a tone (or a short sweep) to <paramref name="buffer"/>.</summary>
    private static void AddTone(
        float[] buffer,
        double start,
        double duration,
        double hertz,
        double decay,
        float gain,
        Waveform waveform = Waveform.Sine)
    {
        var from = (int)Math.Round(start * Synth.SampleRate);
        var count = Math.Min(buffer.Length - from, (int)Math.Round(duration * Synth.SampleRate));

        if (count <= 0)
        {
            return;
        }

        var phase = 0.0;

        for (var index = 0; index < count; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, decay, 0.002) * gain;

            buffer[from + index] += Wave(waveform, phase) * (float)level;
            phase = Synth.Advance(phase, hertz);
        }
    }

    /// <summary>Adds a tone whose pitch glides from one frequency to another.</summary>
    private static void AddSweep(
        float[] buffer,
        double start,
        double duration,
        double startHz,
        double endHz,
        double decay,
        float gain)
    {
        var from = (int)Math.Round(start * Synth.SampleRate);
        var count = Math.Min(buffer.Length - from, (int)Math.Round(duration * Synth.SampleRate));

        if (count <= 0)
        {
            return;
        }

        var phase = 0.0;

        for (var index = 0; index < count; index++)
        {
            var progress = index / (double)count;
            var t = index / (double)Synth.SampleRate;

            buffer[from + index] +=
                Synth.Sine(phase) * (float)(Synth.Pluck(t, decay, 0.003) * gain);

            phase = Synth.Advance(phase, startHz + ((endHz - startHz) * progress));
        }
    }

    /// <summary>Adds low-passed noise with an envelope.</summary>
    private static void AddNoise(
        float[] buffer,
        double start,
        double duration,
        double cutoff,
        double decay,
        float gain,
        uint seed,
        double attack = 0.002)
    {
        var from = (int)Math.Round(start * Synth.SampleRate);
        var count = Math.Min(buffer.Length - from, (int)Math.Round(duration * Synth.SampleRate));

        if (count <= 0)
        {
            return;
        }

        var noise = new NoiseSource(seed);
        var filter = new OnePole(cutoff);

        for (var index = 0; index < count; index++)
        {
            var t = index / (double)Synth.SampleRate;
            var level = Synth.Pluck(t, decay, attack) * gain;

            buffer[from + index] += filter.Process(noise.Next()) * (float)level;
        }
    }

    /// <summary>Adds a sustained chord of sines with a soft attack and release.</summary>
    private static void AddChord(
        float[] buffer,
        double start,
        double duration,
        double attack,
        double release,
        double[] notes,
        float gain)
    {
        var from = (int)Math.Round(start * Synth.SampleRate);
        var count = Math.Min(buffer.Length - from, (int)Math.Round(duration * Synth.SampleRate));

        if (count <= 0)
        {
            return;
        }

        for (var note = 0; note < notes.Length; note++)
        {
            var phase = 0.0;
            var detuned = 0.0;
            var voiceGain = gain / notes.Length;

            for (var index = 0; index < count; index++)
            {
                var t = index / (double)Synth.SampleRate;
                var level = Synth.Sustain(t, duration, attack, release) * voiceGain;

                buffer[from + index] +=
                    ((Synth.Sine(phase) * 0.7f) + (Synth.Sine(detuned) * 0.3f)) * (float)level;

                phase = Synth.Advance(phase, notes[note]);
                detuned = Synth.Advance(detuned, notes[note] * 1.004);
            }
        }
    }

    /// <summary>Evaluates one sample of a waveform at <paramref name="phase"/>.</summary>
    private static float Wave(Waveform waveform, double phase) => waveform switch
    {
        Waveform.Triangle => Synth.Triangle(phase),
        Waveform.Saw => Synth.Saw(phase),
        Waveform.Square => Synth.Square(phase),
        _ => Synth.Sine(phase),
    };

    /// <summary>Scales a finished recipe so its loudest sample is <paramref name="peak"/>.</summary>
    private static float[] Finish(float[] buffer, float peak)
    {
        var current = Synth.Peak(buffer);

        if (current > 0)
        {
            var gain = peak / current;

            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] *= gain;
            }
        }

        return buffer;
    }

    /// <summary>The oscillator a recipe asks for.</summary>
    private enum Waveform
    {
        /// <summary>A pure tone.</summary>
        Sine,

        /// <summary>A soft, hollow tone.</summary>
        Triangle,

        /// <summary>A bright, buzzy tone.</summary>
        Saw,

        /// <summary>A hollow, hard tone.</summary>
        Square,
    }
}

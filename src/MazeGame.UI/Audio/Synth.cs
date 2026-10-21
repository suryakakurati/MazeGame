using System;

namespace MazeGame.UI.Audio;

/// <summary>
/// The small DSP toolkit every sound in the game is built from: oscillators, noise, envelopes,
/// filters and a cheap reverb.
/// </summary>
/// <remarks>
/// <para>
/// The game ships no audio files, so this is the whole of the "asset pipeline" for sound: a
/// handful of primitives that <see cref="SfxBank"/> and <see cref="ScoreRenderer"/> arrange into
/// effects and music. Nothing here touches Raylib, which is what lets the test suite check the
/// maths (no clipping, no NaN, a seamless loop) without an audio device.
/// </para>
/// <para>
/// All state is held in classes allocated once, so the per-sample path is allocation-free: a
/// synth that allocates is a synth that stutters.
/// </para>
/// </remarks>
public static class Synth
{
    /// <summary>The sample rate every generated buffer is rendered at.</summary>
    public const int SampleRate = 44100;

    /// <summary>The highest value a float sample may hold; anything above this is clipping.</summary>
    public const float PeakCeiling = 0.98f;

    /// <summary>
    /// Converts a MIDI note number to a frequency in hertz.
    /// </summary>
    /// <param name="midi">The note number, where 69 is A4 (440 Hz).</param>
    /// <returns>The frequency in hertz.</returns>
    public static double MidiToHz(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

    /// <summary>Returns a sine wave sample for <paramref name="phase"/>, a value in [0, 1).</summary>
    /// <param name="phase">The oscillator's phase.</param>
    public static float Sine(double phase) => (float)Math.Sin(phase * Math.Tau);

    /// <summary>
    /// Returns a sine sample from a lookup table instead of calling the maths library.
    /// </summary>
    /// <param name="phase">The oscillator's phase.</param>
    /// <returns>The sine of <paramref name="phase" /> turns, interpolated between table entries.</returns>
    /// <remarks>
    /// The score is rendered offline in one pass, and it evaluates a sine several million times;
    /// the table keeps a floor's track at tens of milliseconds instead of hundreds. One extra bit
    /// of noise at -80 dB is inaudible under a reverb, and the table is deterministic, so a seed
    /// still reproduces a track exactly.
    /// </remarks>
    public static float SineFast(double phase)
    {
        var position = (phase - Math.Floor(phase)) * SineTable.Length;
        var index = (int)position;
        var fraction = (float)(position - index);
        var current = SineTable[index & (SineTable.Length - 1)];
        var next = SineTable[(index + 1) & (SineTable.Length - 1)];

        return current + ((next - current) * fraction);
    }

    /// <summary>One cycle of a sine, sampled once so the score does not call <see cref="Math.Sin"/>.</summary>
    private static readonly float[] SineTable = BuildSineTable();

    /// <summary>Fills <see cref="SineTable"/> with one cycle of a sine.</summary>
    private static float[] BuildSineTable()
    {
        var table = new float[8192];

        for (var index = 0; index < table.Length; index++)
        {
            table[index] = (float)Math.Sin(index / (double)table.Length * Math.Tau);
        }

        return table;
    }

    /// <summary>Returns a band-limited-enough saw wave sample for <paramref name="phase"/>.</summary>
    /// <param name="phase">The oscillator's phase.</param>
    public static float Saw(double phase) => (float)(2.0 * (phase - Math.Floor(phase + 0.5)));

    /// <summary>Returns a triangle wave sample for <paramref name="phase"/>.</summary>
    /// <param name="phase">The oscillator's phase.</param>
    public static float Triangle(double phase) =>
        (float)(1.0 - (4.0 * Math.Abs(phase - Math.Floor(phase + 0.5))));

    /// <summary>Returns a square wave sample for <paramref name="phase"/>.</summary>
    /// <param name="phase">The oscillator's phase.</param>
    public static float Square(double phase) => Saw(phase) >= 0 ? 1.0f : -1.0f;

    /// <summary>
    /// Advances <paramref name="phase"/> by <paramref name="hertz"/> for one sample and wraps it
    /// into [0, 1).
    /// </summary>
    /// <param name="phase">The phase to advance.</param>
    /// <param name="hertz">The oscillator's frequency.</param>
    /// <param name="sampleRate">The rate the buffer is rendered at.</param>
    /// <returns>The next phase.</returns>
    public static double Advance(double phase, double hertz, int sampleRate = SampleRate)
    {
        var next = phase + (hertz / sampleRate);
        return next - Math.Floor(next);
    }

    /// <summary>
    /// A linear attack followed by a linear release, the shape used for pads and sustained voices.
    /// </summary>
    /// <param name="t">Seconds since the note started.</param>
    /// <param name="duration">The note's full length in seconds.</param>
    /// <param name="attack">Seconds to reach full level.</param>
    /// <param name="release">Seconds to fall back to silence before <paramref name="duration"/>.</param>
    /// <returns>A gain from 0 to 1.</returns>
    public static double Sustain(double t, double duration, double attack, double release)
    {
        if (t < 0 || t >= duration)
        {
            return 0;
        }

        var level = attack > 0 ? Math.Min(1.0, t / attack) : 1.0;

        if (release > 0 && t > duration - release)
        {
            level = Math.Min(level, (duration - t) / release);
        }

        return Math.Max(0, level);
    }

    /// <summary>
    /// An exponential decay with a short attack ramp, the shape used for plucks and hits.
    /// </summary>
    /// <param name="t">Seconds since the note started.</param>
    /// <param name="decay">The time constant: the note is at 1/e after this long.</param>
    /// <param name="attack">Seconds to reach full level; 0 for an instant hit.</param>
    /// <returns>A gain from 0 to 1.</returns>
    public static double Pluck(double t, double decay, double attack = 0.0)
    {
        if (t < 0)
        {
            return 0;
        }

        var ramp = attack > 0 ? Math.Min(1.0, t / attack) : 1.0;
        return ramp * Math.Exp(-t / Math.Max(0.0001, decay));
    }

    /// <summary>
    /// Soft-clips a sample with tanh, so a busy mix folds into distortion instead of hard edges.
    /// </summary>
    /// <param name="sample">The sample to shape.</param>
    /// <param name="drive">How hard to push it before folding; 1 is gentle.</param>
    /// <returns>A sample inside (-1, 1).</returns>
    public static float SoftClip(float sample, float drive = 1.0f)
    {
        var shaped = Math.Tanh(sample * drive) / Math.Tanh(drive);
        return (float)Math.Clamp(shaped, -0.999, 0.999);
    }

    /// <summary>Finds the largest absolute sample in <paramref name="buffer"/>.</summary>
    /// <param name="buffer">The samples to inspect.</param>
    /// <returns>The peak magnitude, or 0 for an empty buffer.</returns>
    public static float Peak(ReadOnlySpan<float> buffer)
    {
        var peak = 0.0f;

        foreach (var sample in buffer)
        {
            var magnitude = Math.Abs(sample);
            if (magnitude > peak)
            {
                peak = magnitude;
            }
        }

        return peak;
    }

    /// <summary>
    /// Computes the root-mean-square level of <paramref name="buffer"/>, which is what "how loud"
    /// means to an ear and to the tests.
    /// </summary>
    /// <param name="buffer">The samples to measure.</param>
    /// <returns>The RMS level, or 0 for an empty buffer.</returns>
    public static double Rms(ReadOnlySpan<float> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0.0;
        }

        var sum = 0.0;

        foreach (var sample in buffer)
        {
            sum += sample * (double)sample;
        }

        return Math.Sqrt(sum / buffer.Length);
    }

    /// <summary>
    /// Returns the largest step between neighbouring samples: a click detector for loop seams.
    /// </summary>
    /// <param name="buffer">The samples to inspect.</param>
    /// <returns>The largest absolute difference, or 0 for a buffer shorter than two samples.</returns>
    public static float MaxStep(ReadOnlySpan<float> buffer)
    {
        var largest = 0.0f;

        for (var index = 1; index < buffer.Length; index++)
        {
            var step = Math.Abs(buffer[index] - buffer[index - 1]);
            if (step > largest)
            {
                largest = step;
            }
        }

        return largest;
    }

    /// <summary>
    /// Measures the discontinuity at the point a buffer loops: the step from its last sample back
    /// to its first.
    /// </summary>
    /// <param name="buffer">The looped buffer.</param>
    /// <returns>The absolute difference across the seam, or 0 for an empty buffer.</returns>
    public static float SeamStep(ReadOnlySpan<float> buffer) =>
        buffer.Length < 2 ? 0.0f : Math.Abs(buffer[0] - buffer[^1]);
}

/// <summary>
/// A cheap white-noise source: xorshift32, seeded so an effect or a hi-hat is reproducible.
/// </summary>
public sealed class NoiseSource
{
    private uint _state;

    /// <summary>
    /// Creates a noise source from <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">The seed; zero is replaced, because xorshift cannot start there.</param>
    public NoiseSource(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

    /// <summary>Returns the next sample, in [-1, 1).</summary>
    /// <returns>A white-noise sample.</returns>
    public float Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;

        // 24 bits of mantissa is plenty for audio and keeps the division cheap.
        return ((_state >> 8) & 0xFFFFFF) / 8388608.0f - 1.0f;
    }
}

/// <summary>
/// A one-pole low-pass filter, used to take the fizz off noise and to give the choir its body.
/// </summary>
public sealed class OnePole
{
    private readonly double _coefficient;
    private double _state;

    /// <summary>
    /// Creates a low-pass filter with its cutoff at <paramref name="hertz"/>.
    /// </summary>
    /// <param name="hertz">The cutoff frequency.</param>
    /// <param name="sampleRate">The rate the signal runs at.</param>
    public OnePole(double hertz, int sampleRate = Synth.SampleRate)
    {
        var clamped = Math.Clamp(hertz, 10.0, sampleRate * 0.45);
        _coefficient = Math.Exp(-Math.Tau * clamped / sampleRate);
    }

    /// <summary>Filters one sample.</summary>
    /// <param name="sample">The input sample.</param>
    /// <returns>The filtered sample.</returns>
    public float Process(float sample)
    {
        _state = (sample * (1.0 - _coefficient)) + (_state * _coefficient);
        return (float)_state;
    }

    /// <summary>Forgets everything it has heard, so the next rendered buffer starts clean.</summary>
    public void Reset() => _state = 0;
}

/// <summary>
/// A state-variable band-pass, used for the formant shapes (choir, sonar ping, seal crack).
/// </summary>
public sealed class BandPass
{
    private readonly double _f;
    private readonly double _damping;
    private double _low;
    private double _band;

    /// <summary>
    /// Creates a band-pass centred on <paramref name="hertz"/>.
    /// </summary>
    /// <param name="hertz">The centre frequency.</param>
    /// <param name="q">The resonance; 0.7 is wide, 8 is a whistle.</param>
    /// <param name="sampleRate">The rate the signal runs at.</param>
    public BandPass(double hertz, double q = 1.0, int sampleRate = Synth.SampleRate)
    {
        _f = 2.0 * Math.Sin(Math.PI * Math.Clamp(hertz, 20.0, sampleRate * 0.45) / sampleRate);
        _damping = Math.Clamp(1.0 / Math.Max(0.1, q), 0.05, 2.0);
    }

    /// <summary>Filters one sample.</summary>
    /// <param name="sample">The input sample.</param>
    /// <returns>The band-passed sample.</returns>
    public float Process(float sample)
    {
        _low += _f * _band;
        var high = sample - _low - (_damping * _band);
        _band += _f * high;
        return (float)_band;
    }

    /// <summary>Clears the filter's state.</summary>
    public void Reset()
    {
        _low = 0;
        _band = 0;
    }
}

/// <summary>
/// A Schroeder reverb: four parallel combs into two series all-passes.
/// </summary>
/// <remarks>
/// This is the single cheapest way to make a synthesised sound sit in a room, and the score uses
/// the same network for every floor, only varying the wet amount. Running a loop through the
/// network twice and keeping the second pass is what makes the reverb tail wrap around the loop
/// point, so a looping track has no seam to click on.
/// </remarks>
public sealed class Reverb
{
    private static readonly int[] CombDelays = [1116, 1188, 1277, 1356];
    private static readonly int[] AllPassDelays = [225, 556];

    private readonly float[][] _combs;
    private readonly float[][] _allPasses;
    private readonly int[] _combIndices;
    private readonly int[] _allPassIndices;
    private readonly float[] _combDamp;

    /// <summary>Creates a reverb with fixed, musically chosen delay lengths.</summary>
    public Reverb()
    {
        _combs = new float[CombDelays.Length][];
        _combIndices = new int[CombDelays.Length];
        _combDamp = new float[CombDelays.Length];

        for (var index = 0; index < CombDelays.Length; index++)
        {
            _combs[index] = new float[CombDelays[index]];
        }

        _allPasses = new float[AllPassDelays.Length][];
        _allPassIndices = new int[AllPassDelays.Length];

        for (var index = 0; index < AllPassDelays.Length; index++)
        {
            _allPasses[index] = new float[AllPassDelays[index]];
        }
    }

    /// <summary>Runs one sample through the network.</summary>
    /// <param name="input">The dry sample.</param>
    /// <returns>The reverberated sample.</returns>
    public float Process(float input)
    {
        var wet = 0.0f;

        for (var index = 0; index < _combs.Length; index++)
        {
            var line = _combs[index];
            var position = _combIndices[index];
            var delayed = line[position];

            // A little damping in the feedback path: real rooms lose their treble first.
            _combDamp[index] = delayed + (0.35f * (_combDamp[index] - delayed));
            line[position] = input + (_combDamp[index] * 0.79f);

            _combIndices[index] = position + 1 == line.Length ? 0 : position + 1;
            wet += delayed;
        }

        wet *= 0.25f;

        for (var index = 0; index < _allPasses.Length; index++)
        {
            var line = _allPasses[index];
            var position = _allPassIndices[index];
            var delayed = line[position];
            var output = delayed - wet;

            line[position] = wet + (delayed * 0.5f);
            _allPassIndices[index] = position + 1 == line.Length ? 0 : position + 1;
            wet = output;
        }

        return wet;
    }

    /// <summary>Clears every delay line.</summary>
    public void Reset()
    {
        foreach (var line in _combs)
        {
            Array.Clear(line);
        }

        foreach (var line in _allPasses)
        {
            Array.Clear(line);
        }

        Array.Clear(_combIndices);
        Array.Clear(_allPassIndices);
        Array.Clear(_combDamp);
    }

    /// <summary>
    /// Mixes <paramref name="wet"/> of the reverb into <paramref name="dry"/>.
    /// </summary>
    /// <param name="dry">The buffer to reverberate, modified in place.</param>
    /// <param name="wet">How much of the reverberated signal to add; 0 is a no-op.</param>
    /// <param name="circular">
    /// When true the buffer is a loop: the network is run over it twice and only the second pass
    /// is kept, which wraps the tail around the loop point instead of letting it ring past the end.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dry"/> is null.</exception>
    public void Apply(float[] dry, double wet, bool circular)
    {
        ArgumentNullException.ThrowIfNull(dry);

        if (wet <= 0 || dry.Length == 0)
        {
            return;
        }

        var amount = (float)Math.Clamp(wet, 0.0, 1.0);

        if (circular)
        {
            // First pass fills the delay lines with the end of the previous cycle.
            for (var index = 0; index < dry.Length; index++)
            {
                Process(dry[index]);
            }

            for (var index = 0; index < dry.Length; index++)
            {
                dry[index] += Process(dry[index]) * amount;
            }

            return;
        }

        for (var index = 0; index < dry.Length; index++)
        {
            dry[index] += Process(dry[index]) * amount;
        }
    }
}

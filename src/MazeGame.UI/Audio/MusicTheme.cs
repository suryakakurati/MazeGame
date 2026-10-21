using System;

namespace MazeGame.UI.Audio;

/// <summary>
/// The mode a floor's score is built in: which of the seven steps are lowered.
/// </summary>
/// <remarks>
/// Twenty floors cannot have twenty key signatures and still sound like one descent, so the
/// catalog keeps a handful of modes and moves them around. The mode is where a floor's mood
/// comes from: aeolian is the home key, dorian lifts the sixth, phrygian is the flat second that
/// sounds like a warning, and locrian is the one that never resolves.
/// </remarks>
public enum MusicMode
{
    /// <summary>The natural minor: home.</summary>
    Aeolian,

    /// <summary>Minor with a raised sixth: hopeful, old.</summary>
    Dorian,

    /// <summary>Minor with a flat second: tense, exotic.</summary>
    Phrygian,

    /// <summary>Minor with a flat second and fifth: unstable, unresolved.</summary>
    Locrian,

    /// <summary>Major with a raised fourth: bright and slightly wrong.</summary>
    Lydian,

    /// <summary>Major with a flat seventh: warm, worn.</summary>
    Mixolydian,
}

/// <summary>
/// The kind of piece a floor is: which instruments, which rhythm and which shape the arrangement
/// takes. It is the difference between two floors sounding like two tracks rather than the same
/// track in a new key.
/// </summary>
/// <remarks>
/// A style owns the arrangement, not the tune: the melody, key and progression come from the
/// theme. Two floors may share a style and still be different pieces, but no two adjacent floors
/// do — the descent changes the kind of music, not just its numbers.
/// </remarks>
public enum MusicStyle
{
    /// <summary>A slow, wide drone with one voice over it: pressure, not a tune.</summary>
    Drone,

    /// <summary>A music box: high plucked notes, swaying, no drums.</summary>
    Lullaby,

    /// <summary>A slow march: a heavy beat, a walking bass, brass in the lead.</summary>
    Procession,

    /// <summary>Voices first: a formant choir over an organ bed and a soft frame drum.</summary>
    Chant,

    /// <summary>Driving four-to-the-floor with a filtered arpeggio under the motif.</summary>
    Pulse,

    /// <summary>Three beats to the bar, a light kick and a plucked melody.</summary>
    Waltz,

    /// <summary>Sparse and quiet: a piano-ish arpeggio, no drums, long tails.</summary>
    Nocturne,

    /// <summary>Metal: inharmonic bells, interlocking parts and no drum kit.</summary>
    Gamelan,

    /// <summary>Industrial: odd meter, noise hits, a distorted bass and a clanking rhythm.</summary>
    Machine,

    /// <summary>An organ and a bell under a slow, heavy tune.</summary>
    Requiem,
}

/// <summary>
/// How loud each voice family is on a floor.
/// </summary>
/// <param name="Pad">The sustained chord bed.</param>
/// <param name="Bass">The low ostinato.</param>
/// <param name="Lead">The motif statement and bells.</param>
/// <param name="Perc">Kick, hat and snare.</param>
/// <param name="Choir">The slow formant voice that only appears deeper down.</param>
public sealed record InstrumentMix(
    double Pad,
    double Bass,
    double Lead,
    double Perc,
    double Choir);

/// <summary>
/// One floor's music: the parameters the score engine needs to arrange a track, and nothing else.
/// </summary>
/// <param name="ThemeId">The floor theme this track belongs to; it is also the atlas id.</param>
/// <param name="RootMidi">The tonic, as a MIDI note number.</param>
/// <param name="Mode">The scale the floor is written in.</param>
/// <param name="Style">The kind of piece: instruments, rhythm and shape.</param>
/// <param name="Tempo">Beats per minute.</param>
/// <param name="StepsPerBar">Sixteenth steps in a bar: 16 is common time, 12 is 3/4, 20 is 5/4.</param>
/// <param name="BarsPerLoop">How many bars one pass of the arrangement lasts.</param>
/// <param name="Progression">One scale degree per bar, cycling over the loop.</param>
/// <param name="Melody">This floor's own tune, in scale degrees over its bar's root.</param>
/// <param name="Mix">Per-family gains for this floor.</param>
/// <param name="Density">0..1: how many steps carry a note or a drum.</param>
/// <param name="Tension">0..1: how far the bed detunes and how hard the percussion pushes.</param>
/// <param name="ReverbWet">0..1: how much of the reverb return is mixed in.</param>
/// <param name="Band">1..4: how deep into the descent the floor is, for the loudness arc.</param>
/// <param name="Canon">Whether the melody is also stated half a loop later, at a lower level.</param>
/// <remarks>
/// The theme is a <i>composition</i> expressed as parameters, not a recording. Every floor has its
/// own melody and style, so no two floors are the same piece at a different pitch; the descent is
/// heard as twenty pieces that get heavier, not one piece that gets detuned.
/// </remarks>
public sealed record MusicTheme(
    string ThemeId,
    int RootMidi,
    MusicMode Mode,
    MusicStyle Style,
    double Tempo,
    int StepsPerBar,
    int BarsPerLoop,
    int[] Progression,
    Motif Melody,
    InstrumentMix Mix,
    double Density,
    double Tension,
    double ReverbWet,
    int Band,
    bool Canon = false)
{
    /// <summary>The semitone offsets of each mode's seven steps.</summary>
    private static readonly int[][] Intervals =
    [
        [0, 2, 3, 5, 7, 8, 10],
        [0, 2, 3, 5, 7, 9, 10],
        [0, 1, 3, 5, 7, 8, 10],
        [0, 1, 3, 5, 6, 8, 10],
        [0, 2, 4, 6, 7, 9, 11],
        [0, 2, 4, 5, 7, 9, 10],
    ];

    /// <summary>
    /// Gets the semitone offsets of <paramref name="mode"/>'s seven steps from its tonic.
    /// </summary>
    /// <param name="mode">The mode to spell out.</param>
    /// <returns>Seven semitone offsets, ascending from 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is unknown.</exception>
    public static ReadOnlySpan<int> ScaleOf(MusicMode mode) =>
        mode >= 0 && (int)mode < Intervals.Length
            ? Intervals[(int)mode]
            : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown mode.");

    /// <summary>
    /// Converts a scale degree to a MIDI note, wrapping octaves in both directions.
    /// </summary>
    /// <param name="degree">
    /// The scale degree counted from the tonic; 0 is the tonic, 7 is the octave above, and -1 is
    /// the seventh below.
    /// </param>
    /// <returns>The MIDI note for that degree.</returns>
    public int Midi(int degree)
    {
        var octave = (int)Math.Floor(degree / 7.0);
        var step = degree - (octave * 7);

        return RootMidi + (octave * 12) + ScaleOf(Mode)[step];
    }

    /// <summary>
    /// Gets the MIDI note of a chord tone: the tonic of a bar plus a scale step.
    /// </summary>
    /// <param name="barDegree">The bar's scale degree, from <see cref="Progression"/>.</param>
    /// <param name="chordStep">0 for the root, 2 for the third, 4 for the fifth, 6 for the seventh.</param>
    /// <returns>The MIDI note of that chord tone.</returns>
    public int ChordTone(int barDegree, int chordStep) => Midi(barDegree + chordStep);
}

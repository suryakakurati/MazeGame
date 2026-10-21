using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.UI.Audio;

/// <summary>
/// One of the five melodic ideas every floor is built from.
/// </summary>
/// <param name="Degrees">Scale degrees, in order; negative degrees sit below the tonic.</param>
/// <param name="Spacing">How many sixteenth notes each note lasts.</param>
public sealed record Motif(int[] Degrees, int Spacing);

/// <summary>
/// The score's content: twenty floor themes and the five motifs they are arranged from.
/// </summary>
/// <remarks>
/// <para>
/// The list is in floor order and its ids are the atlas ids, so floor N's music and floor N's
/// tiles cannot drift apart. A test compares it with <see cref="MazeGame.Core.Game.FloorCatalog"/>
/// for exactly that reason.
/// </para>
/// <para>
/// The arrangement escalates in four bands: 1–5 is pad and one motif, 6–10 adds bass and a
/// second voice, 11–15 adds choir and bells, and 16–20 adds the distorted lead and driving
/// percussion. Floor 20 is the exception the plan asks for: slow, wide, and carrying the title
/// motif in canon.
/// </para>
/// </remarks>
public static class MusicCatalog
{
    private const double BandOneBass = 0.45;
    private const double BandOneLead = 0.42;
    private const double BandOnePerc = 0.4;

    /// <summary>Gets the five motifs, shared by every floor.</summary>
    public static IReadOnlyList<Motif> Motifs { get; } = new ReadOnlyCollection<Motif>(
    [
        new Motif([0, 2, 4, 3, 2, 0], 2),
        new Motif([0, 4, 2, 6, 4, 2], 2),
        new Motif([0, 3, 4, 2, 0, -1], 3),
        new Motif([0, 2, 0, 5, 4, 2], 2),
        new Motif([0, 6, 4, 3, 1, 0], 2),
    ]);

    /// <summary>Gets the twenty floor themes, in floor order.</summary>
    public static IReadOnlyList<MusicTheme> All { get; } = new ReadOnlyCollection<MusicTheme>(
    [
        // 1-5: the descent begins. One motif, a pad, and as much silence as notes.
        new MusicTheme(
            "shallows", 45, MusicMode.Aeolian, 72, 4, [0, 5, 3, 4],
            new InstrumentMix(1.0, BandOneBass, BandOneLead, BandOnePerc, 0.0),
            Density: 0.34, Tension: 0.12, ReverbWet: 0.30, Band: 1),

        new MusicTheme(
            "rootbound", 45, MusicMode.Dorian, 68, 4, [0, 3, 5, 4],
            new InstrumentMix(1.0, 0.50, 0.46, 0.44, 0.0),
            Density: 0.38, Tension: 0.18, ReverbWet: 0.34, Band: 1),

        new MusicTheme(
            "drowned", 38, MusicMode.Aeolian, 70, 4, [0, 5, 4, 3],
            new InstrumentMix(1.0, 0.46, 0.40, 0.34, 0.12),
            Density: 0.32, Tension: 0.20, ReverbWet: 0.44, Band: 1),

        new MusicTheme(
            "saltchoir", 43, MusicMode.Dorian, 76, 4, [0, 4, 5, 3],
            new InstrumentMix(1.0, 0.52, 0.52, 0.48, 0.30),
            Density: 0.42, Tension: 0.24, ReverbWet: 0.40, Band: 1),

        new MusicTheme(
            "ember", 41, MusicMode.Phrygian, 84, 4, [0, 1, 5, 4],
            new InstrumentMix(1.0, 0.58, 0.55, 0.54, 0.10),
            Density: 0.46, Tension: 0.30, ReverbWet: 0.28, Band: 1),

        // 6-10: a bass ostinato and a second voice arrive.
        new MusicTheme(
            "gilded", 47, MusicMode.Lydian, 88, 4, [0, 4, 5, 2],
            new InstrumentMix(1.0, 0.66, 0.60, 0.62, 0.22),
            Density: 0.50, Tension: 0.34, ReverbWet: 0.36, Band: 2),

        new MusicTheme(
            "hush", 40, MusicMode.Aeolian, 62, 4, [0, 5, 3, 4],
            new InstrumentMix(1.0, 0.60, 0.44, 0.26, 0.18),
            Density: 0.26, Tension: 0.22, ReverbWet: 0.52, Band: 2),

        new MusicTheme(
            "gallery", 45, MusicMode.Mixolydian, 82, 4, [0, 3, 4, 5],
            new InstrumentMix(1.0, 0.68, 0.62, 0.66, 0.28),
            Density: 0.52, Tension: 0.36, ReverbWet: 0.46, Band: 2),

        new MusicTheme(
            "warren", 43, MusicMode.Dorian, 94, 4, [0, 5, 6, 4],
            new InstrumentMix(1.0, 0.74, 0.66, 0.72, 0.16),
            Density: 0.60, Tension: 0.40, ReverbWet: 0.30, Band: 2),

        new MusicTheme(
            "orchard", 48, MusicMode.Lydian, 86, 4, [0, 6, 4, 5],
            new InstrumentMix(1.0, 0.70, 0.64, 0.60, 0.32),
            Density: 0.54, Tension: 0.38, ReverbWet: 0.38, Band: 2),

        // 11-15: choir and bells, wider and more wrong.
        new MusicTheme(
            "blindmile", 41, MusicMode.Locrian, 78, 4, [0, 1, 6, 3],
            new InstrumentMix(1.0, 0.76, 0.72, 0.78, 0.50),
            Density: 0.56, Tension: 0.54, ReverbWet: 0.48, Band: 3),

        new MusicTheme(
            "foundry", 38, MusicMode.Phrygian, 102, 4, [0, 6, 1, 5],
            new InstrumentMix(1.0, 0.84, 0.74, 0.86, 0.34),
            Density: 0.70, Tension: 0.60, ReverbWet: 0.30, Band: 3),

        new MusicTheme(
            "mothgarden", 46, MusicMode.Dorian, 84, 4, [0, 4, 2, 5],
            new InstrumentMix(1.0, 0.78, 0.80, 0.74, 0.56),
            Density: 0.58, Tension: 0.46, ReverbWet: 0.50, Band: 3),

        new MusicTheme(
            "ossuary", 39, MusicMode.Aeolian, 74, 4, [0, 3, 6, 4],
            new InstrumentMix(1.0, 0.80, 0.70, 0.72, 0.62),
            Density: 0.50, Tension: 0.50, ReverbWet: 0.56, Band: 3),

        new MusicTheme(
            "tide", 43, MusicMode.Mixolydian, 72, 4, [0, 5, 3, 6],
            new InstrumentMix(1.0, 0.82, 0.76, 0.80, 0.48),
            Density: 0.62, Tension: 0.52, ReverbWet: 0.54, Band: 3),

        // 16-20: the driving band. Floor 20 is half-time, with everything on it.
        new MusicTheme(
            "bramble", 40, MusicMode.Dorian, 98, 4, [0, 6, 4, 1],
            new InstrumentMix(1.0, 0.88, 0.90, 0.92, 0.58),
            Density: 0.74, Tension: 0.66, ReverbWet: 0.36, Band: 4),

        new MusicTheme(
            "vault", 38, MusicMode.Locrian, 90, 4, [0, 1, 4, 6],
            new InstrumentMix(1.0, 0.90, 0.96, 0.88, 0.52),
            Density: 0.68, Tension: 0.74, ReverbWet: 0.42, Band: 4),

        new MusicTheme(
            "loom", 45, MusicMode.Phrygian, 106, 4, [0, 5, 1, 4],
            new InstrumentMix(1.0, 0.92, 0.94, 0.98, 0.60),
            Density: 0.80, Tension: 0.78, ReverbWet: 0.32, Band: 4),

        new MusicTheme(
            "ashgarden", 41, MusicMode.Aeolian, 82, 4, [0, 6, 3, 5],
            new InstrumentMix(1.0, 0.86, 0.98, 0.86, 0.66),
            Density: 0.66, Tension: 0.70, ReverbWet: 0.48, Band: 4),

        new MusicTheme(
            "deep", 36, MusicMode.Aeolian, 58, 4, [0, 5, 3, 6],
            new InstrumentMix(1.0, 0.80, 1.0, 0.62, 0.80),
            Density: 0.44, Tension: 0.88, ReverbWet: 0.62, Band: 4, Canon: true),
    ]);

    /// <summary>
    /// Finds the track for a floor theme.
    /// </summary>
    /// <param name="themeId">The floor's theme id, as used by the atlas.</param>
    /// <returns>The matching theme.</returns>
    /// <exception cref="ArgumentException">Thrown when there is no track for that id.</exception>
    public static MusicTheme For(string themeId)
    {
        foreach (var theme in All)
        {
            if (string.Equals(theme.ThemeId, themeId, StringComparison.Ordinal))
            {
                return theme;
            }
        }

        throw new ArgumentException($"No music theme for '{themeId}'.", nameof(themeId));
    }

    /// <summary>
    /// Gets the motif a floor is arranged from: floors cycle through the five, so a run hears
    /// each of them four times in increasingly wrong shapes.
    /// </summary>
    /// <param name="floorNumber">The floor's number, starting at 1.</param>
    /// <returns>The motif for that floor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the floor number is below 1.</exception>
    public static Motif MotifFor(int floorNumber)
    {
        if (floorNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(floorNumber),
                floorNumber,
                "Floors are numbered from 1.");
        }

        return Motifs[(floorNumber - 1) % Motifs.Count];
    }
}

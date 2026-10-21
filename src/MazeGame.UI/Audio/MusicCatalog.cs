using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.UI.Audio;

/// <summary>
/// One melodic idea: scale degrees and how many sixteenths each note lasts.
/// </summary>
/// <param name="Degrees">Scale degrees, in order; negative degrees sit below the tonic.</param>
/// <param name="Spacing">How many sixteenth notes each note lasts.</param>
public sealed record Motif(int[] Degrees, int Spacing);

/// <summary>
/// The score's content: twenty floor pieces, one per floor, each its own tune, key, meter and style.
/// </summary>
/// <remarks>
/// <para>
/// The list is in floor order and its ids are the atlas ids, so floor N's music and floor N's
/// tiles cannot drift apart. A test compares it with <see cref="MazeGame.Core.Game.FloorCatalog"/>
/// for exactly that reason.
/// </para>
/// <para>
/// Twenty floors are twenty pieces, not twenty takes on one: every entry has its own
/// <see cref="MusicTheme.Melody"/>, its own <see cref="MusicStyle"/> (drone, lullaby, procession,
/// chant, pulse, waltz, nocturne, gamelan, machine, requiem), and some change the meter as well
/// (3/4 on the waltzes and the last floor, 5/4 in the Foundry and the Bramble). The descent is a
/// sequence of different kinds of music that all share a palette, which is what makes each floor
/// announce itself the moment the intro card drops.
/// </para>
/// <para>
/// Loudness still rises: <see cref="MusicTheme.Band"/> goes from 1 on the Shallows to 4 at the
/// bottom, and the mix opens up the choir and the percussion as it goes.
/// </para>
/// </remarks>
public static class MusicCatalog
{
    /// <summary>Gets the twenty floor pieces, in floor order.</summary>
    public static IReadOnlyList<MusicTheme> All { get; } = new ReadOnlyCollection<MusicTheme>(
    [
        // 1 — Drone. A held low fifth with one slow voice over it: the room before the room.
        new MusicTheme(
            "shallows", 45, MusicMode.Aeolian, MusicStyle.Drone,
            Tempo: 54, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 0, 5, 0],
            Melody: new Motif([0, 2, 4, 2, 0, -1], 4),
            Mix: new InstrumentMix(1.0, 0.18, 0.55, 0.0, 0.0),
            Density: 0.22, Tension: 0.08, ReverbWet: 0.62, Band: 1),

        // 2 — Lullaby. A music box in 6/8, no drums, swaying.
        new MusicTheme(
            "rootbound", 45, MusicMode.Dorian, MusicStyle.Lullaby,
            Tempo: 88, StepsPerBar: 12, BarsPerLoop: 6, Progression: [0, 5, 3, 4],
            Melody: new Motif([0, 2, 3, 2, 0, 4], 3),
            Mix: new InstrumentMix(0.9, 0.0, 0.78, 0.0, 0.0),
            Density: 0.30, Tension: 0.12, ReverbWet: 0.55, Band: 1),

        // 3 — Procession. A slow march through a flooded nave: heavy feet and a walking bass.
        new MusicTheme(
            "drowned", 38, MusicMode.Aeolian, MusicStyle.Procession,
            Tempo: 64, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 5, 4, 3],
            Melody: new Motif([0, -1, 2, 4, 2, 0], 4),
            Mix: new InstrumentMix(0.85, 0.72, 0.50, 0.42, 0.30),
            Density: 0.40, Tension: 0.26, ReverbWet: 0.66, Band: 1),

        // 4 — Chant. Voices first: a choir over an organ bed and a frame drum.
        new MusicTheme(
            "saltchoir", 43, MusicMode.Dorian, MusicStyle.Chant,
            Tempo: 58, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 4, 5, 3],
            Melody: new Motif([0, 3, 4, 3, 2, 0], 4),
            Mix: new InstrumentMix(0.80, 0.35, 0.42, 0.16, 0.95),
            Density: 0.34, Tension: 0.22, ReverbWet: 0.74, Band: 1),

        // 5 — Pulse. Embers: a filtered arpeggio, four on the floor, the first real drive.
        new MusicTheme(
            "ember", 41, MusicMode.Phrygian, MusicStyle.Pulse,
            Tempo: 112, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 1, 5, 4],
            Melody: new Motif([0, 4, 2, 5, 4, 2], 2),
            Mix: new InstrumentMix(0.85, 0.78, 0.72, 0.80, 0.0),
            Density: 0.62, Tension: 0.40, ReverbWet: 0.30, Band: 1),

        // 6 — Waltz. Gilded and hollow: three beats, a plucked tune, a light kick.
        new MusicTheme(
            "gilded", 47, MusicMode.Lydian, MusicStyle.Waltz,
            Tempo: 132, StepsPerBar: 12, BarsPerLoop: 8, Progression: [0, 4, 5, 2],
            Melody: new Motif([0, 2, 4, 6, 4, 2], 3),
            Mix: new InstrumentMix(0.92, 0.55, 0.80, 0.34, 0.20),
            Density: 0.46, Tension: 0.30, ReverbWet: 0.44, Band: 2),

        // 7 — Nocturne. The Hush: a piano in a sealed room, no drums, almost no light.
        new MusicTheme(
            "hush", 40, MusicMode.Aeolian, MusicStyle.Nocturne,
            Tempo: 62, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 5, 3, 4],
            Melody: new Motif([0, 2, 1, 0, -1, 0], 6),
            Mix: new InstrumentMix(0.70, 0.22, 0.86, 0.0, 0.0),
            Density: 0.18, Tension: 0.18, ReverbWet: 0.78, Band: 2),

        // 8 — Procession. The Long Gallery: a processional with bells in the empty bays.
        new MusicTheme(
            "gallery", 45, MusicMode.Mixolydian, MusicStyle.Procession,
            Tempo: 76, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 3, 4, 5],
            Melody: new Motif([0, 4, 3, 2, 4, 6], 3),
            Mix: new InstrumentMix(0.90, 0.66, 0.74, 0.50, 0.34),
            Density: 0.50, Tension: 0.32, ReverbWet: 0.58, Band: 2),

        // 9 — Pulse. The Warren: tight, fast, claustrophobic; the melody climbs and never rests.
        new MusicTheme(
            "warren", 43, MusicMode.Dorian, MusicStyle.Pulse,
            Tempo: 124, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 5, 6, 4],
            Melody: new Motif([0, 2, 4, 6, 7, 6], 2),
            Mix: new InstrumentMix(0.80, 0.86, 0.84, 0.92, 0.18),
            Density: 0.72, Tension: 0.48, ReverbWet: 0.26, Band: 2),

        // 10 — Gamelan. The Glass Orchard: metal, interlocking parts, no drum kit at all.
        new MusicTheme(
            "orchard", 48, MusicMode.Lydian, MusicStyle.Gamelan,
            Tempo: 92, StepsPerBar: 16, BarsPerLoop: 6, Progression: [0, 6, 4, 5],
            Melody: new Motif([0, 5, 2, 4, 1, 3], 2),
            Mix: new InstrumentMix(0.72, 0.30, 0.90, 0.0, 0.28),
            Density: 0.56, Tension: 0.36, ReverbWet: 0.60, Band: 2),

        // 11 — Drone. The Blind Mile: lower, wider, no rhythm to follow, just pressure.
        new MusicTheme(
            "blindmile", 36, MusicMode.Locrian, MusicStyle.Drone,
            Tempo: 48, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 1, 0, 6],
            Melody: new Motif([0, -1, -2, -1, 0, 2], 6),
            Mix: new InstrumentMix(1.0, 0.22, 0.62, 0.0, 0.58),
            Density: 0.20, Tension: 0.60, ReverbWet: 0.82, Band: 3),

        // 12 — Machine. The Foundry in 5/4: press hits, a distorted bass, a clanking line.
        new MusicTheme(
            "foundry", 38, MusicMode.Phrygian, MusicStyle.Machine,
            Tempo: 104, StepsPerBar: 20, BarsPerLoop: 4, Progression: [0, 6, 1, 5],
            Melody: new Motif([0, 0, 3, 0, 5, 3], 2),
            Mix: new InstrumentMix(0.78, 0.92, 0.80, 0.96, 0.0),
            Density: 0.80, Tension: 0.66, ReverbWet: 0.24, Band: 3),

        // 13 — Lullaby. The Moth Garden: a soft box tune under a flutter, five beats of sway.
        new MusicTheme(
            "mothgarden", 46, MusicMode.Dorian, MusicStyle.Lullaby,
            Tempo: 96, StepsPerBar: 12, BarsPerLoop: 6, Progression: [0, 4, 2, 5],
            Melody: new Motif([0, 2, 4, 7, 4, 2], 3),
            Mix: new InstrumentMix(0.88, 0.0, 0.84, 0.10, 0.40),
            Density: 0.34, Tension: 0.28, ReverbWet: 0.62, Band: 3),

        // 14 — Requiem. The Ossuary: an organ, a bell, and a tune that keeps falling.
        new MusicTheme(
            "ossuary", 39, MusicMode.Aeolian, MusicStyle.Requiem,
            Tempo: 56, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 3, 6, 4],
            Melody: new Motif([0, 3, 2, 3, 0, -2], 6),
            Mix: new InstrumentMix(0.95, 0.30, 0.66, 0.12, 0.86),
            Density: 0.30, Tension: 0.54, ReverbWet: 0.80, Band: 3),

        // 15 — Nocturne. The Tide: flowing right-hand figures over a slow bass, no drums.
        new MusicTheme(
            "tide", 43, MusicMode.Mixolydian, MusicStyle.Nocturne,
            Tempo: 84, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 5, 3, 6],
            Melody: new Motif([0, 4, 5, 4, 2, 4], 2),
            Mix: new InstrumentMix(0.76, 0.40, 0.90, 0.0, 0.22),
            Density: 0.44, Tension: 0.34, ReverbWet: 0.70, Band: 3),

        // 16 — Machine. The Bramble in 5/4: snapping, unlaced, nothing resolves.
        new MusicTheme(
            "bramble", 40, MusicMode.Dorian, MusicStyle.Machine,
            Tempo: 108, StepsPerBar: 20, BarsPerLoop: 4, Progression: [0, 6, 4, 1],
            Melody: new Motif([0, 2, 1, 4, 3, 0], 3),
            Mix: new InstrumentMix(0.80, 0.90, 0.86, 0.94, 0.26),
            Density: 0.78, Tension: 0.70, ReverbWet: 0.30, Band: 4),

        // 17 — Requiem. The Vault: heavier and slower, an organ under a stone ceiling.
        new MusicTheme(
            "vault", 34, MusicMode.Locrian, MusicStyle.Requiem,
            Tempo: 50, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 1, 4, 6],
            Melody: new Motif([0, -1, 0, 2, 0, -1], 6),
            Mix: new InstrumentMix(1.0, 0.36, 0.60, 0.20, 0.92),
            Density: 0.32, Tension: 0.72, ReverbWet: 0.84, Band: 4),

        // 18 — Gamelan. The Loom: two interlocking metal parts weaving around each other.
        new MusicTheme(
            "loom", 45, MusicMode.Phrygian, MusicStyle.Gamelan,
            Tempo: 116, StepsPerBar: 16, BarsPerLoop: 6, Progression: [0, 5, 1, 4],
            Melody: new Motif([0, 4, 6, 4, 2, 5], 2),
            Mix: new InstrumentMix(0.70, 0.44, 0.94, 0.0, 0.30),
            Density: 0.66, Tension: 0.58, ReverbWet: 0.54, Band: 4),

        // 19 — Chant. The Ash Garden: three voices, a frame drum, and nothing that tires.
        new MusicTheme(
            "ashgarden", 41, MusicMode.Aeolian, MusicStyle.Chant,
            Tempo: 66, StepsPerBar: 16, BarsPerLoop: 4, Progression: [0, 6, 3, 5],
            Melody: new Motif([0, 2, 0, 3, 2, 0], 4),
            Mix: new InstrumentMix(0.84, 0.44, 0.40, 0.30, 1.0),
            Density: 0.38, Tension: 0.68, ReverbWet: 0.76, Band: 4),

        // 20 — Requiem in 3/4, in canon: the floor that ends the descent.
        new MusicTheme(
            "deep", 36, MusicMode.Aeolian, MusicStyle.Requiem,
            Tempo: 48, StepsPerBar: 12, BarsPerLoop: 4, Progression: [0, 5, 3, 6],
            Melody: new Motif([0, 4, 2, 0, -1, 0], 6),
            Mix: new InstrumentMix(1.0, 0.34, 0.58, 0.26, 1.0),
            Density: 0.30, Tension: 0.86, ReverbWet: 0.90, Band: 4, Canon: true),
    ]);

    /// <summary>
    /// Finds the piece for a floor theme.
    /// </summary>
    /// <param name="themeId">The floor's theme id, as used by the atlas.</param>
    /// <returns>The matching theme.</returns>
    /// <exception cref="ArgumentException">Thrown when there is no piece for that id.</exception>
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
}

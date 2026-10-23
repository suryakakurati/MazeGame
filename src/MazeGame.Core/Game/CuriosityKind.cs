using System;

namespace MazeGame.Core.Game;

/// <summary>
/// The one optional find a floor holds besides its blooms and its arch.
/// </summary>
/// <remarks>
/// <para>
/// A floor's objective is always the same: take the blooms, open the arch. A curiosity is what the
/// floor has to offer a player who looks away from that route — a small, loud bargain that pays in
/// hearts, blooms or light. It is deliberately not a room: a room is a screen with a price on it, a
/// curiosity is a thing standing in the dark that the player chooses to touch.
/// </para>
/// <para>
/// Every generated floor holds exactly one, placed off the shortest route to the arch, so a run's
/// twenty floors carry twenty optional finds. Which kind is drawn is a function of the run's seed
/// and the floor number, so a replayed run finds the same things in the same places.
/// </para>
/// </remarks>
public enum CuriosityKind
{
    /// <summary>Pays a heart, and every hunter on the floor hears it.</summary>
    Bell,

    /// <summary>Costs a heart (never the last): two blooms and a charge of some verb.</summary>
    Reliquary,

    /// <summary>Gives one step of vision for the rest of the floor, and a bloom.</summary>
    Shrine,
}

/// <summary>
/// How far along its cycle the Drowned Halls' tide is.
/// </summary>
/// <remarks>
/// The swell is the warning: two seconds of the water audibly gathering before the light goes, so
/// losing the torch is a beat the player can react to rather than a flicker of the ceiling.
/// </remarks>
public enum TidePhase
{
    /// <summary>The water is out and the floor reads normally.</summary>
    Dry,

    /// <summary>The water is gathering: two seconds of warning before the flood.</summary>
    Swell,

    /// <summary>The tide is in: the torch loses a step and the hunters surge.</summary>
    Flood,
}

/// <summary>
/// The curiosities' names, hints and the draw that decides which one a floor holds.
/// </summary>
/// <remarks>
/// The draw is its own hash rather than a pull from the floor's random source: a curiosity must not
/// shift where the hunters, the blooms or the Echo of an already-seeded run were placed.
/// </remarks>
public static class CuriosityCatalog
{
    private const int CuriositySalt = 0x2C1B;

    private const int FloorStride = 4643;

    private const int PowerupStride = 104_729;

    /// <summary>Picks the curiosity a floor holds.</summary>
    /// <param name="seed">The run's seed (or the floor number on a hand-built floor).</param>
    /// <param name="floorNumber">The floor the find is on.</param>
    /// <returns>The kind, never out of range.</returns>
    public static CuriosityKind Pick(int seed, int floorNumber) =>
        (CuriosityKind)(Hash(seed, floorNumber, CuriositySalt) % Enum.GetValues<CuriosityKind>().Length);

    /// <summary>Picks the verb an opened reliquary awards a charge of.</summary>
    /// <param name="seed">The run's seed.</param>
    /// <param name="floorNumber">The floor the find is on.</param>
    /// <returns>The kind, never out of range.</returns>
    public static PowerupKind BonusPowerup(int seed, int floorNumber) =>
        (PowerupKind)(Hash(seed, floorNumber, PowerupStride) % Enum.GetValues<PowerupKind>().Length);

    /// <summary>Gets the object's name, as the HUD and the floor's hint line call it.</summary>
    /// <param name="kind">The curiosity.</param>
    /// <returns>The name, in lower case.</returns>
    public static string Name(CuriosityKind kind) => kind switch
    {
        CuriosityKind.Bell => "a drowned bell",
        CuriosityKind.Reliquary => "a sealed reliquary",
        CuriosityKind.Shrine => "a gardener's shrine",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown curiosity."),
    };

    /// <summary>Gets the one line that says what touching it does.</summary>
    /// <param name="kind">The curiosity.</param>
    /// <returns>The price and the reward, in one sentence.</returns>
    public static string Promise(CuriosityKind kind) => kind switch
    {
        CuriosityKind.Bell => "ring it for a heart, and every hunter hears",
        CuriosityKind.Reliquary => "open it with a heart: two blooms and a charge",
        CuriosityKind.Shrine => "touch it for a step of light and a bloom",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown curiosity."),
    };

    /// <summary>Hashes a seed, a floor and a salt into a stable positive number.</summary>
    private static int Hash(int seed, int floorNumber, int salt) =>
        unchecked(((seed * 397) ^ (floorNumber * FloorStride) ^ salt) & 0x7FFFFFFF);
}

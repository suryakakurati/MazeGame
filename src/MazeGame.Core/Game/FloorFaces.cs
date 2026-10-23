using System;

namespace MazeGame.Core.Game;

/// <summary>
/// The mood a floor woke up in: the authored floor as written, or one of three variations the
/// run rolls for.
/// </summary>
/// <remarks>
/// Faces exist so that two runs over the same campaign are not the same descent. The campaign
/// authors the spine — number, name, theme, rule, cast — and a face bends one lever of it,
/// loudly enough that the player feels the difference on the first step and on the intro card.
/// A face never changes the floor's number, so the campaign's gates, ties and draft schedule
/// still line up.
/// </remarks>
public enum FloorFace
{
    /// <summary>The floor exactly as the campaign wrote it.</summary>
    Authored,

    /// <summary>Fewer eyes, softer steps, and a step more of light.</summary>
    Quiet,

    /// <summary>One more bloom to find, and one more hunter looking for it.</summary>
    Greedy,

    /// <summary>The dark eats a step of vision, and the walls grow more ways through.</summary>
    Haunted,

    /// <summary>One bloom fewer to find, and a hungrier hunt.</summary>
    Starved,
}

/// <summary>
/// Rolls and applies <see cref="FloorFace"/> variations to the authored floors of a run.
/// </summary>
/// <remarks>
/// <para>
/// The roll is a pure function of the run seed and the floor number, called once when a campaign
/// is built. That keeps a save deterministic without storing anything extra: reloading a run
/// rebuilds the same faces. Tests can therefore assert a specific run's shape from its seed.
/// </para>
/// <para>
/// Authored is weighted double, so most floors keep the shape the campaign wrote and a run differs
/// in two or three places rather than everywhere. A floor whose differences would not be felt
/// (the first floor, where one hunter is the whole cast) is not protected: a quiet or greedy first
/// floor is still a different first floor, and protecting it would make every run open the same.
/// </para>
/// </remarks>
public static class FloorFaces
{
    /// <summary>The number of tickets in the weighted roll.</summary>
    public const int RollSize = 6;

    /// <summary>
    /// The first floor that can wear a face other than the authored one.
    /// </summary>
    /// <remarks>
    /// The opening floors are the teaching ladder, and a lesson that changes shape between runs
    /// is a lesson the player has to relearn. They stay authored; from the fourth floor on, a
    /// run's shape is its own.
    /// </remarks>
    public const int FirstVariedFloor = 4;

    /// <summary>Chooses the face a floor wears in this run.</summary>
    /// <param name="floor">The authored floor.</param>
    /// <param name="runSeed">The run's seed; the same seed always picks the same face.</param>
    /// <returns>One of the four faces.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public static FloorFace Drawn(FloorDefinition floor, int runSeed)
    {
        ArgumentNullException.ThrowIfNull(floor);

        if (floor.Number < FirstVariedFloor)
        {
            return FloorFace.Authored;
        }

        // Two tickets for the authored floor, one for each variation, spread by the hash so a
        // seed does not put every variation next to each other in the campaign.
        var ticket = (int)(Mix(runSeed, floor.Number) % RollSize);

        return ticket switch
        {
            0 or 1 => FloorFace.Authored,
            2 => FloorFace.Quiet,
            3 => FloorFace.Greedy,
            4 => FloorFace.Haunted,
            _ => FloorFace.Starved,
        };
    }

    /// <summary>Returns <paramref name="floor"/> bent by <paramref name="face"/>.</summary>
    /// <param name="floor">The authored floor.</param>
    /// <param name="face">The face to apply.</param>
    /// <returns>A floor with the same number and a different mood.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    public static FloorDefinition Apply(FloorDefinition floor, FloorFace face)
    {
        ArgumentNullException.ThrowIfNull(floor);

        return face switch
        {
            FloorFace.Quiet => floor with
            {
                HunterCount = Math.Max(1, floor.HunterCount - 1),
                EnemyStepSeconds = Math.Round(floor.EnemyStepSeconds * 1.25, 3),
                VisionRadius = (floor.VisionRadius ?? 3) + 1,
                Gimmick = Describe("a quiet descent", floor.Gimmick),
            },
            FloorFace.Greedy => floor with
            {
                KeyCount = floor.KeyCount + 1,
                HunterCount = floor.HunterCount + 1,
                EnemyStepSeconds = Math.Round(floor.EnemyStepSeconds * 0.9, 3),
                Gimmick = Describe("a greedy descent", floor.Gimmick),
            },
            FloorFace.Starved => floor with
            {
                // A hungrier floor: less to find, and the hunt a touch quicker. One bloom is
                // still a floor, so a one-bloom floor stays clearable on its own terms.
                KeyCount = Math.Max(1, floor.KeyCount - 1),
                EnemyStepSeconds = Math.Round(floor.EnemyStepSeconds * 0.95, 3),
                Gimmick = Describe("a starved descent", floor.Gimmick),
            },
            FloorFace.Haunted => floor with
            {
                // A floor that already has a rule keeps it: two tricks at once stops being a
                // variation and becomes a different floor. The haunted cost is taken in light.
                VisionRadius = Math.Max(1, (floor.VisionRadius ?? 3) - 1),
                ExtraLoops = (floor.ExtraLoops ?? 0) + 6,
                DeadEndBraidPercent = Math.Max(floor.DeadEndBraidPercent ?? 0, 80),
                Rule = floor.Rule ?? FloorRule.BurningLantern,
                Gimmick = Describe("a haunted descent", floor.Gimmick),
            },
            _ => floor,
        };
    }

    /// <summary>Returns the short label a floor's face adds to its name, or an empty string.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The face's lower-case name, or an empty string for an authored floor.</returns>
    public static string Label(FloorFace face) => face switch
    {
        FloorFace.Quiet => "quiet",
        FloorFace.Greedy => "greedy",
        FloorFace.Haunted => "haunted",
        FloorFace.Starved => "starved",
        _ => string.Empty,
    };

    /// <summary>
    /// Mixes a seed and a floor number into a stable, well-spread non-negative integer.
    /// </summary>
    /// <remarks>
    /// A plain <c>seed + number</c> would put the same face on the same floor of neighbouring
    /// seeds, which is exactly the repetition faces exist to break. This is the FNV-style multiply
    /// from the maze generator, small enough to read and stable across platforms.
    /// </remarks>
    private static uint Mix(int seed, int number)
    {
        unchecked
        {
            var mixed = (uint)seed * 16777619u;
            mixed ^= (uint)number * 2166136261u;
            mixed ^= mixed >> 13;
            mixed *= 2654435761u;
            mixed ^= mixed >> 15;
            return mixed;
        }
    }

    /// <summary>Prefixes the floor's own gimmick with the face's clause.</summary>
    private static string Describe(string clause, string? gimmick) =>
        string.IsNullOrWhiteSpace(gimmick) ? clause : $"{clause}: {gimmick}";
}

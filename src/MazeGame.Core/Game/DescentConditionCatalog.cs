using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Game;

/// <summary>
/// The names and one-line rules of every descent condition, plus the seeded draw for a gate.
/// </summary>
/// <remarks>
/// Like <see cref="CardCatalog"/>, this is content as data: the UI and the console both read the
/// same name and sentence, so a condition cannot say one thing on the window and another in the
/// terminal. The offer is a pure function of the run seed and the gate floor, which keeps a
/// seeded run reproducible.
/// </remarks>
public static class DescentConditionCatalog
{
    private static readonly DescentCondition[] Conditions = (DescentCondition[])Enum.GetValues(typeof(DescentCondition));

    /// <summary>Gets every condition, in enum order.</summary>
    public static IReadOnlyList<DescentCondition> All { get; } = new ReadOnlyCollection<DescentCondition>(Conditions);

    /// <summary>Returns the display name of <paramref name="condition"/>.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The name, for example "Regrowth".</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is unknown.</exception>
    public static string Name(DescentCondition condition) => condition switch
    {
        DescentCondition.Regrowth => "Regrowth",
        DescentCondition.Fracture => "Fracture",
        DescentCondition.Eclipse => "Eclipse",
        DescentCondition.Listener => "The Listener",
        DescentCondition.Pack => "Pack",
        DescentCondition.Hunted => "Hunted",
        DescentCondition.Mute => "Mute",
        DescentCondition.Drift => "Drift",
        _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown descent condition."),
    };

    /// <summary>Returns the one-sentence rule shown on the condition card.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The rule, in the present tense.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is unknown.</exception>
    public static string Description(DescentCondition condition) => condition switch
    {
        DescentCondition.Regrowth => "A dead end seals every 18 seconds.",
        DescentCondition.Fracture => "A wall opens and another closes every 22 seconds.",
        DescentCondition.Eclipse => "Your light falls a step for 6 seconds every 16.",
        DescentCondition.Listener => "The hunters only step when you step, and sprinting gives them two.",
        DescentCondition.Pack => "The hunters move together on one clock.",
        DescentCondition.Hunted => "One hunter is always lit and 20% faster.",
        DescentCondition.Mute => "One tool is unavailable each floor, which starts with an extra heart.",
        DescentCondition.Drift => "The exit moves after every key.",
        _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown descent condition."),
    };

    /// <summary>Returns the short all-caps line used by the HUD and the intro card.</summary>
    /// <param name="conditions">The conditions in force.</param>
    /// <returns>For example <c>REGROWTH + ECLIPSE</c>, or an empty string for none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conditions"/> is null.</exception>
    public static string Line(IReadOnlyList<DescentCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        if (conditions.Count == 0)
        {
            return string.Empty;
        }

        var parts = new string[conditions.Count];

        for (var index = 0; index < conditions.Count; index++)
        {
            parts[index] = Name(conditions[index]).ToUpperInvariant();
        }

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Returns two distinct conditions to offer at a gate, excluding any already active.
    /// </summary>
    /// <param name="runSeed">The run's seed.</param>
    /// <param name="gateFloor">The floor the choice leads into (8 or 14).</param>
    /// <param name="active">The conditions already chosen.</param>
    /// <returns>Two offers in draw order, or fewer when the pool is exhausted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="active"/> is null.</exception>
    public static IReadOnlyList<DescentCondition> Offer(
        int runSeed,
        int gateFloor,
        IReadOnlyList<DescentCondition> active)
    {
        ArgumentNullException.ThrowIfNull(active);

        var pool = new List<DescentCondition>();

        foreach (var condition in Conditions)
        {
            if (!active.Contains(condition))
            {
                pool.Add(condition);
            }
        }

        var random = new SeededRandom(Mix(runSeed, gateFloor));

        // A shuffle, then take two: uniform without a rejection loop, and the same seed always
        // produces the same pair.
        random.Shuffle(pool);

        var count = Math.Min(2, pool.Count);
        var offers = new DescentCondition[count];
        pool.CopyTo(0, offers, 0, count);

        return new ReadOnlyCollection<DescentCondition>(offers);
    }

    /// <summary>Derives a gate's seed from the run seed and the floor, like the draft does.</summary>
    private static int Mix(int runSeed, int gateFloor) =>
        unchecked((runSeed * 397) ^ (gateFloor * 7919));
}

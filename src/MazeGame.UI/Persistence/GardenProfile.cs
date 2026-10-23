using System;

namespace MazeGame.UI.Persistence;

/// <summary>
/// The garden grown across every run the player has finished.
/// </summary>
/// <remarks>
/// The garden is deliberately not a currency and unlocks nothing: it is a record, the way a
/// tally on a wall is. A run adds its blooms when it ends, and the deepest floor is kept as the
/// run's mark on the wall.
/// </remarks>
public sealed record GardenProfile
{
    /// <summary>An empty garden, for a first launch or an unreadable file.</summary>
    public static GardenProfile Empty { get; } = new();

    /// <summary>Gets how many blooms every finished run has grown, together.</summary>
    public int Blooms { get; init; }

    /// <summary>Gets the deepest floor any finished run reached.</summary>
    public int DeepestFloor { get; init; }

    /// <summary>Gets how many runs have been finished.</summary>
    public int Runs { get; init; }

    /// <summary>Returns a garden with one finished run added.</summary>
    /// <param name="blooms">The blooms that run grew.</param>
    /// <param name="floorReached">The deepest floor that run reached.</param>
    /// <returns>A new profile; this one is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="blooms"/> is negative or <paramref name="floorReached"/> is
    /// below one (every run plays at least its first floor).
    /// </exception>
    public GardenProfile Add(int blooms, int floorReached)
    {
        if (blooms < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blooms), blooms, "A run cannot un-grow a bloom.");
        }

        if (floorReached < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(floorReached),
                floorReached,
                "A finished run has played at least its first floor.");
        }

        return this with
        {
            Blooms = Blooms + blooms,
            DeepestFloor = Math.Max(DeepestFloor, floorReached),
            Runs = Runs + 1,
        };
    }
}

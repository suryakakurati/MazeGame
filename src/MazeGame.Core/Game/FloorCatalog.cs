using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The floors a shipped run descends through, in order.
/// </summary>
/// <remarks>
/// <para>
/// This is the content seam: the first playable campaign has three floors, and the eventual
/// twenty exist as more records here (with more themes) without touching the run rules. Each
/// floor changes at least one number that matters — keys or killer speed — so the descent is
/// felt rather than only read.
/// </para>
/// <para>
/// The list is a read-only snapshot: a caller cannot reorder or replace floors mid-run, which
/// keeps the save file and a seeded replay honest.
/// </para>
/// </remarks>
public static class FloorCatalog
{
    /// <summary>How many floors the shipped campaign has.</summary>
    public const int ShippedFloorCount = 3;

    private static readonly FloorDefinition[] Floors =
    [
        new()
        {
            Number = 1,
            Name = "The Shallows",
            ThemeId = "shallows",
            KeyCount = 3,
            EnemyStepSeconds = 0.60,
        },
        new()
        {
            Number = 2,
            Name = "Rootbound",
            ThemeId = "rootbound",
            KeyCount = 3,
            EnemyStepSeconds = 0.54,
        },
        new()
        {
            Number = 3,
            Name = "Drowned Halls",
            ThemeId = "drowned",
            KeyCount = 4,
            EnemyStepSeconds = 0.48,
        },
    ];

    /// <summary>
    /// Gets the shipped floors, in run order, as a read-only list.
    /// </summary>
    public static IReadOnlyList<FloorDefinition> Shipped { get; } =
        new ReadOnlyCollection<FloorDefinition>(Floors);

    /// <summary>
    /// Returns the floor with <paramref name="number"/> from <see cref="Shipped"/>.
    /// </summary>
    /// <param name="number">One-based floor number.</param>
    /// <returns>The matching floor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when no shipped floor has that number.
    /// </exception>
    public static FloorDefinition ByNumber(int number)
    {
        foreach (var floor in Floors)
        {
            if (floor.Number == number)
            {
                return floor;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(number),
            number,
            $"The shipped campaign has floors 1 to {ShippedFloorCount}.");
    }
}

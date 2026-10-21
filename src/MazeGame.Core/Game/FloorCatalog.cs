using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// The floors a shipped run descends through, in order.
/// </summary>
/// <remarks>
/// <para>
/// This is the content seam: twenty floors, each a record here plus a theme in the sprite
/// compiler, with no new code path. Every floor changes at least one number that matters — keys,
/// pace, the size of the maze — and most carry a <see cref="FloorDefinition.Gimmick"/> that
/// changes how the floor is played rather than only how fast it is. The knobs are the floor's own
/// optional fields, composed over the run's settings by <see cref="GameConfig.ForFloor"/>, so a
/// gimmick is data and never an <c>if</c> inside <see cref="GameState"/>.
/// </para>
/// <para>
/// The list is a read-only snapshot: a caller cannot reorder or replace floors mid-run, which
/// keeps the save file and a seeded replay honest.
/// </para>
/// <para>
/// The descent is designed in arcs, and each arc starts slower than the one before it ends: the
/// first three teach the loop, four to nine vary the shape and the light, ten to fifteen double the
/// hunt, sixteen to twenty triple it. A hunter walks slightly faster every floor, but the
/// **number** of hunters is the real step up — one for nine floors, two for six, three for the last
/// five — and the player's cadence is a sawtooth around that: 0.14 s on the first floor, tightening
/// to 0.10 by the Warren, then reset and tightened again in each arc. Speed is the small dial;
/// another hunter is the big one.
/// </para>
/// </remarks>
public static class FloorCatalog
{
    /// <summary>How many floors the shipped campaign has.</summary>
    public const int ShippedFloorCount = 20;

    private static readonly FloorDefinition[] Floors =
    [
        new()
        {
            Number = 1,
            Name = "The Shallows",
            ThemeId = "shallows",
            Gimmick = "the lantern reaches three steps",
            KeyCount = 3,
            EnemyStepSeconds = 0.62,
            PlayerStepSeconds = 0.14,
        },
        new()
        {
            // The first floor that takes something away: roots close in over the light.
            Number = 2,
            Name = "Rootbound",
            ThemeId = "rootbound",
            Gimmick = "roots drink the light",
            KeyCount = 3,
            EnemyStepSeconds = 0.58,
            PlayerStepSeconds = 0.13,
            VisionRadius = 2,
            Shape = ShapeKind.Blob,
        },
        new()
        {
            Number = 3,
            Name = "Drowned Halls",
            ThemeId = "drowned",
            Gimmick = "somebody built this, and the sea took it",
            KeyCount = 4,
            EnemyStepSeconds = 0.55,
            PlayerStepSeconds = 0.13,
        },
        new()
        {
            // Lit and slow: the trade is sight for safety, and the floor is about being seen.
            Number = 4,
            Name = "The Salt Choir",
            ThemeId = "saltchoir",
            Gimmick = "it hears you coming: lit, and slow",
            KeyCount = 3,
            Rows = 17,
            Columns = 17,
            EnemyStepSeconds = 0.95,
            PlayerStepSeconds = 0.12,
            VisionRadius = 5,
            Shape = ShapeKind.Ellipse,
        },
        new()
        {
            Number = 5,
            Name = "The Ember Vault",
            ThemeId = "ember",
            Gimmick = "everything glows, and so do you",
            KeyCount = 2,
            Rows = 17,
            Columns = 17,
            EnemyStepSeconds = 0.50,
            PlayerStepSeconds = 0.12,
        },
        new()
        {
            // No barriers at all: the first floor that removes a verb.
            Number = 6,
            Name = "The Gilded Cage",
            ThemeId = "gilded",
            Gimmick = "no seals down here",
            KeyCount = 5,
            Rows = 17,
            Columns = 17,
            EnemyStepSeconds = 0.54,
            PlayerStepSeconds = 0.11,
            BarrierBudget = 0,
        },
        new()
        {
            Number = 7,
            Name = "The Hush",
            ThemeId = "hush",
            Gimmick = "the dark is absolute",
            KeyCount = 3,
            Rows = 19,
            Columns = 19,
            EnemyStepSeconds = 0.62,
            PlayerStepSeconds = 0.12,
            VisionRadius = 1,
            Shape = ShapeKind.Blob,
        },
        new()
        {
            Number = 8,
            Name = "The Long Gallery",
            ThemeId = "gallery",
            Gimmick = "too many doors",
            KeyCount = 4,
            EnemyStepSeconds = 0.52,
            PlayerStepSeconds = 0.11,
            Rows = 21,
            Columns = 21,
            Shape = ShapeKind.Ellipse,
        },
        new()
        {
            Number = 9,
            Name = "The Warren",
            ThemeId = "warren",
            Gimmick = "a maze that fits in your arms",
            KeyCount = 5,
            EnemyStepSeconds = 0.60,
            PlayerStepSeconds = 0.10,
            Rows = 11,
            Columns = 11,
        },
        new()
        {
            Number = 10,
            Name = "The Glass Orchard",
            ThemeId = "orchard",
            Gimmick = "two of them walk the branches",
            KeyCount = 3,
            Rows = 19,
            Columns = 19,
            EnemyStepSeconds = 0.72,
            PlayerStepSeconds = 0.11,
            HunterCount = 2,
            ExtraLoops = 20,
            Shape = ShapeKind.Ellipse,
        },
        new()
        {
            // The killer's glow goes out: it arrives without warning until it is close.
            Number = 11,
            Name = "The Blind Mile",
            ThemeId = "blindmile",
            Gimmick = "two of them, and you hear them late",
            KeyCount = 4,
            Rows = 19,
            Columns = 19,
            EnemyStepSeconds = 0.66,
            PlayerStepSeconds = 0.10,
            HunterCount = 2,
            EnemyAuraRadius = 0,
            EnemyAuraDistance = 0,
        },
        new()
        {
            Number = 12,
            Name = "The Foundry",
            ThemeId = "foundry",
            Gimmick = "two of them, and six seals to spend",
            KeyCount = 3,
            Rows = 21,
            Columns = 21,
            EnemyStepSeconds = 0.64,
            PlayerStepSeconds = 0.10,
            HunterCount = 2,
            BarrierBudget = 6,
            LureCount = 0,
            Shape = ShapeKind.Blob,
        },
        new()
        {
            Number = 13,
            Name = "The Moth Garden",
            ThemeId = "mothgarden",
            Gimmick = "two of them; moths are cheap, light is not",
            KeyCount = 4,
            Rows = 21,
            Columns = 21,
            EnemyStepSeconds = 0.62,
            PlayerStepSeconds = 0.10,
            HunterCount = 2,
            LureCount = 4,
            VisionRadius = 2,
        },
        new()
        {
            // A catch barely moves it: the same hunter is back on you almost at once.
            Number = 14,
            Name = "The Ossuary",
            ThemeId = "ossuary",
            Gimmick = "two of them, and it keeps what it catches",
            KeyCount = 4,
            Rows = 21,
            Columns = 21,
            EnemyStepSeconds = 0.60,
            PlayerStepSeconds = 0.09,
            HunterCount = 2,
            RespawnDistance = 3,
        },
        new()
        {
            // Fast water, slow hunter: the floor is about the player's own pace.
            Number = 15,
            Name = "The Tide",
            ThemeId = "tide",
            Gimmick = "fast water, slow hunters",
            KeyCount = 3,
            Rows = 23,
            Columns = 23,
            EnemyStepSeconds = 0.90,
            PlayerStepSeconds = 0.07,
            HunterCount = 2,
            Shape = ShapeKind.Ellipse,
        },
        new()
        {
            Number = 16,
            Name = "The Bramble",
            ThemeId = "bramble",
            Gimmick = "three of them; nothing ends in a corner",
            KeyCount = 4,
            Rows = 23,
            Columns = 23,
            EnemyStepSeconds = 0.62,
            PlayerStepSeconds = 0.09,
            HunterCount = 3,
            DeadEndBraidPercent = 100,
        },
        new()
        {
            Number = 17,
            Name = "The Vault",
            ThemeId = "vault",
            Gimmick = "heavy doors, heavy steps, three of them",
            KeyCount = 5,
            Rows = 23,
            Columns = 23,
            EnemyStepSeconds = 0.60,
            PlayerStepSeconds = 0.15,
            HunterCount = 3,
        },
        new()
        {
            Number = 18,
            Name = "The Loom",
            ThemeId = "loom",
            Gimmick = "threads of light, and three of them",
            KeyCount = 4,
            Rows = 25,
            Columns = 25,
            EnemyStepSeconds = 0.58,
            PlayerStepSeconds = 0.09,
            HunterCount = 3,
            VisionRadius = 5,
        },
        new()
        {
            // The grace window is halved: the floor asks for clean movement, not recoveries.
            Number = 19,
            Name = "The Ash Garden",
            ThemeId = "ashgarden",
            Gimmick = "it does not tire, and there are three",
            KeyCount = 4,
            Rows = 25,
            Columns = 25,
            EnemyStepSeconds = 0.56,
            PlayerStepSeconds = 0.085,
            HunterCount = 3,
            InvulnerabilitySeconds = 0.5,
        },
        new()
        {
            Number = 20,
            Name = "The Deep",
            ThemeId = "deep",
            Gimmick = "the bottom of everything, and three of them",
            KeyCount = 5,
            EnemyStepSeconds = 0.54,
            PlayerStepSeconds = 0.08,
            HunterCount = 3,
            Rows = 25,
            Columns = 25,
            VisionRadius = 4,
            Shape = ShapeKind.Blob,
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

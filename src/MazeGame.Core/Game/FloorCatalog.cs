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
/// The descent is designed in arcs: the first three teach the loop, four to nine vary the shape and
/// the light, ten to fifteen double the hunt, sixteen to twenty triple it. Hunters quicken and more
/// of them arrive, but the **number** of hunters is the real step up — one for nine floors, two for
/// six, three for the last five. The player's cadence is the constant
/// <see cref="PlayerCadenceSeconds"/> on every floor: only a card (or sprint) makes the player
/// faster, so a floor can never quietly hand out speed.
/// </para>
/// </remarks>
public static class FloorCatalog
{
    /// <summary>How many floors the shipped campaign has.</summary>
    public const int ShippedFloorCount = 20;

    /// <summary>
    /// Gets the player's cadence on every shipped floor, in seconds per cell.
    /// </summary>
    /// <remarks>
    /// This is a run-wide constant on purpose. A roguelike's power curve belongs to the cards, not
    /// to the floor number: the hunt escalates through enemy pace and hunter count, while the
    /// player only gets faster by taking a speed card (or spending sprint stamina).
    /// </remarks>
    public const double PlayerCadenceSeconds = 0.26;

    private static readonly FloorDefinition[] Floors =
    [
        new()
        {
            Number = 1,
            Name = "The Shallows",
            ThemeId = "shallows",
            Gimmick = "the lantern reaches three steps",
            KeyCount = 3,
            EnemyStepSeconds = 1.20,
            PlayerStepSeconds = PlayerCadenceSeconds,
        },
        new()
        {
            // The first floor that takes something away: roots close in over the light.
            Number = 2,
            Name = "Rootbound",
            ThemeId = "rootbound",
            Gimmick = "roots drink the light",
            KeyCount = 3,
            EnemyStepSeconds = 1.10,
            PlayerStepSeconds = PlayerCadenceSeconds,
            VisionRadius = 2,
            Shape = ShapeKind.Blob,
        },
        new()
        {
            // The tide is the floor's clock: twelve seconds of dry hall, two of warning, then
            // eight of water that takes a step of sight and hurries the hunt by a third. The
            // player reads the swell and plans the next room around it.
            Number = 3,
            Name = "Drowned Halls",
            ThemeId = "drowned",
            Gimmick = "the tide comes in and takes the light",
            KeyCount = 3,
            EnemyStepSeconds = 1.00,
            PlayerStepSeconds = PlayerCadenceSeconds,
            VisionRadius = 4,
            Shape = ShapeKind.Ellipse,
            Rule = FloorRule.Tide,
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
            EnemyStepSeconds = 1.40,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.90,
            PlayerStepSeconds = PlayerCadenceSeconds,
        },
        new()
        {
            // Three locks instead of a fifth bloom: the arch is barred until all three gilded
            // cages are thrown, and they sit near, middle and far from where the player starts.
            // The floor is a route to plan, not a number to beat.
            Number = 6,
            Name = "The Gilded Cage",
            ThemeId = "gilded",
            Gimmick = "three locks bar the arch",
            KeyCount = 2,
            Rows = 17,
            Columns = 17,
            EnemyStepSeconds = 0.85,
            PlayerStepSeconds = PlayerCadenceSeconds,
            Rule = FloorRule.ThreeLocks,
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
            EnemyStepSeconds = 1.00,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.80,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.90,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 1.00,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.70,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 2,
            EnemyAuraRadius = 0,
            EnemyAuraDistance = 0,
        },
        new()
        {
            Number = 12,
            Name = "The Foundry",
            ThemeId = "foundry",
            Gimmick = "two of them, and shoves are cheap",
            KeyCount = 3,
            Rows = 21,
            Columns = 21,
            EnemyStepSeconds = 0.62,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 2,
            Powerups = PowerupCharges.ForFloor(12).Adjust(PowerupKind.Shove, 2),
            Shape = ShapeKind.Blob,
        },
        new()
        {
            Number = 13,
            Name = "The Moth Garden",
            ThemeId = "mothgarden",
            Gimmick = "two of them; lanterns are cheap",
            KeyCount = 4,
            Rows = 21,
            Columns = 21,
            EnemyStepSeconds = 0.58,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 2,
            Powerups = PowerupCharges.ForFloor(13).Adjust(PowerupKind.Lantern, 2),
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
            EnemyStepSeconds = 0.54,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 2,
            RespawnDistance = 3,

            // One of the two is asleep. It wakes on proximity, on a bloom, or on a catch, so
            // crossing the floor quietly is a route rather than a button.
            Rule = FloorRule.Sleeper,
        },
        new()
        {
            // Slow hunters and open water: the floor is about distance and patience, not a faster
            // player. The current is atmosphere; the cadence stays the run's constant.
            Number = 15,
            Name = "The Tide",
            ThemeId = "tide",
            Gimmick = "slow hunters, wide water",
            KeyCount = 3,
            Rows = 23,
            Columns = 23,
            EnemyStepSeconds = 0.80,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.60,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 3,
            DeadEndBraidPercent = 100,
        },
        new()
        {
            Number = 17,
            Name = "The Vault",
            ThemeId = "vault",
            Gimmick = "heavy doors, three of them",
            KeyCount = 5,
            Rows = 23,
            Columns = 23,
            EnemyStepSeconds = 0.40,
            PlayerStepSeconds = PlayerCadenceSeconds,
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
            EnemyStepSeconds = 0.34,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 3,
            VisionRadius = 5,

            // Two linked pads: the Loom's threads carry you across, with a two-second cooldown so
            // holding a direction cannot ping-pong between them.
            Rule = FloorRule.ThreadGates,
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
            EnemyStepSeconds = 0.28,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 3,
            InvulnerabilitySeconds = 0.5,

            // The Ash Garden's light burns down: one step of vision every forty seconds, restored
            // by every bloom. The objective is the light.
            Rule = FloorRule.BurningLantern,
        },
        new()
        {
            Number = 20,
            Name = "The Deep",
            ThemeId = "deep",
            Gimmick = "the bottom of everything, and three of them",
            KeyCount = 5,
            EnemyStepSeconds = 0.22,
            PlayerStepSeconds = PlayerCadenceSeconds,
            HunterCount = 3,
            Rows = 25,
            Columns = 25,
            VisionRadius = 4,
            Shape = ShapeKind.Blob,

            // The climax is a hold under pressure rather than a last sprint: the arch takes five
            // continuous seconds of standing.
            Rule = FloorRule.LongDoor,
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

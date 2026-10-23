using System;
using MazeGame.Core.Abstractions;

namespace MazeGame.Core.Game;

/// <summary>
/// The rooms a safe room can be, their weights and the words on their cards.
/// </summary>
/// <remarks>
/// The weights are content, not rules: they decide what a run of two rooms most often sees (one
/// money room and one utility room). Reincarnation is only eligible from floor six, so a short run
/// cannot be turned into a loop of free rewinds.
/// </remarks>
public static class RoomCatalog
{
    /// <summary>Gets the first floor on which The Turn may appear.</summary>
    public const int TurnFloor = 6;

    /// <summary>
    /// Gets the largest stake the table will take on <paramref name="floorNumber"/>.
    /// </summary>
    /// <param name="floorNumber">The floor the casino is on.</param>
    /// <returns>The cap, in hearts: twice the floor number.</returns>
    /// <remarks>
    /// The ceiling deepens with the descent instead of sitting at a flat five: a run on floor
    /// nineteen has earned a wager of thirty-eight, and the cap is what keeps the list a card the
    /// player can scroll rather than the run's whole heart count. It is still capped by the hearts
    /// the run is carrying, so the last heart is never on the table.
    /// </remarks>
    public static int MaxStakeFor(int floorNumber) => Math.Max(1, floorNumber * 2);

    /// <summary>Gets the percentage chance that a stake doubles.</summary>
    /// <remarks>
    /// Slightly under even on purpose: 45% on a double is 0.9× per heart, so the table is a
    /// temptation and never an income. The odds are printed on the card.
    /// </remarks>
    public const int GambleWinPercent = 45;

    /// <summary>
    /// Gets the odds a House Favour's promised table pays for its one play.
    /// </summary>
    /// <remarks>
    /// 66% on a double is 1.32× per heart, a real edge the card buys for exactly one bet. The
    /// promise makes that floor's room a casino and skips the spawn roll, so the edge is paid for
    /// in the card's own drawback rather than in extra visits.
    /// </remarks>
    public const int FavouredGambleWinPercent = 66;

    private const int SeedStride = 7919;

    private const int SeedSalt = 0x5200;

    private const int GambleSalt = 0x6A3B1E;

    /// <summary>Rolls the casino's single gamble for a floor.</summary>
    /// <param name="runSeed">The run's seed, so a replay stakes the same way.</param>
    /// <param name="floorNumber">The floor the room is on.</param>
    /// <param name="winPercent">
    /// The table's odds in whole percent. It defaults to the house's usual price and is raised
    /// only for the floor a House Favour promised.
    /// </param>
    /// <returns><see langword="true"/> when the stake doubles.</returns>
    public static bool GambleWins(
        int runSeed,
        int floorNumber,
        int winPercent = GambleWinPercent)
    {
        var random = new SeededRandom(
            unchecked((runSeed * 397) ^ (floorNumber * SeedStride) ^ GambleSalt));

        return random.NextInt(100) < Math.Clamp(winPercent, 0, 100);
    }

    /// <summary>Picks the room this floor's safe room holds.</summary>
    /// <param name="runSeed">The run's seed, so a replay visits the same rooms.</param>
    /// <param name="floorNumber">The floor the room is on.</param>
    /// <returns>The kind of room, never <see cref="RoomKind"/> out of range.</returns>
    /// <remarks>
    /// The draw is its own stream, derived from the run seed and floor: a room does not shift a
    /// draft's offers or a maze's walls just because it was rolled.
    /// </remarks>
    public static RoomKind Pick(int runSeed, int floorNumber)
    {
        var random = new SeededRandom(unchecked((runSeed * 397) ^ (floorNumber * SeedStride) ^ SeedSalt));

        var total = 0;

        foreach (var kind in Enum.GetValues<RoomKind>())
        {
            total += Weight(kind, floorNumber);
        }

        var roll = random.NextInt(total);

        foreach (var kind in Enum.GetValues<RoomKind>())
        {
            var weight = Weight(kind, floorNumber);

            if (roll < weight)
            {
                return kind;
            }

            roll -= weight;
        }

        // Unreachable while the weights add up to `total`; the loop above returns first.
        return RoomKind.Cartographer;
    }

    /// <summary>Gets how likely a room is, out of one hundred, on <paramref name="floorNumber"/>.</summary>
    /// <param name="kind">The room.</param>
    /// <param name="floorNumber">The floor it would appear on.</param>
    /// <returns>The weight; zero when the room is not eligible yet.</returns>
    public static int Weight(RoomKind kind, int floorNumber) => kind switch
    {
        RoomKind.LongTable => 24,
        RoomKind.Stall => 24,
        RoomKind.Confessional => 15,
        RoomKind.Toll => 10,
        RoomKind.Cartographer => 8,
        RoomKind.SeedVault => 8,
        RoomKind.Mirror => 6,
        RoomKind.Turn => floorNumber >= TurnFloor ? 5 : 0,
        _ => 0,
    };

    /// <summary>Gets the room's name, drawn as the card's title.</summary>
    /// <param name="kind">The room.</param>
    /// <returns>The name.</returns>
    public static string Name(RoomKind kind) => kind switch
    {
        RoomKind.Cartographer => "The Cartographer",
        RoomKind.LongTable => "The Long Table",
        RoomKind.Stall => "The Stall",
        RoomKind.Confessional => "The Confessional",
        RoomKind.Toll => "The Toll",
        RoomKind.SeedVault => "The Seed Vault",
        RoomKind.Mirror => "The Mirror",
        RoomKind.Turn => "The Turn",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown room."),
    };

    /// <summary>Gets the card's prose: what the room offers, in one or two short lines.</summary>
    /// <param name="kind">The room.</param>
    /// <param name="winPercent">The odds this floor's table pays, for the casino's prose.</param>
    /// <returns>The prose.</returns>
    public static string Prompt(RoomKind kind, int winPercent = GambleWinPercent) => kind switch
    {
        RoomKind.Cartographer => "Charts of this floor, lit until you leave it.",
        RoomKind.LongTable => $"Stake hearts. Win at {winPercent}%, lose at {100 - winPercent}%.",
        RoomKind.Stall => "Three cards, one heart each.",
        RoomKind.Confessional => "Surrender a card and a heart, and be lighter. Refusing costs the same; it chooses the card.",
        RoomKind.Toll => "A heart buys the next floor one hunter fewer.",
        RoomKind.SeedVault => "Plant a seed. Three floors down it blooms, and pays a heart.",
        RoomKind.Mirror => "Trade a card for one the mirror chooses.",
        RoomKind.Turn => "Go back five floors. Your wounds come with you.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown room."),
    };

    /// <summary>Gets the card's price line, which never changes with the run's state.</summary>
    /// <param name="kind">The room.</param>
    /// <returns>The price, in capitals.</returns>
    public static string Price(RoomKind kind) => kind switch
    {
        RoomKind.LongTable => "YOUR STAKE",
        RoomKind.Stall or RoomKind.Confessional or RoomKind.Toll => "ONE HEART",
        RoomKind.Turn => "ONE HEART TO START OVER",
        _ => "NOTHING",
    };
}

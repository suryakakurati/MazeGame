using System;

namespace MazeGame.Core.Game;

/// <summary>
/// How many uses of each powerup a floor allows.
/// </summary>
/// <remarks>
/// <para>
/// Charges are per floor and refilled when it is built; there is no shared pool and nothing is
/// unlocked by a key or a card. The shipped ladder introduces one verb at a time and then deepens
/// the counts, so the HUD can show the whole kit from floor 1 without asking the player to learn
/// five buttons at once.
/// </para>
/// <para>
/// The record is immutable, so a card or a condition composes a new set with
/// <see cref="With"/> or <see cref="Adjust"/> rather than mutating a floor's data.
/// </para>
/// </remarks>
public sealed record PowerupCharges
{
    /// <summary>Gets the default counts: the full kit at its middle-of-the-run strength.</summary>
    public static PowerupCharges Default { get; } = new();

    /// <summary>Gets a kit with no uses at all.</summary>
    public static PowerupCharges None { get; } = new()
    {
        Blink = 0,
        Veil = 0,
        Drag = 0,
        Lantern = 0,
        Shove = 0,
    };

    /// <summary>Gets how many blinks a floor allows. Defaults to 2.</summary>
    public int Blink { get; init; } = 2;

    /// <summary>Gets how many veils a floor allows. Defaults to 3.</summary>
    public int Veil { get; init; } = 3;

    /// <summary>Gets how many drags a floor allows. Defaults to 1.</summary>
    public int Drag { get; init; } = 1;

    /// <summary>Gets how many lantern throws a floor allows. Defaults to 1.</summary>
    public int Lantern { get; init; } = 1;

    /// <summary>Gets how many shoves a floor allows. Defaults to 1.</summary>
    public int Shove { get; init; } = 1;

    /// <summary>
    /// Returns the shipped teaching ladder for a floor number.
    /// </summary>
    /// <param name="floorNumber">The one-based floor.</param>
    /// <returns>The charges that floor starts with.</returns>
    /// <remarks>
    /// The first floor carries only Blink, the second adds Veil, the fourth Drag, the sixth
    /// Lantern and the eighth Shove. From there the counts step up twice, so late floors have more
    /// uses without introducing another button.
    /// </remarks>
    public static PowerupCharges ForFloor(int floorNumber)
    {
        if (floorNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(floorNumber), floorNumber, "Floors are numbered from 1.");
        }

        return floorNumber switch
        {
            1 => None with { Blink = 2 },
            <= 3 => None with { Blink = 2, Veil = 2 },
            <= 5 => None with { Blink = 2, Veil = 2, Drag = 1 },
            <= 7 => None with { Blink = 2, Veil = 2, Drag = 1, Lantern = 1 },
            <= 8 => None with { Blink = 2, Veil = 3, Drag = 1, Lantern = 1, Shove = 1 },
            <= 14 => None with { Blink = 3, Veil = 3, Drag = 2, Lantern = 1, Shove = 1 },
            _ => None with { Blink = 3, Veil = 3, Drag = 2, Lantern = 2, Shove = 2 },
        };
    }

    /// <summary>Returns how many uses of <paramref name="kind"/> the set holds.</summary>
    /// <param name="kind">The powerup to count.</param>
    /// <returns>The charge count, never negative.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is unknown.</exception>
    public int Of(PowerupKind kind) => kind switch
    {
        PowerupKind.Blink => Blink,
        PowerupKind.Veil => Veil,
        PowerupKind.Drag => Drag,
        PowerupKind.Lantern => Lantern,
        PowerupKind.Shove => Shove,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown powerup."),
    };

    /// <summary>Returns a copy with <paramref name="kind"/> set to <paramref name="count"/>.</summary>
    /// <param name="kind">The powerup to change.</param>
    /// <param name="count">The new count; must not be negative.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is negative.</exception>
    public PowerupCharges With(PowerupKind kind, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "A powerup cannot have negative charges.");
        }

        return kind switch
        {
            PowerupKind.Blink => this with { Blink = count },
            PowerupKind.Veil => this with { Veil = count },
            PowerupKind.Drag => this with { Drag = count },
            PowerupKind.Lantern => this with { Lantern = count },
            PowerupKind.Shove => this with { Shove = count },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown powerup."),
        };
    }

    /// <summary>
    /// Returns a copy with <paramref name="delta"/> added to one powerup, clamped at zero.
    /// </summary>
    /// <param name="kind">The powerup to change.</param>
    /// <param name="delta">The amount to add; may be negative.</param>
    /// <returns>A new set; this one is unchanged.</returns>
    public PowerupCharges Adjust(PowerupKind kind, int delta) =>
        With(kind, Math.Max(0, Of(kind) + delta));

    /// <summary>Checks that no charge count is negative.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a count is negative.</exception>
    public void Validate()
    {
        foreach (var kind in Enum.GetValues<PowerupKind>())
        {
            if (Of(kind) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A powerup cannot have negative charges.");
            }
        }
    }
}

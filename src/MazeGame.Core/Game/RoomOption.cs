using System;

namespace MazeGame.Core.Game;

/// <summary>
/// One thing the player may do in a room, with its price already visible.
/// </summary>
/// <remarks>
/// The card never shows an option the run cannot pay for: a stake above <c>hearts - 1</c>, a card
/// the run cannot afford, or a swap with nothing eligible is simply not offered. That is why
/// <see cref="Note"/> is a price and not a warning.
/// </remarks>
/// <param name="Id">The stable id the campaign resolves.</param>
/// <param name="Label">The line drawn on the card, in capitals.</param>
/// <param name="Note">The price or odds under the label, or null for none.</param>
/// <param name="Preview">
/// The card's face text, shown when the option is a card for sale. Players are not expected to
/// memorise the catalog, so the shop states what each card does before it takes a heart for it.
/// </param>
public sealed record RoomOption(string Id, string Label, string? Note = null, string? Preview = null)
{
    /// <summary>Gets the option that closes the card without spending anything.</summary>
    public static RoomOption Leave { get; } = new("leave", "LEAVE", null);

    /// <summary>Checks that the option can be drawn and resolved.</summary>
    /// <exception cref="ArgumentException">Thrown when the id or label is blank.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("A room option needs an id.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Label))
        {
            throw new ArgumentException("A room option needs a label.", nameof(Label));
        }
    }
}

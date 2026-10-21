namespace MazeGame.Core.Game;

/// <summary>
/// The five tools a run can carry, one per key.
/// </summary>
/// <remarks>
/// The kit is a small set of verbs rather than a shared resource: each kind has its own charge
/// count, so a player who likes one tool is not spending another one's budget. The enum order is
/// the HUD order and the order a muted floor rotates through, so changing it changes both.
/// </remarks>
public enum PowerupKind
{
    /// <summary>Step through one wall, leaving the breach open behind you.</summary>
    Blink,

    /// <summary>Stand still to go unseen while the hunters search where you were.</summary>
    Veil,

    /// <summary>Slow the hunt for a moment, then pay for it with a faster one.</summary>
    Drag,

    /// <summary>Throw your light ahead and let the hunters follow it.</summary>
    Lantern,

    /// <summary>Push an adjacent hunter away and stun it.</summary>
    Shove,
}

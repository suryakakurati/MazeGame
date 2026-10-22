namespace MazeGame.Core.Game;

/// <summary>
/// What a safe room offers: one interaction, priced in hearts or in nothing at all.
/// </summary>
/// <remarks>
/// A room is an <i>event</i>, not a shop route: the first floor always holds one to teach the
/// mechanic, and every floor after it rolls a fifty percent chance, so a twenty-floor run expects
/// about ten of them. The kind is drawn when the card is opened, from the run's seed, so a
/// replayed run visits the same rooms.
/// </remarks>
public enum RoomKind
{
    /// <summary>Reveals this floor's whole map for the rest of the floor. Free.</summary>
    Cartographer,

    /// <summary>Stake hearts on a 45% double. The last heart can never be staked.</summary>
    LongTable,

    /// <summary>Three cards, one heart each. One purchase per visit.</summary>
    Stall,

    /// <summary>Give up one card and one heart: the only removal in the run.</summary>
    Confessional,

    /// <summary>The next floor keeps one hunter at home, never below one.</summary>
    Toll,

    /// <summary>Plant a seed that blooms three floors down for a heart.</summary>
    SeedVault,

    /// <summary>Swap one card for a random eligible one, free, once.</summary>
    Mirror,

    /// <summary>Go back five floors, or to floor one for a heart. Once per run.</summary>
    Turn,
}

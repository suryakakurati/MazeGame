namespace MazeGame.Core.Game;

/// <summary>
/// What the player is currently aiming, if anything.
/// </summary>
/// <remarks>
/// Aiming is a game state, not a UI mode: while it is active the clock is frozen, the player
/// cannot walk, and the direction keys move the aiming cursor. Both placeable items share the
/// same cursor and the same freeze, so the frontends only have to know which one they are
/// aiming.
/// </remarks>
public enum PlacementMode
{
    /// <summary>The player is not aiming; direction keys move the player.</summary>
    None,

    /// <summary>A barrier is being aimed, and will seal the cursor's cell when confirmed.</summary>
    Barrier,

    /// <summary>A wisp is being aimed, and will glow on the cursor's cell when confirmed.</summary>
    Lure,
}

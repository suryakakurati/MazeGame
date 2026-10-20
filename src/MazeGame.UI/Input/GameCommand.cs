namespace MazeGame.UI.Input;

/// <summary>
/// One player intention, independent of the keyboard it came from.
/// </summary>
/// <remarks>
/// Both frontends (the Raylib window and the console fallback) translate their own keys into
/// these values, and <see cref="GameCommands"/> applies them to the game. That keeps the
/// key-to-rule mapping in exactly one place instead of once per frontend.
/// </remarks>
public enum GameCommand
{
    /// <summary>Step north, or move the aiming cursor north while aiming.</summary>
    MoveNorth,

    /// <summary>Step south, or move the aiming cursor south while aiming.</summary>
    MoveSouth,

    /// <summary>Step west, or move the aiming cursor west while aiming.</summary>
    MoveWest,

    /// <summary>Step east, or move the aiming cursor east while aiming.</summary>
    MoveEast,

    /// <summary>Start aiming a barrier, which also pauses the game clock.</summary>
    BeginAim,

    /// <summary>Place the barrier on the aiming cursor's cell.</summary>
    ConfirmAim,

    /// <summary>Stop aiming without spending a barrier.</summary>
    CancelAim,

    /// <summary>Leave the game.</summary>
    Quit,
}

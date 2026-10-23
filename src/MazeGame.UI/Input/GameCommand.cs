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

    /// <summary>Step through the wall the player faces.</summary>
    Blink,

    /// <summary>Stand still to become unseen.</summary>
    Veil,

    /// <summary>Slow the hunt for a moment.</summary>
    Drag,

    /// <summary>Throw the lantern ahead.</summary>
    Lantern,

    /// <summary>Shove the hunter in front of the player.</summary>
    Shove,

    /// <summary>
    /// Open the safe room's card, when the player is standing in one.
    /// </summary>
    /// <remarks>
    /// The floor itself has nothing to do with this command: rooms belong to the campaign, which is
    /// why <see cref="GameCommands"/> leaves it alone and the frontends hand it to the run.
    /// </remarks>
    Interact,

}

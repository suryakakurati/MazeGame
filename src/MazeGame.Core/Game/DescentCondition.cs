namespace MazeGame.Core.Game;

/// <summary>
/// A rule the run carries for the rest of its descent, chosen at a gate.
/// </summary>
/// <remarks>
/// Conditions are not cards: they are not drawn from a pool after every floor and they are never
/// stacked more than twice. Each one changes the space or the hunt itself, so two runs that reach
/// floor 12 with different conditions are playing different floors.
/// </remarks>
public enum DescentCondition
{
    /// <summary>Dead ends seal over time, so the maze stops forgiving backtracking.</summary>
    Regrowth,

    /// <summary>Walls open and close on a timer, so routes are provisional.</summary>
    Fracture,

    /// <summary>The lantern gutters on a cycle, so the floor breathes darkness.</summary>
    Eclipse,

    /// <summary>Hunters only step when the player steps; sprinting gives them two.</summary>
    Listener,

    /// <summary>Hunters share a clock, so they commit together.</summary>
    Pack,

    /// <summary>One hunter is always lit and slightly faster than the rest.</summary>
    Hunted,

    /// <summary>One tool is unavailable on each floor, which heals a heart instead.</summary>
    Mute,

    /// <summary>The exit moves after every key.</summary>
    Drift,
}

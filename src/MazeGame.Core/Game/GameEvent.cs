namespace MazeGame.Core.Game;

/// <summary>
/// Something that happened inside a floor, in the order it happened.
/// </summary>
/// <remarks>
/// <para>
/// These are the moments a floor cannot be replayed from its state alone: a step taken, a key
/// lifted, a heart spent, a seal torn apart. The UI drains them once a frame and turns them into
/// sound and effect, which keeps the rules free of any reference to audio, and keeps the audio
/// from "guessing" what happened by diffing two frames.
/// </para>
/// <para>
/// They are deliberately <i>not</i> a full log. Anything the player can see from the state
/// (where they are, how many hearts are left) does not need an event; only the transition does.
/// </para>
/// </remarks>
public enum GameEvent
{
    /// <summary>The player moved one cell onto ordinary floor.</summary>
    Step,

    /// <summary>The player stepped onto a key and it left the maze.</summary>
    KeyPicked,

    /// <summary>The killer caught the player and the catch cost a heart, but not the last one.</summary>
    HeartLost,

    /// <summary>The killer caught the player and that was the last heart.</summary>
    HeartLostLast,

    /// <summary>A card turned a catch aside: no heart was lost.</summary>
    Graced,

    /// <summary>A fatal catch left the player at one heart instead of ending the run.</summary>
    Revived,

    /// <summary>The killer stepped one cell while hunting the player.</summary>
    KillerStep,

    /// <summary>A barrier was confirmed and its seal started.</summary>
    SealPlaced,

    /// <summary>A placed seal ran out of time and cracked: the barrier is now only a slowdown.</summary>
    SealCracked,

    /// <summary>The killer tore a live seal apart to get through.</summary>
    SealBroken,

    /// <summary>A wisp was lit.</summary>
    WispLit,

    /// <summary>The killer reached the wisp, spending it.</summary>
    WispConsumed,

    /// <summary>The player stepped through a wall, leaving a breach.</summary>
    Blink,

    /// <summary>A veil completed and the player is now unseen.</summary>
    Veiled,

    /// <summary>The hunt slowed for a moment.</summary>
    Drag,

    /// <summary>The lantern left the player's hand.</summary>
    LanternThrown,

    /// <summary>The lantern came back: retrieved, timed out or put out by a hunter.</summary>
    LanternReturned,

    /// <summary>A hunter was shoved away and stunned.</summary>
    Shoved,

    /// <summary>A dead end sealed under Regrowth.</summary>
    WallSealed,

    /// <summary>A wall opened under Fracture.</summary>
    WallOpened,

    /// <summary>A passage closed under Fracture.</summary>
    WallClosed,

    /// <summary>The exit moved under Drift.</summary>
    ExitMoved,

    /// <summary>The dark half of an Eclipse began.</summary>
    EclipseFell,

    /// <summary>The lit half of an Eclipse returned.</summary>
    EclipseLifted,

    /// <summary>The player reached the unlocked exit: this floor is cleared.</summary>
    FloorCleared,

    /// <summary>The run is over because the last heart was spent.</summary>
    RunLost,
}

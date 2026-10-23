namespace MazeGame.Core.Game;

/// <summary>
/// The one exclusive verb a late floor has and no other floor has.
/// </summary>
/// <remarks>
/// <para>
/// Everything that made floors differ before this was a number — speed, vision, hunter count,
/// braid percentage — and by the deep floors the player has learned all of them. A rule is
/// different in kind: the floor's intro line names it and the first encounter teaches it, with no
/// new key and no menu.
/// </para>
/// <para>
/// A rule is one enum value on a floor, read in exactly one place in <see cref="GameState"/>, and
/// covered by its own tests. A floor never has two: a floor that needs two is a floor that needs a
/// redesign.
/// </para>
/// <para>
/// <b>The pressure gate is deliberately absent.</b> A portcullis held open only while the player
/// stands on a plate cannot be walked through by the player holding it open, so it needs a reward
/// that can be taken from a distance — content the room pass is only now making possible. Shipping
/// it without that reward would be a door that never helps. It goes in with the next content wave.
/// </para>
/// </remarks>
public enum FloorRule
{
    /// <summary>Two linked pads: stepping on one places you on the other, with a cooldown.</summary>
    ThreadGates,

    /// <summary>One hunter starts dormant and wakes when you come close, take a bloom, or are caught.</summary>
    Sleeper,

    /// <summary>The arch needs five continuous seconds of standing, not a touch.</summary>
    LongDoor,

    /// <summary>Vision falls one step every forty seconds, to a floor of one; each bloom restores it.</summary>
    BurningLantern,

    /// <summary>
    /// The tide comes in and goes out: while it is in, the torch loses a step and the hunters
    /// surge. The swell warns two seconds before it floods.
    /// </summary>
    Tide,

    /// <summary>Three gilded locks bar the arch; each must be thrown before the arch opens.</summary>
    ThreeLocks,
}

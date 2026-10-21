using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// Turns a held direction into a single step, or into a repeating walk when it is actually held.
/// </summary>
/// <remarks>
/// <para>
/// A tap is worth exactly one tile: the first frame of a press returns the direction once, and the
/// frames after it return nothing until the key has been down for
/// <see cref="HoldDelaySeconds"/> and the previous step has finished. That is the difference
/// between "press and release to move one cell" and "hold to walk", and it exists because sending
/// the held direction every frame let a slow tap leave a press in the buffer and move a second
/// tile after the player had let go.
/// </para>
/// <para>
/// This is input policy, and it lives in Core for one reason: it is the only way to test it without
/// a window, and it depends on nothing but the step cooldown. It never moves anything itself — it
/// only decides which frames are worth sending a direction on.
/// </para>
/// </remarks>
public sealed class StepRepeat
{
    /// <summary>
    /// How long a direction must stay down before it repeats. Longer than a deliberate tap, and
    /// longer than one frame at 60 Hz, so a quick press can never leave a repeat behind.
    /// </summary>
    public const double HoldDelaySeconds = 0.15;

    private Direction? _direction;
    private double _since;

    /// <summary>Gets the direction currently being repeated, or null when none is held.</summary>
    public Direction? Held => _direction;

    /// <summary>
    /// Returns the direction to send this frame, or null when this frame sends none.
    /// </summary>
    /// <param name="held">The direction whose key is down, or null when none is.</param>
    /// <param name="stepping">Whether the player's previous step is still cooling down.</param>
    /// <param name="now">Seconds on a monotonic clock, in the same units as the delay.</param>
    /// <returns>
    /// The direction on the frame it is first pressed, and again on every frame once the key has
    /// been held past <see cref="HoldDelaySeconds"/> and no step is running. Null otherwise.
    /// </returns>
    public Direction? Next(Direction? held, bool stepping, double now)
    {
        if (held is not { } direction)
        {
            Clear();
            return null;
        }

        if (_direction != direction)
        {
            // A fresh press, or a change of direction mid-hold: always worth one step. If the last
            // step is still running, the game buffers it and spends it when the step ends.
            _direction = direction;
            _since = now;
            return direction;
        }

        if (now < _since)
        {
            // A clock that went backwards (a restarted window) must not look like a long hold.
            _since = now;
            return null;
        }

        if (now - _since < HoldDelaySeconds || stepping)
        {
            return null;
        }

        return direction;
    }

    /// <summary>Forgets the held direction, so the next press counts as fresh.</summary>
    public void Clear()
    {
        _direction = null;
        _since = 0;
    }
}

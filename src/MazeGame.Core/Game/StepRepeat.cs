using System;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// Turns a held direction into a single step, or into a repeating walk when it is actually held.
/// </summary>
/// <remarks>
/// <para>
/// A tap is worth exactly one tile: the first frame of a press returns the direction once, and the
/// frames after it return nothing until the step that press caused has finished. Holding sends the
/// direction again the moment that step ends, so walking is paced by the player's own step time
/// rather than by the frame rate and without an extra wait between tiles.
/// </para>
/// <para>
/// The previous version waited <c>0.15 s</c> after the press before it would repeat, on top of the
/// step itself. That made a hold feel like a sequence of taps with a stutter in it, and it made the
/// only way to cross a room quickly be a hold that was already running late. Since the repeat is
/// gated on the previous step having <b>finished</b>, the delay is not needed for correctness: a
/// key released during the step never leaves a repeat behind, because the gate stops sending as
/// soon as the key is up.
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
    /// The shortest time between two repeats, in seconds.
    /// </summary>
    /// <remarks>
    /// Only matters when the floor has no step time at all (a debug config with instant movement):
    /// with a real step the previous step's own duration paces the walk, and sprinting or a turn
    /// penalty is therefore felt immediately instead of being flattened to one rate.
    /// </remarks>
    public const double MinRepeatSeconds = 0.05;

    private Direction? _direction;
    private double _lastSent = double.NegativeInfinity;

    /// <summary>Gets the direction currently being repeated, or null when none is held.</summary>
    public Direction? Held => _direction;

    /// <summary>
    /// Returns the direction to send this frame, or null when this frame sends none.
    /// </summary>
    /// <param name="held">The direction whose key is down, or null when none is.</param>
    /// <param name="stepping">Whether the player's previous step is still cooling down.</param>
    /// <param name="now">Seconds on a monotonic clock, in the same units as the step time.</param>
    /// <param name="stepSeconds">
    /// How long the previous step took. Used as the minimum gap between repeats, so a hold walks at
    /// the floor's own cadence; a value below <see cref="MinRepeatSeconds"/> is treated as that.
    /// </param>
    /// <returns>
    /// The direction on the frame it is first pressed, and again on the frame each step finishes
    /// while the key is still down. Null otherwise.
    /// </returns>
    public Direction? Next(Direction? held, bool stepping, double now, double stepSeconds = MinRepeatSeconds)
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
            _lastSent = now;
            return direction;
        }

        if (now < _lastSent)
        {
            // A clock that went backwards (a restarted window) must not look like a long hold.
            _lastSent = now;
            return null;
        }

        // Waiting for the step to finish is what keeps a release from leaving a queued tile behind:
        // by the time a repeat is due, the key has to still be down to be sent.
        if (stepping || now - _lastSent < Math.Max(MinRepeatSeconds, stepSeconds))
        {
            return null;
        }

        _lastSent = now;
        return direction;
    }

    /// <summary>Forgets the held direction, so the next press counts as fresh.</summary>
    public void Clear()
    {
        _direction = null;
        _lastSent = double.NegativeInfinity;
    }
}

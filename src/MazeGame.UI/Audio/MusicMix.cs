using System;
using MazeGame.Core.Game;

namespace MazeGame.UI.Audio;

/// <summary>
/// How much of the score the player should hear right now.
/// </summary>
/// <remarks>
/// The arrangement does not change file; only which stems are open. Calm is the bed alone,
/// tense lets the bass in, and the chase opens the motif and the drums. Hurt is not a state the
/// player sits in — it is the duck applied under a damage stinger.
/// </remarks>
public enum MusicIntensity
{
    /// <summary>Nothing is hunting: pad and choir only.</summary>
    Calm,

    /// <summary>The killer is somewhere in the fog: the bass arrives.</summary>
    Tense,

    /// <summary>The killer is close: motif, drums, the whole arrangement.</summary>
    Chase,

    /// <summary>A heart was just spent: everything ducks under the stinger.</summary>
    Hurt,
}

/// <summary>
/// The three stem gains that make one intensity.
/// </summary>
/// <param name="Bed">Gain for the pad and choir.</param>
/// <param name="Rhythm">Gain for the bass and drums.</param>
/// <param name="Lead">Gain for the motif and bells.</param>
public readonly record struct StemGains(float Bed, float Rhythm, float Lead);

/// <summary>
/// Turns a floor's state into a mix, and a mix into numbers.
/// </summary>
/// <remarks>
/// This is the whole of "the music reacts": a pure function of the game state, so the change can
/// be tested without an audio device, and the engine can apply it on a bar line rather than the
/// instant a number crossed a threshold.
/// </remarks>
public static class MusicMix
{
    /// <summary>How many cells away the killer counts as close.</summary>
    public const int ChaseDistance = 3;

    /// <summary>How many cells away the killer stops being background noise.</summary>
    public const int TenseDistance = 7;

    /// <summary>
    /// Gets the stem gains for <paramref name="intensity"/>.
    /// </summary>
    /// <param name="intensity">The intensity to mix.</param>
    /// <returns>The three gains.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the intensity is unknown.</exception>
    public static StemGains Gains(MusicIntensity intensity) => intensity switch
    {
        MusicIntensity.Calm => new StemGains(1.0f, 0.0f, 0.0f),
        MusicIntensity.Tense => new StemGains(1.0f, 0.5f, 0.12f),
        MusicIntensity.Chase => new StemGains(1.0f, 0.95f, 0.8f),
        MusicIntensity.Hurt => new StemGains(0.45f, 0.35f, 0.3f),
        _ => throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Unknown intensity."),
    };

    /// <summary>
    /// Chooses the intensity for <paramref name="state"/> from the distance to the killer.
    /// </summary>
    /// <param name="state">The floor being played.</param>
    /// <returns>The intensity the mix should move towards.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
    /// <remarks>
    /// Aiming freezes the clock, so it also settles the music: a player planning a barrier is not
    /// being chased this second, even if the killer is one cell away. A stunned killer is a
    /// reprieve, not a chase, which is exactly what the drop back to tense should say.
    /// </remarks>
    public static MusicIntensity From(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != GameStatus.InProgress || state.IsAiming || state.IsEnemyStunned)
        {
            return MusicIntensity.Calm;
        }

        // The mix follows the nearest hunter: with two of them, the far one is not the thing that
        // should decide whether the drums are playing.
        var distance = int.MaxValue;

        foreach (var hunter in state.Enemies)
        {
            distance = Math.Min(distance, Math.Max(
                Math.Abs(hunter.Cell.Row - state.Player.Row),
                Math.Abs(hunter.Cell.Column - state.Player.Column)));
        }

        return ForDistance(distance);
    }

    /// <summary>
    /// Chooses the intensity for a killer <paramref name="distance"/> cells away.
    /// </summary>
    /// <param name="distance">The distance between the killer and the player, in cells.</param>
    /// <returns>The intensity that distance deserves.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is negative.</exception>
    public static MusicIntensity ForDistance(int distance)
    {
        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "A distance cannot be negative.");
        }

        if (distance <= ChaseDistance)
        {
            return MusicIntensity.Chase;
        }

        return distance <= TenseDistance ? MusicIntensity.Tense : MusicIntensity.Calm;
    }
}

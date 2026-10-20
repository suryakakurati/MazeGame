namespace MazeGame.Core.Game;

/// <summary>
/// The outcome of a game in progress.
/// </summary>
/// <remarks>
/// Exactly one status is true at a time, and once a game leaves
/// <see cref="InProgress"/> it stays there: commands and ticks become no-ops. That makes
/// the end of a game a single, testable transition instead of a set of flags that can
/// contradict each other.
/// </remarks>
public enum GameStatus
{
    /// <summary>The game is still running.</summary>
    InProgress,

    /// <summary>
    /// The player collected every key and reached the exit. The exit cannot be won without
    /// the keys, so this status alone means the objective was completed.
    /// </summary>
    Won,

    /// <summary>
    /// The killer took the player's last heart. Earlier catches only cost a heart and reset the
    /// chase, so this status always means the run is over.
    /// </summary>
    Caught,
}

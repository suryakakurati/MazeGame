namespace MazeGame.Core.Game;

/// <summary>
/// The outcome of a whole run, as opposed to one floor.
/// </summary>
/// <remarks>
/// <see cref="GameStatus.Won"/> means "this floor's exit was reached". In a campaign that is a
/// step forward, not the end, so the run's result lives here instead of overloading one enum.
/// A run is <see cref="Lost"/> the moment a floor loses its last heart, and <see cref="Won"/>
/// only when the last floor's exit is reached.
/// </remarks>
public enum CampaignStatus
{
    /// <summary>The run is still going: the current floor has not been lost or finished.</summary>
    InProgress,

    /// <summary>The player cleared the final floor.</summary>
    Won,

    /// <summary>A floor spent the last heart; the run is over.</summary>
    Lost,
}

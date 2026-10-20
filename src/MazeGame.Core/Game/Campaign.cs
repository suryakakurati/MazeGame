using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// One run of the game: a fixed list of floors, the floor being played, and the hearts that
/// carry from one floor to the next.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameState"/> stays exactly what it was — one floor — so every existing rule test
/// keeps meaning what it meant. The campaign is the layer above it: it builds the next floor
/// with the hearts the player has left, and it owns the answer to "is the run over?".
/// </para>
/// <para>
/// A cleared floor is <see cref="GameStatus.Won"/>; in a campaign that is <b>progress, not
/// victory</b>. <see cref="Status"/> maps it to <see cref="CampaignStatus.InProgress"/> until
/// <see cref="TryAdvanceFloor"/> walks past the last floor, and maps a floor's
/// <see cref="GameStatus.Caught"/> to <see cref="CampaignStatus.Lost"/> immediately.
/// </para>
/// </remarks>
public sealed class Campaign
{
    private readonly IReadOnlyList<FloorDefinition> _floors;
    private readonly Func<FloorDefinition, int, GameState> _createFloor;

    private int _floorIndex;
    private GameState _floor;

    /// <summary>
    /// Starts a new run on the first floor of <paramref name="floors"/>.
    /// </summary>
    /// <param name="floors">The run's floors, in order. The list is snapshotted.</param>
    /// <param name="createFloor">
    /// Builds one playable floor from a definition and the hearts the player enters it with.
    /// The production implementation is <see cref="GameFactory.CreateFloor"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="floors"/> or <paramref name="createFloor"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the list is empty, when a floor is invalid, or when the floor numbers are not
    /// strictly increasing.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the floor builder returns null.
    /// </exception>
    public Campaign(IReadOnlyList<FloorDefinition> floors, Func<FloorDefinition, int, GameState> createFloor)
    {
        ArgumentNullException.ThrowIfNull(floors);
        ArgumentNullException.ThrowIfNull(createFloor);

        if (floors.Count == 0)
        {
            throw new ArgumentException("A campaign needs at least one floor.", nameof(floors));
        }

        var snapshot = new FloorDefinition[floors.Count];
        for (var index = 0; index < floors.Count; index++)
        {
            var floor = floors[index]
                ?? throw new ArgumentException("The floor list contains null.", nameof(floors));

            floor.Validate();

            if (index > 0 && floor.Number <= snapshot[index - 1].Number)
            {
                throw new ArgumentException(
                    $"Floor numbers must increase: {snapshot[index - 1].Number} is followed by {floor.Number}.",
                    nameof(floors));
            }

            snapshot[index] = floor;
        }

        _floors = new ReadOnlyCollection<FloorDefinition>(snapshot);
        _createFloor = createFloor;
        _floor = Build(snapshot[0], snapshot[0].MaxHearts);
    }

    /// <summary>Gets the run's floors, in order.</summary>
    public IReadOnlyList<FloorDefinition> Floors => _floors;

    /// <summary>Gets the definition of the floor currently being played.</summary>
    public FloorDefinition CurrentFloor => _floors[_floorIndex];

    /// <summary>Gets the floor currently being played, with all of its rules.</summary>
    public GameState Floor => _floor;

    /// <summary>Gets the one-based number of the current floor.</summary>
    public int FloorNumber => CurrentFloor.Number;

    /// <summary>Gets how many floors the run has.</summary>
    public int FloorCount => _floors.Count;

    /// <summary>Gets the hearts the player has left on the current floor.</summary>
    public int Hearts => _floor.Hearts;

    /// <summary>Gets the most hearts the current floor allows.</summary>
    public int MaxHearts => _floor.MaxHearts;

    /// <summary>Gets how many floors have been cleared so far.</summary>
    public int FloorsCleared => _floor.Status == GameStatus.Won ? _floorIndex + 1 : _floorIndex;

    /// <summary>Gets a value indicating whether the current floor is the last of the run.</summary>
    public bool IsOnLastFloor => _floorIndex == _floors.Count - 1;

    /// <summary>
    /// Gets a value indicating whether the current floor's exit has been reached. The run may
    /// still continue; call <see cref="TryAdvanceFloor"/> to move on.
    /// </summary>
    public bool IsFloorCleared => _floor.Status == GameStatus.Won;

    /// <summary>Gets the outcome of the whole run.</summary>
    /// <remarks>
    /// A lost floor ends the run at once. A cleared floor only ends it once the player has also
    /// left the last floor, which is what <see cref="TryAdvanceFloor"/> does.
    /// </remarks>
    public CampaignStatus Status
    {
        get
        {
            if (_floor.Status == GameStatus.Caught)
            {
                return CampaignStatus.Lost;
            }

            return IsOnLastFloor && _floor.Status == GameStatus.Won
                ? CampaignStatus.Won
                : CampaignStatus.InProgress;
        }
    }

    /// <summary>
    /// Moves from the cleared floor to the next one, carrying the hearts the player has left.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a new floor was built; <see langword="false"/> when the current
    /// floor has not been cleared, or when it was the last floor (the run is won).
    /// </returns>
    /// <remarks>
    /// The new floor starts with exactly the hearts left on the old one, out of its own ceiling,
    /// so damage is remembered across the run without a second health model. Keys, barriers and
    /// the layout are per floor and are naturally reset by building a new <see cref="GameState"/>.
    /// </remarks>
    public bool TryAdvanceFloor()
    {
        if (!IsFloorCleared || IsOnLastFloor)
        {
            return false;
        }

        var hearts = _floor.Hearts;
        var next = _floors[_floorIndex + 1];

        _floorIndex++;
        _floor = Build(next, Math.Min(hearts, next.MaxHearts));
        return true;
    }

    /// <summary>
    /// Builds one floor and rejects a builder that returns nothing.
    /// </summary>
    /// <param name="floor">The floor to build.</param>
    /// <param name="hearts">Hearts the player brings to it.</param>
    private GameState Build(FloorDefinition floor, int hearts) =>
        _createFloor(floor, hearts)
        ?? throw new InvalidOperationException("The floor builder returned null.");
}

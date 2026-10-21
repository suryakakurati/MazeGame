using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// One run of the game: a fixed list of floors, the floor being played, the cards held, and the
/// hearts that carry from one floor to the next.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameState"/> stays exactly what it was — one floor — so every existing rule test
/// keeps meaning what it meant. The campaign is the layer above it: it builds the next floor with
/// the hearts the player has left and the cards they hold, and it owns the answer to "is the run
/// over?".
/// </para>
/// <para>
/// A cleared floor is <see cref="GameStatus.Won"/>; in a campaign that is <b>progress, not
/// victory</b>. Clearing a floor that is not the last one opens a <see cref="Draft"/>, and the run
/// will not advance until that draft is resolved — taking a card or skipping for a heart. That
/// gate is deliberate: a frontend cannot accidentally let a player skip the choice, and a replay
/// reproduces the same offers because they are derived from the run seed.
/// </para>
/// </remarks>
public sealed class Campaign
{
    private readonly IReadOnlyList<FloorDefinition> _floors;
    private readonly Func<FloorDefinition, int, RunModifiers, GameState> _createFloor;
    private readonly int _runSeed;

    private int _floorIndex;
    private GameState _floor;
    private Draft? _draft;
    private RunModifiers _modifiers = RunModifiers.None;

    /// <summary>
    /// Hearts waiting to be applied when the next floor is built: the skip-to-heal, a card's
    /// one-shot heal, and the per-clear heal all land here rather than on a cleared floor.
    /// </summary>
    private int _carryHearts;

    private int _nextDraftBonus;
    private int _followingDraftBonus;

    /// <summary>
    /// Starts a new run on the first floor of <paramref name="floors"/>.
    /// </summary>
    /// <param name="floors">The run's floors, in order. The list is snapshotted.</param>
    /// <param name="createFloor">
    /// Builds one playable floor from a definition, the hearts the player enters it with, and the
    /// cards held. The production implementation is <see cref="GameFactory.CreateFloor"/>.
    /// </param>
    /// <param name="runSeed">
    /// The run's seed, used to derive the draft offers, or <see langword="null"/> for a fresh
    /// stream of offers on every run. A seeded campaign therefore reproduces its drafts as well as
    /// its mazes.
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
    public Campaign(
        IReadOnlyList<FloorDefinition> floors,
        Func<FloorDefinition, int, RunModifiers, GameState> createFloor,
        int? runSeed = null)
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
        _runSeed = runSeed ?? Random.Shared.Next();
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

    /// <summary>Gets the cards taken this run and the totals they add up to.</summary>
    public RunModifiers Modifiers => _modifiers;

    /// <summary>Gets the hearts already banked for the next floor, including a pending skip-to-heal.</summary>
    /// <remarks>
    /// The skip-to-heal and a card's one-shot heal cannot be applied to a cleared floor, so they
    /// wait here until the next floor is built. <see cref="TryAdvanceFloor"/> also adds the
    /// per-clear heal of a card such as <c>trophy-hunter</c>, which is a property of holding the
    /// card rather than a banked event; <see cref="HeartsOnNextFloor"/> is the total the next
    /// floor will actually start with.
    /// </remarks>
    public int PendingHeals => _carryHearts;

    /// <summary>
    /// Gets the hearts the next floor will start with, capped by that floor's ceiling (raised by
    /// the run's cards), or the current hearts when this is the last floor.
    /// </summary>
    /// <remarks>
    /// The cap has to include the run's heart bonus. A ceiling taken from the floor definition
    /// alone would drop the extra slot a card such as <c>blood-pact</c> paid for, and every heal
    /// banked above the floor's own ceiling would vanish on the way in.
    /// </remarks>
    public int HeartsOnNextFloor
    {
        get
        {
            if (IsOnLastFloor)
            {
                return _floor.Hearts;
            }

            var next = _floors[_floorIndex + 1];
            var ceiling = Math.Max(1, next.MaxHearts + _modifiers.MaxHearts);
            return Math.Min(_floor.Hearts + _carryHearts + _modifiers.HealOnFloorClear, ceiling);
        }
    }

    /// <summary>
    /// Gets the draft for the next floor, or <see langword="null"/> while the floor is still being
    /// played or when the run is on its last floor.
    /// </summary>
    /// <remarks>
    /// Reading this after the floor is cleared is what opens the draft: the offers are generated
    /// once, seeded from the run and the floor, and then cached until the draft is resolved.
    /// </remarks>
    public Draft? Draft
    {
        get
        {
            if (!IsFloorCleared || IsOnLastFloor)
            {
                return null;
            }

            return _draft ??= CreateDraft();
        }
    }

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
    /// Takes <paramref name="card"/> from the open draft and folds it into the run's modifiers.
    /// </summary>
    /// <param name="card">The card to take; it must be one of the draft's offers.</param>
    /// <returns>
    /// <see langword="true"/> when the card was taken; <see langword="false"/> when there is no
    /// open draft, the draft is already resolved, or the card is not on the table.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is null.</exception>
    /// <remarks>
    /// A card's one-shot effects land here: a heal is banked for the next floor, and a change to
    /// the size of a coming draft is queued rather than applied to the draft already on screen.
    /// </remarks>
    public bool TakeCard(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var draft = Draft;
        if (draft is null || !draft.TryTake(card))
        {
            return false;
        }

        _modifiers = _modifiers.With(card);
        _carryHearts += card.Effect.HealNow;

        // The bonus applies to the next draft and the penalty to the one after it, so the queue
        // is read one draft late. That is what makes the extra choice cost something.
        _nextDraftBonus += card.Effect.NextDraftOfferBonus;
        _followingDraftBonus += card.Effect.FollowingDraftOfferBonus;

        return true;
    }

    /// <summary>
    /// Skips the open draft, healing one heart instead.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the draft was skipped; <see langword="false"/> when there is no
    /// open draft or it is already resolved.
    /// </returns>
    /// <remarks>
    /// One heart, not a full bar: skipping is a real choice for a wounded run, not a reset button.
    /// </remarks>
    public bool SkipDraft()
    {
        var draft = Draft;
        if (draft is null || !draft.TrySkip())
        {
            return false;
        }

        _carryHearts++;
        return true;
    }

    /// <summary>
    /// Moves from the cleared floor to the next one, carrying the hearts and cards of the run.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a new floor was built; <see langword="false"/> when the current
    /// floor has not been cleared, when it was the last floor, or when its draft is still open.
    /// </returns>
    /// <remarks>
    /// The new floor starts with the hearts left on the old one plus any pending heals, capped by
    /// its own ceiling, so damage is remembered across the run without a second health model. Keys,
    /// barriers and the layout are per floor and are naturally reset by building a new
    /// <see cref="GameState"/>; the cards are not, because they live in
    /// <see cref="RunModifiers"/> and are applied to every floor built from now on.
    /// </remarks>
    public bool TryAdvanceFloor()
    {
        if (!IsFloorCleared || IsOnLastFloor || Draft is { IsResolved: false })
        {
            return false;
        }

        var next = _floors[_floorIndex + 1];
        var hearts = HeartsOnNextFloor;

        _floorIndex++;
        _draft = null;
        _carryHearts = 0;
        _floor = Build(next, hearts);
        return true;
    }

    /// <summary>
    /// Builds the draft for the floor that was just cleared.
    /// </summary>
    /// <returns>The open draft.</returns>
    /// <remarks>
    /// This is where the queued size adjustments are read: the draft being built consumes the
    /// pending bonus and promotes the following one. The per-clear heal is not banked here — it
    /// belongs to <see cref="TryAdvanceFloor"/>, so that looking at a draft cannot change the
    /// run's health.
    /// </remarks>
    private Draft CreateDraft()
    {
        var count = Math.Clamp(
            DraftFactory.DefaultOfferCount + _nextDraftBonus,
            DraftFactory.MinOfferCount,
            DraftFactory.MaxOfferCount);

        _nextDraftBonus = _followingDraftBonus;
        _followingDraftBonus = 0;

        // A run has one draft after each floor but the last, so the drafts still to come are the
        // floors after the one this draft leads into. A card paid for in a later draft is withheld
        // when those do not exist — the shipped three-floor run has no second draft after its
        // first, which is why lucky-find cannot appear there.
        var draftsRemaining = _floors.Count - _floorIndex - 2;

        var offers = DraftFactory.Offers(
            _runSeed,
            FloorNumber,
            _modifiers.Cards,
            count,
            draftsRemaining);

        return new Draft(offers);
    }

    /// <summary>
    /// Builds one floor with the run's cards and rejects a builder that returns nothing.
    /// </summary>
    /// <param name="floor">The floor to build.</param>
    /// <param name="hearts">Hearts the player brings to it.</param>
    private GameState Build(FloorDefinition floor, int hearts) =>
        _createFloor(floor, hearts, _modifiers)
        ?? throw new InvalidOperationException("The floor builder returned null.");
}

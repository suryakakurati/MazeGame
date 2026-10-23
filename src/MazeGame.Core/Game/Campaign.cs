using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
    /// <summary>How many floors the Turn takes the player back.</summary>
    private const int TurnFloors = 5;

    /// <summary>How many floors a planted seed waits before it blooms.</summary>
    private const int SeedBloomFloors = 3;

    /// <summary>How long the Cartographer's map stays revealed: the rest of the floor.</summary>
    /// <remarks>
    /// The card used to say "this floor and the next two rooms" and delivered thirty seconds and
    /// nothing about the next two rooms. A promise no system keeps is an unfinished feature, so
    /// the map now simply holds until the floor is left, which is what the card says it does.
    /// </remarks>
    private const double CartographerRevealSeconds = 3600.0;

    /// <summary>Mixed into the stall's offers, so a shop is not a second draft of the same cards.</summary>
    private const int ShopSalt = 0x5107;

    /// <summary>Mixed into the mirror's replacement, so it is not the shop's first card.</summary>
    private const int MirrorSalt = 0x33A1;

    /// <summary>
    /// Mixed into a rerolled draft, so a reroll is a new draw rather than the same three cards
    /// with one swapped.
    /// </summary>
    private const int RerollSalt = 0x72A5;

    /// <summary>How many hearts the first reroll of a draft costs.</summary>
    private const int RerollBaseCost = 1;

    private readonly IReadOnlyList<FloorDefinition> _floors;
    private readonly Func<FloorDefinition, int, RunModifiers, IReadOnlyList<DescentCondition>, GameState> _createFloor;
    private readonly int _runSeed;
    private readonly List<DescentCondition> _conditions = [];
    private readonly HashSet<int> _chosenGates = [];

    private int _floorIndex;
    private GameState _floor;
    private Draft? _draft;
    private ConditionOffer? _conditionOffer;
    private RunModifiers _modifiers = RunModifiers.None;

    /// <summary>
    /// Hearts waiting to be applied when the next floor is built: the skip-to-heal, a card's
    /// one-shot heal, and the per-clear heal all land here rather than on a cleared floor.
    /// </summary>
    private int _carryHearts;

    // Blooms picked on floors the run has already left, so the garden keeps growing after the
    // floor that grew it has been rebuilt or left behind.
    private int _bloomsBanked;

    private int _nextDraftBonus;
    private int _followingDraftBonus;

    // How many rerolls this floor's draft has already had. It resets with the draft, so the price
    // starts at one heart again on the next floor.
    private int _draftRerolls;

    // The safe room: the card on screen, whether this floor's room has been used, and the cards a
    // shop is selling (fixed at the moment the card is opened, so a purchase cannot reroll them).
    private RoomOffer? _room;
    private bool _roomSpent;
    private IReadOnlyList<Card> _shopCards = Array.Empty<Card>();

    // Promises a room made about the floors ahead.
    private int _huntersOffNextFloor;
    private int? _seedBloomsOnFloor;

    // The one floor a House Favour promised a casino on, and whether its favoured table has
    // already been played. The promise outlives the floor's own room state: the room is drawn
    // when it is opened, not when the floor is built.
    private int? _casinoFloor;
    private bool _casinoGambleUsed;

    // The Turn is once per run, and the draft's rarity follows the deepest floor ever reached so a
    // rewind cannot hand out late-run rares on an early floor.
    private bool _reincarnated;
    private int _deepestFloor;

    /// <summary>
    /// Starts a new run on the first floor of <paramref name="floors"/>.
    /// </summary>
    /// <param name="floors">The run's floors, in order. The list is snapshotted.</param>
    /// <param name="createFloor">
    /// Builds one playable floor from a definition, the hearts the player enters it with, and the
    /// cards held. The production implementation is <c>GameFactory.CreateFloor</c>.
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
        Func<FloorDefinition, int, RunModifiers, IReadOnlyList<DescentCondition>, GameState> createFloor,
        int? runSeed = null)
        : this(floors, createFloor, runSeed, null)
    {
    }

    /// <summary>
    /// Builds a campaign, either at its first floor or from a saved run.
    /// </summary>
    /// <param name="floors">The run's floors, in order. The list is snapshotted.</param>
    /// <param name="createFloor">Builds one playable floor.</param>
    /// <param name="runSeed">The seed to use, or null for a fresh one.</param>
    /// <param name="resume">A saved run to restore, or null to start at floor one.</param>
    private Campaign(
        IReadOnlyList<FloorDefinition> floors,
        Func<FloorDefinition, int, RunModifiers, IReadOnlyList<DescentCondition>, GameState> createFloor,
        int? runSeed,
        RunSnapshot? resume)
    {
        ArgumentNullException.ThrowIfNull(floors);
        ArgumentNullException.ThrowIfNull(createFloor);

        if (floors.Count == 0)
        {
            throw new ArgumentException("A campaign needs at least one floor.", nameof(floors));
        }

        // The seed comes first because the run's floors wear faces drawn from it. A resume keeps
        // the saved seed, so reloading a run rebuilds the same faces on the same floors.
        _runSeed = resume?.RunSeed ?? runSeed ?? Random.Shared.Next();

        var snapshot = new FloorDefinition[floors.Count];
        for (var index = 0; index < floors.Count; index++)
        {
            var floor = floors[index]
                ?? throw new ArgumentException("The floor list contains null.", nameof(floors));

            floor.Validate();

            // Every floor of every run is bent by the face its seed rolls for it. The authored
            // floor is the common case, but a run is never guaranteed to meet it everywhere.
            var drawn = FloorFaces.Apply(floor, FloorFaces.Drawn(floor, _runSeed));
            drawn.Validate();

            if (index > 0 && drawn.Number <= snapshot[index - 1].Number)
            {
                throw new ArgumentException(
                    $"Floor numbers must increase: {snapshot[index - 1].Number} is followed by {drawn.Number}.",
                    nameof(floors));
            }

            snapshot[index] = drawn;
        }

        _floors = new ReadOnlyCollection<FloorDefinition>(snapshot);
        _createFloor = createFloor;

        if (resume is null)
        {
            _deepestFloor = snapshot[0].Number;
            _floor = Build(snapshot[0], snapshot[0].MaxHearts);
            return;
        }

        // A restore rebuilds the floor from the run's seed rather than carrying a floor's state:
        // the seed is what makes every wall, key and hunter land where they did.
        resume.Validate(snapshot.Length);
        _floorIndex = resume.FloorIndex;
        _deepestFloor = Math.Max(resume.DeepestFloor, snapshot[resume.FloorIndex].Number);
        _reincarnated = resume.Reincarnated;
        _bloomsBanked = resume.BloomsBanked;
        _carryHearts = resume.CarryHearts;
        _draftRerolls = resume.DraftRerolls;
        _nextDraftBonus = resume.NextDraftBonus;
        _followingDraftBonus = resume.FollowingDraftBonus;
        _huntersOffNextFloor = resume.HuntersOffNextFloor;
        _seedBloomsOnFloor = resume.SeedBloomsOnFloor;
        _casinoFloor = resume.CasinoFloor;
        _casinoGambleUsed = resume.CasinoGambleUsed;

        foreach (var condition in resume.Conditions)
        {
            if (!_conditions.Contains(condition))
            {
                _conditions.Add(condition);
            }
        }

        foreach (var gate in resume.ChosenGates)
        {
            _chosenGates.Add(gate);
        }

        var cards = new Card[resume.CardIds.Count];

        for (var index = 0; index < cards.Length; index++)
        {
            // A relic is stored as a card, so the save's list can name either. Both catalogs are
            // asked before a save is called corrupt.
            cards[index] = CardCatalog.ById(resume.CardIds[index])
                ?? RelicCatalog.ById(resume.CardIds[index])
                ?? throw new ArgumentException($"The save names an unknown card: '{resume.CardIds[index]}'.", nameof(resume));
        }

        _modifiers = RunModifiers.FromCards(cards);

        // The pending promises belong to the floor after this one: rebuilding the saved floor must
        // not spend a Toll or a Seed Vault heal the player has not reached yet.
        _floor = Build(snapshot[_floorIndex], resume.Hearts, consumePending: false);
    }

    /// <summary>
    /// Writes the run in a form the UI can store and <see cref="Restore"/> can rebuild.
    /// </summary>
    /// <returns>The run's progress, with this floor's blooms already banked.</returns>
    /// <remarks>
    /// The snapshot is taken from a floor that still has a heart: saving a run after the last
    /// catch would save a game that cannot be played.
    /// </remarks>
    public RunSnapshot Snapshot() => new()
    {
        RunSeed = _runSeed,
        FloorIndex = _floorIndex,
        Hearts = _floor.Hearts,
        CarryHearts = _carryHearts,
        BloomsBanked = _bloomsBanked + _floor.KeysCollected,
        DraftRerolls = _draftRerolls,
        DeepestFloor = _deepestFloor,
        Reincarnated = _reincarnated,
        NextDraftBonus = _nextDraftBonus,
        FollowingDraftBonus = _followingDraftBonus,
        HuntersOffNextFloor = _huntersOffNextFloor,
        SeedBloomsOnFloor = _seedBloomsOnFloor,
        CasinoFloor = _casinoFloor,
        CasinoGambleUsed = _casinoGambleUsed,
        CardIds = new List<string>(_modifiers.Cards.Select(card => card.Id)),
        Conditions = new List<DescentCondition>(_conditions),
        ChosenGates = new List<int>(_chosenGates.OrderBy(number => number)),
    };

    /// <summary>
    /// Folds a relic the player has taken into the run's cards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor records that its altar was touched; only the campaign can make the relic last
    /// past it. Frontends call this once a frame, immediately after the floor ticks, so the new
    /// effect is live before the next step — and a save taken after the altar already holds it.
    /// </para>
    /// <para>
    /// The run never holds two of the same relic, so a second call for the same altar does
    /// nothing. That also makes the call idempotent for a frontend that ticks twice in a frame.
    /// </para>
    /// </remarks>
    public void Sync()
    {
        if (_floor.Relic is not { } kind || !_floor.IsRelicTaken)
        {
            return;
        }

        var relic = RelicCatalog.CardFor(kind);

        if (_modifiers.Cards.All(card => !string.Equals(card.Id, relic.Id, StringComparison.Ordinal)))
        {
            AddCard(relic);
        }
    }

    /// <summary>
    /// Rebuilds a run from a saved snapshot, starting again at the beginning of its floor.
    /// </summary>
    /// <param name="floors">The run's floors, in order.</param>
    /// <param name="createFloor">Builds one playable floor.</param>
    /// <param name="snapshot">The saved run.</param>
    /// <returns>A campaign whose current floor is the saved one, already playable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the snapshot is from another build, names a floor the catalog does not hold, or
    /// has no hearts left.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the snapshot names a card this build does not ship.</exception>
    public static Campaign Restore(
        IReadOnlyList<FloorDefinition> floors,
        Func<FloorDefinition, int, RunModifiers, IReadOnlyList<DescentCondition>, GameState> createFloor,
        RunSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(floors);
        ArgumentNullException.ThrowIfNull(createFloor);
        ArgumentNullException.ThrowIfNull(snapshot);

        return new Campaign(floors, createFloor, snapshot.RunSeed, snapshot);
    }

    /// <summary>Gets the run's floors, in order.</summary>
    public IReadOnlyList<FloorDefinition> Floors => _floors;

    /// <summary>
    /// Gets the seed the whole run was built from.
    /// </summary>
    /// <remarks>
    /// The score is a pure function of the floor's theme and this seed, so two runs from the same
    /// seed sound the same as well as look the same.
    /// </remarks>
    public int RunSeed => _runSeed;

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

    /// <summary>
    /// Gets how many blooms the run has picked, including the ones on the floor being played.
    /// </summary>
    /// <remarks>
    /// The number the garden strip draws. It is banked when a floor is left rather than when it is
    /// cleared, so a rewind keeps the blooms it took and a rebuilt floor starts its own count at
    /// zero.
    /// </remarks>
    public int BloomsPicked => _bloomsBanked + _floor.KeysCollected + _floor.BonusBlooms;

    /// <summary>
    /// Gets how many blooms the garden is worth: one for every bloom picked, plus one for every
    /// page turned.
    /// </summary>
    /// <remarks>
    /// This is the visible answer to "am I working toward something?". It grows on every cleared
    /// card and resets with the run, by design: there is no unlock and no codex behind it.
    /// </remarks>
    public int GardenCount => BloomsPicked + FloorsCleared;

    /// <summary>
    /// Gets the page of the gardener's book for the current floor.
    /// </summary>
    public StoryPage CurrentPage => StoryCatalog.Page(FloorNumber);

    /// <summary>
    /// Gets how the run looked when the current floor's page was turned, which decides whether the
    /// gardener adds a line under it.
    /// </summary>
    /// <remarks>
    /// The last heart outranks a condition: a run about to end reads differently from a run that
    /// merely took a rule at a gate.
    /// </remarks>
    public StoryMood CurrentMood =>
        _floor.Hearts <= 1 ? StoryMood.LastHeart :
        _conditions.Count > 0 ? StoryMood.Conditioned :
        StoryMood.None;

    /// <summary>
    /// Gets the extra line the gardener adds to the current page, or <see langword="null"/> when
    /// there is nothing to say.
    /// </summary>
    public string? CurrentMoodLine => StoryCatalog.MoodLine(CurrentMood);

    /// <summary>Gets a value indicating whether the current floor is the last of the run.</summary>
    public bool IsOnLastFloor => _floorIndex == _floors.Count - 1;

    /// <summary>
    /// Gets a value indicating whether the current floor's exit has been reached. The run may
    /// still continue; call <see cref="TryAdvanceFloor"/> to move on.
    /// </summary>
    public bool IsFloorCleared => _floor.Status == GameStatus.Won;

    /// <summary>Gets the cards taken this run and the totals they add up to.</summary>
    public RunModifiers Modifiers => _modifiers;

    /// <summary>Gets the descent conditions chosen this run, in the order they were taken.</summary>
    public IReadOnlyList<DescentCondition> ActiveConditions => _conditions;

    /// <summary>
    /// Gets the condition choice open at a gate, or <see langword="null"/> when the run is not at
    /// one.
    /// </summary>
    /// <remarks>
    /// Reading this after a floor is cleared is what opens the offer, exactly as <see cref="Draft"/> is
    /// what opens the card choice. A gate is resolved once; the run then carries its choice for
    /// the rest of the descent.
    /// </remarks>
    public ConditionOffer? ConditionOffer
    {
        get
        {
            if (!IsFloorCleared || IsOnLastFloor)
            {
                return null;
            }

            var next = _floors[_floorIndex + 1].Number;

            if (!IsConditionGate(next) || _chosenGates.Contains(next))
            {
                return null;
            }

            return _conditionOffer ??= new ConditionOffer(
                DescentConditionCatalog.Offer(_runSeed, next, _conditions));
        }
    }

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
    /// Gets the hearts the next floor will start with, or the current hearts when this is the
    /// last floor.
    /// </summary>
    /// <remarks>
    /// There is no ceiling to clip against: the run keeps every heart it has earned, so a player
    /// who banked a heal at full health walks into the next floor holding it.
    /// </remarks>
    public int HeartsOnNextFloor
    {
        get
        {
            if (IsOnLastFloor)
            {
                return _floor.Hearts;
            }

            return _floor.Hearts + _carryHearts + _modifiers.HealOnFloorClear;
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

    /// <summary>
    /// Gets how many hearts the next reroll of the open draft costs.
    /// </summary>
    /// <remarks>
    /// The first reroll costs one heart, the second two, and so on: the price is the reroll's
    /// ordinal, not a fraction of the run's health. It resets with every floor's draft, so a run
    /// that rerolls once per floor never pays more than a heart.
    /// </remarks>
    public int DraftRerollCost => RerollBaseCost + _draftRerolls;

    /// <summary>Gets how many rerolls the open draft has had.</summary>
    public int DraftRerolls => _draftRerolls;

    /// <summary>
    /// Gets a value indicating whether a reroll can be paid for right now.
    /// </summary>
    /// <remarks>
    /// The last heart is never a price: a reroll cannot turn a choice between floors into the end
    /// of the run.
    /// </remarks>
    public bool CanRerollDraft =>
        Draft is { IsResolved: false, Offers.Count: > 0 } &&
        _floor.Hearts - DraftRerollCost >= 1;

    /// <summary>
    /// Spends hearts to replace the open draft's offers with a fresh seeded draw.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the table changed; <see langword="false"/> when there is no
    /// open draft, it is resolved, or the run cannot pay without spending its last heart.
    /// </returns>
    /// <remarks>
    /// The draw is seeded from the run, the floor and the reroll number, so a replay reproduces
    /// the same sequence of tables. Nothing about the run changes except the offers and the hearts
    /// that bought them.
    /// </remarks>
    public bool TryRerollDraft()
    {
        var draft = Draft;

        if (draft is null || draft.IsResolved || draft.Offers.Count == 0)
        {
            return false;
        }

        var cost = DraftRerollCost;

        if (!_floor.TrySpendHearts(cost))
        {
            return false;
        }

        _draftRerolls++;

        var draftsRemaining = _floors.Count - _floorIndex - 2;
        var offers = DraftFactory.Offers(
            _runSeed,
            FloorNumber,
            _modifiers.Cards,
            draft.Offers.Count,
            draftsRemaining,
            effectiveFloor: _deepestFloor,
            salt: unchecked(RerollSalt ^ (_draftRerolls * 7919)));

        return draft.TryReplace(offers);
    }

    /// <summary>
    /// Gets a value indicating whether the player is standing in this floor's safe room.
    /// </summary>
    /// <remarks>
    /// Entering is enough: no key opens the room, because a safe room behind a keypress is a safe
    /// room half the players never find. The world is already paused while this is true.
    /// </remarks>
    public bool IsInSafeRoom => _floor.IsInSafeRoom;

    /// <summary>
    /// Gets a value indicating whether the room's interaction can be opened right now.
    /// </summary>
    /// <remarks>
    /// Being inside is not enough on its own: a spent room is a bare counter, and the player has to
    /// be able to hear the floor's rules before a card covers them.
    /// </remarks>
    public bool CanOpenRoom =>
        _floor.SafeRoom is not null &&
        _floor.IsInSafeRoom &&
        !_roomSpent &&
        _floor.Status == GameStatus.InProgress;

    /// <summary>Gets the room card on screen, or <see langword="null"/> when none is open.</summary>
    public RoomOffer? Room => _room;

    /// <summary>
    /// Gets the percentage chance this floor's casino table pays on a stake.
    /// </summary>
    /// <remarks>
    /// The house's usual 45% applies everywhere except the single floor a House Favour promised,
    /// where the first gamble pays 66%. The card is spent by making that bet, win or lose, so the
    /// second visit reads the ordinary price again.
    /// </remarks>
    public int CasinoWinPercent =>
        _casinoFloor == FloorNumber && !_casinoGambleUsed
            ? RoomCatalog.FavouredGambleWinPercent
            : RoomCatalog.GambleWinPercent;

    /// <summary>
    /// Opens the safe room's card.
    /// </summary>
    /// <returns><see langword="true"/> when the card is now open; false when the player is not in a
    /// room, the room is spent, or a card is already open.</returns>
    /// <remarks>
    /// The room's kind is drawn from the run's seed and this floor, so a replay visits the same
    /// rooms in the same order. The card is built once and cached, so the offers cannot change
    /// under the player while it is on screen.
    /// </remarks>
    public bool OpenRoom()
    {
        if (CanOpenRoom is false)
        {
            return false;
        }

        // A House Favour replaces the floor's draw rather than adding a second room: the floor it
        // promised holds the Long Table and nothing else.
        var kind = _casinoFloor == FloorNumber
            ? RoomKind.LongTable
            : RoomCatalog.Pick(_runSeed, FloorNumber);

        _room ??= CreateRoom(kind);
        return true;
    }

    /// <summary>Closes the card without spending the room.</summary>
    /// <remarks>
    /// Looking is free: a player who wants to read the odds and walk away can come back to the same
    /// card, which is why only a resolved choice marks a room spent.
    /// </remarks>
    public void CloseRoom() => _room = null;

    /// <summary>
    /// Resolves the open room card with <paramref name="option"/>.
    /// </summary>
    /// <param name="option">The option to take; it must be one of the card's.</param>
    /// <returns>
    /// <see langword="true"/> when the option was applied; false when no card is open, the card is
    /// spent, or the option is not offered.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="option"/> is null.</exception>
    public bool ResolveRoom(RoomOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        var offer = _room;

        if (offer is null || offer.IsResolved)
        {
            return false;
        }

        // Leaving is not a resolution: the counter stays as it was and the player can return.
        if (string.Equals(option.Id, RoomOption.Leave.Id, StringComparison.Ordinal))
        {
            _room = null;
            return true;
        }

        if (!offer.TryResolve(option))
        {
            return false;
        }

        // A Turn rebuilds the run, which resets the room state: it must not then be marked spent
        // on the floor the player just arrived on.
        var rebuilt = false;

        switch (offer.Kind)
        {
            case RoomKind.Cartographer:
                _floor.RevealMapFor(CartographerRevealSeconds);
                offer.RecordOutcome("THE MAP IS YOURS");
                break;

            case RoomKind.LongTable:
                ResolveStake(offer);
                break;

            case RoomKind.Stall:
                ResolveBuy(offer);
                break;

            case RoomKind.Confessional:
                ResolveGive(offer);
                break;

            case RoomKind.Toll:
                if (!_floor.TrySpendHeart())
                {
                    offer.RecordOutcome("NOT ENOUGH HEARTS");
                    break;
                }

                _huntersOffNextFloor = 1;
                offer.RecordOutcome("THE NEXT FLOOR KEEPS ONE AT HOME");
                break;

            case RoomKind.SeedVault:
                _seedBloomsOnFloor = FloorNumber + SeedBloomFloors;
                offer.RecordOutcome($"IT BLOOMS ON FLOOR {_seedBloomsOnFloor}");
                break;

            case RoomKind.Mirror:
                ResolveSwap(offer);
                break;

            case RoomKind.Turn:
                rebuilt = ResolveTurn(offer);
                break;
        }

        if (!rebuilt)
        {
            _roomSpent = true;
        }

        return true;
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

        // The bonus applies to the next draft and the penalty to the one after it, so the queue
        // is read one draft late. That is what makes the extra choice cost something.
        AddCard(card);

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
    /// Takes <paramref name="condition"/> from the open gate and makes it part of the run.
    /// </summary>
    /// <param name="condition">The condition to take; it must be on the table.</param>
    /// <returns>
    /// <see langword="true"/> when it was taken; <see langword="false"/> when no gate is open or
    /// the condition is not one of the offers.
    /// </returns>
    /// <remarks>
    /// There is no skip. The gate exists to make the late run different, so the player has to
    /// choose one of the rules on the table.
    /// </remarks>
    public bool TakeCondition(DescentCondition condition)
    {
        var offer = ConditionOffer;

        if (offer is null || !offer.TryTake(condition))
        {
            return false;
        }

        _conditions.Add(condition);
        _chosenGates.Add(_floors[_floorIndex + 1].Number);
        return true;
    }

    /// <summary>
    /// Moves from the cleared floor to the next one, carrying the hearts, cards and conditions of
    /// the run.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when a new floor was built; <see langword="false"/> when the current
    /// floor has not been cleared, when it was the last floor, or when its draft is still open.
    /// </returns>
    /// <remarks>
    /// The new floor starts with the hearts left on the old one plus any pending heals, with no
    /// ceiling, so damage is remembered across the run without a second health model. Keys,
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

        // A gate is not a suggestion: the run cannot step onto floor 8 or 14 until its rule has
        // been chosen. Checking the chosen set rather than the live offer also stops a frontend
        // that never read the offer from skipping it.
        if (IsConditionGate(next.Number) && !_chosenGates.Contains(next.Number))
        {
            return false;
        }

        var hearts = HeartsOnNextFloor;

        // The garden keeps what this run picked: a floor's blooms are banked before the next floor
        // starts its own count at zero.
        _bloomsBanked += _floor.KeysCollected;

        _floorIndex++;
        _deepestFloor = Math.Max(_deepestFloor, next.Number);

        // The reroll ladder belongs to the draft it was paid against, so it dies with it: the
        // next draft starts at one heart again.
        _draft = null;
        _draftRerolls = 0;
        _conditionOffer = null;
        _carryHearts = 0;
        _room = null;
        _roomSpent = false;
        _floor = Build(next, hearts);
        return true;
    }

    /// <summary>
    /// Turns the descent back: to floor one, or five floors up.
    /// </summary>
    /// <param name="toStart">
    /// When true, go back to the first floor for one heart; otherwise rewind five floors for free.
    /// </param>
    /// <returns><see langword="true"/> when the run was rebuilt on the target floor.</returns>
    /// <remarks>
    /// <para>
    /// Cards, conditions and hearts are kept: a rewind undoes position and difficulty, not damage.
    /// The floors are rebuilt from their definitions, so a replayed floor's hunters move at that
    /// floor's own cadence times the cards, never at the speed of the floor the player left.
    /// </para>
    /// <para>
    /// Once per run, and there has to be somewhere to go: a floor-five Turn has no five floors
    /// behind it, so the card is not offered there.
    /// </para>
    /// </remarks>
    public bool TryReincarnate(bool toStart)
    {
        if (_reincarnated || _floor.Status != GameStatus.InProgress)
        {
            return false;
        }

        var target = toStart ? 0 : Math.Max(0, _floorIndex - TurnFloors);

        if (target >= _floorIndex)
        {
            return false;
        }

        if (toStart && !_floor.TrySpendHeart())
        {
            return false;
        }

        var hearts = _floor.Hearts;

        _reincarnated = true;
        _bloomsBanked += _floor.KeysCollected;
        _floorIndex = target;
        _draft = null;
        _draftRerolls = 0;
        _conditionOffer = null;
        _carryHearts = 0;
        _room = null;
        _roomSpent = false;
        _floor = Build(_floors[target], hearts);
        return true;
    }

    /// <summary>Returns whether <paramref name="floorNumber"/> is a condition gate.</summary>
    /// <param name="floorNumber">The floor the run is about to enter.</param>
    /// <returns><see langword="true"/> for the two gates.</returns>
    private static bool IsConditionGate(int floorNumber) => floorNumber is 8 or 14;

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
            draftsRemaining,
            effectiveFloor: _deepestFloor);

        return new Draft(offers);
    }

    /// <summary>Builds the room card for <paramref name="kind"/> from the run's current state.</summary>
    /// <param name="kind">The room this floor holds.</param>
    /// <returns>The card, with only the options the run can actually pay for.</returns>
    /// <remarks>
    /// The options are a snapshot, not a menu: a stake the player cannot cover, a swap with nothing
    /// eligible and a confession with no cards are simply absent. Every price on the card is
    /// therefore a price that can be paid, which is what makes taking the last heart impossible
    /// without a special case in the resolution.
    /// </remarks>
    private RoomOffer CreateRoom(RoomKind kind)
    {
        var options = new List<RoomOption>(4);

        switch (kind)
        {
            case RoomKind.Cartographer:
                options.Add(new RoomOption("reveal", "REVEAL THE MAP", "THE WHOLE FLOOR, UNTIL YOU LEAVE IT"));
                break;

            case RoomKind.LongTable:
                // The stake is bounded by the hearts the run is actually carrying, not by the
                // floor's design, and by the card's own row budget: with no ceiling a rich run
                // would otherwise be offered a list longer than the window.
                var most = Math.Min(_floor.Hearts - 1, RoomCatalog.MaxStake);

                var winPercent = CasinoWinPercent;

                for (var stake = 1; stake <= most; stake++)
                {
                    options.Add(new RoomOption(
                        $"stake-{stake}",
                        $"STAKE {stake}",
                        $"{winPercent}% DOUBLE"));
                }

                break;

            case RoomKind.Stall:
                // The last heart is not a price: at one heart there is nothing on the counter.
                if (_floor.Hearts <= 1)
                {
                    break;
                }

                var shop = DraftFactory.Offers(
                    _runSeed,
                    FloorNumber,
                    _modifiers.Cards,
                    DraftFactory.DefaultOfferCount,
                    Math.Max(0, _floors.Count - _floorIndex - 2),
                    effectiveFloor: _deepestFloor,
                    salt: ShopSalt);

                // A casino promise with no floor below it would be a torch penalty and nothing
                // else, so the last floor's stall does not stock it.
                if (_floorIndex + 1 >= _floors.Count)
                {
                    var sellable = new List<Card>(shop.Count);

                    foreach (var card in shop)
                    {
                        if (!card.Effect.GuaranteedCasino)
                        {
                            sellable.Add(card);
                        }
                    }

                    shop = sellable;
                }

                _shopCards = shop;

                for (var index = 0; index < _shopCards.Count; index++)
                {
                    var card = _shopCards[index];

                    options.Add(new RoomOption(
                        $"buy-{index}",
                        card.Name.ToUpperInvariant(),
                        $"ONE HEART - {card.Tier}",
                        card.FaceText));
                }

                break;

            case RoomKind.Confessional:
                // Removal is a heart; with one left there is nothing to give up.
                if (_floor.Hearts <= 1)
                {
                    break;
                }

                for (var index = 0; index < _modifiers.Cards.Count; index++)
                {
                    options.Add(new RoomOption(
                        $"give-{index}",
                        _modifiers.Cards[index].Name.ToUpperInvariant(),
                        "ONE HEART"));
                }

                break;

            case RoomKind.Toll:
                if (_floor.Hearts > 1)
                {
                    options.Add(new RoomOption("toll", "PAY THE TOLL", "ONE HEART"));
                }

                break;

            case RoomKind.SeedVault:
                options.Add(new RoomOption("plant", "PLANT A SEED", "BLOOMS THREE FLOORS DOWN"));
                break;

            case RoomKind.Mirror:
                for (var index = 0; index < _modifiers.Cards.Count; index++)
                {
                    if (ReplacementFor(index) is null)
                    {
                        continue;
                    }

                    options.Add(new RoomOption(
                        $"swap-{index}",
                        $"GIVE {_modifiers.Cards[index].Name.ToUpperInvariant()}",
                        "FOR ONE THE MIRROR PICKS"));
                }

                break;

            case RoomKind.Turn:
                options.Add(new RoomOption("back-5", "GO BACK FIVE FLOORS", "NOTHING"));

                if (_floor.Hearts > 1)
                {
                    options.Add(new RoomOption("back-1", "GO BACK TO FLOOR ONE", "ONE HEART"));
                }

                break;
        }

        options.Add(RoomOption.Leave);

        return new RoomOffer(
            kind,
            RoomCatalog.Name(kind),
            RoomCatalog.Prompt(kind, CasinoWinPercent),
            RoomCatalog.Price(kind),
            options);
    }

    /// <summary>Applies the casino's stake and roll.</summary>
    private void ResolveStake(RoomOffer offer)
    {
        var stake = OptionIndex(offer.Chosen!.Id, "stake-");

        if (stake < 1 || stake > _floor.Hearts - 1)
        {
            offer.RecordOutcome("THE TABLE REFUSES");
            return;
        }

        for (var spent = 0; spent < stake; spent++)
        {
            _floor.TrySpendHeart();
        }

        var winPercent = CasinoWinPercent;

        if (RoomCatalog.GambleWins(_runSeed, FloorNumber, winPercent))
        {
            var winnings = stake * 2;
            _floor.AddHearts(winnings);
            _casinoGambleUsed |= _casinoFloor == FloorNumber;
            offer.RecordOutcome($"WON {winnings} HEARTS");
            return;
        }

        // The favoured table is a single play: once this floor's gamble is made, a later visit
        // (or a rewind back to it) reads the house's usual 45%, not the card's 66%.
        _casinoGambleUsed |= _casinoFloor == FloorNumber;
        offer.RecordOutcome($"LOST {stake}");
    }

    /// <summary>Buys one card from the stall for a heart.</summary>
    private void ResolveBuy(RoomOffer offer)
    {
        var index = OptionIndex(offer.Chosen!.Id, "buy-");

        if (index < 0 || index >= _shopCards.Count)
        {
            offer.RecordOutcome("THE STALL IS EMPTY");
            return;
        }

        var card = _shopCards[index];

        if (!_floor.TrySpendHeart())
        {
            offer.RecordOutcome("NOT ENOUGH HEARTS");
            return;
        }

        AddCard(card);
        offer.RecordOutcome($"BOUGHT {card.Name.ToUpperInvariant()}");
    }

    /// <summary>Gives up one card for a heart, and rebuilds the run's totals.</summary>
    private void ResolveGive(RoomOffer offer)
    {
        var index = OptionIndex(offer.Chosen!.Id, "give-");
        var cards = _modifiers.Cards;

        if (index < 0 || index >= cards.Count)
        {
            offer.RecordOutcome("THE CONFESSIONAL IS EMPTY");
            return;
        }

        if (!_floor.TrySpendHeart())
        {
            offer.RecordOutcome("NOT ENOUGH HEARTS");
            return;
        }

        var name = cards[index].Name.ToUpperInvariant();
        _modifiers = RunModifiers.FromCards(Without(cards, index));
        offer.RecordOutcome($"GAVE UP {name}");
    }

    /// <summary>Swaps one card for one the mirror picks, free.</summary>
    private void ResolveSwap(RoomOffer offer)
    {
        var index = OptionIndex(offer.Chosen!.Id, "swap-");
        var replacement = ReplacementFor(index);

        if (replacement is null)
        {
            offer.RecordOutcome("THE MIRROR IS BLANK");
            return;
        }

        var kept = Without(_modifiers.Cards, index);
        var next = new List<Card>(kept) { replacement };

        _modifiers = RunModifiers.FromCards(next);
        offer.RecordOutcome($"NOW HOLDING {replacement.Name.ToUpperInvariant()}");
    }

    /// <summary>Turns the descent back, if the run can still afford to.</summary>
    private bool ResolveTurn(RoomOffer offer)
    {
        var toStart = string.Equals(offer.Chosen!.Id, "back-1", StringComparison.Ordinal);

        if (!TryReincarnate(toStart))
        {
            offer.RecordOutcome("THE TURN DOES NOT TAKE");
            return false;
        }

        offer.RecordOutcome(toStart ? "THE DESCENT BEGINS AGAIN" : $"BACK ON FLOOR {FloorNumber}");
        return true;
    }

    /// <summary>
    /// Returns the card the mirror would offer in place of the one at <paramref name="index"/>.
    /// </summary>
    /// <remarks>
    /// The replacement is drawn with the draft's own rules — eligibility, tier weights, the vision
    /// cap — from a salted stream, so a mirror on a seeded run always hands back the same card.
    /// </remarks>
    private Card? ReplacementFor(int index)
    {
        var cards = _modifiers.Cards;

        if (index < 0 || index >= cards.Count)
        {
            return null;
        }

        var offers = DraftFactory.Offers(
            _runSeed,
            FloorNumber,
            Without(cards, index),
            1,
            Math.Max(0, _floors.Count - _floorIndex - 2),
            effectiveFloor: _deepestFloor,
            salt: MirrorSalt);

        return offers.Count > 0 ? offers[0] : null;
    }

    /// <summary>Adds a card to the run, banking its one-shot effects exactly as a draft does.</summary>
    private void AddCard(Card card)
    {
        _modifiers = _modifiers.With(card);
        _carryHearts += card.Effect.HealNow;
        _nextDraftBonus += card.Effect.NextDraftOfferBonus;
        _followingDraftBonus += card.Effect.FollowingDraftOfferBonus;

        // A casino promise needs a floor to land on. A stall on the last floor cannot offer the
        // card (see CreateRoom), so this only protects a direct resolution from inventing one.
        if (card.Effect.GuaranteedCasino && _floorIndex + 1 < _floors.Count)
        {
            _casinoFloor = _floors[_floorIndex + 1].Number;
        }
    }

    /// <summary>Returns <paramref name="cards"/> without the entry at <paramref name="index"/>.</summary>
    private static List<Card> Without(IReadOnlyList<Card> cards, int index)
    {
        var kept = new List<Card>(cards.Count - 1);

        for (var position = 0; position < cards.Count; position++)
        {
            if (position != index)
            {
                kept.Add(cards[position]);
            }
        }

        return kept;
    }

    /// <summary>Reads the number at the end of an option id, or -1 when it is missing.</summary>
    private static int OptionIndex(string id, string prefix)
    {
        if (!id.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(id.AsSpan(prefix.Length), out var index))
        {
            return -1;
        }

        return index;
    }

    /// <summary>
    /// Returns the relic a floor's altar should hold, skipping every relic the run already has.
    /// </summary>
    /// <param name="floorNumber">The floor being built.</param>
    /// <returns>A kind the run does not hold, or null when it holds them all.</returns>
    /// <remarks>
    /// The walk is deterministic from the seed and the floor, so a restored run stocks the same
    /// altar. It is a walk rather than a re-roll because re-rolling could, in principle, keep
    /// landing on held relics; stepping once per held relic is bounded by the pool size.
    /// </remarks>
    private RelicKind? NextRelic(int floorNumber)
    {
        for (var skip = 0; skip < RelicCatalog.Cards.Count; skip++)
        {
            var kind = RelicCatalog.Pick(_runSeed, floorNumber, skip);
            var id = RelicCatalog.CardFor(kind).Id;

            if (_modifiers.Cards.All(card => !string.Equals(card.Id, id, StringComparison.Ordinal)))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds one floor with the run's cards, the promises rooms made and rejects a builder that
    /// returns nothing.
    /// </summary>
    /// <param name="floor">The floor to build.</param>
    /// <param name="hearts">Hearts the player brings to it.</param>
    /// <param name="consumePending">
    /// Whether the promises that pay on this floor are spent by building it. A restore passes
    /// false: its pending promises belong to the floor after the saved one.
    /// </param>
    private GameState Build(FloorDefinition floor, int hearts, bool consumePending = true)
    {
        var definition = floor;

        // The Toll: one hunter stays home, never below one. Consumed by the next floor only.
        if (consumePending && _huntersOffNextFloor > 0)
        {
            definition = floor with
            {
                HunterCount = Math.Max(1, floor.HunterCount - _huntersOffNextFloor),
            };

            _huntersOffNextFloor = 0;
        }

        // The Seed Vault: the bloom pays on the floor it lands on, not a floor later. The heart
        // is never clipped, like every other heal in the run.
        if (consumePending && _seedBloomsOnFloor is { } bloom && floor.Number >= bloom)
        {
            hearts++;
            _seedBloomsOnFloor = null;
        }

        // A promised casino needs a room to stand in, so the roll is skipped for that one floor.
        if (_casinoFloor == floor.Number)
        {
            definition = definition with { ForceRoom = true };
        }

        // The altar is stocked here rather than authored, because it is a run decision: the relic
        // a floor offers must be one the run does not already hold. A test floor that names its
        // own relic keeps it.
        if (definition.Relic is null)
        {
            definition = definition with { Relic = NextRelic(floor.Number) };
        }

        return _createFloor(definition, hearts, _modifiers, _conditions)
            ?? throw new InvalidOperationException("The floor builder returned null.");
    }

}

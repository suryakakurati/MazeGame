using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The card a safe room shows: its name, its two lines of prose, its price and its options.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="Draft"/>, this is a read model with one mutation: only <see cref="Campaign"/> may
/// resolve it. That keeps a frontend from spending a heart without the run hearing about it.
/// </para>
/// <para>
/// An offer that is resolved stays in the run as a record of what was chosen, so a replayed
/// resolution cannot pay out twice; closing a card without choosing anything leaves the room
/// unspent, so a player who only wanted to look can walk back in.
/// </para>
/// </remarks>
public sealed class RoomOffer
{
    private readonly RoomOption[] _options;
    private RoomOption? _chosen;

    /// <summary>Creates an offer. Internal: the campaign builds every card.</summary>
    /// <param name="kind">What the room does.</param>
    /// <param name="name">The room's name, shown as the card's title.</param>
    /// <param name="prompt">Two short lines of prose, or one.</param>
    /// <param name="price">The card's price line.</param>
    /// <param name="options">The choices, in display order. May not be empty.</param>
    internal RoomOffer(
        RoomKind kind,
        string name,
        string prompt,
        string price,
        IReadOnlyList<RoomOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            throw new ArgumentException("A room offers at least one option.", nameof(options));
        }

        _options = new RoomOption[options.Count];

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index]
                ?? throw new ArgumentException("The option list contains null.", nameof(options));

            option.Validate();
            _options[index] = option;
        }

        Kind = kind;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Price = price ?? throw new ArgumentNullException(nameof(price));
    }

    /// <summary>Gets what the room does.</summary>
    public RoomKind Kind { get; }

    /// <summary>Gets the room's name, drawn as the card's title.</summary>
    public string Name { get; }

    /// <summary>Gets the card's prose.</summary>
    public string Prompt { get; }

    /// <summary>Gets the card's price line.</summary>
    public string Price { get; }

    /// <summary>Gets the choices, in display order.</summary>
    public IReadOnlyList<RoomOption> Options => new ReadOnlyCollection<RoomOption>(_options);

    /// <summary>Gets a value indicating whether the card has been spent.</summary>
    public bool IsResolved => _chosen is not null;

    /// <summary>Gets the option chosen, or <see langword="null"/> while the card is open.</summary>
    public RoomOption? Chosen => _chosen;

    /// <summary>
    /// Gets what the choice actually did, in one line, or <see langword="null"/> while the card is
    /// open.
    /// </summary>
    /// <remarks>
    /// A resolution the player cannot read is a resolution they cannot learn from: the card says
    /// whether the stake doubled, which card was bought, or how far the descent turned back.
    /// </remarks>
    public string? Outcome { get; private set; }

    /// <summary>Resolves the card with <paramref name="option"/>.</summary>
    /// <returns><see langword="false"/> when the card is already spent or the option is not on it.</returns>
    internal bool TryResolve(RoomOption option)
    {
        if (option is null || IsResolved || !IsOffered(option))
        {
            return false;
        }

        _chosen = option;
        return true;
    }

    /// <summary>Records what resolving the card did.</summary>
    /// <param name="outcome">The one-line result.</param>
    internal void RecordOutcome(string outcome) => Outcome = outcome;

    /// <summary>Tests whether <paramref name="option"/> is one of the card's choices, by id.</summary>
    private bool IsOffered(RoomOption option)
    {
        foreach (var offered in _options)
        {
            if (string.Equals(offered.Id, option.Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

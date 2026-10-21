using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MazeGame.Core.Game;

/// <summary>
/// The two descent conditions on offer at a gate, and whether one has been taken.
/// </summary>
/// <remarks>
/// This is the campaign's gate in the same way <see cref="Draft"/> is its card gate: the run
/// cannot advance while the offer is unresolved, so no frontend can accidentally skip the choice.
/// There is no skip: the whole point is that the run changes.
/// </remarks>
public sealed class ConditionOffer
{
    private readonly DescentCondition[] _options;

    /// <summary>Creates an offer from the conditions to show.</summary>
    /// <param name="options">The conditions; one or two.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the list is empty or holds duplicates.</exception>
    public ConditionOffer(IReadOnlyList<DescentCondition> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            throw new ArgumentException("A gate must offer at least one condition.", nameof(options));
        }

        _options = new DescentCondition[options.Count];
        var seen = new HashSet<DescentCondition>();

        for (var index = 0; index < options.Count; index++)
        {
            var condition = options[index];

            if (!seen.Add(condition))
            {
                throw new ArgumentException($"The gate offers {condition} twice.", nameof(options));
            }

            _options[index] = condition;
        }

        Options = new ReadOnlyCollection<DescentCondition>(_options);
    }

    /// <summary>Gets the conditions on the table, in display order.</summary>
    public IReadOnlyList<DescentCondition> Options { get; }

    /// <summary>Gets a value indicating whether a condition has been taken.</summary>
    public bool IsResolved { get; private set; }

    /// <summary>Gets the condition taken, or null while the offer is open.</summary>
    public DescentCondition? Taken { get; private set; }

    /// <summary>Takes <paramref name="condition"/> from the offer.</summary>
    /// <param name="condition">The condition to take; it must be on the table.</param>
    /// <returns>
    /// <see langword="true"/> when it was taken; <see langword="false"/> when the offer was
    /// already resolved or the condition is not one of the options.
    /// </returns>
    public bool TryTake(DescentCondition condition)
    {
        if (IsResolved || !Options.Contains(condition))
        {
            return false;
        }

        IsResolved = true;
        Taken = condition;
        return true;
    }
}

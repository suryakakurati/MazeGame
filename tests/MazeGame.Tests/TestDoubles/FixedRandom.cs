using System;
using System.Collections.Generic;
using MazeGame.Core.Abstractions;

namespace MazeGame.Tests.TestDoubles;

/// <summary>
/// A deterministic <see cref="IRandom"/> used to prove that generators depend only on the
/// injected randomness. <see cref="Shuffle{T}"/> deliberately does nothing, so the caller
/// sees the order the items were added in.
/// </summary>
internal sealed class FixedRandom : IRandom
{
    private readonly int _value;

    /// <summary>
    /// Creates a generator that always answers <c>_value % maxExclusive</c>.
    /// </summary>
    /// <param name="value">Fixed value to return, reduced into range.</param>
    public FixedRandom(int value) => _value = value;

    /// <inheritdoc />
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "Upper bound must be positive.");
        }

        return _value % maxExclusive;
    }

    /// <inheritdoc />
    public void Shuffle<T>(IList<T> items) => ArgumentNullException.ThrowIfNull(items);
}

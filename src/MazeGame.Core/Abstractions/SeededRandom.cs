using System;
using System.Collections.Generic;

namespace MazeGame.Core.Abstractions;

/// <summary>
/// The production <see cref="IRandom"/>: a thin, seedable wrapper around
/// <see cref="System.Random"/>.
/// </summary>
/// <remarks>
/// A non-null seed produces the same sequence on every run, which is what makes a maze
/// reproducible from a number (useful for debugging, tests, and sharing a level).
/// A <see langword="null"/> seed produces a different sequence on every run.
/// </remarks>
public sealed class SeededRandom : IRandom
{
    private readonly Random _random;

    /// <summary>
    /// Creates a generator from <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">
    /// The seed to reproduce a sequence, or <see langword="null"/> for a fresh,
    /// non-reproducible sequence.
    /// </param>
    public SeededRandom(int? seed)
    {
        // A null seed means "surprise me": System.Random seeds itself from the clock.
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Random.Next(int)"/> returns 0 for a bound of 0 instead of complaining.
    /// A zero bound is always a caller bug ("pick one of nothing"), so it is rejected
    /// here rather than silently producing a value.
    /// </remarks>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "Upper bound must be positive.");
        }

        return _random.Next(maxExclusive);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Fisher-Yates: walk from the end, swap each position with a randomly chosen
    /// position at or before it. Every permutation is equally likely, and the list is
    /// touched exactly once, so this is O(n) and unbiased.
    /// </remarks>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}

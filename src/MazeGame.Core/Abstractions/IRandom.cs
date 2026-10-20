using System.Collections.Generic;

namespace MazeGame.Core.Abstractions;

/// <summary>
/// The single source of randomness for Core logic.
/// </summary>
/// <remarks>
/// Core never calls <see cref="System.Random"/> directly. Injecting randomness behind
/// this interface means a test can supply a fixed sequence (or a fake) and get identical
/// results every run, while the game still uses a seedable generator at runtime.
/// </remarks>
public interface IRandom
{
    /// <summary>
    /// Returns a non-negative random integer strictly less than
    /// <paramref name="maxExclusive"/>.
    /// </summary>
    /// <param name="maxExclusive">Upper bound, exclusive. Must be positive.</param>
    /// <returns>A value in the range <c>0 .. maxExclusive - 1</c>.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxExclusive"/> is negative or zero.
    /// </exception>
    /// <remarks>
    /// Named <c>NextInt</c> rather than <c>Next</c> because <c>Next</c> is a reserved
    /// keyword in other .NET languages, which would make the interface awkward to
    /// implement outside C# (CA1716).
    /// </remarks>
    int NextInt(int maxExclusive);

    /// <summary>
    /// Shuffles <paramref name="items"/> in place into a uniformly random permutation.
    /// </summary>
    /// <typeparam name="T">Element type of the list.</typeparam>
    /// <param name="items">The list to reorder. Its contents are changed, not replaced.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="items"/> is <see langword="null"/>.
    /// </exception>
    void Shuffle<T>(IList<T> items);
}

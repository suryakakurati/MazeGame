using System;
using System.Collections.Generic;

namespace MazeGame.Core.Game;

/// <summary>
/// One chapter of the herbarium: the title and the lines shown on the card after a floor is
/// cleared.
/// </summary>
/// <remarks>
/// <para>
/// A page is immutable and validated where it is made, so the card never has to defend itself
/// against a fourth line or an empty chapter. The catalog is the only place pages are built.
/// </para>
/// <para>
/// The page is deliberately not tied to a <see cref="FloorDefinition"/>: a floor is numbers and a
/// page is words, and a test keeps the two in step so a floor can be re-tuned without the prose
/// moving under it.
/// </para>
/// </remarks>
public sealed class StoryPage
{
    /// <summary>
    /// Creates a page.
    /// </summary>
    /// <param name="floorNumber">The floor the page belongs to, one-based.</param>
    /// <param name="title">The page's title, in capitals by convention.</param>
    /// <param name="lines">The chapter, at most <see cref="StoryCatalog.MaxPageLines"/> lines.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="title"/> or <paramref name="lines"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the floor number is below one, when there are no lines, or when there are more
    /// than <see cref="StoryCatalog.MaxPageLines"/>.
    /// </exception>
    public StoryPage(int floorNumber, string title, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(lines);

        if (floorNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(floorNumber),
                floorNumber,
                "Floors are numbered from one.");
        }

        if (lines.Count is < 1 or > StoryCatalog.MaxPageLines)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lines),
                lines.Count,
                $"A page has one to {StoryCatalog.MaxPageLines} lines.");
        }

        FloorNumber = floorNumber;
        Title = title;
        Lines = lines;
    }

    /// <summary>Gets the floor this page belongs to, one-based.</summary>
    public int FloorNumber { get; }

    /// <summary>Gets the page's title, in capitals by convention.</summary>
    public string Title { get; }

    /// <summary>Gets the chapter's lines, in reading order.</summary>
    public IReadOnlyList<string> Lines { get; }
}

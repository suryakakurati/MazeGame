using System;
using System.Collections.Generic;

namespace MazeGame.Core.Game;

/// <summary>
/// One page of the guided tutorial: what it is about, what to read, and the keys it teaches.
/// </summary>
/// <remarks>
/// The text lives in Core so the window and the terminal teach exactly the same lesson; only the
/// drawing differs. A step carries no rules, which is why it can be asserted as content.
/// </remarks>
public sealed record TutorialStep
{
    /// <summary>Gets the short caption shown above the heading.</summary>
    public required string Caption { get; init; }

    /// <summary>Gets the page's heading.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the prose, one paragraph per entry.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Gets the keys this page teaches, in capitals, or an empty string when it teaches none.</summary>
    public string Keys { get; init; } = string.Empty;

    /// <summary>Checks that the page says something.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the caption, title or prose is missing.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Caption))
        {
            throw new ArgumentException("A tutorial page needs a caption.", nameof(Caption));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("A tutorial page needs a heading.", nameof(Title));
        }

        if (Lines is null || Lines.Count == 0)
        {
            throw new ArgumentException("A tutorial page needs prose.", nameof(Lines));
        }

        foreach (var line in Lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new ArgumentException("A tutorial page cannot hold a blank line.", nameof(Lines));
            }
        }
    }
}

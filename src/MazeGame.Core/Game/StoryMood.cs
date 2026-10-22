namespace MazeGame.Core.Game;

/// <summary>
/// How the run looked when a page was turned, which decides whether the gardener adds a line.
/// </summary>
/// <remarks>
/// This is Hades' trick at the smallest possible size: the story notices how the run is going
/// without a line per situation. There are three states, so three sentences cover the whole game,
/// and a page never changes — only the sentence under it does.
/// </remarks>
public enum StoryMood
{
    /// <summary>Nothing worth remarking on: the page stands alone.</summary>
    None,

    /// <summary>The floor was cleared on the last heart.</summary>
    LastHeart,

    /// <summary>A descent condition chose at a gate is still in force.</summary>
    Conditioned,
}

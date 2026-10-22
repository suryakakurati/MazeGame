namespace MazeGame.Core.Game;

/// <summary>
/// How rare a card is, and how likely the draft is to offer it.
/// </summary>
/// <remarks>
/// <para>
/// The tier is used two ways: the draft weights its draw by it, so commons dominate early and
/// rares feel like an event, and the card face is coloured by it in the window.
/// </para>
/// <para>
/// The draft's floor bands are weights, not hard gates: commons thin out and rares grow more
/// likely as the run descends, and every fifth floor's draft guarantees one rare when one is
/// still eligible. That keeps early floors legible without making a late run a different
/// catalog.
/// </para>
/// </remarks>
public enum CardTier
{
    /// <summary>The everyday trade; offered most often.</summary>
    Common,

    /// <summary>A sharper trade, offered less often than a common.</summary>
    Uncommon,

    /// <summary>A run-defining trade; the rarest of the offers.</summary>
    Rare,

    /// <summary>
    /// A once-in-a-descent trade, offered only in the deep floors. The promised casino is the
    /// only card at this tier: a whole guaranteed room is worth more than any single stat.
    /// </summary>
    SuperRare,

    /// <summary>
    /// A pure cost, never offered on its own — a curse is only ever the price attached to a rare.
    /// The catalog carries the tier so the pairing rule has something to recognise; this phase
    /// ships no pairings, so no curse is in the shipped catalog yet.
    /// </summary>
    Curse,
}

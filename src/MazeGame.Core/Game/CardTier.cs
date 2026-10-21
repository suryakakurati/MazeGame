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
/// The plan also gates tiers by floor band (commons on floors 1–8, uncommons from 5, rares from
/// 11). That gate is deliberately not implemented while the shipped run has three floors: every
/// band would collapse onto floor 1. It arrives with the twenty-floor campaign, and the weight
/// below is what makes the current run feel graded in the meantime.
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
    /// A pure cost, never offered on its own — a curse is only ever the price attached to a rare.
    /// The catalog carries the tier so the pairing rule has something to recognise; this phase
    /// ships no pairings, so no curse is in the shipped catalog yet.
    /// </summary>
    Curse,
}

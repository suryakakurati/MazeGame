namespace MazeGame.Core.Game;

/// <summary>
/// The deltas one card contributes to a run, as data.
/// </summary>
/// <remarks>
/// <para>
/// A card never carries code: it carries numbers, and <see cref="RunModifiers"/> adds them up and
/// clamps the total before it becomes a floor's <see cref="GameConfig"/>. That is what keeps the
/// rules in <see cref="GameState"/> untouched — cards change the <i>inputs</i> of a floor, not the
/// way a floor is played.
/// </para>
/// <para>
/// Most fields are additive. The two nullable ones are absolute: a card that says "seals last
/// 18 seconds" means exactly that, and the catalog marks the cards that disagree as mutually
/// exclusive so two absolutes can never compete.
/// </para>
/// </remarks>
public sealed record CardEffect
{
    /// <summary>Gets the change to the player's vision radius, in steps.</summary>
    public int VisionRadius { get; init; }

    /// <summary>Gets the change to the distance at which the killer's aura is visible.</summary>
    public int EnemyAuraDistance { get; init; }

    /// <summary>Gets the change to the barriers available on each floor.</summary>
    public int BarrierBudget { get; init; }

    /// <summary>Gets the seal lifetime to use instead of the floor's, or null to keep it.</summary>
    public double? BarrierSealSeconds { get; init; }

    /// <summary>
    /// Gets the multiplier applied to the killer's step time. Defaults to 1, so a card can only
    /// change the pace by saying so; below 1 means a faster killer.
    /// </summary>
    public double EnemyStepScale { get; init; } = 1.0;

    /// <summary>
    /// Gets the multiplier on the player's step time. Defaults to 1; below 1 is faster, above 1 is
    /// slower. Multiplicative like <see cref="EnemyStepScale"/>, so two speed cards compose rather
    /// than cancel out.
    /// </summary>
    public double PlayerStepScale { get; init; } = 1.0;

    /// <summary>
    /// Gets the change to the grace window after a catch, in seconds. Additive; defaults to zero.
    /// </summary>
    public double InvulnerabilitySeconds { get; init; }

    /// <summary>
    /// Gets the multiplier on the sprint stamina bar. Defaults to 1, so a card can lengthen a
    /// sprint without redefining what a second of it means.
    /// </summary>
    public double SprintStaminaScale { get; init; } = 1.0;

    /// <summary>Gets the change to the speed earned per straight step, as a fraction.</summary>
    public double MomentumRampPerStep { get; init; }

    /// <summary>Gets the change to the most speed a straight run may earn, as a fraction.</summary>
    public double MomentumMaxBonus { get; init; }

    /// <summary>Gets the change to how much slower the first step after a turn is, as a fraction.</summary>
    public double TurnPenaltyScale { get; init; }

    /// <summary>Gets the change to how long the turn penalty lasts, in seconds.</summary>
    public double TurnPenaltySeconds { get; init; }

    /// <summary>Gets the change to the heart ceiling of each floor.</summary>
    public int MaxHearts { get; init; }

    /// <summary>Gets the change to how many keys each floor from now on demands.</summary>
    public int KeyCount { get; init; }

    /// <summary>
    /// Gets the change to how far the blooms glow after a hunter's catch.
    /// </summary>
    /// <remarks>
    /// Zero (the default) leaves the flowers dark. One lights every bloom on the floor on its own
    /// tile for a couple of seconds; each further step of reach lights that many cells around each
    /// glowing bloom as well, so a second copy is a halo rather than a bigger map reveal.
    /// </remarks>
    public int BloomGlowReach { get; init; }

    /// <summary>Gets the change to the wisps available on each floor.</summary>
    public int LureCount { get; init; }

    /// <summary>Gets the wisp lifetime to use instead of the floor's, or null to keep it.</summary>
    public double? LureSeconds { get; init; }

    /// <summary>
    /// Gets the seconds the killer is stunned when it reaches a wisp. Additive, so a second card
    /// could lengthen it; zero means "no stun", which is the shipped default.
    /// </summary>
    public double LureStunSeconds { get; init; }

    /// <summary>
    /// Gets how many steps of vision the player loses while a wisp is burning. Additive; zero
    /// means the wisp costs no sight, which is the shipped default.
    /// </summary>
    public int LureVisionPenalty { get; init; }

    /// <summary>
    /// Gets the hearts restored the moment the card is picked. This is a one-shot, not a
    /// modifier: the campaign applies it to the hearts it carries to the next floor.
    /// </summary>
    public int HealNow { get; init; }

    /// <summary>
    /// Gets the hearts restored every time a floor is cleared. Unlike <see cref="HealNow"/> this
    /// repeats, so it lives in the run modifiers rather than in a queue of pending heals.
    /// </summary>
    public int HealOnFloorClear { get; init; }

    /// <summary>Gets the change to the blinks available on each floor.</summary>
    public int BlinkCharges { get; init; }

    /// <summary>Gets the change to the veils available on each floor.</summary>
    public int VeilCharges { get; init; }

    /// <summary>Gets the change to the drags available on each floor.</summary>
    public int DragCharges { get; init; }

    /// <summary>Gets the change to the lantern throws available on each floor.</summary>
    public int LanternCharges { get; init; }

    /// <summary>Gets the change to the shoves available on each floor.</summary>
    public int ShoveCharges { get; init; }

    /// <summary>Gets a value indicating whether the first catch each floor costs no heart.</summary>
    public bool FirstCatchFree { get; init; }

    /// <summary>Gets a value indicating whether keys within one step are collected automatically.</summary>
    public bool MagneticKeys { get; init; }

    /// <summary>Gets a value indicating whether the first fatal catch each floor leaves one heart.</summary>
    public bool ReviveOnce { get; init; }

    /// <summary>Gets a value indicating whether taking a key returns a blink charge.</summary>
    public bool BlinkRefundOnKey { get; init; }

    /// <summary>Gets the change to the hearts a single catch costs.</summary>
    public int CatchCost { get; init; }

    /// <summary>Gets the change to how long a completed veil lasts, in seconds.</summary>
    public double VeilDurationSeconds { get; init; }

    /// <summary>Gets the change to how long Drag slows the hunt, in seconds.</summary>
    public double DragSeconds { get; init; }

    /// <summary>Gets the change to how long the hunt speeds up after Drag, in seconds.</summary>
    public double DragPenaltySeconds { get; init; }

    /// <summary>Gets the change to how far a lantern may be thrown, in cells.</summary>
    public int LanternRange { get; init; }

    /// <summary>Gets the extra offers the next draft makes (one draft only).</summary>
    public int NextDraftOfferBonus { get; init; }

    /// <summary>
    /// Gets the change to the draft after the next one. It is what makes a bonus draft cost the
    /// player something, rather than being a free extra choice.
    /// </summary>
    public int FollowingDraftOfferBonus { get; init; }

    /// <summary>
    /// Gets the change to the chance that a floor holds a secret room, in whole percentage points.
    /// The base rate is ten percent, so a single copy of a room card doubles the run's rooms.
    /// </summary>
    public int RoomChancePercent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the next floor is promised a casino room, and whether that
    /// visit's table pays the favoured odds once before returning to the house's usual price.
    /// </summary>
    public bool GuaranteedCasino { get; init; }

    /// <summary>
    /// Gets a value indicating whether this effect changes what the player can see, directly or
    /// through a burning wisp. The draft's rate limit counts these.
    /// </summary>
    public bool TouchesVision => VisionRadius != 0 || LureVisionPenalty != 0;
}

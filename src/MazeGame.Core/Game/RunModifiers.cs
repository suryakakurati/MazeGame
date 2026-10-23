using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MazeGame.Core.Game;

/// <summary>
/// The totals of every card held in a run, applied to a floor's <see cref="GameConfig"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole point of cards being data: a floor is still built by the factory, still an
/// ordinary <see cref="GameState"/>, and every rule test still means what it meant. The run only
/// changes the numbers the floor is built from.
/// </para>
/// <para>
/// Composition is clamped. A run can stack cards in ways no single card anticipated, so
/// <see cref="Apply"/> enforces the floor's own limits — vision never falls below one step once a
/// card has touched it, budgets never go negative, the killer's step time never drops under
/// <see cref="MinimumStepSeconds"/>, and the heart ceiling never falls below the hearts the
/// player is carrying. The result is validated again, so a combination that cannot exist fails
/// loudly instead of producing a broken floor.
/// </para>
/// </remarks>
public sealed class RunModifiers
{
    /// <summary>The fastest the killer may ever become, in seconds per normal step.</summary>
    public const double MinimumStepSeconds = 0.05;

    private static readonly Card[] NoCards = [];

    private readonly Card[] _cards;

    private RunModifiers(
        Card[] cards,
        int visionRadius,
        int enemyAuraDistance,
        double enemyStepScale,
        double playerStepScale,
        double invulnerabilitySeconds,
        double sprintStaminaScale,
        double momentumRampPerStep,
        double momentumMaxBonus,
        double turnPenaltyScale,
        double turnPenaltySeconds,
        int keyCount,
        int bloomGlowRadius,
        int blinkCharges,
        int veilCharges,
        int dragCharges,
        int lanternCharges,
        int shoveCharges,
        bool firstCatchFree,
        bool magneticKeys,
        bool reviveOnce,
        bool blinkRefundOnKey,
        int catchCost,
        double veilDurationSeconds,
        double dragSeconds,
        double dragPenaltySeconds,
        int lanternRange,
        int healOnFloorClear,
        int roomChancePercent,
        bool guaranteedCasino)
    {
        _cards = cards;
        VisionRadius = visionRadius;
        EnemyAuraDistance = enemyAuraDistance;
        EnemyStepScale = enemyStepScale;
        PlayerStepScale = playerStepScale;
        InvulnerabilitySeconds = invulnerabilitySeconds;
        SprintStaminaScale = sprintStaminaScale;
        MomentumRampPerStep = momentumRampPerStep;
        MomentumMaxBonus = momentumMaxBonus;
        TurnPenaltyScale = turnPenaltyScale;
        TurnPenaltySeconds = turnPenaltySeconds;
        KeyCount = keyCount;
        BloomGlowReach = bloomGlowRadius;
        BlinkCharges = blinkCharges;
        VeilCharges = veilCharges;
        DragCharges = dragCharges;
        LanternCharges = lanternCharges;
        ShoveCharges = shoveCharges;
        FirstCatchFree = firstCatchFree;
        MagneticKeys = magneticKeys;
        ReviveOnce = reviveOnce;
        BlinkRefundOnKey = blinkRefundOnKey;
        CatchCost = catchCost;
        VeilDurationSeconds = veilDurationSeconds;
        DragSeconds = dragSeconds;
        DragPenaltySeconds = dragPenaltySeconds;
        LanternRange = lanternRange;
        HealOnFloorClear = healOnFloorClear;
        RoomChancePercent = roomChancePercent;
        GuaranteedCasino = guaranteedCasino;
    }

    /// <summary>Gets the run before any card has been taken.</summary>
    public static RunModifiers None { get; } = new(
        NoCards,
        visionRadius: 0,
        enemyAuraDistance: 0,
        enemyStepScale: 1.0,
        playerStepScale: 1.0,
        invulnerabilitySeconds: 0,
        sprintStaminaScale: 1.0,
        momentumRampPerStep: 0,
        momentumMaxBonus: 0,
        turnPenaltyScale: 0,
        turnPenaltySeconds: 0,
        keyCount: 0,
        bloomGlowRadius: 0,
        blinkCharges: 0,
        veilCharges: 0,
        dragCharges: 0,
        lanternCharges: 0,
        shoveCharges: 0,
        firstCatchFree: false,
        magneticKeys: false,
        reviveOnce: false,
        blinkRefundOnKey: false,
        catchCost: 0,
        veilDurationSeconds: 0,
        dragSeconds: 0,
        dragPenaltySeconds: 0,
        lanternRange: 0,
        healOnFloorClear: 0,
        roomChancePercent: 0,
        guaranteedCasino: false);

    /// <summary>Gets the cards held, in the order they were taken.</summary>
    public IReadOnlyList<Card> Cards => _cards;

    /// <summary>Gets the change to the player's vision radius, in steps.</summary>
    public int VisionRadius { get; }

    /// <summary>Gets the change to the distance at which the killer's aura shows.</summary>
    public int EnemyAuraDistance { get; }

    /// <summary>Gets the multiplier on the killer's step time.</summary>
    public double EnemyStepScale { get; }

    /// <summary>Gets the multiplier on the player's step time.</summary>
    public double PlayerStepScale { get; }

    /// <summary>Gets the change to the post-catch grace window, in seconds.</summary>
    public double InvulnerabilitySeconds { get; }

    /// <summary>Gets the multiplier on the sprint stamina bar.</summary>
    public double SprintStaminaScale { get; }

    /// <summary>Gets the change to the speed earned per straight step.</summary>
    public double MomentumRampPerStep { get; }

    /// <summary>Gets the change to the most speed a straight run may earn.</summary>
    public double MomentumMaxBonus { get; }

    /// <summary>Gets the change to how much slower a turn makes the next step.</summary>
    public double TurnPenaltyScale { get; }

    /// <summary>Gets the change to how long the turn penalty lasts.</summary>
    public double TurnPenaltySeconds { get; }

    /// <summary>Gets the change to each floor's key count.</summary>
    public int KeyCount { get; }

    /// <summary>
    /// Gets the change to how far the blooms glow after a catch: one lights every bloom on the
    /// floor, and each step above one lights a step around each of them.
    /// </summary>
    public int BloomGlowReach { get; }

    /// <summary>Gets the change to each floor's blink charges.</summary>
    public int BlinkCharges { get; }

    /// <summary>Gets the change to each floor's veil charges.</summary>
    public int VeilCharges { get; }

    /// <summary>Gets the change to each floor's drag charges.</summary>
    public int DragCharges { get; }

    /// <summary>Gets the change to each floor's lantern charges.</summary>
    public int LanternCharges { get; }

    /// <summary>Gets the change to each floor's shove charges.</summary>
    public int ShoveCharges { get; }

    /// <summary>Gets a value indicating whether the first catch each floor is free.</summary>
    public bool FirstCatchFree { get; }

    /// <summary>Gets a value indicating whether adjacent keys are collected automatically.</summary>
    public bool MagneticKeys { get; }

    /// <summary>Gets a value indicating whether the first fatal catch each floor is survived.</summary>
    public bool ReviveOnce { get; }

    /// <summary>Gets a value indicating whether a key returns a blink charge.</summary>
    public bool BlinkRefundOnKey { get; }

    /// <summary>Gets the change to the hearts a catch costs.</summary>
    public int CatchCost { get; }

    /// <summary>Gets the change to a veil's duration.</summary>
    public double VeilDurationSeconds { get; }

    /// <summary>Gets the change to Drag's slow window.</summary>
    public double DragSeconds { get; }

    /// <summary>Gets the change to Drag's hangover.</summary>
    public double DragPenaltySeconds { get; }

    /// <summary>Gets the change to the lantern's throw range.</summary>
    public int LanternRange { get; }

    /// <summary>Gets the hearts restored on every floor clear.</summary>
    public int HealOnFloorClear { get; }

    /// <summary>Gets the change to the chance a floor holds a secret room, in percentage points.</summary>
    public int RoomChancePercent { get; }

    /// <summary>Gets a value indicating whether the next floor is promised a casino room.</summary>
    public bool GuaranteedCasino { get; }

    /// <summary>
    /// Gets how many held cards change what the player can see. The draft's rate limit keeps this
    /// at or below <see cref="DraftFactory.MaxVisionCards"/>.
    /// </summary>
    public int VisionCardCount
    {
        get
        {
            var count = 0;
            foreach (var card in _cards)
            {
                if (card.Effect.TouchesVision)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Rebuilds the run's totals from exactly <paramref name="cards"/>.
    /// </summary>
    /// <param name="cards">The cards the run now holds, in the order they were taken.</param>
    /// <returns>A new instance; the cards' order does not change the totals.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cards"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a card is invalid.</exception>
    /// <remarks>
    /// Removal needs a way back from the totals, and this is it: the Confessional hands over the
    /// run's cards minus the one given up, and the fold runs again from nothing. Every total is
    /// already a fold over the cards (see <see cref="With"/>), so rebuilding is the same arithmetic
    /// in the same order — which is what makes "rebuild equals with" a test rather than a hope.
    /// </remarks>
    public static RunModifiers FromCards(IReadOnlyList<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var modifiers = None;

        foreach (var card in cards)
        {
            modifiers = modifiers.With(card);
        }

        return modifiers;
    }

    /// <summary>Returns a new set of modifiers with <paramref name="card"/> added.</summary>
    /// <param name="card">The card just taken.</param>
    /// <returns>A new instance; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="card"/> is invalid.</exception>
    public RunModifiers With(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.Validate();

        var cards = new Card[_cards.Length + 1];
        Array.Copy(_cards, cards, _cards.Length);
        cards[^1] = card;

        var effect = card.Effect;

        return new RunModifiers(
            cards,
            VisionRadius + effect.VisionRadius,
            EnemyAuraDistance + effect.EnemyAuraDistance,
            EnemyStepScale * effect.EnemyStepScale,
            PlayerStepScale * effect.PlayerStepScale,
            InvulnerabilitySeconds + effect.InvulnerabilitySeconds,
            SprintStaminaScale * effect.SprintStaminaScale,
            MomentumRampPerStep + effect.MomentumRampPerStep,
            MomentumMaxBonus + effect.MomentumMaxBonus,
            TurnPenaltyScale + effect.TurnPenaltyScale,
            TurnPenaltySeconds + effect.TurnPenaltySeconds,
            KeyCount + effect.KeyCount,
            BloomGlowReach + effect.BloomGlowReach,
            BlinkCharges + effect.BlinkCharges,
            VeilCharges + effect.VeilCharges,
            DragCharges + effect.DragCharges,
            LanternCharges + effect.LanternCharges,
            ShoveCharges + effect.ShoveCharges,
            FirstCatchFree || effect.FirstCatchFree,
            MagneticKeys || effect.MagneticKeys,
            ReviveOnce || effect.ReviveOnce,
            BlinkRefundOnKey || effect.BlinkRefundOnKey,
            CatchCost + effect.CatchCost,
            VeilDurationSeconds + effect.VeilDurationSeconds,
            DragSeconds + effect.DragSeconds,
            DragPenaltySeconds + effect.DragPenaltySeconds,
            LanternRange + effect.LanternRange,
            HealOnFloorClear + effect.HealOnFloorClear,
            RoomChancePercent + effect.RoomChancePercent,
            GuaranteedCasino || effect.GuaranteedCasino);
    }

    /// <summary>
    /// Applies these modifiers to a floor's configuration, clamped and ready to build.
    /// </summary>
    /// <param name="config">The floor's configuration, straight from the floor definition.</param>
    /// <returns>A new configuration; <paramref name="config"/> is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the composition cannot describe a playable floor (for example a floor whose
    /// extra keys do not fit in the maze). It fails here rather than deep in placement.
    /// </exception>
    public GameConfig Apply(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // A card that touched vision can never leave the player blind: the floor's own zero
        // vision is a legal configuration, but a card's -2 must clamp at one step of sight.
        var vision = config.VisionRadius + VisionRadius;
        if (VisionRadius != 0)
        {
            vision = Math.Max(1, vision);
        }

        var powerups = config.Powerups
            .Adjust(PowerupKind.Blink, BlinkCharges)
            .Adjust(PowerupKind.Veil, VeilCharges)
            .Adjust(PowerupKind.Drag, DragCharges)
            .Adjust(PowerupKind.Lantern, LanternCharges)
            .Adjust(PowerupKind.Shove, ShoveCharges);
        var stepSeconds = Math.Max(MinimumStepSeconds, config.EnemyStepSeconds * EnemyStepScale);

        var composed = config with
        {
            VisionRadius = vision,
            EnemyAuraDistance = Math.Max(0, config.EnemyAuraDistance + EnemyAuraDistance),
            KeyCount = Math.Max(0, config.KeyCount + KeyCount),
            BloomGlowReach = Math.Max(0, config.BloomGlowReach + BloomGlowReach),
            Powerups = powerups,
            FirstCatchFree = config.FirstCatchFree || FirstCatchFree,
            MagneticKeys = config.MagneticKeys || MagneticKeys,
            ReviveOnce = config.ReviveOnce || ReviveOnce,
            BlinkRefundOnKey = config.BlinkRefundOnKey || BlinkRefundOnKey,
            CatchCost = Math.Max(1, config.CatchCost + CatchCost),
            VeilDurationSeconds = Math.Max(0.1, config.VeilDurationSeconds + VeilDurationSeconds),
            DragSeconds = Math.Max(0, config.DragSeconds + DragSeconds),
            DragPenaltySeconds = Math.Max(0, config.DragPenaltySeconds + DragPenaltySeconds),
            LanternRange = Math.Max(0, config.LanternRange + LanternRange),
            EnemyStepSeconds = stepSeconds,
            PlayerStepSeconds = Math.Max(0, config.PlayerStepSeconds * PlayerStepScale),
            InvulnerabilitySeconds = Math.Max(0, config.InvulnerabilitySeconds + InvulnerabilitySeconds),
            SprintStaminaSeconds = Math.Max(0, config.SprintStaminaSeconds * SprintStaminaScale),
            MomentumRampPerStep = Math.Max(0, config.MomentumRampPerStep + MomentumRampPerStep),
            MomentumMaxBonus = Math.Max(0, config.MomentumMaxBonus + MomentumMaxBonus),
            TurnPenaltyScale = Math.Max(0, config.TurnPenaltyScale + TurnPenaltyScale),
            TurnPenaltySeconds = Math.Max(0, config.TurnPenaltySeconds + TurnPenaltySeconds),
        };

        composed.Validate();
        return composed;
    }

    /// <summary>
    /// Returns the run's totals as one short line for the draft screen and the floor heading,
    /// for example <c>VISION +2   AURA +3   SEALS 18s</c>.
    /// </summary>
    /// <returns>
    /// The totals, or <c>NO CARDS</c> when nothing has been taken yet. Never null or empty.
    /// </returns>
    public string Describe()
    {
        if (_cards.Length == 0)
        {
            return "NO CARDS";
        }

        var text = new StringBuilder();

        Append(text, "VISION", VisionRadius);
        Append(text, "AURA", EnemyAuraDistance);
        Append(text, "BLINK", BlinkCharges);
        Append(text, "VEIL", VeilCharges);
        Append(text, "DRAG", DragCharges);
        Append(text, "LANTERN", LanternCharges);
        Append(text, "SHOVE", ShoveCharges);
        Append(text, "BLOOMS", KeyCount);
        Append(text, "BLOOM GLOW", BloomGlowReach);
        Append(text, "LANTERN RANGE", LanternRange);
        Append(text, "ROOMS", RoomChancePercent);

        // House Favour is deliberately absent: its promise is spent on one floor, so a line that
        // outlived it would tell the player the next floor still holds a casino.

        if (CatchCost != 0)
        {
            AppendText(
                text,
                $"CATCH COST {CatchCost.ToString("+0;-0", CultureInfo.InvariantCulture)}");
        }

        if (VeilDurationSeconds != 0)
        {
            AppendText(
                text,
                $"VEIL {VeilDurationSeconds.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}s");
        }

        if (DragSeconds != 0)
        {
            AppendText(
                text,
                $"DRAG {DragSeconds.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}s");
        }

        if (DragPenaltySeconds != 0)
        {
            AppendText(
                text,
                $"DRAG HANGOVER {DragPenaltySeconds.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}s");
        }

        if (Math.Abs(EnemyStepScale - 1.0) > 0.0001)
        {
            // Shown as the change to the killer's pace, so a faster killer reads as a negative
            // number and cannot be mistaken for a bonus.
            var percent = (EnemyStepScale - 1.0) * 100;
            AppendText(
                text,
                $"KILLER PACE {percent.ToString("+0;-0", CultureInfo.InvariantCulture)}%");
        }

        if (Math.Abs(PlayerStepScale - 1.0) > 0.0001)
        {
            // Shown as the change to walking speed, so a slower player reads as a negative number.
            var percent = (1.0 - PlayerStepScale) * 100;
            AppendText(
                text,
                $"STEP {percent.ToString("+0;-0", CultureInfo.InvariantCulture)}%");
        }

        if (SprintStaminaScale != 1.0)
        {
            AppendText(
                text,
                $"SPRINT x{SprintStaminaScale.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        if (InvulnerabilitySeconds != 0)
        {
            AppendText(
                text,
                $"GRACE {InvulnerabilitySeconds.ToString("+0.#;-0.#", CultureInfo.InvariantCulture)}s");
        }

        if (MomentumMaxBonus > 0)
        {
            var percent = MomentumMaxBonus * 100;
            AppendText(
                text,
                $"MOMENTUM +{percent.ToString("0", CultureInfo.InvariantCulture)}%");
        }

        if (HealOnFloorClear > 0)
        {
            AppendText(text, $"HEAL/CLEAR +{HealOnFloorClear}");
        }

        return text.Length == 0 ? "NO CHANGE" : text.ToString();

        static void Append(StringBuilder builder, string label, int value)
        {
            if (value != 0)
            {
                AppendText(builder, $"{label} {value.ToString("+0;-0", CultureInfo.InvariantCulture)}");
            }
        }

        static void AppendText(StringBuilder builder, string entry)
        {
            if (builder.Length > 0)
            {
                builder.Append("   ");
            }

            builder.Append(entry);
        }
    }
}

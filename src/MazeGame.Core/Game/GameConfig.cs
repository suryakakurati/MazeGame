using System;
using System.Collections.ObjectModel;
using MazeGame.Core.Maze;

namespace MazeGame.Core.Game;

/// <summary>
/// Every tunable number in the game, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Game logic reads its numbers from here instead of hard-coding them, so difficulty can be
/// changed (or tested) without touching rules. Defaults describe the intended game:
/// a 15x15 maze with three keys, three barriers, two lures and a killer that takes a step
/// every six tenths of a second.
/// </para>
/// <para>
/// Construct with an object initializer and call <see cref="Validate"/> before use
/// (a <see cref="GameState"/> does that for you):
/// <c>new GameConfig { Rows = 21, Columns = 21, Seed = 7 }</c>.
/// </para>
/// <para>
/// The values are init-only, so a config can be shared safely once built — nothing can
/// change it mid-game. It is a record because cards compose a run's modifiers <i>over</i> a
/// floor's config with a <c>with</c> expression: every field is copied by the compiler, so a
/// new tuning number cannot be silently forgotten when a card is applied.
/// </para>
/// </remarks>
public sealed record GameConfig
{
    /// <summary>
    /// Gets the number of maze rows. Defaults to 15.
    /// </summary>
    public int Rows { get; init; } = 15;

    /// <summary>
    /// Gets the number of maze columns. Defaults to 15.
    /// </summary>
    public int Columns { get; init; } = 15;

    /// <summary>
    /// Gets how many keys must be collected before the exit opens. Defaults to 3.
    /// Zero is allowed and means the exit is open from the start.
    /// </summary>
    public int KeyCount { get; init; } = 3;

    /// <summary>
    /// Gets the seconds the killer needs to enter a normal cell. Defaults to 0.6, which is
    /// slow enough for the player to keep walking away. Must be positive.
    /// </summary>
    public double EnemyStepSeconds { get; init; } = 0.6;

    /// <summary>
    /// Gets how long the player waits between steps. Defaults to zero, which means movement is
    /// instant — the behaviour every rule test and tool relies on. The shipped floors override
    /// it (about 0.11 s, roughly five and a half times the killer's pace). Must be finite and
    /// not negative.
    /// </summary>
    /// <remarks>
    /// The rules stay grid-based: the player's cell changes the moment a step is accepted, and this
    /// value only gates when the *next* step may start. That is what lets the renderer slide the
    /// figure between cells without any rule ever seeing a position between two cells.
    /// </remarks>
    public double PlayerStepSeconds { get; init; }

    /// <summary>
    /// Gets how much faster sprinting is than walking. Defaults to 1.4 (40% faster). Must be at
    /// least 1 and finite.
    /// </summary>
    public double SprintMultiplier { get; init; } = 1.4;

    /// <summary>
    /// Gets how many seconds of sprinting a full stamina bar holds. Defaults to 1.2. Zero disables
    /// sprinting entirely. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// Sprint is a resource rather than a card: it makes the first moment of an escape better and
    /// the rest worse, and a run that spends it on feeling fast instead of breaking line of sight
    /// is a run that gets caught.
    /// </remarks>
    public double SprintStaminaSeconds { get; init; } = 1.2;

    /// <summary>
    /// Gets how long an empty stamina bar takes to refill. Defaults to 1.6. Must be finite and
    /// positive.
    /// </summary>
    public double SprintRecoverySeconds { get; init; } = 1.6;

    /// <summary>
    /// Gets how much of a speed bonus each straight step in a row earns, as a fraction. Zero, the
    /// default, switches momentum off entirely. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// Momentum is a run rule rather than a player mode: it only changes the length of the next
    /// step, so every rule still sees the player on one cell at a time. A card is what turns it on
    /// (see <c>momentum</c>); a run without the card pays nothing for turning.
    /// </remarks>
    public double MomentumRampPerStep { get; init; }

    /// <summary>
    /// Gets the most speed a straight run may earn, as a fraction. Zero switches momentum off.
    /// Defaults to zero. Must be finite and not negative.
    /// </summary>
    public double MomentumMaxBonus { get; init; }

    /// <summary>
    /// Gets how much slower the first step after a turn is, as a fraction. Defaults to zero, so
    /// turning costs nothing unless a card says so. Must be finite and not negative.
    /// </summary>
    public double TurnPenaltyScale { get; init; }

    /// <summary>
    /// Gets how long the turn penalty lasts. Defaults to zero. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// The penalty is a window rather than a single step, which is what makes a corner feel like
    /// a cost: the killer closes while the player is still recovering from the turn.
    /// </remarks>
    public double TurnPenaltySeconds { get; init; }

    /// <summary>
    /// Gets the powerup charges this floor starts with. Defaults to the full kit at its
    /// middle-of-the-run counts; a floor definition usually overrides it with the teaching ladder.
    /// </summary>
    public PowerupCharges Powerups { get; init; } = PowerupCharges.Default;

    /// <summary>
    /// Gets the run-scoped descent conditions in force on this floor, if any.
    /// </summary>
    /// <remarks>
    /// Conditions are composed into the floor's inputs rather than checked with an <c>if</c> in
    /// every rule, so an ordinary test floor has none and behaves exactly as it did before.
    /// </remarks>
    public IReadOnlyList<DescentCondition> Conditions { get; init; } = Array.Empty<DescentCondition>();

    /// <summary>
    /// Gets the powerup a Mute floor has taken away, or null when Mute is not active.
    /// </summary>
    /// <remarks>
    /// The campaign rotates this per floor, so Mute is a different missing button each time
    /// rather than a permanent hole in the kit.
    /// </remarks>
    public PowerupKind? MutedPowerup { get; init; }

    /// <summary>Gets a value indicating whether the first catch on the floor costs no heart.</summary>
    /// <remarks>
    /// The free catch still throws the hunter back and grants the grace window, so it is a whole
    /// miss rather than a smaller one. A card grants this, and the floor consumes it once.
    /// </remarks>
    public bool FirstCatchFree { get; init; }

    /// <summary>Gets a value indicating whether keys within one step are collected automatically.</summary>
    /// <remarks>
    /// The pull happens after a step or a blink, never on the same cell, so a key still has to be
    /// walked near. Drift and the exit unlock see the pickup at once.
    /// </remarks>
    public bool MagneticKeys { get; init; }

    /// <summary>Gets a value indicating whether the first fatal catch on the floor leaves one heart.</summary>
    /// <remarks>
    /// The revival is per floor, not per run: each new floor spends it again. It still resets the
    /// chase, so it buys a whole attempt rather than a single frame.
    /// </remarks>
    public bool ReviveOnce { get; init; }

    /// <summary>
    /// Gets how many blink charges taking a key returns. Defaults to zero.
    /// </summary>
    /// <remarks>
    /// A count rather than a flag, so two cards that each refund a blink per bloom refund two.
    /// </remarks>
    public int BlinkRefundPerKey { get; init; }

    /// <summary>
    /// Gets how many hearts a single catch costs. Defaults to 1. Must be at least 1, so a catch is
    /// always a real cost; a free catch is expressed by <see cref="FirstCatchFree"/> instead.
    /// </summary>
    /// <remarks>
    /// <see cref="ForFloor"/> adds one heart to this every four floors and stops at
    /// <see cref="MaxFloorCatchCost"/>: floors 1-4 cost one heart, 5-8 two, 9-12 three, 13-16 four
    /// and 17-20 five. Cards such as Glass Cannon add to the same number through
    /// <see cref="RunModifiers"/>.
    /// </remarks>
    public int CatchCost { get; init; } = 1;

    /// <summary>
    /// Gets the most a floor's own depth may raise <see cref="CatchCost"/> to. Five from floor 17
    /// on: past that the curve stops and only cards change the price of a mistake.
    /// </summary>
    public const int MaxFloorCatchCost = 5;

    /// <summary>
    /// Gets how far the floor's blooms glow after a catch. Defaults to zero, which leaves them
    /// dark; a card is what lights them.
    /// </summary>
    /// <remarks>
    /// The glow is a rule, not a drawing effect: <see cref="GameState.IsVisible"/> includes
    /// <b>every</b> bloom on the floor for <see cref="BloomGlowSeconds"/>, and a reach of one
    /// reveals each bloom's own tile and nothing else. A reach above one lights that many steps
    /// around every glowing bloom. A renderer cannot reveal them on its own, and a test can read
    /// the reveal without a window.
    /// </remarks>
    public int BloomGlowReach { get; init; }

    /// <summary>
    /// Gets how long a bloom glow lasts after a catch, in seconds. Defaults to 2. Ignored when
    /// <see cref="BloomGlowReach"/> is zero. Must be finite and not negative.
    /// </summary>
    public double BloomGlowSeconds { get; init; } = 2.0;

    /// <summary>
    /// Gets how far, in tiles, a bloom is felt through the dark. Defaults to zero, which leaves
    /// the blooms to the torch; a legendary relic is what lights them.
    /// </summary>
    /// <remarks>
    /// This is straight-line distance in tiles rather than walking distance: a bloom within the
    /// radius is visible on its own cell even through a wall, and no cell around it is revealed.
    /// It is a passive sense, recomputed on every step, so the flowers appear and disappear as
    /// the player walks through them. Must not be negative.
    /// </remarks>
    public int BloomSenseRadius { get; init; }

    /// <summary>Gets how long a completed veil lasts. Defaults to 3 seconds.</summary>
    public double VeilDurationSeconds { get; init; } = 3.0;

    /// <summary>Gets how long Drag slows the hunt. Defaults to 3 seconds.</summary>
    public double DragSeconds { get; init; } = 3.0;

    /// <summary>Gets how long the hunt is faster after Drag ends. Defaults to 5 seconds.</summary>
    public double DragPenaltySeconds { get; init; } = 5.0;

    /// <summary>Gets how far, in cells, a lantern may be thrown. Defaults to 5.</summary>
    public int LanternRange { get; init; } = 5;

    /// <summary>
    /// Gets how many hunters stalk the floor. Defaults to 1. Must be at least 1, and the maze must
    /// hold the player, every hunter, the exit and the keys.
    /// </summary>
    /// <remarks>
    /// Every hunter runs the same rule — target the cheaper of the player and a wisp, walk the
    /// cheapest route, tear seals — but each keeps its own cell, route and timers, so two of them
    /// are not one faster monster. See <see cref="GameState.Enemies"/>.
    /// </remarks>
    public int HunterCount { get; init; } = 1;

    /// <summary>
    /// Gets the radius, in walking steps, that the player can see. Defaults to 3. Zero means
    /// the player sees only their own cell, and walls block sight, so this is a torch range
    /// down corridors rather than a circle. Must not be negative.
    /// </summary>
    public int VisionRadius { get; init; } = 3;

    /// <summary>
    /// Gets the radius, in straight-line cells, of the light that falls on the rock around the
    /// player regardless of walls. Defaults to 2. Must not be negative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sight itself travels down corridors, which is what makes a wall block a view — but it also
    /// means the far side of a wall was never drawn, so a wall you were standing against had no
    /// face and the corridor read as a hole. This radius is the spill of the lantern onto the
    /// stone immediately around the player: it reveals the cells pressed against the walls, not the
    /// corridors beyond them.
    /// </para>
    /// <para>
    /// It is capped by the torch in force (see <see cref="GameState.EffectiveVisionRadius"/>), so a
    /// blind floor stays blind; it is not a way to see through the maze.
    /// </para>
    /// </remarks>
    public int NearSightRadius { get; init; } = 2;

    /// <summary>
    /// Gets how many extra interior walls the generator removes to create loops. Defaults to
    /// 8, which gives a 15x15 maze a handful of alternate routes rather than a warren. Zero
    /// keeps the maze perfect (exactly one route between any two cells). Must not be negative.
    /// </summary>
    /// <remarks>
    /// Loops matter for play, not just for looks: a perfect maze leaves barriers with nothing
    /// to redirect the killer onto, while a braided one lets a well-placed barrier cost the
    /// killer real distance.
    /// </remarks>
    public int ExtraLoops { get; init; } = 8;

    /// <summary>
    /// Gets the share of the maze's dead ends that are braided open, as a percentage. Defaults
    /// to 55, which leaves roughly 22 dead ends on a 15x15 maze instead of 57 and turns most
    /// barrier placements into real detours. Zero keeps every dead end. Must be between 0 and
    /// 100 inclusive.
    /// </summary>
    /// <remarks>
    /// This is the setting that decides how often a chase ends in a corner. It is separate from
    /// <see cref="ExtraLoops"/> because the two do different jobs: braiding removes traps, extra
    /// loops add shortcuts across the map.
    /// </remarks>
    public int DeadEndBraidPercent { get; init; } = 55;

    /// <summary>
    /// Gets the outline the mazes are carved into. Defaults to the full rectangle.
    /// </summary>
    /// <remarks>
    /// A floor's silhouette is a rule, not decoration: rock outside the shape cannot be walked or
    /// seen, so an oval or a blob changes which routes exist and how much of the floor a barrier
    /// can be used to cut off. It is the cheapest way to make two floors with the same size and the
    /// same key count feel like different places.
    /// </remarks>
    public ShapeKind Shape { get; init; } = ShapeKind.Rect;

    /// <summary>
    /// Gets the one exclusive rule this floor has, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// A floor rule is content, not tuning: no card and no descent condition may grant one, and it
    /// is read in exactly one place in <see cref="GameState"/>. Null (the default) is every floor
    /// that came before rules existed, which is what keeps the rule tests' feel identical.
    /// </remarks>
    public FloorRule? Rule { get; init; }

    /// <summary>
    /// Gets the number of the floor this config describes, or zero for a game that is not part of
    /// a run.
    /// </summary>
    /// <remarks>
    /// Only the story reads it: the page on the cleared card and the lines the Echo speaks are
    /// looked up by floor number. A hand-built test floor leaves it at zero and gets neither.
    /// </remarks>
    public int FloorNumber { get; init; }

    /// <summary>
    /// Gets a value indicating whether the floor carries an Echo: a pale standing stone in a dead
    /// end that pauses the hunt, tells the floor's story and restores a heart.
    /// </summary>
    /// <remarks>
    /// <see cref="ForFloor"/> switches it on for every shipped floor; the default is off, so the
    /// rule tests' hand-built corridors keep their plain "step and tick" behaviour.
    /// </remarks>
    public bool EchoEnabled { get; init; }

    /// <summary>
    /// Gets the relic this floor's altar holds, or null for no altar.
    /// </summary>
    /// <remarks>
    /// The campaign stocks the relic when it builds the floor, skipping the relics the run already
    /// holds. The default is null, so a hand-built test floor has no altar to walk to.
    /// </remarks>
    public RelicKind? Relic { get; init; }

    /// <summary>
    /// Gets the radius, in cells, lit by the killer itself. Defaults to 2. Zero means only the
    /// killer's own cell glows. Must not be negative.
    /// </summary>
    public int EnemyAuraRadius { get; init; } = 2;

    /// <summary>
    /// Gets how many walking steps away the killer may be before its own light becomes visible
    /// to the player. Defaults to 6, roughly three and a half seconds of running. Zero means
    /// the glow appears only once the killer reaches the player's cell. Must not be negative.
    /// </summary>
    public int EnemyAuraDistance { get; init; } = 6;

    /// <summary>
    /// Gets how many hearts the player enters the floor with. Defaults to 3.
    /// </summary>
    /// <remarks>
    /// A catch costs exactly one heart and does not end the floor. Carrying damage between
    /// floors is just a smaller <c>StartingHearts</c> on the next floor's config, so the run's
    /// health lives in one number rather than in a second parallel object.
    /// </remarks>
    public int StartingHearts { get; init; } = 3;

    /// <summary>
    /// Gets how many heart slots the floor's HUD draws and how many hearts its design expects.
    /// Defaults to 3.
    /// </summary>
    /// <remarks>
    /// <b>This is no longer a rule.</b> A run has no heart ceiling: a heal adds its hearts, and
    /// hearts carry between floors without being clipped. The number survives because floors are
    /// designed around it and because the footer draws that many slots; a healed run's
    /// <see cref="StartingHearts"/> may legitimately be larger.
    /// </remarks>
    public int MaxHearts { get; init; } = 3;

    /// <summary>
    /// Gets how many seconds the player cannot lose another heart after a catch. Defaults to
    /// 1.5. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// This is what makes a single mistake cost one heart instead of emptying the bar while the
    /// killer stands on top of the player. During the window the killer waits at the player's
    /// cell instead of sharing it, so the pause is visible rather than invisible.
    /// </remarks>
    public double InvulnerabilitySeconds { get; init; } = 1.5;

    /// <summary>
    /// Gets how many walking steps away the killer is placed after a catch. Defaults to 8.
    /// Must be at least 1.
    /// </summary>
    /// <remarks>
    /// Placement prefers a reachable dead end at least this far away, so the player gets a real
    /// head start; when the whole floor is smaller than that, it uses the furthest reachable
    /// cell instead. Damage therefore costs tempo, never the run's progress.
    /// </remarks>
    public int RespawnDistance { get; init; } = 8;

    /// <summary>
    /// Gets how many seconds the killer is stunned after a catch. Defaults to 1.5. Must be
    /// finite and not negative.
    /// </summary>
    /// <remarks>
    /// While stunned the killer neither moves nor builds up its next step, so the stun is a real
    /// window for the player to walk away rather than a delayed first step.
    /// </remarks>
    public double RespawnStunSeconds { get; init; } = 1.5;

    /// <summary>
    /// Gets the seed for maze generation and entity placement, or <see langword="null"/>
    /// for a different game on every run. The same seed always produces the same game.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Checks that the settings describe a game that can actually be played.
    /// </summary>
    /// <remarks>
    /// The size rule is the interesting one: the player, the killer, the exit and every key
    /// need their own cell, so the maze must have at least <c>3 + KeyCount</c> cells. Catching
    /// that here turns a confusing "no free cell" failure at placement time into one clear
    /// error before anything is built.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any individual setting is outside its allowed range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the maze is too small to hold the player, killer, exit and keys.
    /// </exception>
    public void Validate()
    {
        Powerups.Validate();

        if (Conditions is null)
        {
            throw new ArgumentException("The condition list cannot be null.", nameof(Conditions));
        }

        var seenConditions = new HashSet<DescentCondition>();

        foreach (var condition in Conditions)
        {
            if (!Enum.IsDefined(condition))
            {
                throw new ArgumentOutOfRangeException(nameof(Conditions), condition, "Unknown descent condition.");
            }

            if (!seenConditions.Add(condition))
            {
                throw new ArgumentException($"The floor carries {condition} twice.", nameof(Conditions));
            }
        }

        if (MutedPowerup is { } muted && !Enum.IsDefined(muted))
        {
            throw new ArgumentOutOfRangeException(nameof(MutedPowerup), muted, "Unknown powerup to mute.");
        }

        if (CatchCost < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CatchCost), CatchCost, "A catch must cost at least one heart.");
        }

        if (BloomGlowReach < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BloomGlowReach),
                BloomGlowReach,
                "A bloom glow cannot have a negative radius.");
        }

        if (BloomSenseRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BloomSenseRadius),
                BloomSenseRadius,
                "A bloom sense cannot have a negative radius.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(BloomGlowSeconds >= 0) || double.IsInfinity(BloomGlowSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BloomGlowSeconds),
                BloomGlowSeconds,
                "A bloom glow must last a finite, non-negative number of seconds.");
        }

        if (FloorNumber < 0 || FloorNumber > FloorCatalog.ShippedFloorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FloorNumber),
                FloorNumber,
                $"A floor is numbered 0 (no run) or 1 to {FloorCatalog.ShippedFloorCount}.");
        }

        if (EchoEnabled && FloorNumber == 0)
        {
            throw new ArgumentException(
                "An Echo needs a floor number, or it has no lines to speak.",
                nameof(EchoEnabled));
        }

        if (!(VeilDurationSeconds > 0) || double.IsInfinity(VeilDurationSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(VeilDurationSeconds),
                VeilDurationSeconds,
                "A veil must last a finite, positive number of seconds.");
        }

        if (!(DragSeconds >= 0) || double.IsInfinity(DragSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(DragSeconds), DragSeconds, "Drag time must be finite and not negative.");
        }

        if (!(DragPenaltySeconds >= 0) || double.IsInfinity(DragPenaltySeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DragPenaltySeconds),
                DragPenaltySeconds,
                "Drag's hangover must be finite and not negative.");
        }

        if (LanternRange < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LanternRange), LanternRange, "A lantern cannot have a negative range.");
        }

        if (Rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows), Rows, "A game needs at least one row.");
        }

        if (Columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Columns), Columns, "A game needs at least one column.");
        }

        if (KeyCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KeyCount), KeyCount, "The key count cannot be negative.");
        }

        if (DeadEndBraidPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DeadEndBraidPercent),
                DeadEndBraidPercent,
                "The braided share must be a percentage between 0 and 100.");
        }

        if (StartingHearts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartingHearts),
                StartingHearts,
                "A floor must start with at least one heart; a run with none is already over.");
        }

        if (MaxHearts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxHearts),
                MaxHearts,
                "The heart slots are at least one; the run has no ceiling to be below it.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(InvulnerabilitySeconds >= 0) || double.IsInfinity(InvulnerabilitySeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(InvulnerabilitySeconds),
                InvulnerabilitySeconds,
                "The invulnerability window must be a finite, non-negative number of seconds.");
        }

        if (RespawnDistance < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RespawnDistance),
                RespawnDistance,
                "After a catch the killer must be placed at least one step away.");
        }

        if (!(RespawnStunSeconds >= 0) || double.IsInfinity(RespawnStunSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RespawnStunSeconds),
                RespawnStunSeconds,
                "The killer's stun after a catch must be a finite, non-negative number of seconds.");
        }

        // Written as "not greater than zero" so NaN is rejected as well as 0 and negatives.
        if (!(EnemyStepSeconds > 0) || double.IsInfinity(EnemyStepSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyStepSeconds),
                EnemyStepSeconds,
                "The killer's step time must be a finite, positive number of seconds.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(PlayerStepSeconds >= 0) || double.IsInfinity(PlayerStepSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(PlayerStepSeconds),
                PlayerStepSeconds,
                "The player's step time must be a finite, non-negative number of seconds.");
        }

        if (!(SprintMultiplier >= 1) || double.IsInfinity(SprintMultiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SprintMultiplier),
                SprintMultiplier,
                "Sprinting cannot be slower than walking; the multiplier must be at least 1.");
        }

        if (!(SprintStaminaSeconds >= 0) || double.IsInfinity(SprintStaminaSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SprintStaminaSeconds),
                SprintStaminaSeconds,
                "The sprint stamina must be a finite, non-negative number of seconds.");
        }

        if (!(SprintRecoverySeconds > 0) || double.IsInfinity(SprintRecoverySeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SprintRecoverySeconds),
                SprintRecoverySeconds,
                "An empty stamina bar must refill in a finite, positive number of seconds.");
        }

        if (!(MomentumRampPerStep >= 0) || double.IsInfinity(MomentumRampPerStep))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MomentumRampPerStep),
                MomentumRampPerStep,
                "The momentum ramp must be a finite, non-negative fraction.");
        }

        if (!(MomentumMaxBonus >= 0) || double.IsInfinity(MomentumMaxBonus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MomentumMaxBonus),
                MomentumMaxBonus,
                "The momentum cap must be a finite, non-negative fraction.");
        }

        if (!(TurnPenaltyScale >= 0) || double.IsInfinity(TurnPenaltyScale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TurnPenaltyScale),
                TurnPenaltyScale,
                "The turn penalty must be a finite, non-negative fraction.");
        }

        if (!(TurnPenaltySeconds >= 0) || double.IsInfinity(TurnPenaltySeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TurnPenaltySeconds),
                TurnPenaltySeconds,
                "The turn penalty must last a finite, non-negative number of seconds.");
        }

        if (HunterCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HunterCount),
                HunterCount,
                "A floor needs at least one hunter.");
        }

        if (VisionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VisionRadius),
                VisionRadius,
                "The vision radius cannot be negative.");
        }

        if (NearSightRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(NearSightRadius),
                NearSightRadius,
                "The near-sight radius cannot be negative.");
        }

        if (ExtraLoops < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExtraLoops),
                ExtraLoops,
                "The number of extra loops cannot be negative.");
        }

        if (EnemyAuraRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyAuraRadius),
                EnemyAuraRadius,
                "The killer's glow radius cannot be negative.");
        }

        if (EnemyAuraDistance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnemyAuraDistance),
                EnemyAuraDistance,
                "The distance at which the killer's glow appears cannot be negative.");
        }

        if (BlinkRefundPerKey < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BlinkRefundPerKey),
                BlinkRefundPerKey,
                "A key cannot cost a blink charge.");
        }

        // long arithmetic: a huge row count must not overflow into a "big enough" answer.
        var requiredCells = 2L + HunterCount + KeyCount;
        if ((long)Rows * Columns < requiredCells)
        {
            throw new ArgumentException(
                $"A {Rows}x{Columns} maze has {Rows * (long)Columns} cells, but the player, {HunterCount} hunter(s), " +
                $"the exit and {KeyCount} key(s) need {requiredCells} different cells.",
                nameof(KeyCount));
        }
    }

    /// <summary>
    /// Returns the settings for one floor of a run: this config's shared rules (vision, barriers,
    /// wisps, braiding, aura, seed) with the floor's size, key count, killer pace and hearts.
    /// </summary>
    /// <param name="floor">The floor to derive settings for.</param>
    /// <param name="startingHearts">
    /// Hearts the player brings to the floor. At least one; the run has no ceiling, so a healed
    /// run may bring more than the floor's design expects.
    /// </param>
    /// <param name="conditions">
    /// The descent conditions in force, or null for none. Mute adds a heart and takes a tool away.
    /// </param>
    /// <returns>A new config; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the floor is invalid or the carried hearts do not fit the floor's ceiling.
    /// </exception>
    public GameConfig ForFloor(
        FloorDefinition floor,
        int startingHearts,
        IReadOnlyList<DescentCondition>? conditions = null)
    {
        ArgumentNullException.ThrowIfNull(floor);
        floor.Validate();

        var carried = conditions ?? Array.Empty<DescentCondition>();
        var mute = carried.Contains(DescentCondition.Mute);
        var heartBonus = mute ? 1 : 0;
        var ceiling = floor.MaxHearts + heartBonus;

        if (startingHearts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingHearts),
                startingHearts,
                $"Floor {floor.Number} must start with at least one heart.");
        }

        var hearts = startingHearts + heartBonus;
        var charges = floor.Powerups ?? PowerupCharges.ForFloor(floor.Number);
        PowerupKind? muted = null;

        if (mute)
        {
            // A different missing button each floor, deterministic from the floor number so a
            // seeded run and a replay agree without another random stream.
            muted = (PowerupKind)((floor.Number - 1) % Enum.GetValues<PowerupKind>().Length);
            charges = charges.With(muted.Value, 0);
        }

        var floorConfig = new GameConfig
        {
            // Per-floor overrides.
            Rows = floor.Rows,
            Columns = floor.Columns,
            KeyCount = floor.KeyCount,
            HunterCount = floor.HunterCount,
            EnemyStepSeconds = floor.EnemyStepSeconds,
            PlayerStepSeconds = floor.PlayerStepSeconds,
            StartingHearts = hearts,

            // The hunt deepens every four floors and then holds: 1-4 cost one heart, 5-8 two,
            // 9-12 three, 13-16 four and 17-20 five, which is the cap. A card's own surcharge is
            // added over this by RunModifiers, so cards can still make a catch dearer.
            CatchCost = Math.Min(MaxFloorCatchCost, CatchCost + ((floor.Number - 1) / 4)),

            // The design's own slot count. Hearts have no ceiling at runtime, so this is a
            // description of the floor rather than a limit on the hearts a run carries in.
            MaxHearts = ceiling,

            // The floor's own kit, where it has one; otherwise the teaching ladder. Mute has
            // already removed its tool above.
            Powerups = charges,
            Conditions = new ReadOnlyCollection<DescentCondition>(new List<DescentCondition>(carried)),
            MutedPowerup = muted,

            // The floor's own gimmick, where it has one; otherwise the run's shared rule.
            VisionRadius = floor.VisionRadius ?? VisionRadius,
            NearSightRadius = NearSightRadius,
            ExtraLoops = floor.ExtraLoops ?? ExtraLoops,
            DeadEndBraidPercent = floor.DeadEndBraidPercent ?? DeadEndBraidPercent,
            Shape = floor.Shape ?? Shape,
            Rule = floor.Rule ?? Rule,

            // The floor's identity, so the story knows which page it is on. The Echo is switched
            // on by GameFactory, the one place a floor is built for a real run: a hand-built test
            // floor keeps the plain "step and tick" behaviour its tests were written against.
            FloorNumber = floor.Number,
            Relic = floor.Relic,
            EnemyAuraRadius = floor.EnemyAuraRadius ?? EnemyAuraRadius,
            EnemyAuraDistance = floor.EnemyAuraDistance ?? EnemyAuraDistance,
            RespawnDistance = floor.RespawnDistance ?? RespawnDistance,
            InvulnerabilitySeconds = floor.InvulnerabilitySeconds ?? InvulnerabilitySeconds,
            RespawnStunSeconds = floor.RespawnStunSeconds ?? RespawnStunSeconds,

            // Shared rules: everything else the run tunes once and every floor inherits.
            BloomGlowReach = BloomGlowReach,
            BloomGlowSeconds = BloomGlowSeconds,
            BloomSenseRadius = BloomSenseRadius,
            SprintMultiplier = SprintMultiplier,
            SprintStaminaSeconds = SprintStaminaSeconds,
            MomentumRampPerStep = MomentumRampPerStep,
            MomentumMaxBonus = MomentumMaxBonus,
            TurnPenaltyScale = TurnPenaltyScale,
            TurnPenaltySeconds = TurnPenaltySeconds,
            SprintRecoverySeconds = SprintRecoverySeconds,
            Seed = Seed,
        };

        // Fail here rather than at the first catch: a floor whose carried hearts exceed its
        // ceiling, or a maze too small for its keys, is a content bug.
        floorConfig.Validate();
        return floorConfig;
    }
}

using System;

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
    /// Gets how many normal cells a spent (already cracked) barriered cell is worth to the
    /// killer. Defaults to 5, against a normal cell's cost of 1. It is both the Dijkstra weight
    /// of that cell and the number of normal steps the killer needs to enter it, so a spent
    /// barrier always costs real time: with the default step time that is a 3-second pause.
    /// Must be at least 1: barriers slow the killer down, they never make a cell impassable.
    /// </summary>
    /// <remarks>
    /// Fresh barriers are sealed instead and are governed by <see cref="BarrierSealSeconds"/>,
    /// <see cref="BarrierBreakSeconds"/> and <see cref="SealedBarrierCost"/>; this value is what
    /// a barrier is worth once it has cracked.
    /// </remarks>
    public int ObstacleCost { get; init; } = 5;

    /// <summary>
    /// Gets how many barriers the player may place in one game. Defaults to 3.
    /// </summary>
    public int BarrierBudget { get; init; } = 3;

    /// <summary>
    /// Gets how long a freshly placed barrier stays sealed: the killer treats a sealed barrier
    /// as a wall and looks for another way round. Defaults to 10 seconds, deliberately longer
    /// than the walk from the killer's usual distance so a barrier put on its route is still
    /// sealed when it arrives. Zero means barriers are never sealed and only ever cost
    /// <see cref="ObstacleCost"/>. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// After the seal lapses the barrier cracks into a spent husk that is still expensive to
    /// cross, so a barrier has two phases: a short wall, then a permanent speed bump.
    /// </remarks>
    public double BarrierSealSeconds { get; init; } = 10.0;

    /// <summary>
    /// Gets how long the killer takes to tear a sealed barrier apart when the seal is the only
    /// way to reach the player. Defaults to 3 seconds, so a barrier that truly cuts the route
    /// is still broken through rather than soft-locking the game. Must be finite and greater
    /// than zero.
    /// </summary>
    public double BarrierBreakSeconds { get; init; } = 3.0;

    /// <summary>
    /// Gets what a sealed barrier is worth to the killer's pathfinding, in normal cells.
    /// Defaults to 40, which is far more than any detour a 15x15 maze offers, so the killer
    /// takes another route while the seal holds and only attacks the barrier when there is
    /// none. Must be at least 1.
    /// </summary>
    public int SealedBarrierCost { get; init; } = 40;

    /// <summary>
    /// Gets how many wisps the player may place in one game. Defaults to 2.
    /// </summary>
    public int LureCount { get; init; } = 2;

    /// <summary>
    /// Gets how many seconds a placed wisp keeps glowing. Defaults to 6, which is roughly ten
    /// killer steps: long enough to pull it down a side branch, short enough that it cannot be
    /// parked. Must be finite and greater than zero.
    /// </summary>
    public double LureSeconds { get; init; } = 6.0;

    /// <summary>
    /// Gets how long the killer is stunned when it reaches a wisp. Defaults to zero, which means
    /// the decoy only costs it the trip. Must be finite and not negative.
    /// </summary>
    /// <remarks>
    /// A stun is the same window as the one after a catch: the killer neither moves nor banks
    /// progress towards its next step, so a card that grants one buys the player real distance
    /// rather than a step that lands the moment the stun ends.
    /// </remarks>
    public double LureStunSeconds { get; init; }

    /// <summary>
    /// Gets how many steps of vision a burning wisp costs. Defaults to zero, so the wisp costs
    /// nothing to watch. Must not be negative.
    /// </summary>
    /// <remarks>
    /// This is the drawback half of a card, not a rule of the wisp: the wisp is a beacon, and a
    /// card may make watching it expensive. Visibility is recomputed when a wisp starts and when
    /// it clears, so the penalty really does come and go with the glow.
    /// </remarks>
    public int LureVisionPenalty { get; init; }

    /// <summary>
    /// Gets the seconds the killer needs to enter a normal cell. Defaults to 0.6, which is
    /// slow enough for the player to keep walking away. A barriered cell multiplies this by
    /// <see cref="ObstacleCost"/>. Must be positive.
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
    /// Gets the radius, in walking steps, that the player can see. Defaults to 3. Zero means
    /// the player sees only their own cell, and walls block sight, so this is a torch range
    /// down corridors rather than a circle. Must not be negative.
    /// </summary>
    public int VisionRadius { get; init; } = 3;

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
    /// Gets the most hearts the player can hold on this floor. Defaults to 3.
    /// </summary>
    /// <remarks>
    /// This is the cap cards may raise, and it must be at least <see cref="StartingHearts"/>
    /// so a floor cannot start above its own ceiling. It is separate from
    /// <see cref="StartingHearts"/> because a run can arrive on a floor wounded.
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

        if (ObstacleCost < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ObstacleCost),
                ObstacleCost,
                "A barrier must cost at least 1 to enter; it can slow the killer but never block it.");
        }

        if (BarrierBudget < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierBudget),
                BarrierBudget,
                "The barrier budget cannot be negative.");
        }

        if (SealedBarrierCost < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SealedBarrierCost),
                SealedBarrierCost,
                "A sealed barrier must cost at least 1 to enter; it is a detour, never a hole in the map.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(BarrierSealSeconds >= 0) || double.IsInfinity(BarrierSealSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierSealSeconds),
                BarrierSealSeconds,
                "The seal time must be a finite, non-negative number of seconds.");
        }

        if (!(BarrierBreakSeconds > 0) || double.IsInfinity(BarrierBreakSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BarrierBreakSeconds),
                BarrierBreakSeconds,
                "The time to break a sealed barrier must be a finite, positive number of seconds.");
        }

        if (LureCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureCount),
                LureCount,
                "The wisp count cannot be negative.");
        }

        if (!(LureSeconds > 0) || double.IsInfinity(LureSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureSeconds),
                LureSeconds,
                "A wisp must last a finite, positive number of seconds.");
        }

        // Written as "not greater than or equal to zero" so NaN is rejected as well as negatives.
        if (!(LureStunSeconds >= 0) || double.IsInfinity(LureStunSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureStunSeconds),
                LureStunSeconds,
                "The stun a wisp buys must be a finite, non-negative number of seconds.");
        }

        if (LureVisionPenalty < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LureVisionPenalty),
                LureVisionPenalty,
                "A wisp cannot grant vision; the penalty is a non-negative number of steps.");
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

        if (MaxHearts < StartingHearts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxHearts),
                MaxHearts,
                "The heart ceiling cannot be below the hearts the floor starts with.");
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

        if (VisionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VisionRadius),
                VisionRadius,
                "The vision radius cannot be negative.");
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

        // long arithmetic: a huge row count must not overflow into a "big enough" answer.
        var requiredCells = 3L + KeyCount;
        if ((long)Rows * Columns < requiredCells)
        {
            throw new ArgumentException(
                $"A {Rows}x{Columns} maze has {Rows * (long)Columns} cells, but the player, killer, exit and " +
                $"{KeyCount} key(s) need {requiredCells} different cells.",
                nameof(KeyCount));
        }
    }

    /// <summary>
    /// Returns the settings for one floor of a run: this config's shared rules (vision, barriers,
    /// wisps, braiding, aura, seed) with the floor's size, key count, killer pace and hearts.
    /// </summary>
    /// <param name="floor">The floor to derive settings for.</param>
    /// <param name="startingHearts">
    /// Hearts the player brings to the floor. Must be between 1 and <see cref="FloorDefinition.MaxHearts"/>.
    /// </param>
    /// <returns>A new config; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="floor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the floor is invalid or the carried hearts do not fit the floor's ceiling.
    /// </exception>
    public GameConfig ForFloor(FloorDefinition floor, int startingHearts)
    {
        ArgumentNullException.ThrowIfNull(floor);
        floor.Validate();

        var floorConfig = new GameConfig
        {
            // Per-floor overrides.
            Rows = floor.Rows,
            Columns = floor.Columns,
            KeyCount = floor.KeyCount,
            EnemyStepSeconds = floor.EnemyStepSeconds,
            PlayerStepSeconds = floor.PlayerStepSeconds,
            StartingHearts = startingHearts,
            MaxHearts = floor.MaxHearts,

            // The floor's own gimmick, where it has one; otherwise the run's shared rule.
            BarrierBudget = floor.BarrierBudget ?? BarrierBudget,
            LureCount = floor.LureCount ?? LureCount,
            LureSeconds = floor.LureSeconds ?? LureSeconds,
            VisionRadius = floor.VisionRadius ?? VisionRadius,
            ExtraLoops = floor.ExtraLoops ?? ExtraLoops,
            DeadEndBraidPercent = floor.DeadEndBraidPercent ?? DeadEndBraidPercent,
            EnemyAuraRadius = floor.EnemyAuraRadius ?? EnemyAuraRadius,
            EnemyAuraDistance = floor.EnemyAuraDistance ?? EnemyAuraDistance,
            RespawnDistance = floor.RespawnDistance ?? RespawnDistance,
            InvulnerabilitySeconds = floor.InvulnerabilitySeconds ?? InvulnerabilitySeconds,
            RespawnStunSeconds = floor.RespawnStunSeconds ?? RespawnStunSeconds,

            // Shared rules: everything else the run tunes once and every floor inherits.
            ObstacleCost = ObstacleCost,
            BarrierSealSeconds = BarrierSealSeconds,
            BarrierBreakSeconds = BarrierBreakSeconds,
            SealedBarrierCost = SealedBarrierCost,
            LureStunSeconds = LureStunSeconds,
            LureVisionPenalty = LureVisionPenalty,
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

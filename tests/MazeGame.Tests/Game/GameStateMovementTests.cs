using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies timed steps, the one-input buffer and sprint — the parts of movement that are timing
/// rather than position.
/// </summary>
/// <remarks>
/// The corridor fixture walks east along one row, so a step is always legal in the same direction
/// and a wall is always available as the illegal case. The killer's step time is huge by default so
/// it never interferes: these tests are about the player's cadence, not the chase.
/// </remarks>
public sealed class GameStateMovementTests
{
    private const double Step = 0.2;

    private const double Frame = 1.0 / 60.0;

    [Fact]
    public void AnInstantFloor_MovesOnEveryCall()
    {
        // The default step time is zero, which is the behaviour every rule test was written
        // against. Timed movement must not break instant movement; it is opt-in per floor.
        var state = Corridor(playerStepSeconds: 0);

        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(state.Maze.GetCell(0, 2), state.Player);
        Assert.False(state.IsPlayerStepping);
    }

    [Fact]
    public void ATimedFloor_MovesAtOnceThenRefusesASecondStep()
    {
        var state = Corridor();

        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(state.Maze.GetCell(0, 1), state.Player);
        Assert.True(state.IsPlayerStepping);

        // The press is remembered rather than refused outright …
        Assert.False(state.TryMove(Direction.East));
        Assert.Equal(state.Maze.GetCell(0, 1), state.Player);

        // … and spent when the step's cooldown runs out.
        state.Tick(Step);
        Assert.Equal(state.Maze.GetCell(0, 2), state.Player);
    }

    [Fact]
    public void OnlyOnePressIsBuffered_SoMashingCannotOutrunTheCadence()
    {
        var state = Corridor();

        Assert.True(state.TryMove(Direction.East));
        Assert.False(state.TryMove(Direction.East));
        Assert.False(state.TryMove(Direction.East));
        Assert.False(state.TryMove(Direction.East));

        state.Tick(Step);

        // One buffered step, not three: the run after it must wait its own cooldown.
        Assert.Equal(state.Maze.GetCell(0, 2), state.Player);
        Assert.True(state.IsPlayerStepping);
    }

    [Fact]
    public void ABufferedStepIntoAWall_IsDroppedRatherThanFiredLater()
    {
        var state = Corridor();

        Assert.True(state.TryMove(Direction.East));
        Assert.False(state.TryMove(Direction.North));

        state.Tick(Step);

        // Still on the same cell: a press aimed at rock does not sit in the buffer.
        Assert.Equal(state.Maze.GetCell(0, 1), state.Player);
        Assert.False(state.IsPlayerStepping);

        state.Tick(Step);
        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(state.Maze.GetCell(0, 2), state.Player);
    }

    [Fact]
    public void TheStepFraction_SlidesFromThePreviousCell()
    {
        var state = Corridor();
        var from = state.Player;

        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(from, state.PreviousPlayer);
        Assert.Equal(0.0, state.PlayerStepFraction, precision: 9);

        state.Tick(Step / 2);
        Assert.Equal(0.5, state.PlayerStepFraction, precision: 9);

        state.Tick(Step / 2);
        Assert.False(state.IsPlayerStepping);
        Assert.Equal(1.0, state.PlayerStepFraction, precision: 9);
    }

    [Fact]
    public void TheKillersStepFraction_SlidesOverItsOwnStepTime()
    {
        var state = Corridor(enemyStepSeconds: 1.0);
        var killer = state.Enemy;

        state.Tick(1.0);

        Assert.NotEqual(killer, state.Enemy);
        Assert.Equal(killer, state.PreviousEnemy);
        Assert.Equal(0.0, state.EnemyStepFraction, precision: 9);

        state.Tick(0.5);
        Assert.Equal(0.5, state.EnemyStepFraction, precision: 9);
    }

    [Fact]
    public void Sprinting_ShortensTheWaitBetweenSteps()
    {
        var walking = Corridor();
        var sprinting = Corridor(sprintMultiplier: 2.0);

        Assert.True(walking.TryMove(Direction.East));
        sprinting.SetSprinting(true);
        Assert.True(sprinting.IsSprinting);
        Assert.True(sprinting.TryMove(Direction.East));

        walking.Tick(Step / 2);
        sprinting.Tick(Step / 2);

        Assert.True(walking.IsPlayerStepping);
        Assert.False(sprinting.IsPlayerStepping);
    }

    [Fact]
    public void HoldingSprintOnTheSpot_CostsNothing()
    {
        var state = Corridor(sprintStaminaSeconds: 1.0, sprintRecoverySeconds: 10.0);
        state.SetSprinting(true);

        state.Tick(0.5);

        // A player waiting to dash is not sprinting; draining here would empty the bar before
        // they ever set off.
        Assert.True(state.IsSprinting);
        Assert.Equal(1.0, state.SprintStaminaFraction, precision: 9);
    }

    [Fact]
    public void Sprinting_DrainsStamina_OnlyWhileStepping()
    {
        var state = Corridor(playerStepSeconds: 1.0, sprintStaminaSeconds: 1.0, sprintRecoverySeconds: 10.0);
        state.SetSprinting(true);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.3);
        Assert.InRange(state.SprintStaminaFraction, 0.65, 0.75);
    }

    [Fact]
    public void AnEmptyBar_EndsTheSprint()
    {
        var state = Corridor(playerStepSeconds: 1.0, sprintStaminaSeconds: 0.5, sprintRecoverySeconds: 10.0);
        state.SetSprinting(true);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.3);
        Assert.True(state.IsSprinting);

        // The frame that finishes the step still pays for it.
        state.Tick(0.5);

        Assert.False(state.IsSprinting);
        Assert.Equal(0.0, state.SprintStaminaFraction, precision: 9);
    }

    [Fact]
    public void AnExhaustedBar_NeedsAQuarterRefillBeforeSprintingReturns()
    {
        var state = Corridor(playerStepSeconds: 1.0, sprintStaminaSeconds: 0.2, sprintRecoverySeconds: 1.0);
        state.SetSprinting(true);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.2);
        Assert.False(state.IsSprinting);

        // A tenth of the bar is not enough: sprinting would otherwise flicker on and off.
        state.Tick(0.1);
        Assert.False(state.IsSprinting);

        state.Tick(0.2);
        Assert.True(state.IsSprinting);
    }

    [Fact]
    public void Sprinting_RecoversWhenItIsNotRequested()
    {
        var state = Corridor(playerStepSeconds: 1.0, sprintStaminaSeconds: 0.4, sprintRecoverySeconds: 1.0);
        state.SetSprinting(true);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.2);
        var spent = state.SprintStaminaFraction;
        Assert.True(spent < 1.0);

        state.SetSprinting(false);
        state.Tick(0.1);

        Assert.True(state.SprintStaminaFraction > spent);
        Assert.False(state.IsSprinting);
    }

    [Fact]
    public void AnInstantFloor_CannotSprint()
    {
        var state = Corridor(playerStepSeconds: 0);
        state.SetSprinting(true);

        Assert.False(state.IsSprinting);
        Assert.Equal(1.0, state.SprintStaminaFraction, precision: 9);
    }

    [Fact]
    public void AGameThatIsOver_RefusesMovementWithoutBufferingIt()
    {
        var state = Corridor(keyCount: 0);

        // With no keys the exit is open, so the first step east wins the floor.
        Assert.True(state.TryMove(Direction.East));
        Assert.Equal(GameStatus.Won, state.Status);

        Assert.False(state.TryMove(Direction.East));
        state.Tick(Step);
        Assert.Equal(state.Maze.GetCell(0, 1), state.Player);
    }

    [Fact]
    public void Momentum_ShortensEachStraightStepUpToTheCap()
    {
        var state = OpenGrid(playerStepSeconds: 0.5, ramp: 0.1, cap: 0.2);

        // The first step of a line has no bonus yet: 0.5 s.
        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.49);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.02);
        Assert.False(state.IsPlayerStepping);

        // The second is 10% shorter: 0.5 / 1.1 = 0.4545 s.
        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.44);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.02);
        Assert.False(state.IsPlayerStepping);

        // The third and fourth are capped at 20%: 0.5 / 1.2 = 0.4167 s, not shorter.
        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.41);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.01);
        Assert.False(state.IsPlayerStepping);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.41);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.01);
        Assert.False(state.IsPlayerStepping);
    }

    [Fact]
    public void Momentum_ChargesForATurnForAWindow()
    {
        var state = OpenGrid(
            playerStepSeconds: 0.5,
            ramp: 0.1,
            cap: 0.2,
            turnPenalty: 0.15,
            turnPenaltySeconds: 2.0);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.5);

        // A turn is 15% slower: 0.5 * 1.15 = 0.575 s.
        Assert.True(state.TryMove(Direction.South));
        state.Tick(0.57);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.01);
        Assert.False(state.IsPlayerStepping);

        // The penalty is a window, not one step: the step after the turn pays as well, though it
        // has already earned one straight step of momentum, so 0.5 * 1.15 / 1.1 = 0.5227 s.
        Assert.True(state.TryMove(Direction.South));
        state.Tick(0.51);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.02);
        Assert.False(state.IsPlayerStepping);

        // Once the window has closed, the straight run earns its bonus back: two straight steps
        // behind him is the cap, so 0.5 / 1.2 = 0.4167 s.
        state.Tick(2.0);
        Assert.True(state.TryMove(Direction.South));
        state.Tick(0.41);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.01);
        Assert.False(state.IsPlayerStepping);
    }

    [Fact]
    public void WithoutMomentum_EveryStepTakesTheSameTime()
    {
        // The defaults are all zero, so a run that never took the card pays nothing for turning.
        var state = OpenGrid(playerStepSeconds: 0.5);

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.5);

        Assert.True(state.TryMove(Direction.South));
        state.Tick(0.49);
        Assert.True(state.IsPlayerStepping);
        state.Tick(0.02);
        Assert.False(state.IsPlayerStepping);
    }

    /// <summary>
    /// A 6x6 grid with every column open north-south and the rows joined in every column, so a
    /// player can walk a straight line or turn at will. Placement is the usual corridor order:
    /// player (0,0), exit (0,1), keys (0,2)-(0,4), killer (5,5).
    /// </summary>
    private static GameState OpenGrid(
        double playerStepSeconds = Step,
        double ramp = 0,
        double cap = 0,
        double turnPenalty = 0,
        double turnPenaltySeconds = 0) =>
        TestLevels.GameOver(
            WideCorridor(),
            new GameConfig
            {
                Rows = 6,
                Columns = 6,
                KeyCount = 3,
                EnemyStepSeconds = 1000.0,
                PlayerStepSeconds = playerStepSeconds,
                MomentumRampPerStep = ramp,
                MomentumMaxBonus = cap,
                TurnPenaltyScale = turnPenalty,
                TurnPenaltySeconds = turnPenaltySeconds,
                VisionRadius = 3,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = null,
            });

    private static Grid WideCorridor()
    {
        // Six rows so a momentum test can keep walking in one direction long enough to hit the
        // cap and then wait out a turn penalty without hitting a wall.
        var grid = new Grid(6, 6);

        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                grid.RemoveWall(row, column, Direction.East);
            }
        }

        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                grid.RemoveWall(row, column, Direction.South);
            }
        }

        return grid;
    }

    [Fact]
    public void ATapThroughTheRepeatGate_IsWorthExactlyOneTile()
    {
        // The whole input path for one press, with the real gate and the real step clock: the key
        // goes down, stays down for 100 ms (a slow tap), comes up, and the step finishes.
        var state = Corridor();
        var gate = new StepRepeat();
        var now = 0.0;

        Assert.Equal<Direction?>(Direction.East, gate.Next(Direction.East, state.IsPlayerStepping, now));
        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.IsPlayerStepping);

        for (var frame = 0; frame < 6; frame++)
        {
            now += Frame;
            Send(state, gate, Direction.East, now);
        }

        now += Frame;
        Send(state, gate, null, now);

        // The hold left nothing in the buffer, so the completed step is the only step.
        state.Tick(Step);

        Assert.Equal(state.Maze.GetCell(0, 1), state.Player);
        Assert.False(state.IsPlayerStepping);
    }

    [Fact]
    public void AHoldThroughTheRepeatGate_WalksAtTheStepCadence()
    {
        var frames = FramesToReachColumn(10, sprinting: false);

        // Ten tiles at 0.2 s each is about 108 frames at 60 Hz: the hold walks at the game's
        // cadence, not once per polled frame.
        Assert.InRange(frames, 100, 140);
    }

    [Fact]
    public void SprintThroughTheRepeatGate_CoversTheSameGroundSooner()
    {
        var walk = FramesToReachColumn(10, sprinting: false);
        var run = FramesToReachColumn(10, sprinting: true);

        Assert.True(run < walk, $"Sprint should arrive sooner: {run} frames against {walk}.");
    }

    /// <summary>
    /// Sends one frame of the repeat gate into the state, the way the frontend does: the gate
    /// decides whether this frame is worth a direction, and the state takes it or buffers it.
    /// </summary>
    private static void Send(GameState state, StepRepeat gate, Direction? held, double now)
    {
        if (gate.Next(held, state.IsPlayerStepping, now) is { } direction)
        {
            state.TryMove(direction);
        }
    }

    /// <summary>
    /// Holds east through the repeat gate until the player reaches <paramref name="column"/>,
    /// returning how many 60 Hz frames it took.
    /// </summary>
    private static int FramesToReachColumn(int column, bool sprinting)
    {
        var state = RunCorridor(14);
        var gate = new StepRepeat();
        var now = 0.0;
        var frames = 0;

        state.SetSprinting(sprinting);

        while (state.Player.Column < column && frames < 600)
        {
            Send(state, gate, Direction.East, now);
            state.Tick(Frame);
            now += Frame;
            frames++;
        }

        Assert.Equal(column, state.Player.Column);
        return frames;
    }

    /// <summary>
    /// A long corridor for cadence tests: player (0,0), killer (0,13), and enough stamina that a
    /// sprint test measures speed rather than fatigue.
    /// </summary>
    private static GameState RunCorridor(int length) =>
        TestLevels.GameOver(
            TestLevels.Corridor(length),
            new GameConfig
            {
                Rows = 1,
                Columns = length,
                KeyCount = 3,
                EnemyStepSeconds = 1000.0,
                PlayerStepSeconds = Step,
                SprintMultiplier = 1.4,
                SprintStaminaSeconds = 60.0,
                SprintRecoverySeconds = 1.6,
                VisionRadius = 3,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = null,
            });

    /// <summary>
    /// A 1x6 corridor: player (0,0), exit (0,1), keys (0,2)-(0,4), killer (0,5). Everything about
    /// movement is under the caller's control; the killer is slow by default so it stays out of the
    /// way.
    /// </summary>
    private static GameState Corridor(
        double playerStepSeconds = Step,
        double enemyStepSeconds = 1000.0,
        double sprintMultiplier = 1.4,
        double sprintStaminaSeconds = 1.2,
        double sprintRecoverySeconds = 1.6,
        int keyCount = 3) =>
        TestLevels.GameOver(
            TestLevels.Corridor(6),
            new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = keyCount,
                EnemyStepSeconds = enemyStepSeconds,
                PlayerStepSeconds = playerStepSeconds,
                SprintMultiplier = sprintMultiplier,
                SprintStaminaSeconds = sprintStaminaSeconds,
                SprintRecoverySeconds = sprintRecoverySeconds,
                VisionRadius = 3,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = null,
            });
}

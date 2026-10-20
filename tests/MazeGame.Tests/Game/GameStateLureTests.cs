using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the wisp: the player aims it like a barrier, the killer walks to whichever of the
/// wisp and the player is genuinely cheaper by Dijkstra, and the wisp always expires or is
/// consumed.
/// </summary>
/// <remarks>
/// The corridor level is a perfect fixture for this because every comparison is one-dimensional:
/// the killer starts at (0,5), so a wisp at (0,3) is two steps away while the player is five.
/// </remarks>
public sealed class GameStateLureTests
{
    private const double Step = 1.0;

    [Fact]
    public void BeginLurePlacement_StartsOnThePlayerInLureMode()
    {
        var state = Corridor();

        Assert.True(state.BeginLurePlacement());

        Assert.Equal(PlacementMode.Lure, state.AimMode);
        Assert.True(state.IsPlacingLure);
        Assert.False(state.IsPlacingBarrier);
        Assert.True(state.IsAiming);
        Assert.Equal(state.Player, state.AimCursor);
    }

    [Fact]
    public void BeginLurePlacement_WithNoWisps_ReturnsFalse()
    {
        var state = Corridor(lureCount: 0);

        Assert.False(state.BeginLurePlacement());
        Assert.False(state.IsAiming);
    }

    [Fact]
    public void BeginLurePlacement_WhileOneIsGlowing_ReturnsFalse()
    {
        var state = Corridor();
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        Assert.False(state.BeginLurePlacement());
        Assert.Equal(1, state.LuresRemaining);
    }

    [Fact]
    public void PlacingAWisp_SpendsOneChargeAndLightsTheCell()
    {
        var state = Corridor();

        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        Assert.Equal(state.Maze.GetCell(0, 3), state.Lure);
        Assert.True(state.IsLureActive);
        Assert.Equal(1, state.LuresRemaining);
        Assert.False(state.IsAiming);
        Assert.Equal(state.Config.LureSeconds, state.LureSecondsRemaining, precision: 9);
    }

    [Fact]
    public void PlacingAWisp_OnThePlayersOwnCell_IsRefusedAndKeepsAiming()
    {
        // A decoy where the killer is already heading would be a wasted charge, so the rule
        // refuses it and leaves the aim open rather than silently spending.
        var state = Corridor();
        state.BeginLurePlacement();

        Assert.False(state.ConfirmPlacement());
        Assert.True(state.IsPlacingLure);
        Assert.Equal(2, state.LuresRemaining);
        Assert.Null(state.Lure);
    }

    [Fact]
    public void PlacingAWisp_OnTheKillersCell_IsRefusedAndKeepsAiming()
    {
        // A decoy under the killer's feet would be zero steps away, so it would freeze the hunt
        // for the wisp's whole lifetime instead of redirecting it. That is stronger than any
        // barrier, so the rule refuses the placement.
        var state = Corridor();
        var killer = state.Enemy;
        state.BeginLurePlacement();

        while (state.AimCursor != killer && state.AimCursor.Column < state.Maze.Columns - 1)
        {
            Assert.True(state.MoveAimCursor(Direction.East));
        }

        Assert.Equal(killer, state.AimCursor);
        Assert.False(state.ConfirmPlacement());
        Assert.True(state.IsPlacingLure);
        Assert.Equal(2, state.LuresRemaining);
        Assert.Null(state.Lure);
    }

    [Fact]
    public void PlacingAWisp_OnACellThatAlreadyHoldsOne_IsImpossible()
    {
        var state = Corridor(lureCount: 3);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        // Only one wisp may glow, so a second aim is refused outright rather than replacing
        // the first (which would make the charge refundable by placing twice).
        Assert.False(state.BeginLurePlacement());
        Assert.Equal(state.Maze.GetCell(0, 3), state.Lure);
        Assert.Equal(2, state.LuresRemaining);
    }

    [Fact]
    public void Killer_TargetsTheWispWhenItIsTheCheaperRoute()
    {
        var state = Corridor();
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        var lure = state.Maze.GetCell(0, 3);

        state.Tick(Step);
        Assert.Equal(state.Maze.GetCell(0, 4), state.Enemy);

        state.Tick(Step);

        Assert.Equal(lure, state.Enemy);
        Assert.False(state.IsLureActive);
    }

    [Fact]
    public void AfterReachingTheWisp_TheKillerResumesTheChase()
    {
        var state = Corridor();
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        // Two steps to the wisp, then straight on towards the player.
        state.Tick(Step);
        state.Tick(Step);
        state.Tick(Step);

        Assert.Equal(state.Maze.GetCell(0, 2), state.Enemy);
        Assert.False(state.IsLureActive);
    }

    [Fact]
    public void Killer_IgnoresTheWispWhenThePlayerIsCloser()
    {
        // Move the player to (0,1), between the killer and a wisp at (0,0). The wisp is five
        // steps away from the killer and the player four, so Dijkstra says "player".
        var state = Corridor();
        Assert.True(state.TryMove(Direction.East));
        Assert.True(TestLevels.PlaceLureAt(state, 0, 0));

        for (var tick = 0; tick < 4; tick++)
        {
            state.Tick(Step);
        }

        Assert.Equal(GameStatus.Caught, state.Status);
        Assert.True(state.IsLureActive);
    }

    [Fact]
    public void Wisp_ExpiresAfterItsLifetime()
    {
        var state = Corridor(stepSeconds: 1000.0, lureSeconds: 2.0);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        state.Tick(2.0);

        Assert.False(state.IsLureActive);
        Assert.Null(state.Lure);
        Assert.Equal(0.0, state.LureSecondsRemaining);
    }

    [Fact]
    public void Aiming_FreezesTheWispTimer()
    {
        var state = Corridor();
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        var remaining = state.LureSecondsRemaining;
        var killer = state.Enemy;

        state.BeginBarrierPlacement();
        state.Tick(5.0);

        Assert.Equal(killer, state.Enemy);
        Assert.Equal(remaining, state.LureSecondsRemaining, precision: 9);
    }

    [Fact]
    public void Wisp_LightsItsOwnCellInTheDark()
    {
        // The wisp is at (0,9), far outside the torch, and it is the player's own beacon: it
        // must be visible anyway, or the player could not watch the trick work.
        var state = FarCorridor();
        var lure = state.Maze.GetCell(0, 9);

        Assert.False(state.IsVisible(lure));

        Assert.True(TestLevels.PlaceLureAt(state, 0, 9));

        Assert.True(state.IsVisible(lure));
        Assert.True(state.IsVisible(state.Maze.GetCell(0, 10)));
    }

    [Fact]
    public void ABudgetOfWisps_IsTrackedToZero()
    {
        var state = Corridor(lureCount: 2, lureSeconds: 1.0);

        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        state.Tick(1.0);
        Assert.False(state.IsLureActive);

        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        state.Tick(1.0);
        Assert.False(state.IsLureActive);

        Assert.Equal(0, state.LuresRemaining);
        Assert.False(state.BeginLurePlacement());
    }

    /// <summary>
    /// A 1x6 corridor game: player (0,0), exit (0,1), keys (0,2)-(0,4), killer (0,5).
    /// </summary>
    private static GameState Corridor(
        double stepSeconds = Step,
        int lureCount = 2,
        double lureSeconds = 6.0) =>
        TestLevels.GameOver(
            TestLevels.Corridor(),
            new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = 3,
                BarrierBudget = 2,
                EnemyStepSeconds = stepSeconds,
                VisionRadius = 3,
                LureCount = lureCount,
                LureSeconds = lureSeconds,
                Seed = null,
            });

    /// <summary>
    /// A 1x12 corridor, so there are cells far enough away to be outside the torch: player
    /// (0,0), exit and keys near the start, killer (0,11).
    /// </summary>
    private static GameState FarCorridor() =>
        TestLevels.GameOver(
            TestLevels.Corridor(12),
            new GameConfig
            {
                Rows = 1,
                Columns = 12,
                KeyCount = 3,
                BarrierBudget = 2,
                EnemyStepSeconds = Step,
                VisionRadius = 3,
                LureCount = 2,
                LureSeconds = 6.0,
                Seed = null,
            });
}

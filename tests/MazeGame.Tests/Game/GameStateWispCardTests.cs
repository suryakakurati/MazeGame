using MazeGame.Core.Game;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the two rules a card can switch on around the wisp: the stun when the killer reaches
/// it, and the vision a burning wisp can cost.
/// </summary>
/// <remarks>
/// Both are floor rules, not card rules: the card supplies the number through
/// <see cref="GameConfig"/> and the game applies it like any other tuning value. That is why these
/// use <see cref="GameState"/> directly and never mention a card.
/// </remarks>
public sealed class GameStateWispCardTests
{
    private const double Step = 1.0;

    [Fact]
    public void AWispStunsTheKiller_WhenTheFloorAllowsIt()
    {
        var state = ChaseCorridor(lureStunSeconds: 1.0);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));

        // Two steps to the wisp, and reaching it spends it.
        state.Tick(Step);
        state.Tick(Step);

        Assert.False(state.IsLureActive);
        Assert.True(state.IsEnemyStunned);
        Assert.Equal(1.0, state.EnemyStunSecondsRemaining, precision: 9);
    }

    [Fact]
    public void ADefaultWisp_StunsNobody()
    {
        var state = ChaseCorridor();

        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        state.Tick(Step);
        state.Tick(Step);

        Assert.False(state.IsLureActive);
        Assert.False(state.IsEnemyStunned);
    }

    [Fact]
    public void AStunnedKiller_DoesNotBankProgressTowardsItsNextStep()
    {
        var state = ChaseCorridor(lureStunSeconds: 2.0);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        state.Tick(Step);
        state.Tick(Step);
        var atWisp = state.Enemy;

        state.Tick(0.5);

        // Half a second of a two-second stun is not half a step: the killer is still where it
        // consumed the wisp.
        Assert.Equal(atWisp, state.Enemy);
        Assert.True(state.IsEnemyStunned);
    }

    [Fact]
    public void AfterTheStun_TheChaseResumes()
    {
        var state = ChaseCorridor(lureStunSeconds: 1.0);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 3));
        state.Tick(Step);
        state.Tick(Step);
        var atWisp = state.Enemy;

        state.Tick(1.0);

        Assert.False(state.IsEnemyStunned);
        Assert.Equal(state.Maze.GetCell(0, 2), state.Enemy);
        Assert.NotEqual(atWisp, state.Enemy);
    }

    [Fact]
    public void ABurningWisp_CostsTheVisionAPenaltySet()
    {
        var state = FarCorridor(lureVisionPenalty: 2);

        Assert.Equal(3, state.EffectiveVisionRadius);

        Assert.True(TestLevels.PlaceLureAt(state, 0, 9));

        // Three steps less two is one step of torch while the wisp burns.
        Assert.Equal(1, state.EffectiveVisionRadius);
        Assert.True(state.IsVisible(state.Maze.GetCell(0, 1)));
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 2)));
    }

    [Fact]
    public void WhenTheWispExpires_TheTorchComesBack()
    {
        var state = FarCorridor(lureSeconds: 1.0, lureVisionPenalty: 2);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 9));
        Assert.Equal(1, state.EffectiveVisionRadius);

        state.Tick(1.0);

        Assert.False(state.IsLureActive);
        Assert.Equal(3, state.EffectiveVisionRadius);
        Assert.True(state.IsVisible(state.Maze.GetCell(0, 2)));
    }

    [Fact]
    public void WhenTheWispExpires_ItsOwnLightGoesWithIt()
    {
        // The wisp lit its cell even though the torch could not reach it; once it is spent, that
        // light must go too, or the player keeps seeing a beacon that is no longer there.
        var state = FarCorridor(lureSeconds: 1.0);
        var wispCell = state.Maze.GetCell(0, 9);

        Assert.False(state.IsVisible(wispCell));
        Assert.True(TestLevels.PlaceLureAt(state, 0, 9));
        Assert.True(state.IsVisible(wispCell));

        state.Tick(1.0);

        Assert.False(state.IsVisible(wispCell));
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 10)));
    }

    [Fact]
    public void ThePenalty_NeverBlindsThePlayerCompletely()
    {
        var state = FarCorridor(visionRadius: 1, lureVisionPenalty: 5);
        Assert.True(TestLevels.PlaceLureAt(state, 0, 9));

        Assert.Equal(0, state.EffectiveVisionRadius);
        Assert.True(state.IsVisible(state.Player));
        Assert.False(state.IsVisible(state.Maze.GetCell(0, 1)));
    }

    /// <summary>
    /// A 1x6 corridor: player (0,0), exit (0,1), keys (0,2)-(0,4), killer (0,5). A wisp at (0,3)
    /// is two steps from the killer and five from the player, so the killer really does walk to
    /// it — which is what a stun test needs.
    /// </summary>
    private static GameState ChaseCorridor(double lureStunSeconds = 0.0) =>
        TestLevels.GameOver(
            TestLevels.Corridor(6),
            new GameConfig
            {
                Rows = 1,
                Columns = 6,
                KeyCount = 3,
                BarrierBudget = 2,
                EnemyStepSeconds = Step,
                VisionRadius = 3,
                LureCount = 2,
                LureSeconds = 6.0,
                LureStunSeconds = lureStunSeconds,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = null,
            });

    /// <summary>
    /// A 1x12 corridor: player (0,0), exit and keys near the start, killer (0,11), so there is
    /// room for cells outside the torch and for a wisp the killer must walk to.
    /// </summary>
    private static GameState FarCorridor(
        double lureStunSeconds = 0.0,
        int lureVisionPenalty = 0,
        double lureSeconds = 6.0,
        int visionRadius = 3) =>
        TestLevels.GameOver(
            TestLevels.Corridor(12),
            new GameConfig
            {
                Rows = 1,
                Columns = 12,
                KeyCount = 3,
                BarrierBudget = 2,
                EnemyStepSeconds = Step,
                VisionRadius = visionRadius,
                LureCount = 2,
                LureSeconds = lureSeconds,
                LureStunSeconds = lureStunSeconds,
                LureVisionPenalty = lureVisionPenalty,
                StartingHearts = 1,
                MaxHearts = 1,
                Seed = null,
            });
}

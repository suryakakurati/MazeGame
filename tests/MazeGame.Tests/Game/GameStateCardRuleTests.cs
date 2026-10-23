using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the rule hooks the second card wave needs: a free catch, a per-floor revival, a
/// catch that costs more than one heart, magnetic keys, blink refunds and the three duration
/// knobs.
/// </summary>
/// <remarks>
/// Each test checks both halves of its card where the two halves live in different systems: for
/// example <c>featherfall</c> is tested here for the free catch, while its lower heart ceiling is
/// covered by <c>RunModifiersTests</c>. The levels are corridors so the catch happens at a known
/// time rather than by luck.
/// </remarks>
public sealed class GameStateCardRuleTests
{
    [Fact]
    public void Featherfall_GivesTheFirstCatchForFreeThenChargesForTheSecond()
    {
        var state = Corridor(hearts: 2, tweak: config => config with { FirstCatchFree = true });

        TickUntil(state, () => state.IsInvulnerable);

        Assert.Equal(2, state.Hearts);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Contains(GameEvent.Graced, state.DrainEvents());

        TickUntil(state, () => state.Hearts < 2);

        Assert.Equal(1, state.Hearts);
        Assert.Contains(GameEvent.HeartLost, state.DrainEvents());
    }

    [Fact]
    public void Bloom_TurnsOneFatalCatchIntoAHeartThenLetsTheNextOneEndTheRun()
    {
        var state = Corridor(hearts: 1, tweak: config => config with { ReviveOnce = true });

        TickUntil(state, () => state.IsInvulnerable);

        Assert.Equal(1, state.Hearts);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Contains(GameEvent.Revived, state.DrainEvents());

        TickUntil(state, () => state.Status != GameStatus.InProgress);

        Assert.Equal(GameStatus.Caught, state.Status);
    }

    [Fact]
    public void GlassCannon_ACatchCostsTwoHearts()
    {
        var state = Corridor(hearts: 3, tweak: config => config with { CatchCost = 2 });

        TickUntil(state, () => state.Hearts < 3);

        Assert.Equal(1, state.Hearts);
        Assert.Equal(GameStatus.InProgress, state.Status);
    }

    [Fact]
    public void GreedyGrasp_PullsInAKeyFromTheNextCell()
    {
        var state = Corridor(keyCount: 1, hearts: 2, tweak: config => config with { MagneticKeys = true });

        // The exit is locked, so the step east is ordinary; the key on the far side of it is
        // collected by the pull rather than by stepping onto its cell.
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(0, state.KeysRemaining);
        Assert.Contains(GameEvent.KeyPicked, state.DrainEvents());
    }

    [Fact]
    public void BlinkLedger_GivesBackABlinkWhenAKeyIsTaken()
    {
        var state = Corridor(
            keyCount: 1,
            hearts: 2,
            tweak: config => config with { BlinkRefundPerKey = 1 },
            powerups: PowerupCharges.None with { Blink = 0 });

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.2);
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(1, state.PowerupsRemaining.Blink);
    }

    [Fact]
    public void TwoBlinkRefunds_GiveTwoBlinksPerKey_NotOne()
    {
        // Two cards that each refund a blink are two blinks per bloom. A flag would have
        // swallowed the second copy and the second card would have been a dead pick.
        var state = Corridor(
            keyCount: 1,
            hearts: 2,
            tweak: config => config with { BlinkRefundPerKey = 2 },
            powerups: PowerupCharges.None with { Blink = 0 });

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.2);
        Assert.True(state.TryMove(Direction.East));

        Assert.Equal(2, state.PowerupsRemaining.Blink);
    }

    [Fact]
    public void GhostVeil_KeepsThePlayerHiddenForItsLongerDuration()
    {
        var state = Corridor(hearts: 2, tweak: config => config with { VeilDurationSeconds = 5.0 });

        Assert.True(state.TryVeil());
        state.Tick(0.6);

        Assert.True(state.IsVeiled);

        state.Tick(4.0);
        Assert.True(state.IsVeiled);

        state.Tick(1.1);
        Assert.False(state.IsVeiled);
    }

    [Fact]
    public void LongDrag_ExtendsBothTheSlowWindowAndItsHangover()
    {
        var state = Corridor(hearts: 2, tweak: config => config with { DragSeconds = 4.0, DragPenaltySeconds = 8.0 });

        Assert.True(state.TryDrag());

        state.Tick(3.0);
        Assert.True(state.IsDragActive);

        state.Tick(1.1);
        Assert.False(state.IsDragActive);
        Assert.True(state.IsDragPenalty);

        state.Tick(7.9);
        Assert.True(state.IsDragPenalty);

        state.Tick(0.2);
        Assert.False(state.IsDragPenalty);
    }

    [Fact]
    public void WideLight_ThrowsTheLanternFurther()
    {
        var state = Corridor(hearts: 2, length: 9, tweak: config => config with { LanternRange = 6 });

        Assert.True(state.TryMove(Direction.East));
        Assert.True(state.TryThrowLantern());

        Assert.Equal(state.Maze.GetCell(0, 7), state.LanternCell);
    }

    /// <summary>Ticks one second at a time until <paramref name="done"/> or the budget runs out.</summary>
    private static void TickUntil(GameState state, Func<bool> done, int maxTicks = 40)
    {
        for (var tick = 0; tick < maxTicks && !done(); tick++)
        {
            state.Tick(1.0);
        }

        Assert.True(done(), "the expected moment never happened");
    }

    /// <summary>A 1x6 corridor (exit at (0,1), keys behind it, killer at the far end).</summary>
    private static GameState Corridor(
        int keyCount = 3,
        int hearts = 2,
        Func<GameConfig, GameConfig>? tweak = null,
        PowerupCharges? powerups = null,
        int length = 6)
    {
        var config = new GameConfig
        {
            Rows = 1,
            Columns = length,
            KeyCount = keyCount,
            EnemyStepSeconds = 1.0,
            PlayerStepSeconds = 0.1,
            VisionRadius = 3,
            StartingHearts = hearts,
            MaxHearts = hearts,
            Powerups = powerups ?? PowerupCharges.ForFloor(8),
            Seed = null,
        };

        return TestLevels.GameOver(
            TestLevels.Corridor(length),
            tweak is null ? config : tweak(config));
    }
}

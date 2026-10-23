using System;
using MazeGame.Core.Game;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies that <see cref="GameConfig"/> rejects settings that could not produce a playable
/// game, and that the defaults are sane.
/// </summary>
public sealed class GameConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new GameConfig();

        config.Validate();

        Assert.Equal(15, config.Rows);
        Assert.Equal(15, config.Columns);
        Assert.Equal(3, config.KeyCount);
        Assert.Equal(0.6, config.EnemyStepSeconds);
        Assert.Equal(3, config.VisionRadius);
        Assert.Equal(8, config.ExtraLoops);
        Assert.Equal(55, config.DeadEndBraidPercent);
        Assert.Equal(2, config.EnemyAuraRadius);
        Assert.Equal(6, config.EnemyAuraDistance);
        Assert.Equal(3, config.StartingHearts);
        Assert.Equal(3, config.MaxHearts);
        Assert.Equal(1.5, config.InvulnerabilitySeconds);
        Assert.Equal(8, config.RespawnDistance);
        Assert.Equal(1.5, config.RespawnStunSeconds);
        Assert.Null(config.Seed);
    }

    [Fact]
    public void DefaultBraid_LeavesRoughlyAFifthOfTheCellsAsDeadEnds()
    {
        // The published balance: a 15x15 perfect maze has about 64 dead ends, and the shipped
        // braid should leave well under half of them. The share is the knob that decides how
        // often a chase ends in a corner, so it is pinned rather than left to chance.
        var config = new GameConfig();

        Assert.InRange(config.DeadEndBraidPercent, 40, 70);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidRowCount_Throws(int rows)
    {
        var config = new GameConfig { Rows = rows };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidColumnCount_Throws(int columns)
    {
        var config = new GameConfig { Columns = columns };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeKeyCount_Throws()
    {
        var config = new GameConfig { KeyCount = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithInvalidEnemyStepTime_Throws(double seconds)
    {
        var config = new GameConfig { EnemyStepSeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeVisionRadius_Throws()
    {
        var config = new GameConfig { VisionRadius = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeExtraLoops_Throws()
    {
        var config = new GameConfig { ExtraLoops = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeEnemyAuraRadius_Throws()
    {
        var config = new GameConfig { EnemyAuraRadius = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeEnemyAuraDistance_Throws()
    {
        var config = new GameConfig { EnemyAuraDistance = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNoStartingHearts_Throws(int hearts)
    {
        // A floor with no hearts is a run that is already over.
        var config = new GameConfig { StartingHearts = hearts, MaxHearts = Math.Max(hearts, 3) };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_AllowsARunHoldingMoreHeartsThanTheFloorsSlots()
    {
        // Hearts are uncapped: MaxHearts is the number of slots the floor's HUD draws, not a rule
        // that can be violated by a healed run arriving with more.
        var config = new GameConfig { StartingHearts = 4, MaxHearts = 3 };

        config.Validate();
    }

    [Fact]
    public void ForFloor_KeepsEveryCarriedHeart()
    {
        // The floor's own slot count is design, not a cap: ForFloor passes the carried hearts
        // through, and the run's cards widen the slots later in the factory.
        var floor = new FloorDefinition { Number = 1, Name = "Test", ThemeId = "test" };

        var config = new GameConfig().ForFloor(floor, startingHearts: 6);

        Assert.Equal(6, config.StartingHearts);
        Assert.Equal(floor.MaxHearts, config.MaxHearts);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithInvalidInvulnerabilityWindow_Throws(double seconds)
    {
        var config = new GameConfig { InvulnerabilitySeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_WithRespawnDistanceBelowOne_Throws(int distance)
    {
        // A zero-step respawn would put the killer on the player's cell and deal a second hit.
        var config = new GameConfig { RespawnDistance = distance };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithInvalidRespawnStun_Throws(double seconds)
    {
        var config = new GameConfig { RespawnStunSeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithBraidPercentOutsideZeroToHundred_Throws(int percent)
    {
        var config = new GameConfig { DeadEndBraidPercent = percent };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(1, 3, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 2, 2)]
    public void Validate_WhenMazeIsTooSmallForItsContents_Throws(int rows, int columns, int keyCount)
    {
        // Player + killer + exit + keys each need their own cell.
        var config = new GameConfig { Rows = rows, Columns = columns, KeyCount = keyCount };

        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(1, 3, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(2, 2, 1)]
    public void Validate_WhenMazeHasExactlyEnoughCells_DoesNotThrow(int rows, int columns, int keyCount)
    {
        var config = new GameConfig { Rows = rows, Columns = columns, KeyCount = keyCount };

        config.Validate();
    }

    [Fact]
    public void Validate_WithZeroKeys_IsAllowed()
    {
        // Zero keys is a legitimate mode: the exit is open from the start.
        var config = new GameConfig { Rows = 2, Columns = 2, KeyCount = 0 };

        config.Validate();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithInvalidPlayerStepTime_Throws(double seconds)
    {
        var config = new GameConfig { PlayerStepSeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithASprintMultiplierBelowOne_Throws(double multiplier)
    {
        // Sprinting that is slower than walking would be a trap, not a trade.
        var config = new GameConfig { SprintMultiplier = multiplier };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithInvalidSprintStamina_Throws(double seconds)
    {
        var config = new GameConfig { SprintStaminaSeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithANonPositiveSprintRecovery_Throws(double seconds)
    {
        // An empty bar that never refills would silently remove sprint for the rest of the run.
        var config = new GameConfig { SprintRecoverySeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void TheDefaultConfig_MovesInstantlyButCanSprintWhenAFloorAsksForSteps()
    {
        // The base config is the test/tool seam: instant steps, with sprint numbers ready for any
        // floor that opts into timed movement.
        var config = new GameConfig();

        Assert.Equal(0.0, config.PlayerStepSeconds);
        Assert.Equal(1.4, config.SprintMultiplier);
        Assert.Equal(1.2, config.SprintStaminaSeconds);
        Assert.Equal(1.6, config.SprintRecoverySeconds);
    }

    [Fact]
    public void ForFloor_TakesTheFloorsPlayerStepAndKeepsTheRunsSprintRules()
    {
        var baseConfig = new GameConfig { SprintMultiplier = 2.0, SprintStaminaSeconds = 0.5 };
        var floor = new FloorDefinition
        {
            Number = 1,
            Name = "Test",
            ThemeId = "test",
            PlayerStepSeconds = 0.13,
        };

        var config = baseConfig.ForFloor(floor, startingHearts: 3);

        // The step is per floor; the sprint rules are run-wide and inherited.
        Assert.Equal(0.13, config.PlayerStepSeconds, precision: 9);
        Assert.Equal(2.0, config.SprintMultiplier, precision: 9);
        Assert.Equal(0.5, config.SprintStaminaSeconds, precision: 9);
        Assert.Equal(0.0, baseConfig.PlayerStepSeconds);
    }
}

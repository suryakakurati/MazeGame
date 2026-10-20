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
        Assert.Equal(5, config.ObstacleCost);
        Assert.Equal(3, config.BarrierBudget);
        Assert.Equal(0.6, config.EnemyStepSeconds);
        Assert.Equal(3, config.VisionRadius);
        Assert.Null(config.Seed);
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

    [Fact]
    public void DefaultBarrierCost_BuysThreeSeconds()
    {
        // The published balance: one barrier is a 3-second pause at the default step time.
        var config = new GameConfig();

        Assert.Equal(3.0, config.ObstacleCost * config.EnemyStepSeconds, precision: 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_WithObstacleCostBelowOne_Throws(int obstacleCost)
    {
        // A cost below 1 would let a barrier act as a shortcut (or a wall), which breaks the
        // "always a route, never a free pass" rules.
        var config = new GameConfig { ObstacleCost = obstacleCost };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeBarrierBudget_Throws()
    {
        var config = new GameConfig { BarrierBudget = -1 };

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
}

using MazeGame.Core.Abstractions;
using MazeGame.Core.Game;
using MazeGame.Core.Maze;
using MazeGame.Core.Pathfinding;
using MazeGame.Tests.TestDoubles;

namespace MazeGame.Tests.Game;

/// <summary>
/// Verifies the eight descent conditions: each one changes the space or the hunt in a way the
/// player can see, and each one stops changing it when the condition is not in force.
/// </summary>
/// <remarks>
/// The tests are deliberately small: a corridor for the rule flags, and one hand-built dead end
/// for Regrowth so the cell that seals is the only plausible choice. The conditions compose with
/// an ordinary <see cref="GameState"/>, so every test also proves a condition-free floor is
/// unchanged.
/// </remarks>
public sealed class GameStateConditionTests
{
    [Fact]
    public void Eclipse_DarkensTheTorchForSixSecondsThenLightsItAgain()
    {
        var state = CorridorGame(conditions: DescentCondition.Eclipse);

        Assert.False(state.IsEclipseDark);
        Assert.Equal(3, state.EffectiveVisionRadius);

        state.Tick(10.0);

        Assert.True(state.IsEclipseDark);
        Assert.Equal(2, state.EffectiveVisionRadius);
        Assert.Contains(GameEvent.EclipseFell, state.DrainEvents());

        state.Tick(6.0);

        Assert.False(state.IsEclipseDark);
        Assert.Equal(3, state.EffectiveVisionRadius);
        Assert.Contains(GameEvent.EclipseLifted, state.DrainEvents());
    }

    [Fact]
    public void Regrowth_SealsTheEmptyDeadEndAndLeavesThePlayerAlone()
    {
        var state = DeadEndLevel();

        // The branch at (1,1) is the only dead end that holds nothing the floor needs.
        Assert.False(state.Maze.HasWall(1, 1, Direction.West));

        state.Tick(18.0);

        Assert.True(state.Maze.HasWall(1, 1, Direction.West));
        Assert.Contains(GameEvent.WallSealed, state.DrainEvents());
    }

    [Fact]
    public void Regrowth_DoesNotSealAnUnreadEcho()
    {
        // The Echo deliberately sits in a dead end, which is exactly the cell Regrowth wants to
        // close. Sealing it would hide the floor's story and its heal behind a wall the player
        // never saw move.
        var state = DeadEndLevel(echo: true);

        Assert.NotNull(state.EchoCell);
        var echo = state.EchoCell.Value;

        Assert.Single(state.Maze.OpenDirections(echo.Row, echo.Column));

        state.Tick(18.0);

        Assert.Single(state.Maze.OpenDirections(echo.Row, echo.Column));
        Assert.DoesNotContain(GameEvent.WallSealed, state.DrainEvents());
    }

    [Fact]
    public void Fracture_OpensAWallAndClosesOneThatKeepsTheFloorConnected()
    {
        var state = TestLevels.GameOver(
            TestLevels.TwoRoutes(),
            Config(2, 3, keyCount: 1, conditions: DescentCondition.Fracture));

        state.Tick(22.0);

        var events = state.DrainEvents();
        Assert.Contains(GameEvent.WallOpened, events);
        Assert.Contains(GameEvent.WallClosed, events);

        // The exit is still reachable: the close step refused anything else.
        var path = new DijkstraPathfinder().FindPath(state.Maze, state.Player, state.Exit, _ => 1);
        Assert.NotEmpty(path);
    }

    [Fact]
    public void Listener_HuntersOnlyStepWhenThePlayerDoes()
    {
        var state = CorridorGame(conditions: DescentCondition.Listener);
        var hunter = state.Enemy;

        state.Tick(5.0);

        Assert.Equal(hunter, state.Enemy);
        Assert.True(state.TryMove(Direction.East));
        Assert.NotEqual(hunter, state.Enemy);
    }

    [Fact]
    public void Pack_MovesEveryHunterOnOneClick()
    {
        var random = new SeededRandom(11);
        var maze = new MazeGenerator(random).Generate(new MazeOptions(7, 7, 11));
        var state = new GameState(
            Config(7, 7, keyCount: 3, conditions: DescentCondition.Pack) with
            {
                HunterCount = 2,
                Seed = 11,
            },
            maze,
            new DijkstraPathfinder(),
            random);

        var before = state.Enemies.Select(enemy => enemy.Cell).ToArray();

        state.Tick(1.0);

        var after = state.Enemies.Select(enemy => enemy.Cell).ToArray();

        Assert.Equal(2, after.Length);
        Assert.All(after, cell => Assert.DoesNotContain(cell, before));
    }

    [Fact]
    public void Hunted_TheFirstHunterIsAlwaysVisible()
    {
        var state = CorridorGame(
            length: 12,
            keyCount: 3,
            conditions: DescentCondition.Hunted,
            visionRadius: 1);

        Assert.True(state.IsVisible(state.Enemies[0].Cell));
    }

    [Fact]
    public void Drift_MovesTheExitWhenAKeyIsTaken()
    {
        var state = CorridorGame(
            length: 12,
            keyCount: 1,
            conditions: DescentCondition.Drift);
        var exit = state.Exit;

        Assert.True(state.TryMove(Direction.East));
        state.Tick(0.2);
        Assert.True(state.TryMove(Direction.East));

        Assert.NotEqual(exit, state.Exit);
        Assert.Contains(GameEvent.ExitMoved, state.DrainEvents());
    }

    [Fact]
    public void NoConditions_LeaveEveryTimerOff()
    {
        var state = CorridorGame();

        Assert.False(state.IsEclipseDark);
        Assert.Equal(3, state.EffectiveVisionRadius);

        state.Tick(18.0);

        Assert.DoesNotContain(GameEvent.WallSealed, state.DrainEvents());
        Assert.False(state.IsEclipseDark);
    }

    [Fact]
    public void Mute_TakesAToolAwayAndStartsWithAnExtraHeart()
    {
        var floor = FloorCatalog.ByNumber(2);
        var factory = new GameFactory(
            new GameConfig(),
            seed => new SeededRandom(seed ?? 1),
            new DijkstraPathfinder());

        var state = factory.CreateFloor(
            floor,
            floor.MaxHearts,
            RunModifiers.None,
            [DescentCondition.Mute]);

        Assert.Equal(PowerupKind.Veil, state.Config.MutedPowerup);
        Assert.Equal(0, state.PowerupsRemaining.Veil);
        Assert.Equal(floor.MaxHearts + 1, state.MaxHearts);
        Assert.Equal(floor.MaxHearts + 1, state.Hearts);
    }

    /// <summary>
    /// A 5x5 ring with two empty dead ends: a near one at (1,1) and a far one at (3,3). The
    /// killer starts on the far one, so Regrowth has exactly one legal cell to seal.
    /// </summary>
    private static GameState DeadEndLevel(bool echo = false)
    {
        var grid = new Grid(5, 5);

        for (var column = 0; column < 4; column++)
        {
            grid.RemoveWall(0, column, Direction.East);
            grid.RemoveWall(4, column, Direction.East);
        }

        for (var row = 0; row < 4; row++)
        {
            grid.RemoveWall(row, 4, Direction.South);
            grid.RemoveWall(row, 0, Direction.South);
        }

        grid.RemoveWall(1, 0, Direction.East);
        grid.RemoveWall(3, 4, Direction.West);

        var config = Config(5, 5, keyCount: 1, conditions: DescentCondition.Regrowth);

        if (echo)
        {
            config = config with { EchoEnabled = true, FloorNumber = 1 };
        }

        return TestLevels.GameOver(grid, config);
    }

    /// <summary>A 1x6 corridor with the requested conditions.</summary>
    private static GameState CorridorGame(
        int length = 6,
        int keyCount = 3,
        int visionRadius = 3,
        params DescentCondition[] conditions) =>
        TestLevels.GameOver(
            TestLevels.Corridor(length),
            Config(1, length, keyCount, visionRadius, conditions));

    private static GameConfig Config(
        int rows,
        int columns,
        int keyCount,
        int visionRadius = 3,
        params DescentCondition[] conditions) => new()
        {
            Rows = rows,
            Columns = columns,
            KeyCount = keyCount,
            EnemyStepSeconds = 1.0,
            PlayerStepSeconds = 0.1,
            VisionRadius = visionRadius,
            StartingHearts = 3,
            MaxHearts = 3,
            Conditions = conditions,
            Powerups = PowerupCharges.ForFloor(8),
            Seed = null,
        };
}
